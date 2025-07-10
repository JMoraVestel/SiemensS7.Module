using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace vNode.SiemensS7.ChannelConfig
{
    /// <summary>
    /// Convierte un array de objetos de dispositivo Siemens desde JSON a un diccionario,
    /// utilizando el DeviceId como clave.
    /// </summary>
    public class DeviceDictionaryConverter : JsonConverter<Dictionary<string, SiemensDeviceConfig>>
    {
        public override void Write(Utf8JsonWriter writer, Dictionary<string, SiemensDeviceConfig> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var device in value.Values)
            {
                JsonSerializer.Serialize(writer, device, options);
            }

            writer.WriteEndArray();
        }

        public override Dictionary<string, SiemensDeviceConfig> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Se esperaba el inicio de un array de dispositivos.");
            }

            var result = new Dictionary<string, SiemensDeviceConfig>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    var device = JsonSerializer.Deserialize<SiemensDeviceConfig>(ref reader, options);
                    if (device != null && !string.IsNullOrEmpty(device.DeviceId))
                    {
                        result[device.DeviceId] = device;
                    }
                }
            }

            return result;
        }
    }
}
