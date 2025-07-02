using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModbusModule.TagReader;

using vNode.Sdk.Logger;

namespace ModbusModule.Diagnostics
{
    /// <summary>
    /// Diagnostic metrics for an individual tag
    /// </summary>
    public class TagDiagnostics : DiagnosticMetrics
    {
        private readonly ISdkLogger _logger;
        private readonly Queue<double> _recentOverdueTimes;
        private readonly Queue<double> _recentWriteTimes;
        private readonly int _rollingWindowSize;

        private Guid _tagId;
        /// <summary>
        /// Unique identifier for the tag
        /// </summary>
        public Guid TagId
        {
            get => _tagId;
            private set => _tagId = value;
        }

        /// <summary>
        /// Size of the rolling window for calculating average metrics
        /// </summary>
        public int RollingWindowSize => _rollingWindowSize;

        /// <summary>
        /// Average delay in milliseconds for read operations (rolling average of last N reads)
        /// Will be 0 if the most recent N reads were all on time
        /// </summary>
        public new double AvgOverdueTime
        {
            get => base.AvgOverdueTime;
            set => base.AvgOverdueTime = value;
        }

        public TagDiagnostics(Guid tagId, ISdkLogger logger, int rollingWindowSize = 10)
        {
            TagId = tagId;
            _logger = logger;
            _rollingWindowSize = rollingWindowSize;
            _recentOverdueTimes = new Queue<double>(_rollingWindowSize);
            _recentWriteTimes = new Queue<double>(_rollingWindowSize);
        }

        /// <summary>
        /// Method to execute when a tag write operation is complete.
        /// </summary>
        /// <param name="writeSuccess"></param>
        /// <param name="elapsedMs"></param>
        public void WriteCompleted(bool writeSuccess, long elapsedMs)
        {
            if (writeSuccess)
            {
                TotalWrites++;

                // Add to rolling window of write times
                _recentWriteTimes.Enqueue(elapsedMs);

                // Keep only the last N write times
                if (_recentWriteTimes.Count > _rollingWindowSize)
                {
                    _recentWriteTimes.Dequeue();
                }

                // Update the rolling average
                AvgWriteTime = _recentWriteTimes.Count > 0 ? _recentWriteTimes.Average() : 0;
            }
            else
            {
                FailedWrites++;
            }
        }

        public void ReadCompleted(TagReadResultItem resultItem)
        {
            updateReadOverdueStats(resultItem.BatchItem.ReadDueTime, resultItem.BatchItem.ActualReadTime);

            if (resultItem.ResultCode == TagReadResult.TagReadResultType.Success)
                TotalReads++;
            else
                FailedReads++;
        }

        /// <summary>
        /// Updates the overdue time statistics using a rolling window average
        /// </summary>
        /// <param name="dueTime">When the read was scheduled to occur</param>
        /// <param name="actualTime">When the read actually occurred</param>
        private void updateReadOverdueStats(DateTime dueTime, DateTime actualTime)
        {
            double overdueTimeMs = 0;

            // Calculate overdue time if the actual time is after the due time
            if (actualTime > dueTime)
            {
                overdueTimeMs = actualTime.Subtract(dueTime).TotalMilliseconds;

                // Check if overdue exceeds threshold for counting overdue reads
                if (overdueTimeMs > readOverdueThreshold)
                {
                    // Increment the count of overdue reads
                    OverdueReads++;
                }
            }

            // Always add to the rolling window - with 0 for timely reads
            // This ensures 10 consecutive timely reads will result in avg = 0
            _recentOverdueTimes.Enqueue(overdueTimeMs > readOverdueThreshold ? overdueTimeMs : 0);

            // Keep only the last N overdue times
            if (_recentOverdueTimes.Count > _rollingWindowSize)
            {
                _recentOverdueTimes.Dequeue();
            }

            // Update the rolling average based on the window
            AvgOverdueTime = _recentOverdueTimes.Count > 0 ? _recentOverdueTimes.Average() : 0;
        }

        /// <summary>
        /// Resets overdue metrics - called when we want to clear old data
        /// </summary>
        public void ResetOverdueMetrics()
        {
            _recentOverdueTimes.Clear();
            AvgOverdueTime = 0;
        }
    }
}
