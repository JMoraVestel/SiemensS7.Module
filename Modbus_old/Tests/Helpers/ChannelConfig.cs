using System.Text.Json.Nodes;

namespace Tests.Helpers
{
    internal static class ChannelConfig
    {
        public static JsonObject CreateInvalidChannelConfig_InvalidConnectionType()
        {
            var config = new JsonObject();

            string jsonString = @"
            {            
            ""Connection"": {
                ""Type"": ""XXX"",
                ""ModbusMode"": ""Tcp"",
                ""Config"": {
                    ""ConnectionType"": ""Tcp"",
                    ""ReconnectDelay"": 3000,
                    ""Host"":""localhost"",
                    ""Port"":""502""
                }
            },
            ""Devices"": {
                ""1"": {
                    ""ModbusSlaveId"": 1,
                    ""ModbusAddressOffset"": 0,
                    ""EnableModbusFunction6"":1,
                    ""BlockSize"": {
                        ""OutputCoils"": 64,
                        ""InputCoils"": 64,
                        ""InputRegisters"": 64,
                        ""HoldingRegisters"": 64
                    },
                    ""SwapConfig"": {
                        ""BitsIn16Bit"": 0,
                        ""BytesIn16Bit"": 0,
                        ""WordsIn32Bit"": 0,
                        ""DWordsIn64Bit"": 0,
                        ""BytesInStrings"": 0
                    },
                    ""PollOnDemandEnabled"": 1,
                    ""AutoDemotionConfig"": {
                        ""Enabled"": 1,
                        ""Failures"": 10,
                        ""Delay"": 1000
                    }
                },
                ""2"": {
                    ""ModbusSlaveId"": 10,
                    ""ModbusAddressOffset"": 0,
                    ""EnableModbusFunction6"":1,
                    ""BlockSize"": {
                        ""OutputCoils"": 64,
                        ""InputCoils"": 64,
                        ""InputRegisters"": 64,
                        ""HoldingRegisters"": 64
                    },
                    ""SwapConfig"": {
                        ""BitsIn16Bit"": 0,
                        ""BytesIn16Bit"": 0,
                        ""WordsIn32Bit"": 0,
                        ""DWordsIn64Bit"": 0,
                        ""BytesInStrings"": 0
                    },
                    ""PollOnDemandEnabled"": 1,
                    ""AutoDemotionConfig"": {
                        ""Enabled"": 1,
                        ""Failures"": 10,
                        ""Delay"": 1000
                    }
                }
            }                        
            }";

            return JsonNode.Parse(jsonString)!.AsObject();
        }

        public static JsonObject CreateGoodChannelConfig()
        {
            var config = new JsonObject();

            string jsonString = @"
            {            
            ""Connection"": {
                ""Type"": ""Tcp"",
                ""ModbusMode"": ""Tcp"",
                ""Config"": {
                    ""ConnectionType"": ""Tcp"",
                    ""ReconnectDelay"": 3000,
                    ""Host"":""localhost"",
                    ""Port"":""502""
                }
            },
            ""Devices"": {
                ""1"": {
                    ""ModbusSlaveId"": 1,
                    ""ModbusAddressOffset"": 0,
                    ""EnableModbusFunction6"":1,
                    ""BlockSize"": {
                        ""OutputCoils"": 64,
                        ""InputCoils"": 64,
                        ""InputRegisters"": 64,
                        ""HoldingRegisters"": 64
                    },
                    ""SwapConfig"": {
                        ""BitsIn16Bit"": 0,
                        ""BytesIn16Bit"": 0,
                        ""WordsIn32Bit"": 0,
                        ""DWordsIn64Bit"": 0,
                        ""BytesInStrings"": 0
                    },
                    ""PollOnDemandEnabled"": 1,
                    ""AutoDemotionConfig"": {
                        ""Enabled"": 1,
                        ""Failures"": 10,
                        ""Delay"": 1000
                    }
                },
                ""2"": {
                    ""ModbusSlaveId"": 10,
                    ""ModbusAddressOffset"": 0,
                    ""EnableModbusFunction6"":1,
                    ""BlockSize"": {
                        ""OutputCoils"": 64,
                        ""InputCoils"": 64,
                        ""InputRegisters"": 64,
                        ""HoldingRegisters"": 64
                    },
                    ""SwapConfig"": {
                        ""BitsIn16Bit"": 0,
                        ""BytesIn16Bit"": 0,
                        ""WordsIn32Bit"": 0,
                        ""DWordsIn64Bit"": 0,
                        ""BytesInStrings"": 0
                    },
                    ""PollOnDemandEnabled"": 1,
                    ""AutoDemotionConfig"": {
                        ""Enabled"": 1,
                        ""Failures"": 10,
                        ""Delay"": 1000
                    }
                }
            }                        
            }";

            return JsonNode.Parse(jsonString)!.AsObject();
        }
    }
}
