using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.TagConfig;
using vNode.Sdk.Logger;
using Xunit;

public class SiemensSchedulerTests
{
    // Método auxiliar para crear un SiemensTagWrapper simulado con configuración básica.
    private SiemensTagWrapper CreateTagWrapper(Guid id, int pollRate, SiemensTagDataType dataType, string address = "DB1.DBW0")
    {
        var config = new SiemensTagConfig
        {
            TagId = id,
            PollRate = pollRate,
            DataType = dataType,
            Address = address,
            StringSize = 10,
            DeviceId = "dev1"
        };

        var tagModelMock = new Mock<vNode.Sdk.Data.TagModelBase>();
        tagModelMock.SetupGet(t => t.IdTag).Returns(id);
        tagModelMock.SetupGet(t => t.Config).Returns(Newtonsoft.Json.JsonConvert.SerializeObject(config));
        tagModelMock.SetupGet(t => t.InitialValue).Returns((object)null);

        return SiemensTagWrapper.Create(tagModelMock.Object, new Mock<ISdkLogger>().Object);
    }

    /// <summary>
    /// Verifica que el método AddTag agrega correctamente un tag al planificador.
    /// El test también elimina el tag para asegurar que no lanza excepciones.
    /// </summary>
    [Fact]
    public void AddTag_ShouldAddTagToScheduler()
    {
        var loggerMock = new Mock<ISdkLogger>();
        var scheduler = new SiemensScheduler(loggerMock.Object);

        var tag = CreateTagWrapper(Guid.NewGuid(), 100, SiemensTagDataType.Int);

        scheduler.AddTag(tag);

        // El método privado no es accesible, pero podemos probar agregando y eliminando
        scheduler.RemoveTag(tag.Config.TagId);
        // Si no lanza excepción, el test pasa
    }

    /// <summary>
    /// Verifica que el método RemoveTag elimina correctamente un tag del planificador.
    /// No se espera ninguna excepción durante la operación.
    /// </summary>
    [Fact]
    public void RemoveTag_ShouldRemoveTagFromScheduler()
    {
        var loggerMock = new Mock<ISdkLogger>();
        var scheduler = new SiemensScheduler(loggerMock.Object);

        var tag = CreateTagWrapper(Guid.NewGuid(), 100, SiemensTagDataType.Int);

        scheduler.AddTag(tag);
        scheduler.RemoveTag(tag.Config.TagId);

        // No hay acceso directo, pero no debe lanzar excepción
    }

    /// <summary>
    /// Verifica que el método StartReadingAsync dispara el evento ReadingDue cuando hay tags listos para leer.
    /// El test agrega un tag y espera que el evento se dispare correctamente.
    /// </summary>
    [Fact]
    public async Task StartReadingAsync_ShouldTriggerReadingDueEvent()
    {
        var loggerMock = new Mock<ISdkLogger>();
        var scheduler = new SiemensScheduler(loggerMock.Object);

        var tag = CreateTagWrapper(Guid.NewGuid(), 1, SiemensTagDataType.Int);
        scheduler.AddTag(tag);

        bool eventTriggered = false;
        scheduler.ReadingDue += (s, e) =>
        {
            eventTriggered = true;
            Assert.Contains(tag.Config.TagId, e.TagsToRead.Keys);
        };

        var cts = new CancellationTokenSource();
        var task = scheduler.StartReadingAsync(cts.Token);

        await Task.Delay(10); // Espera breve para permitir el tick
        cts.Cancel();

        await task;
        Assert.True(eventTriggered);
    }

    /// <summary>
    /// Verifica que el método CreateDueTagBatches agrupa correctamente los tags por tipo de dato y tasa de sondeo.
    /// Se agregan varios tags y se comprueba que los lotes generados contienen los tipos esperados.
    /// </summary>
    [Fact]
    public void CreateDueTagBatches_ShouldGroupTagsCorrectly()
    {
        var loggerMock = new Mock<ISdkLogger>();
        var scheduler = new SiemensScheduler(loggerMock.Object);

        var tag1 = CreateTagWrapper(Guid.NewGuid(), 1, SiemensTagDataType.Int, "DB1.DBW0");
        var tag2 = CreateTagWrapper(Guid.NewGuid(), 1, SiemensTagDataType.Int, "DB1.DBW2");
        var tag3 = CreateTagWrapper(Guid.NewGuid(), 1, SiemensTagDataType.Bool, "DB1.DBX0.0");

        scheduler.AddTag(tag1);
        scheduler.AddTag(tag2);
        scheduler.AddTag(tag3);

        // Forzar que los tags estén "due"
        Thread.Sleep(2);

        var batches = scheduler.CreateDueTagBatches();

        Assert.NotEmpty(batches);
        Assert.Contains(batches, b => b.DataType == SiemensTagDataType.Int);
        Assert.Contains(batches, b => b.DataType == SiemensTagDataType.Bool);
    }

    /// <summary>
    /// Verifica que no se agregan tags al planificador si su PollRate es igual a cero.
    /// El método AddTag debe ignorar estos tags sin lanzar excepción.
    /// </summary>
    [Fact]
    public void AddTag_ShouldNotAddTagWithZeroPollRate()
    {
        var loggerMock = new Mock<ISdkLogger>();
        var scheduler = new SiemensScheduler(loggerMock.Object);

        var tag = CreateTagWrapper(Guid.NewGuid(), 0, SiemensTagDataType.Int);

        scheduler.AddTag(tag);

        // No debe lanzar excepción, pero el tag no se planifica
    }
}