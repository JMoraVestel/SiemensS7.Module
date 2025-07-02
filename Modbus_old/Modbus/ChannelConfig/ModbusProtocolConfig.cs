using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig
{
    public enum ModbusConnectionTypeType
    {
        Tcp,
        Serial
    }


    public abstract class ModbusProtocolConfig
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonRequired]
        public ModbusConnectionTypeType ConnectionType { get; set; }

        public UInt16 ReconnectDelay { get; set; }        
    }
}
