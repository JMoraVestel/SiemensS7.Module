using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModbusModule.ChannelConfig;

using vNode.Sdk.Logger;

namespace ModbusModule.TagReader
{
    public class DeviceDemotionManager
    {
        private readonly Dictionary<string, DeviceDemotionInfo> _devices;
        private readonly object _lock = new();
        private readonly ISdkLogger _logger;
        private readonly Timer _expirationTimer;
        // Event for device demotion status change
        public event EventHandler<DeviceDemotionEventArgs>? DeviceDemotionStatusChanged;

        // Event args class
        public class DeviceDemotionEventArgs : EventArgs
        {
            public string DeviceId { get; }
            public bool IsDemoted { get; }
            public DateTime? DemotedUntil { get; }

            public DeviceDemotionEventArgs(string deviceId, bool isDemoted, DateTime? demotedUntil)
            {
                DeviceId = deviceId;
                IsDemoted = isDemoted;
                DemotedUntil = demotedUntil;
            }
        }

        public DeviceDemotionManager(ISdkLogger logger)
        {
            _devices = new Dictionary<string, DeviceDemotionInfo>();
            _logger = logger;

            // Create a timer that checks for expired demotions every second
            _expirationTimer = new Timer(CheckDemotionExpirations, null,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void CheckDemotionExpirations(object? state)
        {
            List<string> expiredDevices = new List<string>();

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                foreach (var kvp in _devices)
                {
                    var deviceId = kvp.Key;
                    var info = kvp.Value;

                    if (info.IsDemoted && info.DemotedUntil.HasValue && now >= info.DemotedUntil.Value)
                    {
                        // This device's demotion has expired
                        _logger.Trace("DeviceDemotionManager",
                            $"Device {deviceId} demotion has expired");

                        // Reset the demotion status
                        info.DemotedUntil = null;
                        info.ConsecutiveFailures = 0;

                        // Add to list of expired devices to raise events outside the lock
                        expiredDevices.Add(deviceId);
                    }
                }
            }

            // Raise events for devices whose demotion has expired
            foreach (var deviceId in expiredDevices)
            {
                OnDeviceDemotionStatusChanged(deviceId, false, null);
            }
        }

        public bool IsDemoted(string deviceId)
        {            
            lock (_lock)
            {
                if (_devices.ContainsKey(deviceId))
                {
                    return _devices[deviceId].IsDemoted;
                }
                else return false;
            }            
        }

        public void ReadSuccess(string deviceId)
        {
            bool wasDemoted = false;

            lock (_lock)
            {
                if (_devices.ContainsKey(deviceId))
                {
                    if (_devices.TryGetValue(deviceId, out var info))
                    {
                        wasDemoted = info.IsDemoted;
                        info.ConsecutiveFailures = 0;
                        info.DemotedUntil = null;
                    }

                    if (wasDemoted)
                    {
                        _logger.Trace(deviceId, $"Device {deviceId} has been restored from demotion.");
                        OnDeviceDemotionStatusChanged(deviceId, false, null);
                    }
                }
            }
        }

        protected virtual void OnDeviceDemotionStatusChanged(string deviceId, bool isDemoted, DateTime? demotedUntil)
        {
            DeviceDemotionStatusChanged?.Invoke(this,
                new DeviceDemotionEventArgs(deviceId, isDemoted, demotedUntil));
        }

        public void ReadFail(string deviceId, ModbusAutoDemotionConfig config)
        {
            // If demotion is not enabled for this device, don't track failures
            if (config == null || !config.Enabled)
            {
                return;
            }

            bool wasDemoted = false;
            bool isDemoted = false;
            DateTime? demotedUntil = null;

            lock (_lock)
            {
                if (!_devices.TryGetValue(deviceId, out var info))
                {
                    info = new DeviceDemotionInfo(deviceId);
                    _devices.Add(deviceId, info);
                }

                wasDemoted = info.IsDemoted;

                info.ConsecutiveFailures++;
                if (info.ConsecutiveFailures >= config.Failures)
                {
                    _logger.Trace(deviceId, $"Device {deviceId} has been demoted for {config.Delay}ms due to {info.ConsecutiveFailures} consecutive failures.");
                    info.DemotedUntil = DateTime.UtcNow.AddMilliseconds(config.Delay);
                    isDemoted = true;
                }

                // Raise event if demotion status changed
                if (!wasDemoted && isDemoted)
                {
                    OnDeviceDemotionStatusChanged(deviceId, true, demotedUntil);
                }
            }
        }
    }
}
