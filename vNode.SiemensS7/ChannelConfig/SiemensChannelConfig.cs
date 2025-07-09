using System.Text.Json.Nodes;

namespace vNode.SiemensS7.ChannelConfig
{
    public class SiemensChannelConfig
    {
        public string IpAddress { get; set; }
        public short Rack { get; set; } = 0;
        public short Slot { get; set; } = 1;
        public int PollingIntervalMs { get; set; } = 1000;
        public List<TagConfig.SiemensTagConfig> Tags { get; set; }

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
