using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Filter;
using vNode.Sdk.Helper;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.Diagnostics;
using vNode.SiemensS7.Helper;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;

namespace SiemensModule
{
    /// <summary>
    /// Clase principal para el canal de comunicación Siemens.
    /// Gestiona la configuración, el estado, el registro de tags y la comunicación con un PLC Siemens.
    /// </summary>
    public class Siemens : BaseChannel
    {
        private readonly ISdkLogger _logger;
        private readonly string _nodeName;

        // Colecciones para gestionar tags de usuario y de control/diagnóstico.
        private readonly Dictionary<Guid, SiemensTagWrapper> _tags = [];
        private readonly Dictionary<Guid, SiemensControlTag> _controlTagsDictionary = [];

        // Componentes principales del canal.
        private SiemensChannelConfig _config;
        private ChannelDiagnostics _channelControl;
        private SiemensTagReader _siemensTagReader;
        private SiemensScheduler _scheduler;
        private readonly SiemensControl _siemensControl;

        // Gestión de estado y tareas.
        private CancellationTokenSource? _cts;
        private Task? _readingTask;
        private bool _started = false;

        // Mecanismos de bloqueo para concurrencia.
        private readonly object _lock = new();
        private readonly object _channelLock = new();

        /// <summary>
        /// Constructor para el canal Siemens.
        /// </summary>
        /// <param name="nodeName">Nombre del nodo vNode.</param>
        /// <param name="configJson">Objeto JSON con la configuración del canal.</param>
        /// <param name="logger">Instancia del logger para registrar eventos.</param>
        /// <param name="siemensControl">Instancia de control para la gestión del módulo.</param>
        public Siemens(string nodeName, JsonObject configJson, ISdkLogger logger, SiemensControl siemensControl)
        {
            ArgumentNullException.ThrowIfNull(configJson);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(siemensControl);

            if (string.IsNullOrWhiteSpace(configJson.ToString()))
            {
                throw new ArgumentException("La configuración JSON no puede ser nula o vacía.", nameof(configJson));
            }

            _nodeName = nodeName;
            _logger = logger;
            _siemensControl = siemensControl;

            _logger.Information("Siemens", $"Inicializando el canal Siemens.");
            initializeChannel(configJson);
        }

        /// <summary>
        /// Inicializa o reinicializa el canal con una nueva configuración.
        /// </summary>
        /// <param name="configJson">La configuración del canal en formato JSON.</param>
        private void initializeChannel(JsonObject configJson)
        {
            _logger.Information("Siemens",
                "initializeChannel-> Leyendo configuración. Configuración del canal:\n" +
                JsonHelper.PrettySerialize(configJson));
            try
            {
                _config = SiemensChannelConfig.FromJson(configJson);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "initializeChannel-> Error al leer la configuración del canal.");
                throw new InvalidChannelConfigException(ex);
            }

            // Inicializa el lector de tags
            var plcConnection = new SiemensTcpStrategy(_config.IpAddress, _config.Rack, _config.Slot);
            _siemensTagReader = new SiemensTagReader(plcConnection);

            // Inicializa el planificador de lecturas
            _scheduler = new SiemensScheduler(_logger);
            _scheduler.ReadingDue += handleReadingDue;

            // Inicializa los diagnósticos del canal
            _channelControl = new ChannelDiagnostics(new Dictionary<string, SiemensChannelConfig> { { "plc", _config } }, _logger);
            _channelControl.PropertyChanged += _channelDiagnostics_PropertyChanged;
            _channelControl.DevicePropertyChanged += _channelDiagnostics_DevicePropertyChanged;

            // Vuelve a registrar los tags existentes en el nuevo planificador y control de diagnóstico
            foreach (var tag in _tags.Values)
            {
                if (tag.Status != SiemensTagWrapper.SiemensTagStatusType.ConfigError)
                {
                    _scheduler.AddTag(tag);
                    _channelControl.RegisterTag(tag.IdTag, "plc");
                }
            }
        }

        #region Implementación de BaseChannel (métodos públicos)

        /// <summary>
        /// Inicia el canal, permitiendo la comunicación y el sondeo de tags.
        /// </summary>
        public override bool Start()
        {
            lock (_channelLock)
            {
                if (_started)
                {
                    _logger.Information("Siemens", "Start: El canal ya está iniciado.");
                    return true;
                }

                _logger.Information("Siemens", "Start: Iniciando el canal Siemens...");
                SetChannelEnabledState(true);
                sendInitialData();

                _cts = new CancellationTokenSource();
                _readingTask = Task.Run(() => _scheduler.StartReadingAsync(_cts.Token));

                _started = true;
                _logger.Information("Siemens", "Start: El canal Siemens se ha iniciado correctamente.");
                return true;
            }
        }

