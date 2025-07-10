using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.TagReader;

namespace vNode.SiemensS7.Diagnostics
{
    public class ChannelDiagnostics : DiagnosticMetrics, IDisposable
    {
        private Dictionary<string, DeviceDiagnostics> _deviceDiagnostics = new Dictionary<string, DeviceDiagnostics>();
        private readonly ISdkLogger _logger;
        private readonly int _rollingWindowSize;
        public event EventHandler<DevicePropertyChangedEventArgs> DevicePropertyChanged;


        public ChannelDiagnostics(Dictionary<string, SiemensChannelConfig> devices, ISdkLogger logger, int rollingWindowSize = 10)
        {
            _logger = logger;
            _rollingWindowSize = rollingWindowSize;

            foreach (var device in devices)
            {
                // Auto-create the device with a default name
                var deviceId = device.Key;
                var deviceDiagnostics = new DeviceDiagnostics(device.Key, $"SiemensDevice-{deviceId}", _logger, _rollingWindowSize);

                // Subscribe to property changes on the new device diagnostics
                deviceDiagnostics.PropertyChanged += DeviceDiagnostics_PropertyChanged;
                DeviceDiagnostics.Add(deviceId, deviceDiagnostics);
            }
        }

        /// <summary>
        /// Size of the rolling window for calculating average metrics
        /// </summary>
        public int RollingWindowSize => _rollingWindowSize;

        /// <summary>
        /// Collection of diagnostic data for all devices in this channel
        /// </summary>
        public Dictionary<string, DeviceDiagnostics> DeviceDiagnostics
        {
            get => _deviceDiagnostics;
            private set => SetProperty(ref _deviceDiagnostics, value);
        }


        /// <summary>
        /// Process tag read completion and update diagnostics
        /// </summary>
        public void ReadCompleted(TagReadResult result)
        {
            ThrowIfDisposed();

            // Group result items by device and process each device's items
            var deviceTagMap = MapTagsToDevices(result.Items.Select(i => i.TagId));

            foreach (var deviceId in deviceTagMap.Keys)
            {
                var deviceDiagnostics = GetDeviceDiagnostics(deviceId);
                if (deviceDiagnostics != null)
                {
                    deviceDiagnostics.ReadCompleted(result);
                }
                else
                {
                    _logger.Error("ChannelDiagnostics", $"Invalid device id [{deviceId}]: no device diagnostics found");
                }
            }
        }


        /// <summary>
        /// Process tag read failure and update diagnostics
        /// </summary>
        /// <param name="tagId"></param>
        public void ReadRetry(string deviceId)
        {
            ThrowIfDisposed();

            var deviceDiagnostics = GetDeviceDiagnostics(deviceId);
            if (deviceDiagnostics != null)
            {
                deviceDiagnostics.Retries++;
            }
        }

        /// <summary>
        /// Process tag write completion and update diagnostics
        /// </summary>
        public void WriteCompleted(Guid tagId, bool writeSuccess, long elapsedMs)
        {
            ThrowIfDisposed();

            string? deviceId = GetDeviceForTag(tagId);
            if (deviceId != null)
            {
                var deviceDiagnostics = GetDeviceDiagnostics(deviceId);
                deviceDiagnostics.WriteCompleted(tagId, writeSuccess, elapsedMs);
            }
            else
            {
                _logger.Error("ChannelDiagnostics", $"Tag id {tagId} is not registered");
            }
        }

        /// <summary>
        /// Handle property changes in the device diagnostics to update aggregates
        /// </summary>
        private void DeviceDiagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Forward the device property change with device ID information
            if (sender is DeviceDiagnostics deviceDiagnostics)
            {

                // Raise the device property changed event
                DevicePropertyChanged?.Invoke(this, new DevicePropertyChangedEventArgs(
                    deviceDiagnostics.DeviceId,
                    e.PropertyName));
            }

            UpdateAggregateMetrics();
        }

        /// <summary>
        /// Register a tag with a specific device.
        /// </summary>
        public void RegisterTag(Guid tagId, string deviceId)
        {
            ThrowIfDisposed();

            // Get or create the device diagnostics
            var deviceDiagnostics = GetDeviceDiagnostics(deviceId);

            if (deviceDiagnostics != null)
            {
                _logger.Debug("ChannelDiagnostics", $"Registering tag id [{tagId}] of device [{deviceId}].");
                // Register the tag with the device
                deviceDiagnostics.RegisterTag(tagId);
                return;
            }
            else
            {
                _logger.Error("ChannelDiagnostics", $"Invalid device id [{deviceId}]: no deviceDiagnostics found");
                return;
            }
        }

        /// <summary>
        /// Unregister a tag
        /// </summary>
        /// <param name="tagId"></param>
        /// <param name="deviceId"></param>
        public void UnregisterTag(Guid tagId)
        {
            ThrowIfDisposed();

            // Get or create the device diagnostics
            var deviceDiagnostics = GetDeviceDiagnosticsForTag(tagId);

            if (deviceDiagnostics != null)
            {
                _logger.Warning("ChannelDiagnostics", $"Unregistering tag id [{tagId}]...");
                deviceDiagnostics.UnregisterTag(tagId);
            }
            else
            {
                _logger.Warning("ChannelDiagnostics", $"Cannot unregister tag id [{tagId}] because it is not registered");
            }
        }


