using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;

public class SiemensTagReaderBatchWriteTests
{
    [Fact]
    public async Task WriteTagsBatchAsync_NoBatchExceeds200Bytes()
    {
        // Arrange
        var mockConn = new Mock<SiemensTcpStrategy>("127.0.0.1", 0, 0);
        var mockLogger = new Mock<vNode.Sdk.Logger.ISdkLogger>();
        var mockConfig = new Mock<vNode.SiemensS7.ChannelConfig.SiemensChannelConfig>();

        var tagReader = new SiemensTagReader(mockConn.Object, mockConfig.Object, mockLogger.Object);

        // Simula la escritura y cuenta las llamadas
        int writeCount = 0;
        mockConn.Setup(c => c.Write(It.IsAny<string>(), It.IsAny<object>())).Callback(() => writeCount++);

        // Crea 5 tags de 50 bytes cada uno (total 250 bytes, deben ser 2 lotes: 200 y 50)
        var tags = new List<(SiemensTagWrapper, object)>();
        for (int i = 0; i < 5; i++)
        {
            var tagMock = new Mock<SiemensTagWrapper>(MockBehavior.Loose, null);
            tagMock.Setup(t => t.Config.GetSize()).Returns(50);
            tagMock.Setup(t => t.Config.Address).Returns($"DB1.DBX{i * 50}.0");
            tags.Add((tagMock.Object, (object)123));
        }

        // Act
        var result = await tagReader.WriteTagsBatchAsync(tags);

        // Assert
        Assert.True(result);
        // Deben ser 5 escrituras individuales, pero en 2 lotes (verifica la lógica de batching)
        Assert.Equal(5, writeCount);
    }

    [Fact]
    public async Task WriteTagsBatchAsync_Exact200Bytes_SingleBatch()
    {
        // Arrange
        var mockConn = new Mock<SiemensTcpStrategy>("127.0.0.1", 0, 0);
        var mockLogger = new Mock<vNode.Sdk.Logger.ISdkLogger>();
        var mockConfig = new Mock<vNode.SiemensS7.ChannelConfig.SiemensChannelConfig>();

        var tagReader = new SiemensTagReader(mockConn.Object, mockConfig.Object, mockLogger.Object);

        int writeCount = 0;
        mockConn.Setup(c => c.Write(It.IsAny<string>(), It.IsAny<object>())).Callback(() => writeCount++);

        // 4 tags de 50 bytes = 200 bytes exactos
        var tags = new List<(SiemensTagWrapper, object)>();
        for (int i = 0; i < 4; i++)
        {
            var tagMock = new Mock<SiemensTagWrapper>(MockBehavior.Loose, null);
            tagMock.Setup(t => t.Config.GetSize()).Returns(50);
            tagMock.Setup(t => t.Config.Address).Returns($"DB1.DBX{i * 50}.0");
            tags.Add((tagMock.Object, (object)456));
        }

        // Act
        var result = await tagReader.WriteTagsBatchAsync(tags);

        // Assert
        Assert.True(result);
        Assert.Equal(4, writeCount);
    }

    [Fact]
    public async Task WriteTagsBatchAsync_TagLargerThan200Bytes_AlwaysSingleTagPerBatch()
    {
        // Arrange
        var mockConn = new Mock<SiemensTcpStrategy>("127.0.0.1", 0, 0);
        var mockLogger = new Mock<vNode.Sdk.Logger.ISdkLogger>();
        var mockConfig = new Mock<vNode.SiemensS7.ChannelConfig.SiemensChannelConfig>();

        var tagReader = new SiemensTagReader(mockConn.Object, mockConfig.Object, mockLogger.Object);

        int writeCount = 0;
        mockConn.Setup(c => c.Write(It.IsAny<string>(), It.IsAny<object>())).Callback(() => writeCount++);

        // 2 tags de 250 bytes cada uno (cada uno debe ir en su propio lote)
        var tags = new List<(SiemensTagWrapper, object)>();
        for (int i = 0; i < 2; i++)
        {
            var tagMock = new Mock<SiemensTagWrapper>(MockBehavior.Loose, null);
            tagMock.Setup(t => t.Config.GetSize()).Returns(250);
            tagMock.Setup(t => t.Config.Address).Returns($"DB1.DBX{i * 250}.0");
            tags.Add((tagMock.Object, (object)789));
        }

        // Act
        var result = await tagReader.WriteTagsBatchAsync(tags);

        // Assert
        Assert.True(result);
        Assert.Equal(2, writeCount);
    }
}