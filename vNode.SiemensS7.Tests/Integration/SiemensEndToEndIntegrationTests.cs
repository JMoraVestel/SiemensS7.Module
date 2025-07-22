using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;
using SiemensModule;
using vNode.SiemensS7.SiemensCommonLayer;

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
            Config = Newtonsoft.Json.JsonConvert.SerializeObject(
                config,
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    Converters = new List<Newtonsoft.Json.JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }
                }
            ),
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

        Console.WriteLine("Tags registrados:");
        foreach (var tag in tags)
            Console.WriteLine($"  TagId: {tag.Config.TagId} | Address: {tag.Config.Address} | DeviceId: {tag.Config.DeviceId}");

        var configJson = new System.Text.Json.Nodes.JsonObject
        {
            ["ip"] = "192.168.0.1",
            ["Devices"] = new System.Text.Json.Nodes.JsonObject
            {
                ["dev1"] = new System.Text.Json.Nodes.JsonObject { ["DeviceId"] = "dev1", ["IpAddress"] = "192.168.0.1" },
                ["dev2"] = new System.Text.Json.Nodes.JsonObject { ["DeviceId"] = "dev2", ["IpAddress"] = "192.168.0.2" }
            },
            ["tags"] = new System.Text.Json.Nodes.JsonArray(
                tags.Select(t => new System.Text.Json.Nodes.JsonObject
                {
                    ["tagId"] = t.Config.TagId.ToString(),
                    ["address"] = t.Config.Address,
                    ["pollRate"] = t.Config.PollRate,
                    ["dataType"] = t.Config.DataType.ToString(),
                    ["deviceId"] = t.Config.DeviceId,
                    ["stringSize"] = t.Config.StringSize
                }).ToArray()
            )
        };

        var dummyConnection = new SiemensTcpStrategy("127.0.0.1", 0, 1);
        var dummyChannelConfig = SiemensChannelConfig.FromJson(configJson);

        var tagReaderMock = new Mock<SiemensTagReader>(MockBehavior.Strict, dummyConnection, dummyChannelConfig, loggerMock.Object);
        tagReaderMock.Setup(r => r.ReadManyForSdk(It.IsAny<Dictionary<Guid, SiemensTagWrapper>>()))
            .Returns<Dictionary<Guid, SiemensTagWrapper>>(wrappers =>
            {
                var results = new Dictionary<Guid, TagReadResult>();
                foreach (var kvp in wrappers)
                {
                    var batchItem = new TagReadBatchItem(kvp.Value, DateTime.UtcNow);
                    var resultItem = new TagReadResultItem(
                        batchItem,
                        TagReadResult.TagReadResultType.Success,
                        "VALOR_" + kvp.Value.Config.Address,
                        QualityCodeOptions.Good_Non_Specific
                    );
                    results[kvp.Key] = TagReadResult.CreateSuccess(new List<TagReadResultItem> { resultItem });
                }
                return results;
            });

        var channelConfig = SiemensChannelConfig.FromJson(configJson);

        var siemens = new SiemensModule.Siemens(
            channelConfig,
            loggerMock.Object,
            new SiemensModule.SiemensControl(loggerMock.Object)
        );

        var field = typeof(SiemensModule.Siemens).GetField("_siemensTagReader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(siemens, tagReaderMock.Object);

        // Suscripción directa al evento público
        siemens.OnPostNewEvent += (rawData) =>
        {
            Console.WriteLine($"Evento recibido: IdTag={rawData.IdTag} | Value={rawData.Value}");
            sdkEvents.Add(rawData);
        };

        // Inyección del mock en el diccionario privado _tagReaders
        var tagReadersField = typeof(SiemensModule.Siemens).GetField("_tagReaders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (tagReadersField != null)
        {
            var tagReaders = (Dictionary<string, SiemensTagReader>)tagReadersField.GetValue(siemens);
            tagReaders["dev1"] = tagReaderMock.Object;
            tagReaders["dev2"] = tagReaderMock.Object;
        }
        else
        {
            Console.WriteLine("No se encontró el campo _tagReaders en Siemens.");
        }

        // Registra los tags usando SiemensTagConfig
        foreach (var tag in tags)
            siemens.RegisterTag(tag.Config);

        siemens.Start();

        await Task.Delay(1000); // Aumenta el tiempo para asegurar la lectura
        siemens.Stop();

        Console.WriteLine($"Total eventos recibidos: {sdkEvents.Count}");

        foreach (var tag in tags)
        {
            if (!sdkEvents.Any(e => e.IdTag == tag.Config.TagId))
                Console.WriteLine($"FALTA evento para TagId: {tag.Config.TagId} | Address: {tag.Config.Address} | DeviceId: {tag.Config.DeviceId}");
        }

        Assert.True(sdkEvents.Count >= tags.Count, "No se enviaron todos los datos al SDK");

        foreach (var tag in tags)
        {
            Assert.Contains(sdkEvents, d => d.IdTag == tag.Config.TagId && d.Value.ToString().StartsWith("VALOR_"));
        }

        var agrupaciones = sdkEvents.GroupBy(e => new { e.IdTag, e.Value }).ToList();
        Console.WriteLine($"Agrupaciones encontradas: {agrupaciones.Count}");
        foreach (var grupo in agrupaciones)
        {
            Console.WriteLine($"  Grupo: IdTag={grupo.Key.IdTag} | Value={grupo.Key.Value} | Count={grupo.Count()}");
        }
        Assert.True(agrupaciones.Count >= 3, "No se agruparon correctamente los lotes por DeviceId/DataType/PollRate");
    }
}