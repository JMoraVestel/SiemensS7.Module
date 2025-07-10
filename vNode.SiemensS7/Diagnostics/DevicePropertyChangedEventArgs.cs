using System;

namespace vNode.SiemensS7.Diagnostics
{
    /// <summary>
    /// Event arguments for when a Siemens device property changes
    /// </summary>
    public class DevicePropertyChangedEventArgs : EventArgs
    {
        /// <summary>
        /// IP address of the Siemens PLC where the property changed
        /// </summary>
        public string PlcIpAddress { get; }

        /// <summary>
        /// Name of the property that changed
        /// </summary>
        public string PropertyName { get; }

        public DevicePropertyChangedEventArgs(string plcIpAddress, string propertyName)
        {
            PlcIpAddress = plcIpAddress ?? throw new ArgumentNullException(nameof(plcIpAddress));
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }
    }
}
