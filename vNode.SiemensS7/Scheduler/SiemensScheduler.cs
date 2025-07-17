using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vNode.Sdk.Logger;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Scheduler
{
    /// <summary>
    /// Argumentos del evento que se dispara cuando un grupo de tags debe ser leído.
    /// </summary>
    public class ReadingDueEventArgs : EventArgs
    {
        /// <summary>
        /// Diccionario de tags que deben ser leídos, con su ID como clave y su configuración como valor.
        /// </summary>
        public Dictionary<Guid, SiemensTagConfig> TagsToRead { get; }

        public ReadingDueEventArgs(Dictionary<Guid, SiemensTagConfig> tagsToRead)
        {
            TagsToRead = tagsToRead;
        }
    }

    /// <summary>
    /// Planificador encargado de gestionar y disparar las lecturas periódicas de tags para un PLC Siemens.
    /// </summary>
    public class SiemensScheduler
    {
        private readonly object _lock = new();
        private readonly ISdkLogger _logger;
        private readonly Dictionary<int, Dictionary<Guid, SiemensTagScheduleItem>> _scheduleItemsByPollRate = new();
        private bool _running = false;

        /// <summary>
        /// Evento que se dispara cuando uno o más tags están listos para ser leídos.
        /// </summary>
        public event EventHandler<ReadingDueEventArgs>? ReadingDue;

        public SiemensScheduler(ISdkLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Indica si el planificador está en ejecución.
        /// </summary>
        public bool Running => _running;

        /// <summary>
        /// Detiene el ciclo de lectura del planificador.
        /// </summary>
        public void StopReading()
        {
            _logger.Information("SiemensScheduler", "Deteniendo el planificador de lecturas.");
            _running = false;
        }

        /// <summary>
        /// Añade un tag al planificador para su sondeo periódico.
        /// </summary>
        /// <param name="tag">El wrapper del tag a añadir.</param>
        public void AddTag(SiemensTagWrapper tag)
        {
            if (tag.Config.PollRate <= 0)
            {
                // Los tags sin una tasa de sondeo positiva no se planifican para lectura periódica.
                return;
            }

            lock (_lock)
            {
                int pollRate = tag.Config.PollRate;
                if (!_scheduleItemsByPollRate.ContainsKey(pollRate))
                {
                    _scheduleItemsByPollRate[pollRate] = new Dictionary<Guid, SiemensTagScheduleItem>();
                }

                var scheduleItem = new SiemensTagScheduleItem(tag);
                _scheduleItemsByPollRate[pollRate][tag.Config.TagId] = scheduleItem;
                _logger.Debug("SiemensScheduler", $"Tag {tag.Config.TagId} añadido al planificador con una tasa de sondeo de {pollRate}ms.");
            }
        }

        /// <summary>
        /// Elimina un tag del planificador.
        /// </summary>
        /// <param name="tagId">El ID del tag a eliminar.</param>
        public void RemoveTag(Guid tagId)
        {
            lock (_lock)
            {
                foreach (var pollGroup in _scheduleItemsByPollRate.Values)
                {
                    if (pollGroup.Remove(tagId))
                    {
                        _logger.Debug("SiemensScheduler", $"Tag {tagId} eliminado del planificador.");
                        return; // Tag encontrado y eliminado.
                    }
                }
            }
        }

        /// <summary>
        /// Inicia el ciclo de lectura asíncrono del planificador.
        /// </summary>
        /// <param name="cancellationToken">Token para solicitar la cancelación del ciclo.</param>
        public async Task StartReadingAsync(CancellationToken cancellationToken)
        {
            _logger.Information("SiemensScheduler", "Iniciando el planificador de lecturas.");
            ResetAllReadTimes();

            _running = true;
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1)); // Intervalo de comprobación.

            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await periodicTimer.WaitForNextTickAsync(cancellationToken);

                    var dueTags = GetDueTags();

                    if (dueTags.Any())
                    {
                        ReadingDue?.Invoke(this, new ReadingDueEventArgs(dueTags));
                    }
                }
                catch (OperationCanceledException)
                {
                    _running = false;
                    _logger.Information("SiemensScheduler", "La operación de lectura del planificador fue cancelada.");
                }
                catch (Exception ex)
                {
                    _running = false;
                    _logger.Fatal(ex, "SiemensScheduler", "Error no manejado en el bucle del planificador.");
                }
            }
            _logger.Information("SiemensScheduler", "El planificador de lecturas se ha detenido.");
        }

        /// <summary>
        /// Obtiene un diccionario con los tags cuya lectura está pendiente.
        /// </summary>
        /// <returns>Un diccionario de tags listos para ser leídos.</returns>
        private Dictionary<Guid, SiemensTagConfig> GetDueTags()
        {
            var dueTags = new Dictionary<Guid, SiemensTagConfig>();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                foreach (var item in _scheduleItemsByPollRate.Values.SelectMany(group => group.Values))
                {
                    if (now >= item.NextReadTime)
                    {
                        dueTags.Add(item.Tag.Config.TagId, item.Tag.Config);
                        item.UpdateNextReadTime(); // Reprograma la siguiente lectura.
                    }
                }
            }
            return dueTags;
        }

        /// <summary>
        /// Agrupa los tags listos para leer en lotes por DataType y PollRate, respetando un tamaño máximo de lote.
        /// </summary>
        public List<SiemensTagBatch> CreateDueTagBatches()
        {
            var batches = new List<SiemensTagBatch>();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                // Agrupa los tags por DataType y PollRate
                var grouped = _scheduleItemsByPollRate
                    .SelectMany(kv => kv.Value.Values)
                    .Where(item => now >= item.NextReadTime)
                    .GroupBy(item => new { item.Tag.Config.DataType, item.Tag.Config.PollRate });

                foreach (var group in grouped)
                {
                    var tags = group.OrderBy(t => t.Tag.Config.Address).ToList();
                    int batchStart = 0;

                    while (batchStart < tags.Count)
                    {
                        int batchSize = 0;
                        var batchTags = new List<SiemensTagConfig>();

                        for (int i = batchStart; i < tags.Count; i++)
                        {
                            int tagSize = tags[i].Tag.Config.GetSize();
                            if (batchSize + tagSize > 200 && batchTags.Count > 0)
                                break;

                            batchTags.Add(tags[i].Tag.Config);
                            batchSize += tagSize;
                        }

                        batches.Add(new SiemensTagBatch
                        {
                            DataType = group.Key.DataType,
                            PollRate = group.Key.PollRate,
                            Tags = batchTags
                        });

                        batchStart += batchTags.Count;
                    }
                }
            }

            return batches;
        }

        /// <summary>
        /// Reinicia los tiempos de lectura de todos los tags al iniciar el planificador.
        /// </summary>
        private void ResetAllReadTimes()
        {
            lock (_lock)
            {
                foreach (var item in _scheduleItemsByPollRate.Values.SelectMany(group => group.Values))
                {
                    item.ResetReadTime();
                }
            }
        }
    }

    /// <summary>
    /// Clase interna para almacenar la información de planificación de un tag individual.
    /// </summary>
    internal class SiemensTagScheduleItem
    {
        public SiemensTagWrapper Tag { get; }
        public DateTime NextReadTime { get; private set; }

        public SiemensTagScheduleItem(SiemensTagWrapper tag)
        {
            Tag = tag;
            ResetReadTime();
        }

        /// <summary>
        /// Actualiza la próxima hora de lectura basándose en la hora actual y la tasa de sondeo.
        /// </summary>
        public void UpdateNextReadTime()
        {
            NextReadTime = DateTime.UtcNow.AddMilliseconds(Tag.Config.PollRate);
        }

        /// <summary>
        /// Establece la hora de la próxima lectura al iniciar el planificador.
        /// </summary>
        public void ResetReadTime()
        {
            NextReadTime = DateTime.UtcNow.AddMilliseconds(Tag.Config.PollRate);
        }
    }

    /// <summary>
    /// Representa un lote de tags agrupados por tipo de dato y tasa de sondeo.
    /// </summary>
    public class SiemensTagBatch
    {
        public SiemensTagDataType DataType { get; set; }
        public int PollRate { get; set; }
        public List<SiemensTagConfig> Tags { get; set; } = new();
    }
}
