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

        // CORRECTO: solo pasar el tagModelMock y el logger
        return SiemensTagWrapper.Create(tagModelMock.Object, new Mock<ISdkLogger>().Object);
    }

    [Fact]
    public void AddTag_AddsTagWithoutException()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger);

        var tag = CreateTagWrapper(Guid.NewGuid(), 1000, SiemensTagDataType.Bool);

        Exception ex = Record.Exception(() => scheduler.AddTag(tag));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveTag_RemovesTagWithoutException()
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
    public void AddTagsBatch_AddsMultipleTags()
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
    public void RemoveTags_RemovesMultipleTags()
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
    public async Task StartReadingAsync_RespectsBatchingLimit()
    {
        var logger = Mock.Of<ISdkLogger>();
        var device = new vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig { DeviceId = "dev1", IpAddress = "192.168.0.1" };
        var scheduler = new SiemensScheduler(new List<vNode.SiemensS7.ChannelConfig.SiemensDeviceConfig> { device }, logger, baseTickMs: 10);

        // Crea tags que juntos superen los 200 bytes (por ejemplo, 21 tags de tipo Word de 10 bytes cada uno)
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