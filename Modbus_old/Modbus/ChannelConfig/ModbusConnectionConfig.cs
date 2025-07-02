using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModbusModeType
{
    Tcp,
    TcpRtuEncapsulated,
    Rtu
}

public class ModbusConnectionConfig
{
    [JsonRequired]
    [JsonPropertyName("ModbusMode")]
    public ModbusModeType ModbusMode { get; set; }

    [JsonPropertyName("TcpConfig")]    
    public ModbusTcpProtocolConfig? TcpConfig { get; set; }

    [JsonPropertyName("SerialConfig")]    
    public ModbusSerialProtocolConfig? SerialConfig { get; set; }

    // Helper property to get the active configuration (not serialized)
    [JsonIgnore]
    public ModbusProtocolConfig? ActiveConfig => TcpConfig ?? (ModbusProtocolConfig?) SerialConfig;
}
