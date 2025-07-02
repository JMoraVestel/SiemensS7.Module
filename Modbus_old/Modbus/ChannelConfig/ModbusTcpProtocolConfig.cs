using System;
using System.ComponentModel.DataAnnotations;

namespace ModbusModule.ChannelConfig
{
    public class ModbusTcpProtocolConfig : ModbusProtocolConfig
    {
        public string Host { get; set; } = string.Empty;

        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
        public int Port { get; set; }
    }
}
