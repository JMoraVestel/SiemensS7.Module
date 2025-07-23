using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json.Nodes;

namespace vNode.SiemensS7.TagConfig
{
    /// <summary>
    /// Configuración de un tag para un dispositivo Siemens S7.
    /// </summary>
    public class SiemensTagConfig
    {
        /// <summary>
        /// Nombre del tag.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Identificador único del tag.
        /// </summary>
        public Guid TagId { get; set; }

        /// <summary>
        /// Dirección del tag en el PLC (ej: "DB1.DBW10", "I0.1", "M100.0").
        /// </summary>
        [Newtonsoft.Json.JsonRequired]
        public string Address { get; set; } // Dirección en el PLC  

        /// <summary>
        /// Para tipos BOOL, especifica el número de bit.
        /// </summary>
        public byte? BitNumber { get; set; } // Para tipos BOOL  

        /// <summary>
        /// Para tags de tipo String, especifica la longitud máxima de caracteres.
        /// </summary>
        public byte StringSize { get; set; } = 254; // Tamaño de cadenas  

        /// <summary>
        /// Tamaño del arreglo si el tag es un array.
        /// </summary>
        public int ArraySize { get; set; } = 0; // Tamaño de arreglos  

        /// <summary>
        /// Frecuencia de sondeo en milisegundos.
        /// </summary>
        [Newtonsoft.Json.JsonRequired]
        public int PollRate { get; set; } = 1000; // Frecuencia de sondeo  

        /// <summary>
        /// Tipo de dato del tag.
        /// </summary>
        [Newtonsoft.Json.JsonRequired]
        [JsonConverter(typeof(StringEnumConverter))]
        public SiemensTagDataType DataType { get; set; } // Tipo de dato  

        /// <summary>
        /// Indica si el tag es de solo lectura.
        /// </summary>
        public bool IsReadOnly { get; set; } // En Siemens, los tags generalmente no son de solo lectura  

        /// <summary>
        /// Identificador del dispositivo al que pertenece el tag.
        /// </summary>
        [Newtonsoft.Json.JsonRequired]
        public string DeviceId { get; set; }

        /// <summary>
        /// Crea una instancia de SiemensTagConfig a partir de un objeto JSON.
        /// </summary>
        /// <param name="json">Objeto JSON que contiene la configuración del tag.</param>
        /// <returns>Instancia de SiemensTagConfig.</returns>
        public static SiemensTagConfig FromJson(JsonObject json)
        {
            var config = new SiemensTagConfig
            {
                Name = json["name"]?.ToString(),
                TagId = Guid.TryParse(json["tagId"]?.ToString(), out var tagId) ? tagId : Guid.NewGuid(),
                Address = json["address"]?.ToString() ?? throw new ArgumentException("Falta la dirección del tag"),
                PollRate = int.Parse(json["pollRate"]?.ToString() ?? "1000"),
                DataType = Enum.Parse<SiemensTagDataType>(json["dataType"]?.ToString() ?? throw new ArgumentException("Falta el tipo de dato del tag"), true),
                ArraySize = int.Parse(json["arraySize"]?.ToString() ?? "0"),
                IsReadOnly = bool.Parse(json["isReadOnly"]?.ToString() ?? "false"),
                DeviceId = json["deviceId"]?.ToString() ?? throw new ArgumentException("Falta el DeviceId del tag")
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

        /// <summary>
        /// Obtiene el tamaño del tag basado en su tipo de dato.
        /// </summary>
        /// <returns>Tamaño del tag en bytes.</returns>
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

    /// <summary>
    /// Tipos de datos soportados por los tags Siemens.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
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