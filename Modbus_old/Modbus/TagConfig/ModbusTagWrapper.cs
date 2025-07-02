using System.Text.Json;

using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;

namespace ModbusModule.TagConfig
{
    public class ModbusTagWrapper
    {
        public enum ModbusTagStatusType
        {
            NotInitialized,
            ConfigError,
            Ok
        }

        private readonly ModbusTagConfig _config;
        private readonly TagModelBase _tag;

        public object? CurrentValue { get; set; }
        public QualityCodeOptions CurrentQuality { get; set; }

        private ModbusTagWrapper(TagModelBase tagObject, ModbusTagConfig config)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            _config = config ?? throw new ArgumentNullException(nameof(_config));
            Status = ModbusTagStatusType.Ok;
            CurrentValue = tagObject.InitialValue;
        }

        private ModbusTagWrapper(TagModelBase tagObject)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            CurrentValue = tagObject.InitialValue;
        }


        public TagModelBase Tag => _tag;
        public ModbusTagConfig Config => _config;
        public ModbusTagStatusType Status { get; private set; } = ModbusTagStatusType.NotInitialized;

        // New overload to create a tag with ConfigError status and specific error message
        public static ModbusTagWrapper Create(TagModelBase tagObject, string errorMessage, ISdkLogger logger)
        {
            logger.Error("ModbusTagWrapper", $"Creating tag wrapper with error for tag ID: {tagObject.IdTag} - {errorMessage}");

            // Use a private constructor that sets the Status to ConfigError
            return new ModbusTagWrapper(tagObject) { Status = ModbusTagStatusType.ConfigError };
        }

        public static ModbusTagWrapper Create(TagModelBase tagObject, ISdkLogger logger)
        {
            ArgumentNullException.ThrowIfNull(nameof(tagObject));

            if (string.IsNullOrWhiteSpace(tagObject.Config))
            {
                logger.Warning("ModbusTagWrapper", $"Config is empty or null for tag ID: {tagObject.IdTag}");
                return new ModbusTagWrapper(tagObject) { Status = ModbusTagStatusType.ConfigError };
            }

            ModbusTagConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<ModbusTagConfig>(tagObject.Config);
            }
            catch (Exception ex)
            {
                logger.Error("ModbusTagWrapper",
                    $"Error deserializing JSON config for tag ID {tagObject.IdTag} -> {ex.Message}.\nTag Config: {tagObject.Config}");
                return new ModbusTagWrapper(tagObject) { Status = ModbusTagStatusType.ConfigError };
            }


            if (config == null)
            {
                logger.Error("ModbusTagWrapper",
                    $"Error deserializing JSON config for tag ID: {tagObject.IdTag}: {tagObject.Config}");
                return new ModbusTagWrapper(tagObject) { Status = ModbusTagStatusType.ConfigError };
            }

            if (!ValidateTagConfig(tagObject, config,out var error))
            {
                logger.Error("ModbusTagWrapper","Tag config validation error: " + error);
                return new ModbusTagWrapper(tagObject) { Status = ModbusTagStatusType.ConfigError };
            }

            return new ModbusTagWrapper(tagObject, config);
        }

        private static bool ValidateTagConfig(TagModelBase tag, ModbusTagConfig config, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(config.DeviceId))
            {
                error = "DeviceId is null or empty";
                return false;
            }            
            if (config.PollRate < 0)
            {
                error = "PollRate is less than 0";
                return false;
            }

            if (config.DataType == ModbusTagDataTypeType.String && config.StringSize == 0)
            {
                error = "StringSize is 0";
                return false;
            }

            if (tag.IsArray && config.ArraySize==0)
            {
                error = "ArraySize is 0";
                return false;
            }

            if (config.RegisterAddress.Type== ModbusType.Unknown)
            {
                error = $"RegisterAddress {config.RegisterAddress} is not a valid Modbus address";
                return false;
            }

            if (config.BitNumber != null && !isInRange((int) config.BitNumber, 0, 15))
            {
                error = $"BitNumber {config.BitNumber} is not in range 0-15";
                return false;
            }

            return true;
        }

        private static bool isInRange(int number, int min, int max)
        {
            return number >= min && number <= max;
        }
    }
}
