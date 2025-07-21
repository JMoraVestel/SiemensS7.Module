using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.Sdk.Logger;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Scheduler
{
    public class TickScheduler
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, TagScheduleInfo> _tagSchedules = new();
        private readonly ISdkLogger _logger;
        private long _currentTick = 0;
        private int _baseTickMs;

        /// <summary>
        /// Initializes the tick scheduler with a configurable base tick rate.
        /// The base tick rate determines the precision of scheduling alignment.
        /// </summary>
        /// <param name="logger">Logger for timing-related events</param>
        /// <param name="baseTickMs">Base tick interval in milliseconds (default: 100ms for sub-second precision)</param>
        public TickScheduler(ISdkLogger logger, int baseTickMs = 100)
        {
            _logger = logger;
            _baseTickMs = baseTickMs;
            _logger.Info("TickScheduler", $"Inicializado con base tick rate: {_baseTickMs}ms");
        }

        public int BaseTickMs => _baseTickMs;

        /// <summary>
        /// Adds a tag to the scheduling system by calculating its tick interval
        /// and determining when it should fire next. Rounds up intervals to align with tick boundaries.
        /// </summary>
        /// <param name="tag">The tag to schedule</param>
        public void AddTag(SiemensTagWrapper tag)
        {
            lock (_lock)
            {
                var tickInterval = CalculateTickInterval(tag.Config.PollRate);
                var actualIntervalMs = tickInterval * _baseTickMs;

                var scheduleInfo = new TagScheduleInfo
                {
                    Tag = tag,
                    TickInterval = tickInterval,
                    NextFireTick = _currentTick + tickInterval, // Start from next interval
                    PollRateMs = tag.Config.PollRate,
                    ActualIntervalMs = actualIntervalMs
                };

                _tagSchedules[tag.Config.TagId] = scheduleInfo;

                if (actualIntervalMs != tag.Config.PollRate)
                {
                    _logger.Debug("TickScheduler",
                        $"Tag {tag.Config.TagId}: solicitado {tag.Config.PollRate}ms → real {actualIntervalMs}ms (cada {tickInterval} ticks)");
                }
                else
                {
                    _logger.Debug("TickScheduler",
                        $"Tag {tag.Config.TagId} con intervalo {tag.Config.PollRate}ms (cada {tickInterval} ticks)");
                }
            }
        }

        /// <summary>
        /// Adds multiple tags to the scheduling system in a single operation.
        /// More efficient than calling AddTag multiple times.
        /// </summary>
        /// <param name="tags">Collection of tags to schedule</param>
        public void AddTagsBatch(IList<SiemensTagWrapper> tags)
        {
            lock (_lock)
            {
                var intervalAdjustments = 0;

                foreach (var tag in tags)
                {
                    var tickInterval = CalculateTickInterval(tag.Config.PollRate);
                    var actualIntervalMs = tickInterval * _baseTickMs;

                    var scheduleInfo = new TagScheduleInfo
                    {
                        Tag = tag,
                        TickInterval = tickInterval,
                        NextFireTick = _currentTick + tickInterval,
                        PollRateMs = tag.Config.PollRate,
                        ActualIntervalMs = actualIntervalMs
                    };

                    _tagSchedules[tag.Config.TagId] = scheduleInfo;

                    if (actualIntervalMs != tag.Config.PollRate)
                    {
                        intervalAdjustments++;
                    }
                }

                _logger.Debug("TickScheduler",
                    $"Añadidos {tags.Count} tags en batch ({intervalAdjustments} intervalos ajustados a los ticks)");
            }
        }

        /// <summary>
        /// Removes a tag from the scheduling system by its ID.
        /// </summary>
        /// <param name="tagId">ID of the tag to remove</param>
        public void RemoveTag(Guid tagId)
        {
            lock (_lock)
            {
                if (_tagSchedules.Remove(tagId))
                {
                    _logger.Debug("TickScheduler", $"Tag {tagId} eliminado");
                }
            }
        }

        /// <summary>
        /// Removes multiple tags from the scheduling system in a single operation.
        /// </summary>
        /// <param name="tagIds">Collection of tag IDs to remove</param>
        public void RemoveTags(IList<Guid> tagIds)
        {
            lock (_lock)
            {
                var removedCount = 0;
                foreach (var tagId in tagIds)
                {
                    if (_tagSchedules.Remove(tagId))
                    {
                        removedCount++;
                    }
                }

                _logger.Debug("TickScheduler", $"Eliminados {removedCount} tags");
            }
        }

        /// <summary>
        /// Returns all tags that are due to fire on the current tick.
        /// This is the core timing logic - it determines which tags should be processed now
        /// and automatically schedules their next fire time.
        /// </summary>
        /// <returns>List of tags that are due for processing</returns>
        public List<SiemensTagWrapper> GetDueTags()
        {
            lock (_lock)
            {
                var dueTags = new List<SiemensTagWrapper>();

                foreach (var schedule in _tagSchedules.Values)
                {
                    if (schedule.NextFireTick <= _currentTick)
                    {
                        dueTags.Add(schedule.Tag);
                        // Schedule next fire
                        schedule.NextFireTick = _currentTick + schedule.TickInterval;
                    }
                }

                return dueTags;
            }
        }

        /// <summary>
        /// Advances the internal tick counter by one. This should be called
        /// at each base tick interval to maintain accurate timing.
        /// </summary>
        public void Tick()
        {
            lock (_lock)
            {
                _currentTick++;
            }
        }

        /// <summary>
        /// Resets the tick counter to zero and reschedules all tags to fire
        /// at their next interval. Called when the scheduler starts.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _currentTick = 0;
                foreach (var schedule in _tagSchedules.Values)
                {
                    schedule.NextFireTick = schedule.TickInterval; // Fire on first interval
                }
            }
        }

        /// <summary>
        /// Converts a poll rate in milliseconds to tick intervals, rounding up
        /// to ensure tags never poll faster than requested.
        /// 
        /// Examples with 100ms base tick:
        /// - 150ms → 2 ticks (200ms actual)
        /// - 500ms → 5 ticks (500ms actual)
        /// - 1000ms → 10 ticks (1000ms actual)
        /// </summary>
        /// <param name="pollRateMs">Requested poll rate in milliseconds</param>
        /// <returns>Number of ticks for this interval</returns>
        private int CalculateTickInterval(int pollRateMs)
        {
            // Round up to ensure we never poll faster than requested
            var tickInterval = (int)Math.Ceiling((double)pollRateMs / _baseTickMs);

            // Ensure minimum of 1 tick
            return Math.Max(1, tickInterval);
        }

        /// <summary>
        /// Provides statistics about scheduled intervals for monitoring and debugging.
        /// Shows how requested intervals map to actual tick-aligned intervals.
        /// </summary>
        /// <returns>Dictionary mapping requested intervals to their statistics</returns>
        public Dictionary<int, IntervalInfo> GetScheduleStats()
        {
            lock (_lock)
            {
                return _tagSchedules.Values
                    .GroupBy(s => s.PollRateMs)
                    .ToDictionary(g => g.Key, g => new IntervalInfo
                    {
                        RequestedIntervalMs = g.Key,
                        ActualIntervalMs = g.First().ActualIntervalMs,
                        TagCount = g.Count()
                    });
            }
        }

        /// <summary>
        /// Logs detailed schedule statistics to help understand timing alignment
        /// and identify any interval adjustments made by the tick system.
        /// </summary>
        public void LogScheduleStats()
        {
            var stats = GetScheduleStats();
            _logger.Info("TickScheduler", $"Estadísticas de planificación (Base tick: {_baseTickMs}ms):");

            foreach (var stat in stats.OrderBy(s => s.Key))
            {
                var info = stat.Value;
                if (info.RequestedIntervalMs != info.ActualIntervalMs)
                {
                    _logger.Info("TickScheduler",
                        $"  {info.RequestedIntervalMs}ms → {info.ActualIntervalMs}ms: {info.TagCount} tags");
                }
                else
                {
                    _logger.Info("TickScheduler",
                        $"  {info.RequestedIntervalMs}ms: {info.TagCount} tags");
                }
            }
        }
    }
}
