using SiemensModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Scheduler
{
    /// <summary>
    /// Argumentos del evento que se dispara cuando un lote de tags Siemens debe ser leído.
    /// </summary>
    public class SiemensReadingDueEventArgs : EventArgs
    {
        public SiemensTagBatch Batch { get; }
        public SiemensReadingDueEventArgs(SiemensTagBatch batch)
        {
            Batch = batch;
        }
    }

    /// <summary>
    /// Orquestador principal para la planificación y operaciones de lectura de tags Siemens S7.
    /// </summary>
    public class SiemensScheduler
    {
        private readonly object _lock = new();
        private readonly ISdkLogger _logger;
        private readonly TickScheduler _tickScheduler;
        private readonly HashSet<string> _deviceIds;
        private bool _running = false;

        public event EventHandler<SiemensReadingDueEventArgs>? ReadingDue;

        /// <summary>
        /// Inicializa el planificador Siemens con la configuración de dispositivos y parámetros de temporización.
        /// </summary>
        /// <param name="devices">Lista de dispositivos Siemens a planificar</param>
        /// <param name="logger">Logger para eventos específicos de Siemens</param>
        /// <param name="baseTickMs">Intervalo base de tick para la precisión de temporización (por defecto: 100ms)</param>
        public SiemensScheduler(List<ChannelConfig.SiemensDeviceConfig> devices, ISdkLogger logger, int baseTickMs = 100)
        {
            _logger = logger;
            _tickScheduler = new TickScheduler(logger, baseTickMs);
            _deviceIds = devices.Select(d => d.DeviceId).ToHashSet();
        }

        public bool Running => _running;

        public void StopReading()
        {
            _running = false;
        }

        /// <summary>
        /// Añade un tag Siemens al planificador.
        /// </summary>
        /// <param name="tag">Tag Siemens a planificar para lectura periódica</param>
        public void AddTag(SiemensTagWrapper tag)
        {
            lock (_lock)
            {
                _tickScheduler.AddTag(tag);
                Monitor.Pulse(_lock);
            }
        }

        /// <summary>
        /// Añade múltiples tags Siemens al planificador en una sola operación.
        /// </summary>
        /// <param name="tags">Colección de tags Siemens a planificar</param>
        public void AddTagsBatch(IList<SiemensTagWrapper> tags)
        {
            if (tags == null || tags.Count == 0) return;

            lock (_lock)
            {
                _tickScheduler.AddTagsBatch(tags);
                Monitor.Pulse(_lock);
            }
        }

        /// <summary>
        /// Elimina un tag del planificador.
        /// </summary>
        /// <param name="tagId">ID del tag a eliminar</param>
        public void RemoveTag(Guid tagId)
        {
            _tickScheduler.RemoveTag(tagId);
        }

        /// <summary>
        /// Elimina múltiples tags del planificador en una sola operación.
        /// </summary>
        /// <param name="tagIds">Colección de IDs de tags a eliminar</param>
        public void RemoveTags(IList<Guid> tagIds)
        {
            ArgumentNullException.ThrowIfNull(tagIds);

            if (tagIds.Count == 0) return;

            _logger.Debug("SiemensScheduler", $"Eliminando {tagIds.Count} tags del planificador");
            _tickScheduler.RemoveTags(tagIds);
        }

        /// <summary>
        /// Convierte los tags pendientes en lotes optimizados para la comunicación Siemens.
        /// Agrupa por DeviceId, DataType y ScanRate, y respeta el límite de 200 bytes por lote.
        /// </summary>
        private List<SiemensTagBatch> CreateBatchesFromDueTags(List<SiemensTagWrapper> dueTags)
        {
            var batches = new List<SiemensTagBatch>();

            // Agrupa por DeviceId, DataType y PollRate
            var grouped = dueTags
                .GroupBy(t => new { t.Config.DeviceId, t.Config.DataType, t.Config.PollRate });

            foreach (var group in grouped)
            {
                var tags = group.OrderBy(t => t.Config.Address).ToList();
                int batchStart = 0;

                while (batchStart < tags.Count)
                {
                    int batchSize = 0;
                    var batchTags = new List<SiemensTagConfig>();

                    for (int i = batchStart; i < tags.Count; i++)
                    {
                        int tagSize = tags[i].Config.GetSize();
                        if (batchSize + tagSize > 200 && batchTags.Count > 0)
                            break;

                        batchTags.Add(tags[i].Config);
                        batchSize += tagSize;
                    }

                    batches.Add(new SiemensTagBatch
                    {
                        DataType = group.Key.DataType,
                        PollRate = group.Key.PollRate,
                        DeviceId = group.Key.DeviceId,
                        Tags = batchTags
                    });

                    batchStart += batchTags.Count;
                }
            }

            return batches;
        }

        /// <summary>
        /// Bucle principal de planificación que coordina la temporización y el procesamiento específico de Siemens.
        /// </summary>
        /// <param name="cancellationToken">Token de cancelación para detener el bucle</param>
        public async Task StartReadingAsync(CancellationToken cancellationToken)
        {
            _tickScheduler.Reset();
            _running = true;

            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_tickScheduler.BaseTickMs));

            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await periodicTimer.WaitForNextTickAsync(cancellationToken);

                    _tickScheduler.Tick();
                    var dueTags = _tickScheduler.GetDueTags();

                    if (dueTags.Count == 0)
                        continue;

                    var readBatches = CreateBatchesFromDueTags(dueTags);

                    foreach (var batchItem in readBatches)
                    {
                        OnReadingDue(batchItem);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Fatal(ex, "SiemensScheduler", "Error no manejado en el bucle del planificador");
                }
            }
        }

        /// <summary>
        /// Obtiene estadísticas de planificación delegando en TickScheduler.
        /// </summary>
        public Dictionary<int, IntervalInfo> GetScheduleStats()
        {
            return _tickScheduler.GetScheduleStats();
        }

        /// <summary>
        /// Registra estadísticas detalladas de planificación.
        /// </summary>
        public void LogScheduleStats()
        {
            _tickScheduler.LogScheduleStats();
        }

        /// <summary>
        /// Lanza el evento ReadingDue para notificar que un lote está listo para la comunicación Siemens.
        /// </summary>
        private void OnReadingDue(SiemensTagBatch readBatch)
        {
            ReadingDue?.Invoke(this, new SiemensReadingDueEventArgs(readBatch));
        }
    }

    /// <summary>
    /// Lote de tags Siemens agrupados para lectura eficiente.
    /// </summary>
    public class SiemensTagBatch
    {
        public SiemensTagDataType DataType { get; set; }
        public int PollRate { get; set; }
        public string DeviceId { get; set; }
        public List<SiemensTagConfig> Tags { get; set; } = new();
    }

    /// <summary>
    /// Configuración del canal de producción.
    /// </summary>
    public class ChannelProductionConfig
    {
        public string NodeName { get; set; } = "ChannelProduction1";
        public string IpAddress { get; set; } = "192.168.1.100";
        public string CpuType { get; set; } = "S7300";
        public int Rack { get; set; } = 0;
        public int Slot { get; set; } = 2;
        public int PollingIntervalMs { get; set; } = 1000;
    }

    /// <summary>
    /// Configuración de los tags iniciales.
    /// </summary>
    public static class InitialTagConfig
    {
        public static List<SiemensTagConfig> Tags { get; } = new List<SiemensTagConfig>
        {
            new SiemensTagConfig
            {
                TagId = Guid.Parse("a1b2c3d4-e5f6-7890-1234-56789abcdef0"),
                Name = "Started",
                Address = "DB101.DBX0.0",
                DataType = SiemensTagDataType.Bool,
                PollRate = 500,
                BitNumber = 0,
                StringSize = 0,
                ArraySize = 0,
                IsReadOnly = false,
                DeviceId = "plc1"
            },
            new SiemensTagConfig
            {
                TagId = Guid.Parse("b2c3d4e5-f6a1-8901-2345-6789abcdef01"),
                Name = "Pressure",
                Address = "DB101.DBW2",
                DataType = SiemensTagDataType.Word,
                PollRate = 1000,
                BitNumber = null,
                StringSize = 0,
                ArraySize = 0,
                IsReadOnly = false,
                DeviceId = "plc1"
            }
        };
    }

    /// <summary>
    /// Deserializa la configuración del canal Siemens desde un JSON.
    /// </summary>
    public static class SiemensChannelConfigDeserializer
    {
        /// <summary>
        /// Deserializa un JSON en un objeto SiemensChannelConfig.
        /// </summary>
        /// <param name="channelJson">El JSON del canal.</param>
        /// <returns>Un objeto SiemensChannelConfig.</returns>
        public static SiemensChannelConfig Deserialize(string channelJson)
        {
            return JsonSerializer.Deserialize<SiemensChannelConfig>(channelJson);
        }
    }

    /// <summary>
    /// Método para inicializar el canal Siemens y registrar los tags.
    /// </summary>
    public static class SiemensChannelInitializer
    {
        public static void InitializeChannel(SiemensChannelConfig channelConfig, ISdkLogger logger, SiemensControl siemensControl, List<SiemensTagConfig>? tagsConfig)
        {
            var canal = new Siemens(channelConfig, logger, siemensControl);

            if (tagsConfig != null)
            {
                foreach (var tagConfig in tagsConfig)
                {
                    canal.RegisterTag(tagConfig);
                }
            }
        }
    }

    /// <summary>
    /// Representa un canal Siemens.
    /// </summary>
    public class Siemens
    {
        public Siemens(SiemensChannelConfig config, ISdkLogger logger, SiemensControl siemensControl)
        {
            // Inicialización del canal
        }

        public void RegisterTag(SiemensTagConfig tagConfig)
        {
            // Lógica para crear el wrapper y añadirlo al scheduler y diagnóstico
        }
    }
}
