using System;
using System.Text.Json.Serialization;

namespace ModbusModule.TagConfig
{
    public enum ModbusTagDataTypeType
    {
        Boolean,
        Uint16,
        Int16,
        Uint32,
        Int32,
        Uint64,
        Int64,
        Float32,
        Double64,
        String
    }

    public class ModbusTagConfig
    {
        [JsonRequired]
        public string DeviceId { get; set; }

        [JsonRequired]
        [JsonConverter(typeof(ModbusAddressConverter))]
        public ModbusAddress RegisterAddress { get; set; }

        public byte? BitNumber { get; set; }
        public byte StringSize { get; set; }

        public int ArraySize { get; set; } = 0;

        [JsonRequired]
        public int PollRate { get; set; } = -1;

        [JsonRequired]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModbusTagDataTypeType DataType { get; set; }

        public ushort GetSize()
        {
            ushort typeSize = DataType switch
            {
                ModbusTagDataTypeType.Boolean => 1,
                ModbusTagDataTypeType.Uint16 => 1,
                ModbusTagDataTypeType.Int16 => 1,
                ModbusTagDataTypeType.Uint32 => 2,
                ModbusTagDataTypeType.Int32 => 2,
                ModbusTagDataTypeType.Uint64 => 4,
                ModbusTagDataTypeType.Int64 => 4,
                ModbusTagDataTypeType.Float32 => 2,
                ModbusTagDataTypeType.Double64 => 4,
                ModbusTagDataTypeType.String => (ushort) Math.Ceiling(StringSize / 2.0),
                _ => throw new InvalidDataException($"Don't know the size for DataType {DataType}")
            };

            // If ArraySize is greater than 0, multiply the type size by the array size
            if (ArraySize > 0)
            {
                return (ushort) (typeSize * ArraySize);
            }
            else return typeSize;
        }

        public bool IsReadOnly => RegisterAddress.Type == ModbusType.InputCoil || RegisterAddress.Type == ModbusType.InputRegister;
    }
}
