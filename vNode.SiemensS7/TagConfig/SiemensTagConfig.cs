using Newtonsoft.Json;
using System.Text.Json.Serialization;
using S7.Net;

namespace vNode.SiemensS7.TagConfig
{
    public class SiemensTagConfig
    {
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
                SiemensTagDataType.Bool => 1,
                SiemensTagDataType.Byte => 1,
                SiemensTagDataType.Word => 2,
                SiemensTagDataType.DWord => 4,
                SiemensTagDataType.Int => 2,
                SiemensTagDataType.DInt => 4,
                SiemensTagDataType.Real => 4,
                SiemensTagDataType.String => (ushort)Math.Ceiling(StringSize / 2.0),
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

    public enum SiemensTagDataType
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