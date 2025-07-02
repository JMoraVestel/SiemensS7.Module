using System.Text.Json.Nodes;

namespace vNode.SiemensS7.TagConfig
{
    public class S7TagConfig
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string DataType { get; set; }

        public static S7TagConfig FromJson(JsonObject json)
        {
            return new S7TagConfig
            {
                Name = json["name"]?.ToString() ?? throw new ArgumentException("Falta el nombre del tag"),
                Address = json["address"]?.ToString() ?? throw new ArgumentException("Falta la dirección"),
                DataType = json["dataType"]?.ToString() ?? throw new ArgumentException("Falta el tipo de dato")
            };
        }
    }
}
