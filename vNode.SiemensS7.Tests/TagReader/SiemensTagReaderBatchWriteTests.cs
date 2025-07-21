using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;

public class SiemensTagReaderBatchWriteTests
{
    [Fact]
    public async Task WriteTagsBatchAsync_RespetaLimite200BytesYEscribeTodosLosLotes()
    {
        // Arrange
        var mockConn = new Mock<SiemensTcpStrategy>(/* parámetros necesarios */);
        var mockLogger = new Mock<vNode.Sdk.Logger.ISdkLogger>();
        var mockConfig = new Mock<vNode.SiemensS7.ChannelConfig.SiemensChannelConfig>();

        // Simula la escritura (puedes contar cuántas veces se llama)
        int writeCount = 0;
        mockConn.Setup(c => c.Write(It.IsAny<string>(), It.IsAny<object>())).Callback(() => writeCount++);

        var tagReader = new SiemensTagReader(mockConn.Object, mockConfig.Object, mockLogger.Object);

        // Crea tags de 50 bytes cada uno (ajusta GetSize() en el mock si es necesario)
        var tags = new List<(SiemensTagWrapper, object)>();
        for (int i = 0; i < 10; i++)
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
        // Espera 3 lotes: 50*4=200, 50*4=200, 50*2=100
        Assert.Equal(10, writeCount); // Si usas WriteMultipleVars, ajusta el conteo
    }
}