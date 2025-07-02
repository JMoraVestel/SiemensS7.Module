using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Ports;
using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig
{
    public class ModbusSerialProtocolConfig : ModbusProtocolConfig
    {
        public string PortName { get; set; } = string.Empty;

        public required ModbusSerialPortSettings PortSettings { get; set; } = new ModbusSerialPortSettings();
    }

    public class ModbusSerialPortSettings
    {
        [Range(300, 128000, ErrorMessage = "BaudRate must be between 300 and 128000.")]
        public int BaudRate { get; set; } = 9600;

        [Range(5, 8, ErrorMessage = "DataBits must be between 5 and 8.")]
        public int DataBits { get; set; } = 8;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StopBitsType StopBits { get; set; } = StopBitsType.One;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ParityType Parity { get; set; } = ParityType.None;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FlowControlType FlowControl { get; set; } = FlowControlType.None;

        [Range(256, 65536, ErrorMessage = "BufferSize must be between 256 and 65536.")]
        public int BufferSize { get; set; } = 4096;
    }

    public enum StopBitsType
    {
        None = 0,
        One = 1,
        Two = 2,
        OnePointFive = 3
    }

    public enum ParityType
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4
    }

    public enum FlowControlType
    {
        None = 0,
        XOnXOff = 1,
        RequestToSend = 2,
        RequestToSendXOnXOff = 3
    }
}
