using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using ModbusModule.TagReader;

using vNode.Sdk.Logger;

namespace ModbusModule.Diagnostics
{
    /// <summary>
    /// Diagnostic metrics for a device and all its tags
    /// </summary>
    public class DeviceDiagnostics : DiagnosticMetrics, IDisposable
    {
        private Dictionary<Guid, TagDiagnostics> _tagDiagnostics = new Dictionary<Guid, TagDiagnostics>();
        private readonly ISdkLogger _logger;
        private readonly string _deviceId;
        private string _deviceName;
        private readonly int _rollingWindowSize;
        private bool _disposedValue;
        private readonly object _disposeLock = new object();

        /// <summary>
        /// Unique identifier for the device
        /// </summary>
        public string DeviceId => _deviceId;

        /// <summary>
        /// Name of the device
        /// </summary>
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        /// <summary>
        /// Size of the rolling window for calculating average metrics
        /// </summary>
        public int RollingWindowSize => _rollingWindowSize;

        /// <summary>
        /// Collection of diagnostic data for all tags in this device
        /// </summary>
        public Dictionary<Guid, TagDiagnostics> TagDiagnostics
        {
            get => _tagDiagnostics;
            private set => SetProperty(ref _tagDiagnostics, value);
        }

        public DeviceDiagnostics(string deviceId, string deviceName, ISdkLogger logger, int rollingWindowSize = 10)
        {
            _deviceId = deviceId;
            _deviceName = deviceName;
            _logger = logger;
            _rollingWindowSize = rollingWindowSize;
        }

        /// <summary>
        /// Process tag read completion and update diagnostics
        /// </summary>
        public void ReadCompleted(TagReadResult result)
        {
            ThrowIfDisposed();

            // Only process tags that belong to this device
            foreach (var resultItem in result.Items.Where(i => TagDiagnostics.ContainsKey(i.TagId)))
            {
                var tagDiagnostics = GetTagDiagnostics(resultItem.TagId);
                tagDiagnostics?.ReadCompleted(resultItem);
            }

            // Update aggregates after processing all items
            //UpdateAggregateMetrics();
        }

        /// <summary>
        /// Process tag write completion and update diagnostics
        /// </summary>
        public void WriteCompleted(Guid tagId, bool writeSuccess, long elapsedMs)
        {
            ThrowIfDisposed();

            if (!TagDiagnostics.ContainsKey(tagId))
                return;

            var tagDiagnostics = GetTagDiagnostics(tagId);
            tagDiagnostics?.WriteCompleted(writeSuccess, elapsedMs);
            //UpdateAggregateMetrics();
        }

        /// <summary>
        /// Handle property changes in the tag diagnostics to update aggregates
        /// </summary>
        private void TagDiagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateAggregateMetrics();
        }

        /// <summary>
        /// Register a tag with this device or update its diagnostics if already registered
        /// </summary>
        public TagDiagnostics RegisterTag(Guid tagId)
        {
            ThrowIfDisposed();

            if (!TagDiagnostics.TryGetValue(tagId, out var diagnostics))
            {
                diagnostics = new TagDiagnostics(tagId, _logger, _rollingWindowSize);
                diagnostics.PropertyChanged += TagDiagnostics_PropertyChanged;
                TagDiagnostics.Add(tagId, diagnostics);
                TagsCount = TagDiagnostics.Count();
            }

            return diagnostics;
        }

        /// <summary>
        /// Unregister a tag
        /// </summary>
        /// <param name="tagId"></param>
        /// <param name="deviceId"></param>
        public void UnregisterTag(Guid tagId)
        {
            ThrowIfDisposed();

            if (TagDiagnostics.TryGetValue(tagId, out var diagnostics))
            {
                diagnostics.PropertyChanged -= TagDiagnostics_PropertyChanged;
                TagDiagnostics.Remove(tagId);
                TagsCount = TagDiagnostics.Count();
            }
        }

        /// <summary>
        /// Retrieves the tag's diagnostic information by ID
        /// </summary>
        public TagDiagnostics GetTagDiagnostics(Guid tagId)
        {
            ThrowIfDisposed();

            if (TagDiagnostics.TryGetValue(tagId, out var diagnostics))
            {
                return diagnostics;
            }
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(nameof(DeviceDiagnostics));
            }
        }

        /// <summary>
        /// Calculates aggregate metrics for the entire device based on all contained tags
        /// </summary>
        public void UpdateAggregateMetrics()
        {
            int totalReads = 0;
            int totalWrites = 0;
            int failedWrites = 0;
            int overdueReads = 0;
            int failedReads = 0;
            double sumOverdueTime = 0;
            double sumWriteTime = 0;
            int overdueTimeSamples = 0;
            int writeTimeSamples = 0;

            foreach (var tag in TagDiagnostics.Values)
            {
                totalReads += tag.TotalReads;
                totalWrites += tag.TotalWrites;
                overdueReads += tag.OverdueReads;
                failedReads += tag.FailedReads;
                failedWrites += tag.FailedWrites;

                // Only include non-zero values in the average
                if (tag.AvgOverdueTime > 0)
                {
                    sumOverdueTime += tag.AvgOverdueTime;
                    overdueTimeSamples++;
                }

                if (tag.AvgWriteTime > 0)
                {
                    sumWriteTime += tag.AvgWriteTime;
                    writeTimeSamples++;
                }
            }

            // Set properties which will raise PropertyChanged events
            TotalReads = totalReads;
            TotalWrites = totalWrites;
            OverdueReads = overdueReads;
            FailedReads = failedReads;
            FailedWrites = failedWrites;

            // Calculate simple average of the tag rolling averages
            AvgOverdueTime = overdueTimeSamples > 0 ? sumOverdueTime / overdueTimeSamples : 0;
            AvgWriteTime = writeTimeSamples > 0 ? sumWriteTime / writeTimeSamples : 0;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    lock (_disposeLock)
                    {
                        _logger?.Debug("DeviceDiagnostics", $"Disposing DeviceDiagnostics for device {_deviceId} and cleaning up all registered tags");

                        if (_tagDiagnostics != null)
                        {
                            // Unsubscribe from and dispose all tag diagnostics
                            foreach (var tagDiag in _tagDiagnostics.Values)
                            {
                                tagDiag.PropertyChanged -= TagDiagnostics_PropertyChanged;

                                // Dispose tag diagnostics if it implements IDisposable
                                if (tagDiag is IDisposable disposableTag)
                                {
                                    disposableTag.Dispose();
                                }
                            }
                            _tagDiagnostics.Clear();
                            _tagDiagnostics = null;
                        }
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