        public DeviceDiagnostics GetDeviceDiagnosticsForTag(Guid tagId)
        {
            return DeviceDiagnostics.FirstOrDefault(p => p.Value.TagDiagnostics.ContainsKey(tagId)).Value;
        }

        /// <summary>
        /// Retrieves or creates the device's diagnostic information by ID
        /// </summary>
        public DeviceDiagnostics? GetDeviceDiagnostics(string deviceId)
        {
            if (DeviceDiagnostics.TryGetValue(deviceId, out var diagnostics))
            {
                return diagnostics;
            }
            else
                return null;
        }


        /// <summary>
        /// Determines which device a tag belongs to
        /// </summary>
        private string? GetDeviceForTag(Guid tagId)
        {
            // Look for the tag in all devices
            foreach (var device in DeviceDiagnostics)
            {
                if (device.Value.TagDiagnostics.ContainsKey(tagId))
                {
                    return device.Key;
                }
            }

            // If not found, return empty GUID
            return null;
        }

        /// <summary>
        /// Maps a set of tags to their respective devices
        /// </summary>
        private Dictionary<string, List<Guid>> MapTagsToDevices(IEnumerable<Guid> tagIds)
        {
            var result = new Dictionary<string, List<Guid>>();

            foreach (var tagId in tagIds)
            {
                string? deviceId = GetDeviceForTag(tagId);

                // If tag isn't assigned to a device, we do not map it
                if (deviceId == null)
                {
                    _logger.Warning("ChannelDiagnostics", $"Tag id {tagId} does not belong to any device");
                    continue;
                }

                if (!result.ContainsKey(deviceId))
                {
                    result[deviceId] = new List<Guid>();
                }

                result[deviceId].Add(tagId);
            }

            return result;
        }

        private object _lock = new object();
        private bool _disposedValue;

        /// <summary>
        /// Calculates aggregate metrics for the entire channel based on all contained devices
        /// </summary>
        public void UpdateAggregateMetrics()
        {
            int totalReads = 0;
            int totalWrites = 0;
            int failedWrites = 0;
            int retries = 0;
            int overdueReads = 0;
            int failedReads = 0;
            double sumOverdueTime = 0;
            double sumWriteTime = 0;
            int overdueTimeSamples = 0;
            int writeTimeSamples = 0;
            int tags = 0;

            lock (_lock)
            {
                foreach (var device in DeviceDiagnostics.Values)
                {
                    totalReads += device.TotalReads;
                    totalWrites += device.TotalWrites;
                    retries += device.Retries;
                    overdueReads += device.OverdueReads;
                    failedReads += device.FailedReads;
                    failedWrites += device.FailedWrites;
                    tags += device.TagsCount;

                    // Only include non-zero values in the average
                    if (device.AvgOverdueTime > 0)
                    {
                        sumOverdueTime += device.AvgOverdueTime;
                        overdueTimeSamples++;
                    }

                    if (device.AvgWriteTime > 0)
                    {
                        sumWriteTime += device.AvgWriteTime;
                        writeTimeSamples++;
                    }
                }

                // Set properties which will raise PropertyChanged events
                TotalReads = totalReads;
                TotalWrites = totalWrites;
                Retries = retries;
                OverdueReads = overdueReads;
                FailedReads = failedReads;
                FailedWrites = failedWrites;
                TagsCount = tags;

                // Note: FailedConnectionAttempts and ModbusMasterConnected are managed directly 
                // at the channel level, not aggregated from devices

                // Calculate simple average of the device rolling averages
                AvgOverdueTime = overdueTimeSamples > 0 ? sumOverdueTime / overdueTimeSamples : 0;
                AvgWriteTime = writeTimeSamples > 0 ? sumWriteTime / writeTimeSamples : 0;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger?.Debug("ChannelDiagnostics", "Disposing ChannelDiagnostics and cleaning up all registered tags and devices");

                    lock (_lock)
                    {
                        // Unsubscribe from and dispose all device diagnostics
                        if (_deviceDiagnostics != null)
                        {
                            foreach (var deviceDiag in _deviceDiagnostics.Values)
                            {
                                // Unsubscribe from property changed events
                                deviceDiag.PropertyChanged -= DeviceDiagnostics_PropertyChanged;

                                // Dispose if the DeviceDiagnostics implements IDisposable
                                if (deviceDiag is IDisposable disposableDevice)
                                {
                                    disposableDevice.Dispose();
                                }
                            }
                            _deviceDiagnostics.Clear();
                            _deviceDiagnostics = null;
                        }
                    }
                }

                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ChannelDiagnostics()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(nameof(ChannelDiagnostics));
            }
        }
    }
}