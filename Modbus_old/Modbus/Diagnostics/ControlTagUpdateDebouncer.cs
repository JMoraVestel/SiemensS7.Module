using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusModule.Diagnostics
{
    /// <summary>
    /// A simple debouncer for Modbus control tag updates.
    /// </summary>
    public class ControlTagUpdateDebouncer : IDisposable
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, HashSet<string>> _pendingUpdates = new();
        private readonly TimeSpan _debounceInterval;
        private readonly Action<string, IEnumerable<string>> _updateAction;
        private System.Threading.Timer? _timer;
        private bool _disposed;

        /// <summary>
        /// Creates a new debouncer for control tag updates.
        /// </summary>
        /// <param name="intervalMs">The debounce interval in milliseconds</param>
        /// <param name="updateAction">The action to execute for batch updates</param>
        public ControlTagUpdateDebouncer(int intervalMs, Action<string, IEnumerable<string>> updateAction)
        {
            _debounceInterval = TimeSpan.FromMilliseconds(intervalMs);
            _updateAction = updateAction ?? throw new ArgumentNullException(nameof(updateAction));
        }

        /// <summary>
        /// Queues a control tag update to be processed after the debounce interval,
        /// or processes it immediately if requested.
        /// </summary>
        /// <param name="deviceId">The device ID or null for channel-level tags</param>
        /// <param name="tagName">The name of the tag to update</param>
        /// <param name="immediate">If true, bypasses debouncing and updates immediately</param>
        public void QueueUpdate(string? deviceId, string tagName, bool immediate = false)
        {
            string key = deviceId ?? "channel";

            if (immediate)
            {
                // Process immediately
                _updateAction(key, new[] { tagName });
                return;
            }

            lock (_lock)
            {
                if (_disposed) return;

                if (!_pendingUpdates.TryGetValue(key, out var tags))
                {
                    tags = new HashSet<string>();
                    _pendingUpdates[key] = tags;
                }

                tags.Add(tagName);

                // Start timer if not running
                if (_timer == null)
                {
                    _timer = new System.Threading.Timer(
                        ProcessUpdates,
                        null,
                        _debounceInterval,
                        Timeout.InfiniteTimeSpan);
                }
            }
        }

        private void ProcessUpdates(object? state)
        {
            Dictionary<string, HashSet<string>> updates;

            lock (_lock)
            {
                if (_disposed) return;

                updates = new Dictionary<string, HashSet<string>>(_pendingUpdates);
                _pendingUpdates.Clear();
                _timer = null;
            }

            // Process updates outside the lock
            foreach (var kvp in updates)
            {
                _updateAction(kvp.Key, kvp.Value);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _timer?.Dispose();
                _timer = null;
                _pendingUpdates.Clear();
                _disposed = true;
            }
        }
    }
}
