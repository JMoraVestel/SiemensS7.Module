using System.Text.Json;
using vNode.SiemensS7.ChannelConfig;
using Xunit;

namespace vNode.SiemensS7.Tests.ChannelConfig
{   
    public class SiemensChannelConfigTests
    {   
        /// <summary>
        /// Verifica que la deserialización de un JSON válido produce una instancia de SiemensChannelConfig
        /// con todos los valores correctamente asignados. Comprueba los campos principales y los datos del primer tag.
        /// </summary>
        [Fact]
        public void Deserialize_ValidJson_ConfigIsCorrect()
        {
            var json = @"
            {
                ""nodeName"": ""Nodo1"",
                ""ipAddress"": ""192.168.0.10"",
                ""cpuType"": ""S7300"",
                ""rack"": 0,
                ""slot"": 2,
                ""pollingIntervalMs"": 1000,
                ""tags"": [
                    {
                        ""tagId"": ""e7a1b2c3-d4e5-6789-0123-456789abcdef"",
                        ""name"": ""Temperatura"",
                        ""address"": ""DB1.DBW20"",
                        ""dataType"": ""Int"",
                        ""pollRate"": 1000,
                        ""bitNumber"": null,
                        ""stringSize"": 0,
                        ""arraySize"": 0,
                        ""isReadOnly"": false,
                        ""deviceId"": ""plc""
                    },
                    {
                        ""tagId"": ""f1a2b3c4-d5e6-7890-1234-567890abcdef"",
                        ""name"": ""Presion"",
                        ""address"": ""DB1.DBW22"",
                        ""dataType"": ""Real"",
                        ""pollRate"": 500,
                        ""bitNumber"": null,
                        ""stringSize"": 0,
                        ""arraySize"": 0,
                        ""isReadOnly"": false,
                        ""deviceId"": ""plc""
                    }
                ]
            }";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<SiemensChannelConfig>(json, options);

            Assert.NotNull(config);
            Assert.Equal("Nodo1", config.NodeName);
            Assert.Equal("192.168.0.10", config.IpAddress);
            Assert.Equal(0, config.Rack);
            Assert.Equal(2, config.Slot);
            Assert.Equal(1000, config.PollingIntervalMs);
            Assert.Equal(2, config.Tags.Count);
            Assert.Equal("Temperatura", config.Tags[0].Name);
            Assert.Equal("DB1.DBW20", config.Tags[0].Address);
            Assert.Equal("Presion", config.Tags[1].Name);
            Assert.Equal("DB1.DBW22", config.Tags[1].Address);
        }
    }
}