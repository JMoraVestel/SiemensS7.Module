using SiemensModule.TagConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.Sdk.Data;
using vNode.Sdk.Logger;

namespace vNode.SiemensS7.TagConfig
{
    public class S7TagWrapper
    {
        private readonly SiemensTagConfig _config;
        private readonly TagModelBase _tag;
        public object? CurrentValue { get; set; }
        public S7TagStatusType Status { get; private set; } = S7TagStatusType.NotInitialized;

        public enum S7TagStatusType
        {
            NotInitialized,
            ConfigError,
            Ok
        }

        private S7TagWrapper(TagModelBase tagObject)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            CurrentValue = tagObject.InitialValue;
        }

        private S7TagWrapper(TagModelBase tagObject, SiemensTagConfig config)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Status = S7TagStatusType.Ok;
            CurrentValue = tagObject.InitialValue;
        }

        public static S7TagWrapper Create(TagModelBase tagObject, ISdkLogger logger)
        {
            if (string.IsNullOrEmpty(tagObject?.Config))
            {
                throw new ArgumentException("Tag config cannot be null or empty.", nameof(tagObject.Config));
            }

            SiemensTagConfig? config;
            try
            {
                config = Newtonsoft.Json.JsonConvert.DeserializeObject<SiemensTagConfig>(tagObject.Config);
            }
            catch (Exception ex)
            {
                logger.Error("S7TagWrapper",
                             $"Error deserializing JSON config for tag ID {tagObject.IdTag} -> {ex.Message}.\nTag Config: {tagObject.Config}");
                return null;
            }

            if (config == null)
            {
                logger.Error("S7TagWrapper", $"Deserialized config is null for tag ID {tagObject.IdTag}.");
                return new S7TagWrapper(tagObject) { Status = S7TagStatusType.ConfigError };
            }

            if (!ValidateTagConfig(tagObject, config, out var error))
            {
                logger.Error("S7TagWrapper", $"Invalid tag config for tag ID {tagObject.IdTag}: {error}");
                return new S7TagWrapper(tagObject) { Status = S7TagStatusType.ConfigError };
            }

            return new S7TagWrapper(tagObject, config);
        }

        private static bool ValidateTagConfig(TagModelBase tag, SiemensTagConfig config, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(config.DeviceId))
            {
                error = "DeviceId is required.";
                return false;
            }

            if (config.PollRate < 0)
            {
                error = "PollRate is less than 0";
                return false;
            }

            if (config.DataType == S7TagDataTypeType.String && config.StringSize == 0)
            {
                error = "StringSize must be greater than 0 for String data type.";
                return false;
            }

            if (tag.IsArray && config.ArraySize == 0)
            {
                error = "ArraySize is 0";
                return false;
            }

            if (config.BitNumber != null && !isInRange((int)config.BitNumber, 0, 15))
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
    

