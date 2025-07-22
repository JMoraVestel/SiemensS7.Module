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
    public class Siemens : BaseChannel, IDisposable
    {
        private readonly ISdkLogger _logger;
        private readonly SiemensScheduler _scheduler;
        private readonly SiemensControl _siemensControl;
        private readonly Dictionary<Guid, SiemensTagWrapper> _tags = new();
        private CancellationTokenSource? _cts;
        private Task? _readingTask;

        /// <summary>
        /// Constructor para el canal Siemens.
        /// </summary>
        /// <param name="config">Configuración del canal.</param>
        /// <param name="logger">Instancia del logger para registrar eventos.</param>
        /// <param name="siemensControl">Instancia del control Siemens para gestionar la comunicación.</param>
        /// 
        public Siemens(SiemensChannelConfig config, ISdkLogger logger, SiemensControl siemensControl)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _siemensControl = siemensControl ?? throw new ArgumentNullException(nameof(siemensControl));
            _scheduler = new SiemensScheduler(config.Devices.Values.ToList(), logger);
        }

        public override bool ContainsTag(Guid idTag)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override DiagnosticTree GetChannelDiagnosticsTagsConfig(int idChannel)
        {
            throw new NotImplementedException();
        }

        public override DiagnosticTag GetChannelEnableControlTagConfig(int idChannel)
        {
            throw new NotImplementedException();
        }

        public override DiagnosticTag GetChannelRestartControlTagConfig(int idChannel)
        {
            throw new NotImplementedException();
        }

        public override TagPathFilterBase GetSubscribeFilter()
        {
            throw new NotImplementedException();
        }

        public override void ProcessData(RawData rawData)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Registra un nuevo tag en el canal.
        /// </summary>
        public void RegisterTag(SiemensTagConfig tagConfig)
        {
            if (tagConfig == null)
                throw new ArgumentNullException(nameof(tagConfig));

            var tagModel = new TagModelBase
            {
                IdTag = tagConfig.TagId,
                Config = Newtonsoft.Json.JsonConvert.SerializeObject(tagConfig),
                InitialValue = null
            };

            var wrapper = SiemensTagWrapper.Create(tagModel, _logger);

            _tags[tagConfig.TagId] = wrapper;
            _scheduler.AddTag(wrapper);
            _siemensControl.RegisterTag(tagModel);

            _logger.Information("Siemens", $"Tag registrado: {tagConfig.Name} ({tagConfig.TagId})");
        }

        public override bool RegisterTag(TagModelBase tagObject)
        {
            throw new NotImplementedException();
        }

        public override bool RemoveTag(Guid idTag)
        {
            throw new NotImplementedException();
        }

        public override Task<string> SetTagValue(Guid idTag, object newValue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Inicia el canal Siemens.
        /// </summary>
        public void _start()
        {
            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();
            _readingTask = _scheduler.StartReadingAsync(_cts.Token);
            _logger.Information("Siemens", "Canal Siemens iniciado.");
        }

        public override bool Start()
        {
            try
            {
                _start(); // Llama al método real
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "Error al iniciar el canal Siemens.");
                return false;
            }
        }

        /// <summary>
        /// Detiene el canal Siemens.
        /// </summary>
        public void _stop()
        {
            if (_cts == null)
                return;

            _scheduler.StopReading();
            _cts.Cancel();
            try
            {
                _readingTask?.Wait(500);
            }
            catch { }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _readingTask = null;
            }
            _logger.Information("Siemens", "Canal Siemens detenido.");
        }

        public override bool Stop()
        {
            try
            {
                _stop(); // Llama al método real 
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Siemens", "Error al detener el canal Siemens.");
                return false;
            }
        }

        public override Result TagConfigurationIsValid(TagModelBase tagObject, string config)
        {
            throw new NotImplementedException();
        }

        public override void UpdateConfiguration(JsonObject config)
        {
            throw new NotImplementedException();
        }

        public int TagsCount => _tags.Count;
    }
}