        /// <summary>
        /// Detiene el canal, finalizando la comunicación y el sondeo de tags.
        /// </summary>
        public override bool Stop()
        {
            lock (_channelLock)
            {
                if (!_started)
                {
                    _logger.Information("Siemens", "Stop: El canal ya está detenido.");
                    return true;
                }

                _logger.Information("Siemens", "Stop: Deteniendo el canal Siemens...");

                // Detiene el planificador para evitar nuevas lecturas.
                _scheduler?.StopReading();

                // Cancela las tareas en segundo plano.
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                // Espera a que la tarea de lectura finalice.
                try
                {
                    _readingTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex)
                {
                    _logger.Warning(ex.InnerException ?? ex, "Siemens", "Stop: Error al esperar la finalización de la tarea de lectura.");
                }
                finally
                {
                    _readingTask = null;
                    _cts?.Dispose();
                    _cts = null;
                }

                // Envía una calidad de "fuera de servicio" para todos los tags.
                sendChannelOrDeviceDisabledData();

                // Actualiza el estado del canal.
                SetChannelEnabledState(false);
                _started = false;
                _logger.Information("Siemens", "Stop: El canal Siemens se ha detenido correctamente.");
                return true;
            }
        }

        /// <summary>
        /// Registra un nuevo tag en el canal.
        /// </summary>
        /// <param name="tagObject">El objeto base del tag a registrar.</param>
        /// <returns>True si el registro fue exitoso, de lo contrario false.</returns>
        public override bool RegisterTag(TagModelBase tagObject)
        {
            ArgumentNullException.ThrowIfNull(tagObject);
            lock (_lock)
            {
                if (_tags.ContainsKey(tagObject.IdTag) || _controlTagsDictionary.ContainsKey(tagObject.IdTag))
                {
                    _logger.Warning("Siemens", $"El tag con ID {tagObject.IdTag} ya está registrado.");
                    return false;
                }

                var wrapper = SiemensTagWrapper.Create(tagObject, _logger);
                if (wrapper.Status == SiemensTagWrapper.SiemensTagStatusType.ConfigError)
                {
                    _logger.Error("Siemens", $"No se pudo registrar el tag '{tagObject.Name}' debido a un error de configuración.");
                    _tags[tagObject.IdTag] = wrapper; // Almacenar para reportar el error.
                    return false;
                }

                _tags[tagObject.IdTag] = wrapper;
                _channelControl.RegisterTag(wrapper.IdTag, "plc");
                if (wrapper.Config.PollRate > 0)
                {
                    _scheduler.AddTag(wrapper);
                }

                if (_started)
                {
                    sendInitialData(tagObject.IdTag);
                }

                _logger.Information("Siemens", $"Tag '{tagObject.Name}' (ID: {tagObject.IdTag}) registrado exitosamente.");
                return true;
            }
        }

        /// <summary>
        /// Elimina un tag del canal.
        /// </summary>
        /// <param name="idTag">El ID del tag a eliminar.</param>
        /// <returns>True si la eliminación fue exitosa, de lo contrario false.</returns>
        public override bool RemoveTag(Guid idTag)
        {
            lock (_lock)
            {
                if (_tags.Remove(idTag, out var wrapper))
                {
                    _scheduler.RemoveTag(idTag);
                    _channelControl.UnregisterTag(idTag);
                    _logger.Information("Siemens", $"Tag con ID {idTag} eliminado exitosamente.");
                    return true;
                }
                if (_controlTagsDictionary.Remove(idTag))
                {
                    _logger.Information("Siemens", $"Tag de control con ID {idTag} eliminado exitosamente.");
                    return true;
                }
                _logger.Warning("Siemens", $"No se pudo eliminar el tag con ID {idTag} porque no fue encontrado.");
                return false;
            }
        }

