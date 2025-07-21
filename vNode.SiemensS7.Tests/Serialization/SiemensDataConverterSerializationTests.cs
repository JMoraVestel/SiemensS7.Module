using S7.Net;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;

namespace vNode.SiemensS7.Tests.Serialization
{
    public class SiemensDataConverterSerializationTests
    {
        [Fact]
        public void ConvertToPlc_Bool_SerializesCorrectly()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Bool };
            object result = SiemensDataConverter.ConvertToPlc(config, true);
            Assert.IsType<bool>(result);
            Assert.True((bool)result);
        }

        [Fact]
        public void ConvertToPlc_Int_SerializesCorrectly()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Int };
            object result = SiemensDataConverter.ConvertToPlc(config, 1234);
            Assert.IsType<short>(result);
            Assert.Equal((short)1234, result);
        }

        [Fact]
        public void ConvertToPlc_Real_SerializesCorrectly()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Real };
            object result = SiemensDataConverter.ConvertToPlc(config, 12.34f);
            Assert.IsType<float>(result);
            Assert.Equal(12.34f, (float)result, 2);
        }

        [Fact]
        public void ConvertToPlc_String_SerializesCorrectly()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.String, StringSize = 5 };
            object result = SiemensDataConverter.ConvertToPlc(config, "ABC");
            Assert.IsType<string>(result);
            Assert.Equal("ABC", result);
        }

        [Fact]
        public void ConvertToPlc_Byte_SerializesCorrectly()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Byte };
            object result = SiemensDataConverter.ConvertToPlc(config, 0xAB);
            Assert.IsType<byte>(result);
            Assert.Equal((byte)0xAB, result);
        }

        [Fact]
        public void Deserialize_ValidJson_Channel_ConfigIsCorrect()
        {
            var json = @"{
              ""nodeName"": ""ChannelProduction1"",
              ""ipAddress"": ""192.168.1.100"",
              ""cpuType"": ""S7300"",
              ""rack"": 0,
              ""slot"": 2,
              ""pollingIntervalMs"": 1000
            }";

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<SiemensChannelConfig>(json, options);

            Assert.NotNull(config);
            Assert.Equal("ChannelProduction1", config.NodeName);
            Assert.Equal("192.168.1.100", config.IpAddress);
            Assert.Equal(CpuType.S7300, config.CpuType);
            Assert.Equal(0, config.Rack);
            Assert.Equal(2, config.Slot);
            Assert.Equal(1000, config.PollingIntervalMs);
        }

        [Fact]
        public void Deserialize_ValidJson_Tags_ConfigIsCorrect()
        {
            var json = @"[
              {
                ""tagId"": ""a1b2c3d4-e5f6-7890-1234-56789abcdef0"",
                ""name"": ""Started"",
                ""address"": ""DB101.DBX0.0"",
                ""dataType"": ""Bool"",
                ""pollRate"": 500,
                ""bitNumber"": 0,
                ""stringSize"": 0,
                ""arraySize"": 0,
                ""isReadOnly"": false,
                ""deviceId"": ""plc1""
              },
              {
                ""tagId"": ""b2c3d4e5-f6a1-8901-2345-6789abcdef01"",
                ""name"": ""Pressure"",
                ""address"": ""DB101.DBW2"",
                ""dataType"": ""Word"",
                ""pollRate"": 1000,
                ""bitNumber"": 1,
                ""stringSize"": 0,
                ""arraySize"": 0,
                ""isReadOnly"": false,
                ""deviceId"": ""plc1""
              }
            ]";

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tags = JsonSerializer.Deserialize<SiemensTagConfig[]>(json, options);

            Assert.NotNull(tags);
            Assert.Equal(2, tags.Length);

            Assert.Equal("a1b2c3d4-e5f6-7890-1234-56789abcdef0", tags[0].TagId.ToString());
            Assert.Equal("Started", tags[0].Name);
            Assert.Equal("DB101.DBX0.0", tags[0].Address);
            Assert.Equal(SiemensTagDataType.Bool, tags[0].DataType);
            Assert.Equal(500, tags[0].PollRate);
            Assert.Null(tags[0].BitNumber);
            Assert.Equal(0, tags[0].StringSize);
            Assert.Equal(0, tags[0].ArraySize);
            Assert.False(tags[0].IsReadOnly);
            Assert.Equal("plc1", tags[0].DeviceId);

            Assert.Equal("b2c3d4e5-f6a1-8901-2345-6789abcdef01", tags[1].TagId.ToString());
            Assert.Equal("Pressure", tags[1].Name);
            Assert.Equal("DB101.DBW2", tags[1].Address);
            Assert.Equal(SiemensTagDataType.Word, tags[1].DataType);
            Assert.Equal(1000, tags[1].PollRate);
            Assert.Null(tags[1].BitNumber);
            Assert.Equal(0, tags[1].StringSize);
            Assert.Equal(0, tags[1].ArraySize);
            Assert.False(tags[1].IsReadOnly);
            Assert.Equal("plc1", tags[1].DeviceId);
        }
    }
}
