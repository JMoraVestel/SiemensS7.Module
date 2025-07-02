using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using vNode.Sdk.Data;

namespace ModbusModule.Diagnostics
{
    public class ModbusControlTag
    {
        public required TagModelBase BaseTag { get; init; }
        public required ModbusControlTagConfig TagConfig { get; init; }

        // Forward common properties for convenience
        public Guid IdTag => BaseTag.IdTag;
        public string Name => BaseTag.Name;
        public string Config => BaseTag.Config;
        // Add other forwarded properties as needed        
        public static ModbusControlTag Create(TagModelBase baseTag)
        {
            ArgumentNullException.ThrowIfNull(baseTag, nameof(baseTag));
            ModbusControlTagConfig? tagConfig;
            try
            {
                tagConfig = System.Text.Json.JsonSerializer.Deserialize<ModbusControlTagConfig>(baseTag.Config);
                if (tagConfig == null)
                {
                    throw new InvalidDataException($"Error deserializing control tag config: [{baseTag.Config}]");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Exception deserializing control tag config: [{baseTag.Config}]: {ex.Message}");
            }

            return new ModbusControlTag
            {
                BaseTag = baseTag,
                TagConfig = tagConfig
            };
        }
    }
}
