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
using Xunit;

public class SiemensSchedulerTests
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

        var tagModelMock = new Mock<TagModelBase>();
        tagModelMock.SetupGet(t => t.IdTag).Returns(id);
        tagModelMock.SetupGet(t => t.Config).Returns(Newtonsoft.Json.JsonConvert.SerializeObject(config));
        tagModelMock.SetupGet(t => t.InitialValue).Returns((object)null);

        return SiemensTagWrapper.Create(tagModelMock.Object, new Mock<ISdkLogger>().Object);
    }

    [Fact]
    public void AddTag_DoesNotThrow()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tag = CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Bool);

        Exception ex = Record.Exception(() => scheduler.AddTag(tag));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveTag_DoesNotThrow()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tag = CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Bool);
        scheduler.AddTag(tag);

        Exception ex = Record.Exception(() => scheduler.RemoveTag(tag.Config.TagId));
        Assert.Null(ex);
    }

    [Fact]
    public void AddTagsBatch_DoesNotThrow()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tags = new List<SiemensTagWrapper>
        {
            CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Bool),
            CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Word)
        };

        Exception ex = Record.Exception(() => scheduler.AddTagsBatch(tags));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveTags_DoesNotThrow()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tag1 = CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Bool);
        var tag2 = CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Word);

        scheduler.AddTagsBatch(new List<SiemensTagWrapper> { tag1, tag2 });

        Exception ex = Record.Exception(() => scheduler.RemoveTags(new List<Guid> { tag1.Config.TagId, tag2.Config.TagId }));
        Assert.Null(ex);
    }

    [Fact]
    public async Task StartReadingAsync_RaisesReadingDueEvent()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger, baseTickMs: 10);

        var tag = CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool);
        scheduler.AddTag(tag);

        SiemensTagBatch? receivedBatch = null;
        var evt = new ManualResetEventSlim();

        scheduler.ReadingDue += (s, e) =>
        {
            receivedBatch = e.Batch;
            evt.Set();
        };

        var cts = new CancellationTokenSource();
        var readingTask = scheduler.StartReadingAsync(cts.Token);

        Assert.True(evt.Wait(500), "No se disparó el evento ReadingDue");

        cts.Cancel();
        await readingTask;

        Assert.NotNull(receivedBatch);
        Assert.Contains(receivedBatch.Tags, t => t.TagId == tag.Config.TagId);
    }

    [Fact]
    public async Task StartReadingAsync_BatchingLimitIsRespected()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger, baseTickMs: 10);

        // Crea tags que juntos superen los 200 bytes (por ejemplo, 21 tags de tipo String de 10 bytes cada uno)
        var tags = new List<SiemensTagWrapper>();
        for (int i = 0; i < 21; i++)
        {
            tags.Add(CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.String, "dev1", $"DB1.DBB{i * 10}", 10));
        }
        scheduler.AddTagsBatch(tags);

        List<SiemensTagBatch> receivedBatches = new();
        var evt = new ManualResetEventSlim();

        scheduler.ReadingDue += (s, e) =>
        {
            receivedBatches.Add(e.Batch);
            if (receivedBatches.Count >= 2)
                evt.Set();
        };

        var cts = new CancellationTokenSource();
        var readingTask = scheduler.StartReadingAsync(cts.Token);

        Assert.True(evt.Wait(1000), "No se dispararon suficientes eventos ReadingDue para lotes grandes");

        cts.Cancel();
        await readingTask;

        // Al menos dos lotes, ninguno debe superar los 200 bytes
        Assert.True(receivedBatches.Count >= 2);
        foreach (var batch in receivedBatches)
        {
            int totalSize = batch.Tags.Sum(t => t.GetSize());
            Assert.True(totalSize <= 200, $"El lote supera los 200 bytes: {totalSize}");
        }
    }

    [Fact]
    public async Task StartReadingAsync_AgrupaPorDeviceIdDataTypePollRateYLimitaTamaño()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device1 = new SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var device2 = new SiemensDeviceConfig { DeviceId = "dev2", IpAddress = "192.168.0.2" };
        var scheduler = new SiemensScheduler(new List<SiemensDeviceConfig> { device1, device2 }, logger, baseTickMs: 10);

        // Tags con diferentes DeviceId, DataType y PollRate
        var tags = new List<SiemensTagWrapper>
        {
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev1", "DB1.DBX0.0"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev1", "DB1.DBX0.1"),
            CreateTagWrapper(Guid.NewGuid(), 20, SiemensTagDataType.Word, "dev1", "DB1.DBW2"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool, "dev2", "DB1.DBX0.0"),
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.String, "dev2", "DB1.DBB10", 50), // String grande
            CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.String, "dev2", "DB1.DBB60", 150), // String muy grande
        };
        scheduler.AddTagsBatch(tags);

        List<SiemensTagBatch> receivedBatches = new();
        var evt = new ManualResetEventSlim();

        scheduler.ReadingDue += (s, e) =>
        {
            receivedBatches.Add(e.Batch);
            if (receivedBatches.Count >= 3) // Espera al menos 3 lotes
                evt.Set();
        };

        var cts = new CancellationTokenSource();
        var readingTask = scheduler.StartReadingAsync(cts.Token);

        Assert.True(evt.Wait(1000), "No se dispararon suficientes eventos ReadingDue para lotes agrupados");

        cts.Cancel();
        await readingTask;

        // Verifica agrupación por DeviceId, DataType y PollRate
        Assert.Contains(receivedBatches, b => b.DeviceId == "dev1" && b.DataType == SiemensTagDataType.Bool && b.PollRate == 10);
        Assert.Contains(receivedBatches, b => b.DeviceId == "dev1" && b.DataType == SiemensTagDataType.Word && b.PollRate == 20);
        Assert.Contains(receivedBatches, b => b.DeviceId == "dev2" && b.DataType == SiemensTagDataType.String && b.PollRate == 10);

        // Verifica límite de tamaño (ningún lote debe superar los 200 bytes)
        foreach (var batch in receivedBatches)
        {
            int totalSize = batch.Tags.Sum(t => t.GetSize());
            Assert.True(totalSize <= 200, $"El lote supera los 200 bytes: {totalSize}");
        }
    }

    [Fact]
    public void GetScheduleStats_ReturnsStats()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tag1 = CreateTagWrapper(Guid.NewGuid(), 100, SiemensTagDataType.Bool);
        var tag2 = CreateTagWrapper(Guid.NewGuid(), 200, SiemensTagDataType.Word);

        scheduler.AddTag(tag1);
        scheduler.AddTag(tag2);

        var stats = scheduler.GetScheduleStats();

        Assert.Contains(100, stats.Keys);
        Assert.Contains(200, stats.Keys);
        Assert.True(stats[100].TagCount >= 1);
        Assert.True(stats[200].TagCount >= 1);
    }
}

