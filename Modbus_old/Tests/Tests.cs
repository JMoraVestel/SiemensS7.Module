using System.Text.Json.Nodes;

using ModbusModule.ChannelConfig;

using Serilog;
using Serilog.Core;

using Tests.Helpers;

using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;

namespace Tests;

[TestFixture]
public class Tests
{
    [SetUp]
    public void Setup()
    {
        // Create a logger factory
        // _loggerFactory = LoggerFactory.Create(builder =>
        // {
        //  builder.AddConsole(); // Logs to console
        // });
        //
        // Create a logger for ModbusModule
        // _logger = _loggerFactory.CreateLogger<ModbusModule.Modbus>();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the logger after each test
        Log.CloseAndFlush();
        // _loggerFactory.Dispose();
        _modbus.Dispose();
        _eventTriggered.Dispose();
    }

    private ModbusModule.Modbus _modbus;
    private ISdkLogger _logger = new SdkLogger();
    // private ILoggerFactory _loggerFactory;

    [Test]
    public void ModbusModule_Creation_Fails_WhenConfigIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _modbus = new ModbusModule.Modbus("", null, _logger, null);
        });
    }

    [Test]
    public void ModbusModule_Creation_Fails_WithInvalidConnectionType()
    {
        JsonObject config = ChannelConfig.CreateInvalidChannelConfig_InvalidConnectionType();
        Assert.Throws<InvalidChannelConfigException>(() =>
        {
            _modbus = new ModbusModule.Modbus("", config, _logger, null);
        });
    }

    [Test]
    public void ModbusModule_Creation_Succeeds()
    {
        JsonObject config = ChannelConfig.CreateGoodChannelConfig();

        _modbus = new ModbusModule.Modbus("", config, _logger, null);

        Assert.Pass();
    }

    [Test]
    public async Task ModbusModule_RegisterTag_Succeeds()
    {
        JsonObject config = ChannelConfig.CreateGoodChannelConfig();
        _modbus = new ModbusModule.Modbus("", config, _logger, null);

        TagModelBase tag = TagConfig.CreateTag(1, 40001, 1000, "Int16", 0, ClientAccessOptions.ReadWrite);

        _modbus.RegisterTag(tag);
        _modbus.Start();
    }

    private AutoResetEvent _eventTriggered;
    private RawData _eventValue;
    private bool _initialDataReceived = false;

    [Test]
    public async Task ModbusModule_Read_Success()
    {
        _eventTriggered = new AutoResetEvent(false);

        JsonObject config = ChannelConfig.CreateGoodChannelConfig();
        _modbus = new ModbusModule.Modbus("", config, _logger, null);

        TagModelBase tag = TagConfig.CreateTag(1, 40001, 1000, "Int16", 0, ClientAccessOptions.ReadWrite);
        _modbus.RegisterTag(tag);

        // Subscribe to event and store received value
        _modbus.OnPostNewEvent += (rawData) =>
        {
            _eventValue = rawData;
            if (!_initialDataReceived)
                _initialDataReceived = true; // Ignore first event since it's just the initial data
            else
                _eventTriggered.Set(); // Signal that the event was triggered
        };

        // Act: Start polling
        _modbus.Start();

        // Wait for the event (up to 2 seconds)
        bool eventRaised = _eventTriggered.WaitOne(TimeSpan.FromSeconds(10));

        // Assert: Verify event was raised with a value
        Assert.IsTrue(eventRaised, "Modbus did not poll or raise the event.");
        Assert.IsNotNull(_eventValue, "No value was received in the event.");
        Assert.IsTrue(_eventValue.Quality == QualityCodeOptions.Good_Non_Specific,
            $"Invalid read quality: {_eventValue.Quality}");

        if (eventRaised)

            // Stop polling to clean up
            _modbus.Stop();
    }
}

internal class SdkLogger : ISdkLogger
{
    private Logger _logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

    public void Fatal(string category, string message)
    {
        _logger.Fatal($"[{category}] {message}");
    }

    public void Fatal(Exception exception, string category, string message)
    {
        _logger.Fatal(exception, $"[{category}] {message}");
    }

    public void Error(string category, string message)
    {
        _logger.Error($"[{category}] {message}");
    }

    public void Error(Exception exception, string category, string message)
    {
        _logger.Error(exception, $"[{category}] {message}");
    }

    public void Warn(string category, string message)
    {
        _logger.Warning($"[{category}] {message}");
    }

    public void Warn(Exception exception, string category, string message)
    {
        _logger.Warning(exception, $"[{category}] {message}");
    }

    public void Warning(string category, string message)
    {
        _logger.Warning($"[{category}] {message}");
    }

    public void Warning(Exception exception, string category, string message)
    {
        _logger.Warning(exception, $"[{category}] {message}");
    }

    public void Info(string category, string message)
    {
        _logger.Information($"[{category}] {message}");
    }

    public void Info(Exception exception, string category, string message)
    {
        _logger.Information(exception, $"[{category}] {message}");
    }

    public void Information(string category, string message)
    {
        _logger.Information($"[{category}] {message}");
    }

    public void Information(Exception exception, string category, string message)
    {
        _logger.Information(exception, $"[{category}] {message}");
    }

    public void Debug(string category, string message)
    {
        _logger.Debug($"[{category}] {message}");
    }

    public void Debug(Exception exception, string category, string message)
    {
        _logger.Debug(exception, $"[{category}] {message}");
    }

    public void Trace(string category, string message)
    {
        _logger.Verbose($"[{category}] {message}");
    }

    public void Trace(Exception exception, string category, string message)
    {
        _logger.Verbose(exception, $"[{category}] {message}");
    }
}

//internal class SdkLogger : ISdkLogger
//{
//    public void Error(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Error(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Warn(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Warn(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Warning(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Warning(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Info(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Info(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Information(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Information(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Debug(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Debug(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }

//    public void Trace(string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//    }

//    public void Trace(Exception exception, string category, string message)
//    {
//        Console.Error.WriteLine($"Error: {category} - {message}");
//        Console.Error.WriteLine(exception);
//    }
//}
