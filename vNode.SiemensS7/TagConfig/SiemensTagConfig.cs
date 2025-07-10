using Newtonsoft.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using vNode.Sdk.Enum;

namespace vNode.SiemensS7.TagConfig
{
    public class SiemensTagConfig
    {
        public string Name { get; set; }

        public Guid TagId { get; set; }

        [JsonRequired]
        public string Address { get; set; } // Dirección en el PLC

        public byte? BitNumber { get; set; } // Para tipos BOOL

        public byte StringSize { get; set; } // Tamaño de cadenas

        public int ArraySize { get; set; } = 0; // Tamaño de arreglos

        [JsonRequired]
        public int PollRate { get; set; } = 1000; // Frecuencia de sondeo

        [JsonRequired]
        [Newtonsoft.Json.JsonConverter(typeof(JsonStringEnumConverter))]
        public SiemensTagDataType DataType { get; set; } // Tipo de dato

        public bool IsReadOnly { get; set; } // En Siemens, los tags generalmente no son de solo lectura

        public static SiemensTagConfig FromJson(JsonObject json)
        {
            var config = new SiemensTagConfig
            {
                TagId = Guid.NewGuid(), // Generar un ID único para el tag
                Address = json["address"]?.ToString() ?? throw new ArgumentException("Falta la dirección del tag"),
                PollRate = int.Parse(json["pollRate"]?.ToString() ?? "1000"),
                DataType = Enum.Parse<SiemensTagDataType>(json["dataType"]?.ToString() ?? throw new ArgumentException("Falta el tipo de dato del tag"), true),
                ArraySize = int.Parse(json["arraySize"]?.ToString() ?? "0"),
                IsReadOnly = bool.Parse(json["isReadOnly"]?.ToString() ?? "false")
            };

            if (json.ContainsKey("bitNumber"))
            {
                config.BitNumber = byte.Parse(json["bitNumber"].ToString());
            }

            if (json.ContainsKey("stringSize"))
            {
                config.StringSize = byte.Parse(json["stringSize"].ToString());
            }

            return config;
        }

        public ushort GetSize()
        {
            // Lógica para determinar el tamaño del tag basado en el tipo de dato
            return DataType switch
            {
                SiemensTagDataType.Bool => 1,
                SiemensTagDataType.Byte => 1,
                SiemensTagDataType.Word => 2,
                SiemensTagDataType.DWord => 4,
                SiemensTagDataType.Int => 2,
                SiemensTagDataType.DInt => 4,
                SiemensTagDataType.Real => 4,
                SiemensTagDataType.String => StringSize,
                _ => 0,
            };
        }
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