public class SiemensSchedulerIntegrationTests
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

        var tagModelMock = new Mock<TagModelBase>();
        tagModelMock.SetupGet(t => t.IdTag).Returns(id);
        tagModelMock.SetupGet(t => t.Config).Returns(Newtonsoft.Json.JsonConvert.SerializeObject(config));
        tagModelMock.SetupGet(t => t.InitialValue).Returns((object)null);

        return SiemensTagWrapper.Create(tagModelMock.Object, new Mock<ISdkLogger>().Object);
    }

    [Fact]
    public async Task Scheduler_EnvíaDatosAlSDK_CuandoHayLectura()
    {
        // Arrange
        var loggerMock = new Mock<ISdkLogger>();
        var device = new SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<SiemensDeviceConfig> { device }, loggerMock.Object, baseTickMs: 10);

        var tag = CreateTagWrapper(Guid.NewGuid(), 10, SiemensTagDataType.Bool);
        scheduler.AddTag(tag);

        SiemensTagBatch? batchRecibido = null;
        var eventoRecibido = new ManualResetEventSlim();

        scheduler.ReadingDue += (s, e) =>
        {
            batchRecibido = e.Batch;
            eventoRecibido.Set();
        };

        // Act
        var cts = new CancellationTokenSource();
        var tarea = scheduler.StartReadingAsync(cts.Token);

        // Espera a que se dispare el evento
        Assert.True(eventoRecibido.Wait(500), "No se disparó el evento ReadingDue");

        cts.Cancel();
        await tarea;

        // Assert
        Assert.NotNull(batchRecibido);
        Assert.Contains(batchRecibido.Tags, t => t.TagId == tag.Config.TagId);
        Assert.Equal("dev1", batchRecibido.DeviceId);
        Assert.True(batchRecibido.Tags.Count > 0);
    }
}