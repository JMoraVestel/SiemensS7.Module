using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ModbusModule.ChannelConfig;

public class DevicesDictionaryConverter : JsonConverter<Dictionary<string, ModbusDeviceConfig>>
{
    public override void Write(Utf8JsonWriter writer, Dictionary<string, ModbusDeviceConfig> value, JsonSerializerOptions options)
    {
        // Start writing array
        writer.WriteStartArray();

        // Write each device as an object in the array
        foreach (var device in value.Values)
        {
            JsonSerializer.Serialize(writer, device, options);
        }

        // End the array
        writer.WriteEndArray();
    }

    public override Dictionary<string, ModbusDeviceConfig> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Ensure we're at the start of an array
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array");
        }

        var result = new Dictionary<string, ModbusDeviceConfig>();

        // Read the opening [
        reader.Read();

        // Read each object in the array
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            // Deserialize device
            var device = JsonSerializer.Deserialize<ModbusDeviceConfig>(ref reader, options);

            if (device != null)
            {
                // Use DeviceId as key
                string key = device.DeviceId;
                result[key] = device;
            }
            else
            {
                // Skip to next token if deserialization failed
                reader.Read();
            }

            // Move to next item or end of array
            if (reader.TokenType != JsonTokenType.EndArray)
            {
                reader.Read();
            }
        }

        return result;
    }
}
