using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ModbusModule.Helper;

using vNode.Sdk.Logger;

namespace ModbusModule.ChannelConfig
{    
    public class ModbusChannelConfig
    {
        [JsonIgnore]
        public string NodeName { get; set; }
        [JsonRequired] public required ModbusConnectionConfig Connection { get; set; }
        [JsonRequired] public required ModbusTimingConfig Timing { get; set; }

        [JsonPropertyName("Devices")]
        [JsonConverter(typeof(DevicesDictionaryConverter))]
        public Dictionary<string,ModbusDeviceConfig> Devices { get; set; } =
            new();

        public static ModbusChannelConfig? Create(string nodeName, JsonObject jsonConfig, ISdkLogger logger)
        {
            ModbusChannelConfig config;

            try
            {
                // Setup the serialization options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new ModbusProtocolConfigConverter() },                    
                    PropertyNameCaseInsensitive = true
                };

                config = JsonHelper.DeserializeOrThrow<ModbusChannelConfig>(jsonConfig.ToString(), options,
                    "Error deserializing channel configuration");
            }
            catch (JsonException ex)
            {
                logger.Error("ModbusChannelConfig", "Error deserializing channel config: " + ex.Message);
                return null;
            }

            config.NodeName = nodeName;

            // do additional validations here
            if (!validate(config, out string error))
            {
                logger.Error("ModbusChannelConfig", "Invalid channel config: " + error);
                return null;
            }

            return config;
        }

        private static bool validate(ModbusChannelConfig config, out string error)
        {
            error = string.Empty;

            //Several validations can be done here, like this one:
            if (!config.Devices.Any())
            {
                error = "Channel must contain at least 1 device";
                return false;
            }

            bool hasDuplicateSlaveIds = config.Devices
            .GroupBy(device => device.Value.ModbusSlaveId)
            .Any(group => group.Count() > 1);

            if (hasDuplicateSlaveIds)
            {
                error = "Channel has duplicated slave ids";
                return false;
            }

            return true;
        }
    }
}
