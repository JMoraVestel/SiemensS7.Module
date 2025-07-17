using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using S7.Net;
using vNode.Sdk.Logger;

namespace vNode.SiemensS7.ChannelConfig
{
    public class SiemensChannelConfig
    {
        public string NodeName { get; set; }
        public string IpAddress { get; set; }
        public CpuType CpuType { get; set; }
        public short Rack { get; set; } = 0;
        public short Slot { get; set; } = 1;
        public int PollingIntervalMs { get; set; } = 1000;
        public List<TagConfig.SiemensTagConfig> Tags { get; set; }

        [JsonPropertyName("Devices")]
        [JsonConverter(typeof(DevicesDictionaryConverter))]
        public Dictionary<string, SiemensDeviceConfig> Devices { get; set; } =
            new();

        public static SiemensChannelConfig? Create(string nodeName, JsonObject jsonConfig, ISdkLogger logger)
        {
            if (jsonConfig is null)
            {
                logger.Error("SiemensChannelConfig", "La configuración JSON del canal es nula.");
                return null;
            }

            try
            {
                var config = new SiemensChannelConfig
                {
                    NodeName = nodeName,
                    IpAddress = jsonConfig["IpAddress"]?.ToString() ?? throw new ArgumentException("IpAddress es obligatorio."),
                    CpuType = Enum.TryParse<CpuType>(jsonConfig["CpuType"]?.ToString(), out var cpuType) ? cpuType : throw new ArgumentException("CpuType inválido o no especificado."),
                    Rack = short.TryParse(jsonConfig["Rack"]?.ToString(), out var rack) ? rack : throw new ArgumentException("Rack inválido o no especificado."),
                    Slot = short.TryParse(jsonConfig["Slot"]?.ToString(), out var slot) ? slot : throw new ArgumentException("Slot inválido o no especificado.")
                };

                // Si necesitas más propiedades, agrégalas aquí

                return config;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "SiemensChannelConfig", $"Error al crear la configuración del canal: {ex.Message}");
                return null;
            }
        }

        public static SiemensChannelConfig FromJson(JsonObject json)
        {
            var config = new SiemensChannelConfig();

            config.IpAddress = json["ip"]?.ToString() ?? throw new ArgumentException("Falta la IP");
            config.Rack = short.Parse(json["rack"]?.ToString() ?? "0");
            config.Slot = short.Parse(json["slot"]?.ToString() ?? "1");
            config.PollingIntervalMs = int.Parse(json["pollingIntervalMs"]?.ToString() ?? "1000");

            var tagsJson = json["tags"]?.AsArray() ?? throw new ArgumentException("Falta el array 'tags'");
            config.Tags = new List<TagConfig.SiemensTagConfig>();

            foreach (var tagJson in tagsJson)
            {
                if (tagJson is JsonObject tagObj)
                    config.Tags.Add(TagConfig.SiemensTagConfig.FromJson(tagObj));
            }

            return config;
        }
    }
}
