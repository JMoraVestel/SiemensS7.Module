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
        /// <summary>
        /// Obtiene el identificador único del canal.
        /// </summary>
        public Guid IdChannel { get; }

        private readonly ISdkLogger _logger;
        private readonly string _nodeName;

        // Diccionarios de tags y tags de control.
        private readonly Dictionary<Guid, SiemensTagWrapper> _tags = new();
        private readonly Dictionary<Guid, SiemensControlTag> _controlTagsDictionary = new();

        /// <summary>
        /// Obtiene el número de tags de datos registrados en el canal.
        /// </summary>
        public int TagsCount => _tags.Count;

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
        public Siemens(Guid idChannel, string nodeName, JsonObject configJson, ISdkLogger logger, SiemensControl siemensControl)
        {
            ArgumentNullException.ThrowIfNull(configJson);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(siemensControl);

            if (string.IsNullOrWhiteSpace(configJson.ToString()))
                throw new ArgumentException("La configuración JSON no puede ser nula o vacía.", nameof(configJson));

            IdChannel = idChannel;
            _nodeName = nodeName;
            _logger = logger;
            _siemensControl = siemensControl;

            _logger.Information("Siemens", $"Inicializando el canal Siemens.");
            initializeChannel(configJson);
        }

        /// <summary>
        /// Inicializa o reinicializa el canal con una nueva configuración.
        /// </summary>
        private void initializeChannel(JsonObject configJson)
        {
            _logger.Information("Siemens",
                "initializeChannel-> Leyendo configuración. Configuración del canal:\n" +
                JsonSerializer.Serialize(configJson, new JsonSerializerOptions { WriteIndented = true }));
            try
            {
                _config = SiemensChannelConfig.FromJson(configJson);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "initializeChannel-> Error al leer la configuración del canal.");
                throw new InvalidChannelConfigException(ex);
            }

            // Validación robusta de dispositivos
            var deviceList = _config.Devices?.Values.ToList();
            if (deviceList == null || deviceList.Count == 0)
            {
                _logger.Error("Siemens", "initializeChannel-> No se encontraron dispositivos en la configuración del canal.");
                throw new InvalidChannelConfigException(new Exception("No hay dispositivos configurados en el canal Siemens."));
            }

            // Inicializa el lector de tags
            var plcConnection = new SiemensTcpStrategy(_config.IpAddress, _config.Rack, _config.Slot);
            _siemensTagReader = new SiemensTagReader(plcConnection, _config, _logger);

            // Inicializa el planificador de lecturas
            _scheduler = new SiemensScheduler(deviceList, _logger);
            _scheduler.ReadingDue += HandleReadingDue;

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
                    _channelControl.RegisterTag(tag.Config.TagId, "plc");
                }
            }
        }

        #region Implementación de BaseChannel (métodos públicos)

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
                SendInitialData();

                _cts = new CancellationTokenSource();
                _readingTask = Task.Run(() => _scheduler.StartReadingAsync(_cts.Token));

                _started = true;
                _logger.Information("Siemens", "Start: El canal Siemens se ha iniciado correctamente.");
                return true;
            }
        }

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

                _scheduler?.StopReading();

                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

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

                SendChannelOrDeviceDisabledData();

                SetChannelEnabledState(false);
                _started = false;
                _logger.Information("Siemens", "Stop: El canal Siemens se ha detenido correctamente.");
                return true;
            }
        }

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
                    _tags[tagObject.IdTag] = wrapper;
                    return false;
                }

                _tags[tagObject.IdTag] = wrapper;
                _channelControl.RegisterTag(wrapper.Config.TagId, "plc");
                if (wrapper.Config.PollRate > 0)
                {
                    _scheduler.AddTag(wrapper);
                }

                if (_started)
                {
                    SendInitialData(tagObject.IdTag);
                }

                _logger.Information("Siemens", $"Tag '{tagObject.Name}' (ID: {tagObject.IdTag}) registrado exitosamente.");
                return true;
            }
        }

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

        public override async Task<string> SetTagValue(Guid idTag, object newValue)
        {
            _logger.Debug("Siemens", $"Iniciando escritura del valor '{newValue}' en el tag ID '{idTag}'.");

            if (_controlTagsDictionary.TryGetValue(idTag, out var controlTag))
            {
                _logger.Warning("Siemens", $"La escritura en tags de control (ID: {idTag}) no está implementada.");
                return "Error: La escritura en tags de control no está soportada actualmente.";
            }

            if (!_tags.TryGetValue(idTag, out var tagWrapper))
            {
                _logger.Error("Siemens", $"No se pudo escribir en el tag. ID no encontrado: {idTag}.");
                return "Error: Tag no encontrado.";
            }

            if (tagWrapper.Config.IsReadOnly)
            {
                _logger.Warning("Siemens", $"No se puede escribir en el tag. El tag (ID: {idTag}, Dirección: {tagWrapper.Config.Address}) es de solo lectura.");
                return "Error: El tag es de solo lectura.";
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            try
            {
                bool success = await _siemensTagReader.WriteTagAsync(tagWrapper, newValue);
                stopWatch.Stop();

                if (success)
                {
                    _channelControl.WriteCompleted(tagWrapper.Config.TagId, true, stopWatch.ElapsedMilliseconds);
                    _logger.Debug("Siemens", $"Escritura del tag exitosa. Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                    PostNewEvent(newValue, QualityCodeOptions.Good_Non_Specific, tagWrapper.Config.TagId);
                    return "Ok";
                }
                else
                {
                    _channelControl.WriteCompleted(tagWrapper.Config.TagId, false, stopWatch.ElapsedMilliseconds);
                    _logger.Error("Siemens", $"La escritura en el tag (ID: {idTag}) falló sin una excepción. Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                    return "Error: La operación de escritura falló.";
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException)
            {
                stopWatch.Stop();
                _channelControl.WriteCompleted(tagWrapper.Config.TagId, false, stopWatch.ElapsedMilliseconds);
                _logger.Error(ex, "Siemens", $"Error de datos al escribir en el tag (ID: {idTag}). Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                return $"Error: {ex.Message}";
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                _channelControl.WriteCompleted(tagWrapper.Config.TagId, false, stopWatch.ElapsedMilliseconds);
                _logger.Error(ex, "Siemens", $"Error inesperado al escribir en el tag (ID: {idTag}). Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                return "Error: Ocurrió un error inesperado. Revise los logs para más detalles.";
            }
        }

        public override void Dispose()
        {
            _logger.Information("Siemens", "Dispose: Solicitud de liberación del canal.");
            Stop();
            _channelControl?.Dispose();
            if (_scheduler != null)
            {
                _scheduler.ReadingDue -= HandleReadingDue;
            }
            _siemensControl.UnregisterChannel(this);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Métodos privados y manejadores de eventos

        /// <summary>
        /// Manejador para el evento de lectura programada del planificador.
        /// </summary>
        private void HandleReadingDue(object? sender, SiemensReadingDueEventArgs e)
        {
            if (!_started || e?.Batch == null || e.Batch.Tags == null) return;

            try
            {
                var tagsToReadWrappers = e.Batch.Tags
                    .Select(cfg => cfg.TagId)
                    .Where(id => _tags.ContainsKey(id))
                    .ToDictionary(id => id, id => _tags[id]);

                if (tagsToReadWrappers.Count > 0)
                {
                    var results = _siemensTagReader.ReadManyForSdk(tagsToReadWrappers);
                    ProcessReadResult(results);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "Error durante la ejecución del lote de lectura.");
            }
        }

        /// <summary>
        /// Procesa los resultados de una operación de lectura y publica los nuevos valores.
        /// </summary>
        private void ProcessReadResult(Dictionary<Guid, vNode.SiemensS7.TagReader.TagReadResult> results)
        {
            if (!_started) return;

            foreach (var kvp in results)
            {
                if (kvp.Value.Items != null)
                {
                    foreach (var item in kvp.Value.Items)
                    {
                        PostNewEvent(item.Value, item.ResultCode == vNode.SiemensS7.TagReader.TagReadResult.TagReadResultType.Success
                            ? QualityCodeOptions.Good_Non_Specific
                            : QualityCodeOptions.Bad_Non_Specific,
                            item.BatchItem.Tag.Config.TagId,
                            item.BatchItem.ReadDueTime.ToFileTimeUtc());
                    }
                }
            }
        }

        /// <summary>
        /// Envía los datos iniciales de los tags cuando el canal se inicia o un tag se registra.
        /// </summary>
        private void SendInitialData(Guid? tagId = null)
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
                PostNewEvent(tag.CurrentValue, quality, tag.Config.TagId, timeStamp);
            }
        }

        /// <summary>
        /// Envía una calidad de "fuera de servicio" para los tags cuando el canal se detiene.
        /// </summary>
        private void SendChannelOrDeviceDisabledData(string? deviceId = null)
        {
            long timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var tag in _tags.Values)
            {
                PostNewEvent(tag.CurrentValue, QualityCodeOptions.Bad_Out_of_Service, tag.Config.TagId, timeStamp);
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
