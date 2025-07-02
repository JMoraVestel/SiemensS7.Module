using System.Text.Json;
using System.Text.Json.Nodes;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ModbusModule.Helper
{
    public static class JsonHelper
    {
        public static T DeserializeOrThrow<T>(string json, JsonSerializerOptions options, string errorMessage)
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(json,options);
                if (result == null)
                {
                    throw new ApplicationException(errorMessage);
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new ApplicationException(errorMessage, ex);
            }
        }

        public static string PrettySerialize(JsonObject jsonObject)
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            return $"{JsonSerializer.Serialize(jsonObject, options)}";
        }
    }
}