        /// <summary>
        /// Escribe un valor en un tag.
        /// </summary>
        /// <param name="idTag">ID del tag a escribir.</param>
        /// <param name="newValue">Nuevo valor para el tag.</param>
        /// <returns>Un string indicando el resultado de la operación.</returns>
        public override async Task<string> SetTagValue(Guid idTag, object newValue)
        {
            if (!_tags.TryGetValue(idTag, out var tagWrapper))
            {
                return "Error: Tag no encontrado.";
            }

            if (tagWrapper.Tag.ClientAccess == ClientAccessOptions.ReadOnly)
            {
                return "Error: El tag es de solo lectura.";
            }

            try
            {
                await _siemensTagReader.WriteTagAsync(tagWrapper.Config, newValue);
                PostNewEvent(newValue, QualityCodeOptions.Good_Non_Specific, idTag);
                return "Ok";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", $"Error al escribir en el tag {idTag}.");
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Libera todos los recursos utilizados por el canal.
        /// </summary>
        public override void Dispose()
        {
            _logger.Information("Siemens", "Dispose: Solicitud de liberación del canal.");
            Stop();
            _channelControl?.Dispose();
            _scheduler.ReadingDue -= handleReadingDue;
            _siemensControl.UnregisterChannel(this);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Métodos privados y manejadores de eventos

        /// <summary>
        /// Manejador para el evento de lectura programada del planificador.
        /// </summary>
        private void handleReadingDue(object? sender, ReadingDueEventArgs e)
        {
            if (!_started) return;

            try
            {
                var results = _siemensTagReader.ReadManyForSdk(e.TagsToRead);
                processReadResult(results);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "Error durante la ejecución del lote de lectura.");
                // Opcional: Marcar los tags del lote como erróneos.
            }
        }

        /// <summary>
        /// Procesa los resultados de una operación de lectura y publica los nuevos valores.
        /// </summary>
        private void processReadResult(Dictionary<Guid, Sdk.Data.TagReadResult> results)
        {
            if (!_started) return;

            foreach (var kvp in results)
            {
                PostNewEvent(kvp.Value.Value, kvp.Value.Quality, kvp.Key, kvp.Value.SourceTimestamp.ToFileTimeUtc());
            }
        }

        /// <summary>
        /// Envía los datos iniciales de los tags cuando el canal se inicia o un tag se registra.
        /// </summary>
        private void sendInitialData(Guid? tagId = null)
        {
            long timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var tagsToProcess = tagId.HasValue
                ? _tags.Where(t => t.Key == tagId.Value).Select(t => t.Value)
                : _tags.Values;

            foreach (var tag in tagsToProcess)
            {
                var quality = tag.Status == SiemensTagWrapper.SiemensTagStatusType.ConfigError
                    ? QualityCodeOptions.Bad_Configuration_Error
                    : QualityCodeOptions.Uncertain_Non_Specific;
                PostNewEvent(tag.Tag.InitialValue, quality, tag.IdTag, timeStamp);
            }
        }

        /// <summary>
        /// Envía una calidad de "fuera de servicio" para los tags cuando el canal se detiene.
        /// </summary>
        private void sendChannelOrDeviceDisabledData(string? deviceId = null)
        {
            long timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var tag in _tags.Values)
            {
                PostNewEvent(tag.CurrentValue, QualityCodeOptions.Bad_Out_of_Service, tag.IdTag, timeStamp);
            }
        }

        /// <summary>
        /// Publica un nuevo evento de datos para un tag.
        /// </summary>
        private void PostNewEvent(object value, QualityCodeOptions quality, Guid idTag, long? timeStamp = null)
        {
            if (State != BaseChannelStateOptions.Started) return;

            if (_tags.TryGetValue(idTag, out var tag))
            {
                tag.CurrentValue = value;
                tag.CurrentQuality = quality;
                InvokeOnPostNewEvent(new RawData(value, quality, idTag, timeStamp));
            }
        }

        /// <summary>
        /// Actualiza el estado del canal y el tag de control 'Enable'.
        /// </summary>
        private void SetChannelEnabledState(bool newState)
        {
            State = newState ? BaseChannelStateOptions.Started : BaseChannelStateOptions.Stopped;
            // Lógica para actualizar el tag de control si existe.
        }

        // Manejadores de eventos para diagnósticos.
        private void _channelDiagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e) { /* ... */ }
        private void _channelDiagnostics_DevicePropertyChanged(object sender, DevicePropertyChangedEventArgs e) { /* ... */ }

        #endregion

        #region Métodos no implementados de BaseChannel
        public override bool ContainsTag(Guid idTag) => _tags.ContainsKey(idTag) || _controlTagsDictionary.ContainsKey(idTag);
        public override void UpdateConfiguration(JsonObject config) => initializeChannel(config);
        public override Result TagConfigurationIsValid(TagModelBase tagObject, string config) => Result.Return(true);
        public override void ProcessData(RawData rawData) => throw new NotImplementedException();
        public override TagPathFilterBase GetSubscribeFilter() => TagPathFilterBase.EmptySubscribe();
        public override DiagnosticTree GetChannelDiagnosticsTagsConfig(int idChannel) => new();
        public override DiagnosticTag GetChannelEnableControlTagConfig(int idChannel) => new();
        public override DiagnosticTag GetChannelRestartControlTagConfig(int idChannel) => new();
        #endregion
    }
}
