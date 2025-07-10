using Newtonsoft.Json;
using vNode.Sdk.Data;
using vNode.Sdk.Logger;

namespace vNode.SiemensS7.TagConfig
{
    public class SiemensTagWrapper: SiemensTagConfig 
    {
        
        private readonly SiemensTagConfig _config;
        private readonly TagModelBase _tag;
        public TagModelBase Tag => _tag;
        public SiemensTagConfig Config => _config;
        public string Name => base.Name;
        public object? CurrentValue { get; set; }

        public SiemensTagStatusType Status { get; private set; } = SiemensTagStatusType.NotInitialized;

        public enum SiemensTagStatusType
        {
            NotInitialized,
            ConfigError,
            Ok
        }

        private SiemensTagWrapper(TagModelBase tagObject)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            CurrentValue = tagObject.InitialValue;
        }

        private SiemensTagWrapper(TagModelBase tagObject, SiemensTagConfig config)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Status = SiemensTagStatusType.Ok;
            CurrentValue = tagObject.InitialValue;
        }
        public static SiemensTagWrapper Create(TagModelBase tagObject, string errorMessage, ISdkLogger logger)
        {
            logger.Error("ModbusTagWrapper", $"Creating tag wrapper with error for tag ID: {tagObject.IdTag} - {errorMessage}");

            // Use a private constructor that sets the Status to ConfigError
            return new SiemensTagWrapper(tagObject) { Status = SiemensTagStatusType.ConfigError };
        }

        public static SiemensTagWrapper Create(TagModelBase tagObject, ISdkLogger logger)
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
                return new SiemensTagWrapper(tagObject) { Status = SiemensTagStatusType.ConfigError };
            }

            if (!ValidateTagConfig(tagObject, config, out var error))
            {
                logger.Error("S7TagWrapper", $"Invalid tag config for tag ID {tagObject.IdTag}: {error}");
                return new SiemensTagWrapper(tagObject) { Status = SiemensTagStatusType.ConfigError };
            }

            return new SiemensTagWrapper(tagObject, config);
        }

        private static bool ValidateTagConfig(TagModelBase tag, SiemensTagConfig config, out string error)
        {
            error = string.Empty;

            if (config.PollRate < 0)
            {
                error = "PollRate is less than 0";
                return false;
            }

            if (config.DataType == SiemensTagDataType.String && config.StringSize == 0)
            {
                error = "StringSize must be greater than 0 for String data type.";
                return false;
            }

            if (tag.IsArray && config.ArraySize == 0)
            {
                error = "ArraySize is 0";
                return false;
            }

            // Validar el rango de BitNumber para tipos BOOL
            if (config.DataType == SiemensTagDataType.Bool && config.BitNumber != null && !isInRange((int)config.BitNumber, 0, 7))
            {
                error = $"BitNumber {config.BitNumber} is not in range 0-7";
                return false;
            }

            // Validar que la dirección sea válida según S7NetPlus
            try
            {
                var parsedAddress = ParseAddress(config.Address); // Línea corregida
                if (parsedAddress == null)
                {
                    error = $"Invalid address format: {config.Address}.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Error parsing address: {ex.Message}.";
                return false;
            }

            return true;
        }

        private static object ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address cannot be null or empty.", nameof(address));
            }

            try
            {
                // Utilizar la clase S7Address para analizar la dirección.
                // Se confía en que S7Address.Parse realiza toda la validación necesaria.
                return S7Address.Parse(address);
            }
            catch (Exception ex)
            {
                // Capturamos la excepción de S7Address.Parse y la relanzamos con más contexto.
                throw new ArgumentException($"Error parsing address '{address}': {ex.Message}", ex);
            }
        }

        private static bool isInRange(int number, int min, int max)
        {
            return number >= min && number <= max;
        }
    }
}

