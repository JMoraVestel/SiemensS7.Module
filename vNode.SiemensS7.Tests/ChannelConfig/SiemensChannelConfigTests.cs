using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.TagConfig;
using Moq;
using S7.Net;
using vNode.Sdk.Logger;
using Xunit;

namespace vNode.SiemensS7.Tests.ChannelConfig
{
    public class SiemensChannelConfigTests
    {
        private JsonObject GetValidJsonConfig()
        {
            return new JsonObject
            {
                ["ip"] = "192.168.0.1",
                ["rack"] = 0,
                ["slot"] = 1,
                ["pollingIntervalMs"] = 1000,
                ["tags"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "Tag1",
                        ["tagId"] = Guid.NewGuid().ToString(),
                        ["address"] = "DB1.DBW0",
                        ["pollRate"] = 1000,
                        ["dataType"] = "Word",
                        ["deviceId"] = "dev1"
                    }
                }
            };
        }

        [Fact]
        public void FromJson_ValidConfig_ParsesCorrectly()
        {
            var json = GetValidJsonConfig();
            var config = SiemensChannelConfig.FromJson(json);

            Assert.NotNull(config);
            Assert.Equal("192.168.0.1", config.IpAddress);
            Assert.Equal(0, config.Rack);
            Assert.Equal(1, config.Slot);
            Assert.Equal(1000, config.PollingIntervalMs);
            Assert.NotNull(config.Tags);
            Assert.Single(config.Tags);
            Assert.Equal("Tag1", config.Tags[0].Name);
        }

        [Fact]
        public void FromJson_MissingIp_ThrowsArgumentException()
        {
            var json = GetValidJsonConfig();
            json.Remove("ip");
            Assert.Throws<ArgumentException>(() => SiemensChannelConfig.FromJson(json));
        }

        [Fact]
        public void FromJson_MissingTags_ThrowsArgumentException()
        {
            var json = GetValidJsonConfig();
            json.Remove("tags");
            Assert.Throws<ArgumentException>(() => SiemensChannelConfig.FromJson(json));
        }

        [Fact]
        public void FromJson_DefaultsRackSlotPollingInterval()
        {
            var json = GetValidJsonConfig();
            json.Remove("rack");
            json.Remove("slot");
            json.Remove("pollingIntervalMs");
            var config = SiemensChannelConfig.FromJson(json);

            Assert.Equal(0, config.Rack);
            Assert.Equal(1, config.Slot);
            Assert.Equal(1000, config.PollingIntervalMs); // Por defecto en el método
        }

        [Fact]
        public void Create_NullJson_ReturnsNullAndLogsError()
        {
            var loggerMock = new Mock<ISdkLogger>();
            var config = SiemensChannelConfig.Create("test", null, loggerMock.Object);

            Assert.Null(config);
            loggerMock.Verify(l => l.Error("SiemensChannelConfig", "La configuración JSON del canal es nula."), Times.Once);
        }

        [Fact]
        public void Create_MissingRequiredFields_LogsErrorAndReturnsNull()
        {
            var loggerMock = new Mock<ISdkLogger>();

            // Prueba faltando CpuType
            var jsonSinCpuType = new JsonObject
            {
                ["IpAddress"] = "192.168.0.1",
                ["Rack"] = 0,
                ["Slot"] = 1
            };
            var config1 = SiemensChannelConfig.Create("test", jsonSinCpuType, loggerMock.Object);
            Assert.Null(config1);

            // Prueba faltando Rack
            var jsonSinRack = new JsonObject
            {
                ["IpAddress"] = "192.168.0.1",
                ["CpuType"] = "S71500",
                ["Slot"] = 1
            };
            var config2 = SiemensChannelConfig.Create("test", jsonSinRack, loggerMock.Object);
            Assert.Null(config2);

            // Prueba faltando Slot
            var jsonSinSlot = new JsonObject
            {
                ["IpAddress"] = "192.168.0.1",
                ["CpuType"] = "S71500",
                ["Rack"] = 0
            };
            var config3 = SiemensChannelConfig.Create("test", jsonSinSlot, loggerMock.Object);
            Assert.Null(config3);

            // Prueba faltando todos los requeridos menos IpAddress
            var jsonSoloIp = new JsonObject
            {
                ["IpAddress"] = "192.168.0.1"
            };
            var config4 = SiemensChannelConfig.Create("test", jsonSoloIp, loggerMock.Object);
            Assert.Null(config4);

            // Verifica que se haya llamado al logger por cada error
            loggerMock.Verify(
                l => l.Error(It.IsAny<Exception>(), "SiemensChannelConfig", It.Is<string>(msg => msg.Contains("Error al crear la configuración del canal"))),
                Times.Exactly(4)
            );
        }
    }
}
