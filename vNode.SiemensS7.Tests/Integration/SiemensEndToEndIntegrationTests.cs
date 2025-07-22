using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using vNode.Sdk.Data;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;

public class SiemensEndToEndIntegrationTests
{
    private SiemensTagWrapper CreateTagWrapper(Guid id, int pollRate, SiemensTagDataType dataType, string deviceId = "dev1", string address = "DB1.DBW0", int stringSize = 0)
    {
        var config = new SiemensTagConfig
        {
            TagId = id,
            PollRate = pollRate,
            DataType = dataType,
            Address = address,
            StringSize = (byte)stringSize,
            DeviceId = deviceId
        };

        var tagModel = new TagModelBase //TODO: Implmentar clase TagModelSiemens 
        {
            IdTag = id,
            Config = Newtonsoft.Json.JsonConvert.SerializeObject(config),
            InitialValue = null
        };

        return SiemensTagWrapper.Create(tagModel, new Mock<ISdkLogger>().Object);
    }

    [Fact]
    public async Task EndToEnd_FlujoCompleto_EnvioAlSDK()
    {
        // Arrange
        var loggerMock = new Mock<ISdkLogger>();
        var sdkEvents = new List<RawData>();
        var device1 = new SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var device2 = new SiemensDeviceConfig { DeviceId = "dev2", IpAddress = "192.168.0.2" };

        var tags = new List<SiemensTagWrapper>
        {
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev1", "DB1.DBX0.0"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev1", "DB1.DBX0.1"),
            CreateTagWrapper(Guid.NewGuid(), 20, SiemensTagDataType.Word, "dev1", "DB1.DBW2"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev2", "DB1.DBX0.0"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.String, "dev2", "DB1.DBB10", 50),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.String, "dev2", "DB1.DBB60", 150),
        };

        var tagReaderMock = new Mock<SiemensTagReader>(MockBehavior.Strict, null, null, loggerMock.Object);
        tagReaderMock.Setup(r => r.ReadManyForSdk(It.IsAny<Dictionary<Guid, SiemensTagWrapper>>()))
            .Returns<Dictionary<Guid, SiemensTagWrapper>>(wrappers =>
            {
                var items = new List<TagReadResultItem>();
                foreach (var kvp in wrappers)
                {
                    var batchItem = new TagReadBatchItem(kvp.Value, DateTime.UtcNow);
                    var resultItem = new TagReadResultItem(batchItem, TagReadResult.TagReadResultType.Success, "VALOR_" + kvp.Value.Config.Address);
                    items.Add(resultItem);
                }
                return new Dictionary<Guid, TagReadResult>
                {
                    { Guid.NewGuid(), TagReadResult.CreateSuccess(items) }
                };
            });

        var configJson = new System.Text.Json.Nodes.JsonObject
        {
            ["Devices"] = new System.Text.Json.Nodes.JsonObject
            {
                ["dev1"] = new System.Text.Json.Nodes.JsonObject { ["DeviceId"] = "dev1", ["IpAddress"] = "192.168.0.1" },
                ["dev2"] = new System.Text.Json.Nodes.JsonObject { ["DeviceId"] = "dev2", ["IpAddress"] = "192.168.0.2" }
            }
        };

        var channelConfig = SiemensChannelConfig.FromJson(configJson);

        var siemens = new Siemens(
            channelConfig,
            loggerMock.Object,
            new SiemensModule.SiemensControl(loggerMock.Object)
        );

        var field = typeof(Siemens).GetField("_siemensTagReader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(siemens, tagReaderMock.Object);

        var onPostNewEvent = typeof(Siemens).GetEvent("OnPostNewEvent");
        if (onPostNewEvent != null)
        {
            onPostNewEvent.AddEventHandler(siemens, (Action<RawData>)(rawData => sdkEvents.Add(rawData)));
        }

        // Registra los tags usando SiemensTagConfig
        foreach (var tag in tags)
            siemens.RegisterTag(tag.Config);

        siemens.Start();

        await Task.Delay(500);
        siemens.Stop();

        Assert.True(sdkEvents.Count >= tags.Count, "No se enviaron todos los datos al SDK");

        foreach (var tag in tags)
        {
            Assert.Contains(sdkEvents, d => d.IdTag == tag.Config.TagId && d.Value.ToString().StartsWith("VALOR_"));
        }

        var agrupaciones = sdkEvents.GroupBy(e => new { e.IdTag, e.Value }).ToList();
        Assert.True(agrupaciones.Count >= 3, "No se agruparon correctamente los lotes por DeviceId/DataType/PollRate");
    }
}