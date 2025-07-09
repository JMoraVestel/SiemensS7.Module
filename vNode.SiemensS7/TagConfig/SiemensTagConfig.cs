using Newtonsoft.Json;
using System.Text.Json.Serialization;
using S7.Net;

namespace vNode.SiemensS7.TagConfig
{
    public class SiemensTagConfig
    {
        [JsonRequired]
        public string DeviceId { get; set; }

        [JsonRequired]
        public string Address { get; set; } // Dirección en el PLC

        public byte? BitNumber { get; set; } // Para tipos BOOL

        public byte StringSize { get; set; } // Tamaño de cadenas

        public int ArraySize { get; set; } = 0; // Tamaño de arreglos

        [JsonRequired]
        public int PollRate { get; set; } = -1; // Frecuencia de sondeo

        [JsonRequired]
        [Newtonsoft.Json.JsonConverter(typeof(JsonStringEnumConverter))]
        public SiemensTagDataTypeType DataType { get; set; } // Tipo de dato

        public ushort GetSize()
        {
            ushort typeSize = DataType switch
            {
                SiemensTagDataTypeType.Bool => 1,
                SiemensTagDataTypeType.Byte => 1,
                SiemensTagDataTypeType.Word => 2,
                SiemensTagDataTypeType.DWord => 4,
                SiemensTagDataTypeType.Int => 2,
                SiemensTagDataTypeType.DInt => 4,
                SiemensTagDataTypeType.Real => 4,
                SiemensTagDataTypeType.String => (ushort)Math.Ceiling(StringSize / 2.0),
                _ => throw new System.IO.InvalidDataException($"No se conoce el tamaño para el tipo de dato {DataType}")
            };

            if (ArraySize > 0)
            {
                return (ushort)(typeSize * ArraySize);
            }
            else return typeSize;
        }

        public bool IsReadOnly => false; // En Siemens, los tags generalmente no son de solo lectura
    }

    public enum SiemensTagDataTypeType
    {
        Bool,
        Byte,
        Word,
        DWord,
        Int,
        DInt,
        Real,
        String
    }
}