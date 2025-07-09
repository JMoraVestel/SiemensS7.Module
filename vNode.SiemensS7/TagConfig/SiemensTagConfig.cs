using Newtonsoft.Json;
using S7.Net;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Text.Json.Serialization;

namespace vNode.SiemensS7.TagConfig
{
    public class SiemensTagConfig
    {
        //vNode necesita un identificador único para cada tag, como parte de la lógica de tu aplicación
        //(por ejemplo, para mapear tags a dispositivos o realizar diagnósticos).
        //Este identificador no es proporcionado por S7NetPlus, pero puede ser útil para abstraer 
        //y gestionar los tags en tu sistema.
        public Guid TagId { get; set; }

        [JsonRequired]
        public string Address { get; set; } // Dirección en el PLC

        public byte? BitNumber { get; set; } // Para tipos BOOL

        public byte StringSize { get; set; } // Tamaño de cadenas

        public int ArraySize { get; set; } = 0; // Tamaño de arreglos

        [JsonRequired]
        public int PollRate { get; set; } = -1; // Frecuencia de sondeo

        [JsonRequired]
        [Newtonsoft.Json.JsonConverter(typeof(JsonStringEnumConverter))]
        public SiemensTagDataType DataType { get; set; } // Tipo de dato

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