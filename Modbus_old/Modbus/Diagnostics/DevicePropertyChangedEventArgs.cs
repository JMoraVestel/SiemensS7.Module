using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusModule.Diagnostics
{
    /// <summary>
    /// Event arguments for when a device property changes
    /// </summary>
    public class DevicePropertyChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the device where the property changed
        /// </summary>
        public string DeviceId { get; }

        /// <summary>
        /// Name of the property that changed
        /// </summary>
        public string PropertyName { get; }

        public DevicePropertyChangedEventArgs(string deviceId, string propertyName)
        {
            DeviceId = deviceId;
            PropertyName = propertyName;
        }
    }
}
