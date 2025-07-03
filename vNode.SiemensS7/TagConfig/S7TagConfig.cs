using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SiemensModule.TagConfig
{
    public enum S7TagDataTypeType
    {
        Bool,
        Int16,
        Int32,
        DInt,
        DWord,
        Real,
        String
    }

    public class S7TagConfig
    {
        [JsonRequired]
        public string DeviceId { get; set; }

        [JsonRequired]
        public int DbNumber { get; set; }

        [JsonRequired]
        public int StartByte { get; set; }

        public byte? BitNumber { get; set; }

        public byte StringSize { get; set; } = 0;

        public int ArraySize { get; set; } = 0;

        [JsonRequired]
        public int PollRate { get; set; } = -1;

        [JsonRequired]
        [Newtonsoft.Json.JsonConverter(typeof(JsonStringEnumConverter))]
        public S7TagDataTypeType DataType { get; set; }
    }
}