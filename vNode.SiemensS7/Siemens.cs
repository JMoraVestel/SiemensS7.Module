using System.Text.Json.Nodes;
using vNode.Sdk.Base;
using vNode.Sdk.Helper;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.TagConfig;

namespace SiemensModule {

    public class Siemens: BaseChannel
    {
        private readonly HashSet<Guid> _errorTags = [];
        private readonly ISdkLogger _logger;

        private readonly Dictionary<Guid, SiemensTagWrapper> _tags = new  Dictionary<Guid, SiemensTagWrapper>();

        private SiemensChannelConfig _config;
        private ChannelDiagnostics _channelControl;
        private CancellationTokenSource? _cts;

        // Dictionary to track diagnostic tags by their type
        private readonly Dictionary<Guid, SiemensControlTag> _controlTagsDictionary = [];

        public Siemens(string name, string address, int port = 102) : base(name, address, port)
        {
        }
        public override void Connect()
        {
            // Implement connection logic here
            throw new NotImplementedException();
        }
        public override void Disconnect()
        {
            // Implement disconnection logic here
            throw new NotImplementedException();
        }
        public override void ReadTag(string tagName)
        {
            // Implement tag reading logic here
            throw new NotImplementedException();
        }
        public override void WriteTag(string tagName, object value)
        {
            // Implement tag writing logic here
            throw new NotImplementedException();
        }

        private void initializeChannel(JsonObject configJson)
        {
            // Log de inicio de la inicialización del canal
            _logger.Information("Modbus",
                "initializeChannel-> Reading configuration. Channel config:\n" +
                JsonHelper.PrettySerialize(configJson));
            try
            {
                // Lee y valida la configuración del canal desde el JSON proporcionado
                _config = readConfig(configJson);
            }
            catch (Exception ex)
            {
                // Manejo de errores en caso de que la configuración sea inválida
                _logger.Error(ex, "Modbus", "initializeChannel-> Error reading channel configuration");
                throw new InvalidChannelConfigException(ex);
            }

            // Limpia el control del canal existente si ya está inicializado
            if (_channelControl != null)
            {
                _logger.Information("Modbus", "initializeChannel-> Disposing current channelControl");

                // Desuscribirse de eventos del control del canal
                _channelControl.PropertyChanged -= _channelDiagnostics_PropertyChanged;
                _channelControl.DevicePropertyChanged -= _channelDiagnostics_DevicePropertyChanged;

                // Liberar recursos del control del canal
                _channelControl.Dispose();
                _channelControl = null;
            }

            // Inicializa un nuevo control del canal y suscribe eventos
            _logger.Information("Modbus",
                "initializeChannel-> Creating channelControl and subscribing to metrics property changes ...");
            _channelControl = new(_config.Devices, _logger);
            _channelControl.PropertyChanged += _channelDiagnostics_PropertyChanged;
            _channelControl.DevicePropertyChanged += _channelDiagnostics_DevicePropertyChanged;

            // Re-registra todos los tags existentes en el nuevo control del canal
            foreach (var tag in _tags.Values)
            {
                if (tag.Status != ModbusTagWrapper.ModbusTagStatusType.ConfigError)
                {
                    try
                    {
                        _channelControl.RegisterTag(tag.Tag.IdTag, tag.Config.DeviceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Modbus", $"initializeChannel-> Error re-registering tag {tag.Tag.IdTag} with new channel control");
                    }
                }
            }

            // Limpia el planificador existente si ya está inicializado
            if (_scheduler != null)
            {
                _logger.Trace("Modbus", "initializeChannel-> Disposing current scheduler");
                stopScheduler();
                _scheduler = null;
            }

            // Inicializa un nuevo planificador de lecturas periódicas
            _logger.Information("Modbus", "initializeChannel-> Initializing scheduler...");
            _scheduler = initializeTaskScheduler();

            // Agrega los tags registrados al nuevo planificador
            if (_tags != null && _tags.Count > 0)
            {
                _logger.Debug("Modbus", "initializeChannel-> Adding already registered tags to scheduler...");
                foreach (var userTag in _tags)
                {
                    _scheduler.AddTag(userTag.Value);
                }
            }
        }
    }
    

}