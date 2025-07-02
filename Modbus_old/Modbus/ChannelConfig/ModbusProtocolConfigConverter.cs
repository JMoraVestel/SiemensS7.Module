using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig
{
    public class ModbusProtocolConfigConverter : JsonConverter<ModbusProtocolConfig>
    {
        public override ModbusProtocolConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            // Create a JsonDocument to get the discriminator property
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var rootElement = jsonDoc.RootElement;

            // Get the ConnectionType property to determine which concrete class to create
            if (!rootElement.TryGetProperty("ConnectionType", out var connectionTypeProperty))
            {
                throw new JsonException("Missing ConnectionType property");
            }

            var connectionType = connectionTypeProperty.GetString();
            
            // Use case-insensitive comparison
            ModbusProtocolConfig result;
            if (string.Equals(connectionType, "Tcp", StringComparison.OrdinalIgnoreCase))
            {
                result = JsonSerializer.Deserialize<ModbusTcpProtocolConfig>(rootElement.GetRawText(), options);
            }
            else if (string.Equals(connectionType, "Serial", StringComparison.OrdinalIgnoreCase))
            {
                result = JsonSerializer.Deserialize<ModbusSerialProtocolConfig>(rootElement.GetRawText(), options);
            }
            else
            {                
                throw new JsonException($"Unknown ConnectionType: {connectionType}");
            }

            return result;
        }
        public override void Write(Utf8JsonWriter writer, ModbusProtocolConfig value, JsonSerializerOptions options)
        {
            // Use the built-in serialization for the specific type
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
