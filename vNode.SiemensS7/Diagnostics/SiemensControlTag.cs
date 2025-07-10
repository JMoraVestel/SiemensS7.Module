using System;
using System.Text.Json;
using vNode.Sdk.Data;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Diagnostics
{
    public class SiemensControlTag
    {
        // Replace 'required' keyword with explicit constructor initialization
        public TagModelBase BaseTag { get; init; }
        public SiemensTagConfig TagConfig { get; init; }

        // Forward common properties for convenience
        public Guid IdTag => BaseTag.IdTag;
        public string Name => BaseTag.Name;
        public string Config => BaseTag.Config;
        public string Address => TagConfig.Address; // Dirección del tag en el PLC

        // Add other forwarded properties as needed        
        public SiemensControlTag(TagModelBase baseTag, SiemensTagConfig tagConfig)
        {
            ArgumentNullException.ThrowIfNull(baseTag, nameof(baseTag));
            ArgumentNullException.ThrowIfNull(tagConfig, nameof(tagConfig));
            BaseTag = baseTag;
            TagConfig = tagConfig;
        }

        public static SiemensControlTag Create(TagModelBase baseTag)
        {
            ArgumentNullException.ThrowIfNull(baseTag, nameof(baseTag));
            SiemensTagConfig? tagConfig;
            try
            {
                tagConfig = JsonSerializer.Deserialize<SiemensTagConfig>(baseTag.Config);
                if (tagConfig == null)
                {
                    throw new InvalidDataException($"Error deserializing control tag config: [{baseTag.Config}]");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Exception deserializing control tag config: [{baseTag.Config}]: {ex.Message}");
            }

            return new SiemensControlTag(baseTag, tagConfig);
        }
    }
}
