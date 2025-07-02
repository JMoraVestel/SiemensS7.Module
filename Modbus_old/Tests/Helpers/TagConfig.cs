using System.Text.Json.Nodes;

using vNode.Sdk.Data;
using vNode.Sdk.Enum;

namespace Tests.Helpers
{
    internal class TagConfig
    {
        public static List<JsonObject> CreateNTags(int deviceId, int tagQuantity, int pollRate, string dataType,
            int startRegisterAddress, int tagSizeInWords, int bitNumber = 0, int stringSize = 0)
        {
            List<JsonObject> retVal = new();
            for (int i = startRegisterAddress;
                 i <= startRegisterAddress + (tagQuantity * tagSizeInWords);
                 i += tagSizeInWords)
            {
                retVal.Add(CreateTagConfig(deviceId, i, pollRate, dataType, bitNumber, stringSize));
            }

            return retVal;
        }

        public static TagModelBase CreateTag(int deviceId, int registerAddress, int pollRate, string dataType,
            object defaultValue, ClientAccessOptions clientAccess, int bitNumber = 0, int stringSize = 0)
        {
            JsonObject modbusTagJson = CreateTagConfig(deviceId, registerAddress, pollRate, dataType);
            return new TagModelBase
            {
                IdTag = Guid.NewGuid(),
                TagDataType = TagDataTypeOptions.UInt16,
                InitialValue = defaultValue,
                ClientAccess = clientAccess,
                Config = modbusTagJson.ToJsonString()
            };
        }

        public static JsonObject CreateTagConfig(int deviceId, int registerAddress, int pollRate, string dataType,
            int bitNumber = 0, int stringSize = 0)
        {
            var config = new JsonObject();

            string jsonString = @$"
            {{
            ""DeviceId"": {deviceId},
            ""RegisterAddress"": {registerAddress},
            ""BitNumber"": {bitNumber},
            ""StringSize"": {stringSize},
            ""PollRate"":{pollRate},
            ""DataType"": ""{dataType}""
            }}";

            return JsonNode.Parse(jsonString)!.AsObject();
        }
    }
}
