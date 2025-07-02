using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ModbusModule.ChannelConfig;
using ModbusModule.Diagnostics;
using ModbusModule.TagConfig;
using ModbusModule.Helper;

using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Filter;
using vNode.Sdk.Logger;

using static ModbusModule.TagReader.TagReadResult;

using ModbusModule.Scheduler;
using ModbusModule.TagReader;

using System.ComponentModel.DataAnnotations;

namespace ModbusModule
{
    public class Modbus : BaseChannel
    {
        private readonly HashSet<Guid> _errorTags = [];
        private readonly ISdkLogger _logger;

        private readonly Dictionary<Guid, ModbusTagWrapper> _tags = [];

        // Dictionary to track diagnostic tags by their type
        private readonly Dictionary<Guid, ModbusControlTag> _controlTagsDictionary = [];

        private readonly string _nodeName;
        private ModbusChannelConfig _config;
        private ChannelDiagnostics _channelControl;
        private CancellationTokenSource? _cts;

        private readonly object _lock = new();
        private readonly object _channelLock = new();
        private ModbusTagReader _modbusTagReader;

        private Task? _readingTask;
        private ModbusScheduler _scheduler;

        private readonly ModbusControl _modbusControl;

        private bool _started = false;
        private readonly ControlTagUpdateDebouncer _updateDebouncer;
        private readonly Dictionary<string, Guid> _pollOnDemandStatusTags = [];

        // public Modbus(JsonObject configJson, ISdkLogger logger, ModbusDiagnostics modbusDiagnostics)
        public Modbus(string nodeName, JsonObject configJson, ISdkLogger logger, ModbusControl modbusControl)
        {            
            ArgumentNullException.ThrowIfNull(configJson);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNullOrEmpty(configJson.ToString());           

            _logger = logger;
            _logger.Information("Modbus", $"Initializing Modbus channel.");

            _nodeName = nodeName;
            _modbusControl = modbusControl;
            _updateDebouncer = new ControlTagUpdateDebouncer(1000, ProcessControlTagUpdates);

            initializeChannel(configJson);
        }

        private void initializeChannel(JsonObject configJson)
        {
            _logger.Information("Modbus",
                "initializeChannel-> Reading configuration. Channel config:\n" +
                JsonHelper.PrettySerialize(configJson));
            try
            {
                _config = readConfig(configJson);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Modbus", "initializeChannel-> Error reading channel configuration");
                throw new InvalidChannelConfigException(ex);
            }

            // Properly dispose and clean up the existing channel control
            if (_channelControl != null)
            {
                _logger.Information("Modbus", "initializeChannel-> Disposing current channelControl");

                // Unsubscribe from events first
                _channelControl.PropertyChanged -= _channelDiagnostics_PropertyChanged;
                _channelControl.DevicePropertyChanged -= _channelDiagnostics_DevicePropertyChanged;

                // Dispose - this will handle all internal cleanup including unregistering all tags
                _channelControl.Dispose();
                _channelControl = null;
            }

            _logger.Information("Modbus",
                "initializeChannel-> Creating channelControl and subscribing to metrics property changes ...");
            _channelControl = new(_config.Devices, _logger);
            _channelControl.PropertyChanged += _channelDiagnostics_PropertyChanged;
            _channelControl.DevicePropertyChanged += _channelDiagnostics_DevicePropertyChanged;

            // Re-register all existing tags with the new channel control
            foreach (var tag in _tags.Values)
            {
                if (tag.Status != ModbusTagWrapper.ModbusTagStatusType.ConfigError)
                {
                    try
                    {
                        _channelControl.RegisterTag(tag.Tag.IdTag, tag.Config.DeviceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Modbus", $"initializeChannel-> Error re-registering tag {tag.Tag.IdTag} with new channel control");
                    }
                }
            }

            if (_scheduler != null)
            {
                _logger.Trace("Modbus", "initializeChannel-> Disposing current scheduler");
                stopScheduler();
                _scheduler = null;
            }

            _logger.Information("Modbus", "initializeChannel-> Initializing scheduler...");
            _scheduler = initializeTaskScheduler();

            if (_tags != null && _tags.Count > 0)
            {
                _logger.Debug("Modbus", "initializeChannel-> Adding already registered tags to scheduler...");
                foreach (var userTag in _tags)
                {
                    _scheduler.AddTag(userTag.Value);
                }
            }
        }

        private ModbusChannelConfig readConfig(JsonObject config)
        {
            ModbusChannelConfig? channelConfig = ModbusChannelConfig.Create(_nodeName, config, _logger);

            if (channelConfig == null)
            {
                throw new ArgumentException("Error validating channel config");
            }

            return channelConfig;
        }

        private ModbusScheduler initializeTaskScheduler()
        {
            if (_scheduler != null)
            {
                _scheduler.ReadingDue -= handleReadingDue;
                _scheduler.StopReading();
            }

            _logger.Information("Modbus", "Creating Modbus tag reading scheduler");
            var scheduler = new ModbusScheduler(_config.Devices.Values.ToList(), _logger);
            scheduler.ReadingDue += handleReadingDue;            
            scheduler.OnPollOnDemandStatusChanged += Scheduler_OnPollOnDemandStatusChanged;

            return scheduler;
        }

        public override async void Dispose()
        {
            _logger.Information("Modbus", "Channel dispose requested");

            if (State == BaseChannelStateOptions.Started)
            {
                _logger.Information("Modbus", "Dispose: Stopping channel...");
                Stop();
            }

            // Dispose channel diagnostics
            if (_channelControl != null)
            {
                _logger.Debug("Modbus", "Dispose: Disposing channel diagnostics");
                _channelControl.PropertyChanged -= _channelDiagnostics_PropertyChanged;
                _channelControl.DevicePropertyChanged -= _channelDiagnostics_DevicePropertyChanged;
                _channelControl.Dispose();
                _channelControl = null;
            }

            // Dispose tag reader if not already disposed in Stop()
            if (_modbusTagReader != null)
            {
                _logger.Debug("Modbus", "Dispose: Disposing tag reader");
                _modbusTagReader.DeviceDemotionStatusChanged -= Reader_DeviceDemotionStatusChanged;
                _modbusTagReader.PropertyChanged -= Reader_PropertyChanged;
                _modbusTagReader.Dispose();
                _modbusTagReader = null;
            }

            // Dispose scheduler resources
            if (_scheduler != null)
            {
                _logger.Debug("Modbus", "Dispose: Disposing scheduler");
                _scheduler.ReadingDue -= handleReadingDue;
                _scheduler.OnPollOnDemandStatusChanged -= Scheduler_OnPollOnDemandStatusChanged;
                // Note: ModbusScheduler doesn't seem to implement IDisposable based on the code shown
                _scheduler = null;
            }

            // Dispose debouncer
            _updateDebouncer?.Dispose();

            // Dispose cancellation token source if still present
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }

            _logger.Information("Modbus", "Dispose: Signaling Modbus control to unregister channel");
            _modbusControl.UnregisterChannel(this);
        }

        public override Result TagConfigurationIsValid(TagModelBase tagObject, string config)
        {
            return Result.Return();
            // try
            // {
            //     JsonObject json = JsonSerializer.Deserialize<JsonObject>(config);
            //
            //     if (json.Count == 0)
            //     {
            //         return Result.Return(false,
            //             "Tag Configuration Error: Tag Configuration is empty. Expected parameters: Address");
            //     }
            //
            //     if (!json.TryGetPropertyValue("Address", out JsonNode _))
            //     {
            //         return Result.Return(false,
            //             "Tag Configuration Error: Address value not set. Example usage = \"Address\":\"Ramp\". Valid values are: Ramp, Triangle, Random, Sinusoidal, Boolean, String, Static");
            //     }
            //
            //     // if (!json.TryGetPropertyValue("Rate", out JsonNode rate))
            //     // {
            //     //     return Result.Return(false, "Tag Configuration Error: Rate value not set. Example usage = \"Rate\":\"1000\" ");
            //     // }
            //
            //     return Result.Return();
            // }
            // catch (Exception ex)
            // {
            //     return Result.Return(false, $"Tag Configuration Error: {ex.Message}");
            // }
        }

        private bool registerControlTag(TagModelBase tagObject)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(nameof(tagObject));
            string tagName = tagObject.Name.Trim();            
            ModbusControlTag? controlTag;
            try
            {
                controlTag = ModbusControlTag.Create(tagObject);
            }
            catch (Exception ex)
            {
                _logger.Error("Modbus", $"Error creating a Modbus Control Tag: {ex.Message}");
                return false;
            }

            if (controlTag.TagConfig.DeviceId == null)
            {
                _logger.Debug("Modbus",
                    $"Adding channel control tag '{tagName}' with ID '{controlTag.IdTag}' to tag dictionary...");
            }
            else
            {
                _logger.Debug("Modbus",
                    $"Adding device control tag '{tagName}' for device id '{controlTag.TagConfig.DeviceId}' with ID '{controlTag.IdTag}' to tag dictionary...");
            }

            if (_controlTagsDictionary.ContainsKey(controlTag.IdTag))
            {
                _logger.Warning("Modbus",
                    $"Control tag '{tagName}' with ID '{controlTag.IdTag}' was already present in controlTagsDictionary");
                _controlTagsDictionary[controlTag.IdTag] = controlTag;
            }
            else
            {
                _controlTagsDictionary.Add(controlTag.IdTag, controlTag);
            }

            // Update it with the current value if available
            updateChannelControlTag(controlTag.IdTag);

            return true;
        }

        private readonly object _registerLock = new object();

        private void UpdateAllControlTags()
        {
            foreach (var controlTag in _controlTagsDictionary.Values)
            {
                updateChannelControlTag(controlTag.IdTag);
            }
        }

        public override bool RegisterTag(TagModelBase tagObject)
        {
            ArgumentNullException.ThrowIfNull(tagObject);
            string tagName = tagObject.Name.Trim();
            ArgumentException.ThrowIfNullOrEmpty(tagName);

            lock (_channelLock)
            {
                _logger.Debug("Modbus", $"Registering tag (Name=[{tagName}], Id=[{tagObject.IdTag}])");

                // Check if this is a diagnostic tag by examining the tag name
                if (Shared.ChannelControlTagNames.Contains(tagName))
                {
                    // This is a diagnostic tag
                    if (registerControlTag(tagObject))
                    {
                        _logger.Debug("Modbus",
                            $"Successfully registered control tag '{tagName}' with Id {tagObject.IdTag}");
                        return true;
                    }
                    else
                    {
                        _logger.Error("Modbus",
                            $"Registration of control tag '{tagName}' with Id {tagObject.IdTag} FAILED");
                        return false;
                    }
                }

                ModbusTagWrapper? userTag = null;
                try
                {
                    if (!registerUserTag(tagObject, out userTag))
                    {
                        _logger.Error("Modbus",
                            $"Registration of user tag '{tagName}' with Id {tagObject.IdTag} FAILED");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Modbus",
                        $"Exception registering user tag '{tagName}' with Id {tagObject.IdTag}");
                    return false;
                }
                finally
                {
                    if (_started == true)
                    {
                        sendInitialData(tagObject.IdTag);
                    }
                }

                _logger.Debug("Modbus",
                    $"Successfully registered user tag '{tagName}' with Id {tagObject.IdTag}");
                return true;
            }
        }

        private bool registerUserTag(TagModelBase tagObject, out ModbusTagWrapper modbusTag)
        {
            ArgumentNullException.ThrowIfNull(tagObject);
            // Adding to internal dictionary and scheduler should be thread safe and atomic
            lock (_lock)
            {
                if (_tags.ContainsKey(tagObject.IdTag))
                {
                    _logger.Warning("Modbus", $"Tag already registered: {tagObject.IdTag}");
                    throw new ArgumentException($"Tag already registered: {tagObject.IdTag}");
                }

                try
                {
                    modbusTag = ModbusTagWrapper.Create(tagObject, _logger);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Modbus", "Exception creating Tag Wrapper. Config is invalid");
                    // Create a tag with error status
                    modbusTag = ModbusTagWrapper.Create(tagObject, ex.Message, _logger);

                    // Add to tags dictionary and return false
                    _tags[tagObject.IdTag] = modbusTag;
                    _logger.Error(ex, "Modbus",
                        $"Tag registered with ConfigError status: exception ocurred while deserializing Config (Name=[{tagObject.Name}], Id=[{tagObject.IdTag}])");
                    return false;
                }

                if (modbusTag.Status == ModbusTagWrapper.ModbusTagStatusType.ConfigError)
                {
                    // Add to dictionary with existing config error
                    _tags[tagObject.IdTag] = modbusTag;
                    _logger.Error("Modbus",
                        $"Tag registered with ConfigError status: error deserializing Config (Name=[{tagObject.Name}], Id=[{tagObject.IdTag}])");
                    return false;
                }

                // Check if device exists
                if (!_config.Devices.TryGetValue(modbusTag.Config.DeviceId, out var deviceConfig))
                {
                    // Create a new tag with device error using the factory method
                    var errorTag = ModbusTagWrapper.Create(tagObject,
                        $"Device ID {modbusTag.Config.DeviceId} not found in channel", _logger);
                    _tags[tagObject.IdTag] = errorTag;
                    _logger.Error("Modbus",
                        $"Tag registered with ConfigError status: device id [{modbusTag.Config.DeviceId}] not present in this channel (Name=[{tagObject.Name}], Id=[{tagObject.IdTag}])");
                    return false;
                }

                // NEW VALIDATION: Check if the address offset is valid based on the device's ModbusAddressOffset
                if (!ApplyDeviceOffset(modbusTag.Config.RegisterAddress, deviceConfig.ModbusAddressOffset))
                {
                    // Create a new tag with address error using the factory method
                    var errorTag = ModbusTagWrapper.Create(tagObject,
                        $"Invalid Modbus address offset: Address {modbusTag.Config.RegisterAddress.Offset} is out of range for device with offset {deviceConfig.ModbusAddressOffset}",
                        _logger);
                    _tags[tagObject.IdTag] = errorTag;
                    _logger.Error("Modbus",
                        $"Tag registered with ConfigError status: address offset [{modbusTag.Config.RegisterAddress.Offset}] invalid for device with offset {deviceConfig.ModbusAddressOffset} (Name=[{tagObject.Name}], Id=[{tagObject.IdTag}])");
                    return false;
                }

                //Everythings is ok, this looks like a good TAG
                _tags[modbusTag.Tag.IdTag] = modbusTag;

                // Let modbusControl now to increment tag counter
                _modbusControl.UserTagRegistered(modbusTag);

                // Register tag
                _channelControl.RegisterTag(modbusTag.Tag.IdTag, modbusTag.Config.DeviceId);
                UpdateAllControlTags();

                _logger.Debug("Modbus",
                    $"Registration success for tag id {tagObject.IdTag}. Adding tag to scheduler.\nTag Configuration:\n{tagObject.Config}");
                if (_scheduler != null && modbusTag.Config.PollRate != -1)
                {
                    _scheduler.AddTag(modbusTag);
                }
            }

            return true;
        }

        // Method that applies the device offset to the ModbusAddress and validates the result
        // Only handles device offsets of 0 and -1
        private bool ApplyDeviceOffset(ModbusAddress address, int deviceOffset)
        {
            // Only handle the two specific device offset values
            if (deviceOffset != 0 && deviceOffset != -1)
            {
                _logger.Warning("Modbus",
                    $"Unexpected device offset value: {deviceOffset}. Only 0 and -1 are supported.");
                return false;
            }

            // For deviceOffset = 0, the address offset must be in range 1-65536
            if (deviceOffset == 0)
            {
                if (address.Offset < 1 || address.Offset > 65536)
                {
                    _logger.Warning("Modbus",
                        $"Invalid address offset: {address.Offset} with device offset {deviceOffset}. Must be in range 1-65536.");
                    return false;
                }
            }
            // For deviceOffset = -1, the address offset must be in range 0-65535
            else if (address.Offset < 0 || address.Offset > 65535)
            {
                _logger.Warning("Modbus",
                    $"Invalid address offset: {address.Offset} with device offset {deviceOffset}. Must be in range 0-65535.");
                return false;
            }

            // For deviceOffset = -1, ADD 1 to the original offset
            if (deviceOffset == -1)
            {
                address.Offset += 1;
            }

            return true;
        }

        public override bool RemoveTag(Guid idTag)
        {
            _logger.Information("Modbus", $"Removing tag {idTag}");

            lock (_lock)
            {
                _errorTags.Remove(idTag);

                if (_tags.Remove(idTag))
                {
                    _logger.Debug("Modbus", "RemoveTag: Signaling channel control");
                    _channelControl.UnregisterTag(idTag);
                    _logger.Debug("Modbus", "RemoveTag: Signaling modbus control");
                    _modbusControl.UserTagUnregistered(idTag);
                    _logger.Debug("Modbus", "RemoveTag: updating scheduler");
                    _scheduler.RemoveTag(idTag);
                    _logger.Debug("Modbus", "RemoveTag: updating control tags");
                    //UpdateAllControlTags();
                    return true;
                }

                _logger.Error("Modbus", $"RemoveTag: Error, tag id {idTag}");
                return false;
            }
        }

        private void SetChannelEnabledState(bool newState)
        {
            // updating channel control tag needs the channel to be started.
            if (newState)
            {
                base.State = BaseChannelStateOptions.Started;
                updateChannelControlTag("Enable", null, true);
            }
            else
            {
                updateChannelControlTag("Enable", null, false);
                base.State = BaseChannelStateOptions.Stopped;
            }
        }

        private void stopScheduler()
        {
            try
            {
                if (_readingTask != null)
                {
                    // Ensure clean shutdown
                    if (!_readingTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        _logger.Error("Modbus", "Timeout expired waiting for scheduler to stop");
                    }
                }
            }
            catch (AggregateException ex)
            {
                _logger.Error("Modbus", $"Scheduler stopped with error: {ex.InnerException?.Message}");
            }
        }

        public override bool Stop()
        {
            _logger.Information("Modbus", "Stop: Channel stop requested...");

            lock (_channelLock)
            {
                if (State == BaseChannelStateOptions.Stopped)
                {
                    _logger.Information("Modbus", "Stop: Channel is already stopped");
                    return true;
                }

                try
                {
                    // Step 1: Set channel state to stopping (prevent new operations)
                    

                    // Step 2: Stop the scheduler first to prevent new read requests
                    _logger.Information("Modbus", "Stop: Stopping scheduler...");
                    if (_scheduler != null)
                    {
                        _scheduler.StopReading();
                    }

                    // Step 3: Cancel any ongoing read operations in the tag reader
                    _logger.Information("Modbus", "Stop: Cancelling current read operations...");
                    if (_modbusTagReader != null)
                    {
                        _modbusTagReader.CancelCurrentReads();
                    }

                    // Step 4: Cancel the scheduler task and wait for it to complete
                    _logger.Information("Modbus", "Stop: Waiting for scheduler task to complete...");
                    if (_cts != null && !_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }

                    if (_readingTask != null)
                    {
                        try
                        {
                            // Wait for the reading task to complete with a reasonable timeout
                            if (!_readingTask.Wait(TimeSpan.FromSeconds(5)))
                            {
                                _logger.Warning("Modbus", "Stop: Timeout waiting for scheduler task to complete");
                            }
                            else
                            {
                                _logger.Debug("Modbus", "Stop: Scheduler task completed successfully");
                            }
                        }
                        catch (AggregateException ex)
                        {
                            // This is expected when cancellation occurs
                            if (ex.InnerException is OperationCanceledException)
                            {
                                _logger.Debug("Modbus", "Stop: Scheduler task was cancelled successfully");
                            }
                            else
                            {
                                _logger.Warning("Modbus", $"Stop: Scheduler task stopped with error: {ex.InnerException?.Message}");
                            }
                        }
                        finally
                        {
                            _readingTask = null;
                        }
                    }

                    // Step 5: Dispose cancellation token source
                    if (_cts != null)
                    {
                        _cts.Dispose();
                        _cts = null;
                    }

                    // Step 6: Wait a short time for any in-flight operations to complete
                    _logger.Debug("Modbus", "Stop: Waiting for in-flight operations to complete...");
                    Thread.Sleep(500); // Give time for any ongoing read operations to finish

                    // Step 7: Dispose the tag reader
                    _logger.Information("Modbus", "Stop: Disposing Tag Reader");
                    if (_modbusTagReader != null)
                    {
                        _modbusTagReader.DeviceDemotionStatusChanged -= Reader_DeviceDemotionStatusChanged;
                        _modbusTagReader.PropertyChanged -= Reader_PropertyChanged;
                        _modbusTagReader.Dispose();
                        _modbusTagReader = null;
                    }

                    // Step 8: Send out-of-service data for all tags
                    _logger.Debug("Modbus", "Stop: Sending channel-stopped data for all tags...");
                    sendChannelOrDeviceDisabledData();

                    // Step 9: Set channel state to stopped
                    _logger.Debug("Modbus", "Stop: Setting channel state to stopped");
                    SetChannelEnabledState(false);

                    _logger.Information("Modbus", "Stop: Modbus module STOPPED successfully");
                    _started = false;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Modbus", "Stop: Exception occurred during channel stop");
                    return false;
                }
            }
        }

        public override bool Start()
        {
            lock (_channelLock)
            {
                _logger.Information("Modbus", "Start: Starting Modbus module...");
                SetChannelEnabledState(true);
                _logger.Information("Modbus", "Start: Sending initial data...");
                sendInitialData();
                _logger.Information("Modbus", "Start: Initializing Modbus Tag Reader...");                
                _modbusTagReader = initializeModbusTagReader();
                if (_modbusTagReader.IsConnected)
                {
                    startScheduler();
                }
                else
                {
                    _logger.Warn("Modbus", "Start: Modbus tag reader is not connected. Scheduler will not start yet");
                }

                _logger.Information("Modbus", "Start: Modbus module STARTED");

                _started = true;
                return true;
            }
        }

        private void startScheduler()
        {
            lock (_channelLock)
            {
                _logger.Information("Modbus", "Start: Signaling scheduler to start reading...");
                _cts = new CancellationTokenSource();
                _readingTask = Task.Run(() => _scheduler.StartReadingAsync(_cts.Token));
            }
        }

        private void sendChannelOrDeviceDisabledData(string? deviceId = null)
        {
            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();
            if (deviceId != null)
            {
                foreach (var tag in _tags.Values)
                {
                    if (tag.Config.DeviceId == deviceId)
                    {
                        _logger.Trace("Modbus",
                            $"Sending device-stopped data for tag (DeviceId={tag.Config?.DeviceId ?? "-"}, Name={tag.Tag.Name}, Id={tag.Tag.IdTag})");
                        PostNewEvent(tag.CurrentValue, QualityCodeOptions.Bad_Out_of_Service, tag.Tag.IdTag, timeStamp);
                    }
                }
            }
            else
            {
                foreach (var tag in _tags.Values)
                {
                    _logger.Trace("Modbus",
                        $"Sending channel-stopped data for tag (DeviceId={tag.Config?.DeviceId ?? "-"}, Name={tag.Tag.Name}, Id={tag.Tag.IdTag})");
                    PostNewEvent(tag.CurrentValue, QualityCodeOptions.Bad_Out_of_Service, tag.Tag.IdTag, timeStamp);                    
                }
            }
        }

        private void sendInitialData(Guid? tagId = null)
        {
            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();

            // If a tagId is provided, send initial data only for that tag
            if (tagId != null)
            {
                if (_tags.TryGetValue(tagId.Value, out var tag))
                {
                    _logger.Trace("Modbus",
                        $"Sending initial data for tag (Value={tag.Tag.InitialValue ?? "null"} DeviceId={tag.Config?.DeviceId ?? "-"}, Name={tag.Tag.Name}, Id={tag.Tag.IdTag})");
                    if (tag.Status == ModbusTagWrapper.ModbusTagStatusType.ConfigError)
                    {
                        _logger.Warn("Modbus",
                            $"Initial data quality for tag is Bad_Configuration_Error: Name={tag.Tag.Name}, Id={tag.Tag.IdTag}");
                        PostNewEvent(tag.Tag.InitialValue, QualityCodeOptions.Bad_Configuration_Error, tag.Tag.IdTag,
                            timeStamp);
                    }
                    else
                    {
                        PostNewEvent(tag.Tag.InitialValue, QualityCodeOptions.Uncertain_Non_Specific, tag.Tag.IdTag,
                            timeStamp);
                    }
                }
                else
                {
                    _logger.Error("Modbus", $"Tag id {tagId} not found in channel");
                }

                return;
            }

            // If no tagId is provided, send initial data for all tags
            foreach (ModbusTagWrapper tag in _tags.Values)
            {
                _logger.Trace("Modbus",
                    $"Sending initial data for tag (Value={tag.Tag.InitialValue} DeviceId={tag.Config?.DeviceId ?? "-"}, Name={tag.Tag.Name}, Id={tag.Tag.IdTag})");
                if (tag.Status == ModbusTagWrapper.ModbusTagStatusType.ConfigError)
                {
                    _logger.Warn("Modbus",
                        $"Initial data quality for tag is Bad_Configuration_Error: Name={tag.Tag.Name}, Id={tag.Tag.IdTag}");
                    PostNewEvent(tag.Tag.InitialValue, QualityCodeOptions.Bad_Configuration_Error, tag.Tag.IdTag,
                        timeStamp);
                }
                else
                    PostNewEvent(tag.Tag.InitialValue, QualityCodeOptions.Uncertain_Non_Specific, tag.Tag.IdTag,
                        timeStamp);
            }

            UpdateAllControlTags();
        }

        private void handleReadingDue(object? sender, ReadingDueEventArgs e)
        {
            // Early validation to avoid unnecessary locking
            if (e?.TagReadBatch == null || e.TagReadBatch?.ReadItems == null)
            {
                _logger.Warning("Modbus", "Invalid tag read batch received");
                return;
            }

            // Quick check if we're shutting down - avoid heavy operations
            if (!_started || State != BaseChannelStateOptions.Started)
            {
                _logger.Debug("Modbus", "handleReadingDue: Channel is not started, ignoring read request");
                return;
            }

            string deviceId = e.TagReadBatch.DeviceId;
            ModbusDeviceConfig? deviceConfig = null;
            ModbusTagReader? tagReader = null;
            bool shouldProceed = false;

            // Critical section: Check status and retrieve config under lock
            lock (_channelLock)
            {
                // Double-check state after acquiring lock
                if (!_started || State != BaseChannelStateOptions.Started)
                {
                    _logger.Debug("Modbus", "handleReadingDue: Channel stopped during lock acquisition, aborting read");
                    return;
                }

                // Check if tag reader is available and connected
                if (_modbusTagReader == null)
                {
                    _logger.Debug("Modbus", "handleReadingDue: Modbus tag reader is null, aborting read");
                    return;
                }

                if (!_modbusTagReader.IsConnected)
                {
                    _logger.Debug("Modbus", "handleReadingDue: Modbus tag reader is not connected, aborting read");
                    return;
                }

                // Try to find the device configuration for the given device ID
                if (!_config.Devices.TryGetValue(deviceId, out deviceConfig))
                {
                    _logger.Error("Modbus", $"Device ID {deviceId} is not configured in this channel.");
                    // Handle invalid device - done inside lock as we need to access _tags safely
                    handleInvalidDevice(e.TagReadBatch.ReadItems);
                    return;
                }

                // Check if the device is enabled
                if (deviceConfig.Enabled == false)
                {
                    _logger.Trace("Modbus", $"Device ID {deviceId} is disabled. Skipping read.");
                    return;
                }

                // All checks passed, capture the tag reader reference and proceed
                tagReader = _modbusTagReader;
                shouldProceed = true;
            }

            // Execute the read operation outside the lock to avoid blocking
            // But only if all checks passed and we have valid references
            if (shouldProceed && deviceConfig != null && tagReader != null)
            {
                try
                {
                    // Final check before initiating read - ensure we're still started
                    if (_started && State == BaseChannelStateOptions.Started)
                    {
                        tagReader.ReadBatch(e.TagReadBatch, deviceConfig, result => processReadResult(result));
                    }
                    else
                    {
                        _logger.Debug("Modbus", "handleReadingDue: Channel stopped before read execution, aborting");
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.Debug("Modbus", "handleReadingDue: Tag reader was disposed, aborting read");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Modbus", "handleReadingDue: Unexpected error initiating read batch");
                }
            }
        }

        private void handleInvalidDevice(List<TagReadBatchItem> readItems)
        {
            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();

            foreach (var readItem in readItems)
            {
                // Check if the tag is still registered in this channel
                if (_tags.ContainsKey(readItem.Tag.Tag.IdTag))
                {
                    PostNewEvent(readItem.Tag.Tag.InitialValue, QualityCodeOptions.Bad_Configuration_Error,
                        readItem.Tag.Tag.IdTag, timeStamp);
                }
                else
                {
                    // Tag is not registered anymore, log a warning
                    _logger.Warning("Modbus",
                        $"Tag Id {readItem.Tag.Tag.IdTag} does not belong to this channel anymore");
                }
            }
        }

        private void processReadResult(TagReadResult result)
        {
            // Early check - if channel is not started, don't process results
            if (!_started || State != BaseChannelStateOptions.Started)
            {
                _logger.Debug("Modbus", "processReadResult: Channel not started, ignoring read result");
                return;
            }

            // Update diagnostics - needs lock as it modifies shared state
            lock (_channelLock)
            {
                // Double-check state after acquiring lock
                if (!_started || State != BaseChannelStateOptions.Started)
                {
                    _logger.Debug("Modbus", "processReadResult: Channel stopped during lock acquisition, ignoring read result");
                    return;
                }

                try
                {
                    _channelControl.ReadCompleted(result);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Modbus", "processReadResult: Error updating channel diagnostics");
                }
            }

            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();

            foreach (var item in result.Items)
            {
                // Early check before processing each item
                if (!_started || State != BaseChannelStateOptions.Started)
                {
                    _logger.Debug("Modbus", "processReadResult: Channel stopped while processing results, aborting");
                    return;
                }

                ModbusTagWrapper? channelTag = null;

                // Get the tag under lock
                lock (_channelLock)
                {
                    // Triple-check state inside lock
                    if (!_started || State != BaseChannelStateOptions.Started)
                    {
                        _logger.Debug("Modbus", "processReadResult: Channel stopped during tag lookup, aborting");
                        return;
                    }

                    // Check if the tag is still registered in this channel
                    if (!_tags.TryGetValue(item.TagId, out channelTag))
                    {
                        // Edge case: A tag whose value was just read no longer belongs to the channel.
                        _logger.Debug("Modbus",
                            $"Tag id [{item.TagId}] was read but no longer belongs to this channel");
                        continue;
                    }
                }

                // Skip null tags (should never happen if we got here)
                if (channelTag == null) continue;

                try
                {
                    // Process the tag result based on the read outcome
                    switch (result.ResultCode)
                    {
                        case TagReadResultType.Success:
                            if (item.Value == null)
                            {
                                _logger.Error("Modbus", "Read was success but read value is null. Posting last read value");
                                PostNewEvent(channelTag.CurrentValue, QualityCodeOptions.Good_Non_Specific, item.TagId,
                                    timeStamp);
                            }
                            else
                            {
                                PostNewEvent(item.Value, QualityCodeOptions.Good_Non_Specific, item.TagId, timeStamp);
                            }
                            break;

                        case TagReadResultType.CommsError:
                            PostNewEvent(channelTag.CurrentValue, QualityCodeOptions.Bad_Communication_Failure,
                                item.TagId, timeStamp);
                            break;

                        case TagReadResultType.ParseError:
                            PostNewEvent(channelTag.CurrentValue, QualityCodeOptions.Bad_Configuration_Error,
                                item.TagId, timeStamp);
                            break;

                        default:
                            PostNewEvent(channelTag.CurrentValue, QualityCodeOptions.Bad_Non_Specific,
                                item.TagId, timeStamp);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Modbus", $"processReadResult: Error posting event for tag {item.TagId}");
                }
            }
        }

        public override TagPathFilterBase GetSubscribeFilter() // TODO: PARA QUE ESTA?
        {
            return TagPathFilterBase.EmptySubscribe();
        }

        public override void ProcessData(RawData rawData) // TODO: PARA QUE ESTA?
        {
            //throw new NotImplementedException();
        }

        public async Task processSetControlTagValue(ModbusControlTag controlTag, object newValue)
        {
            switch (controlTag.Name)
            {
                case "Enable":
                    bool enable;
                    if (!ModbusDataConverter.TryParseBool(newValue, out enable))
                    {
                        _logger.Error("Modbus", $"Failed to parse boolean value: {newValue}");
                        throw new ArgumentOutOfRangeException("newValue", $"Failed to parse boolean value: {newValue}");
                    }

                    if (controlTag.TagConfig.DeviceId != null)
                    {
                        // This is a device control tag
                        // Retrieve device with the given deviceId
                        if (!_config.Devices.TryGetValue(controlTag.TagConfig.DeviceId, out var deviceConfig))
                        {
                            _logger.Error("Modbus",
                                $"Device ID {controlTag.TagConfig.DeviceId} is not configured in this channel.");
                            throw new ArgumentOutOfRangeException("controlTag",
                                $"Device ID {controlTag.TagConfig.DeviceId} is not configured in this channel.");
                        }

                        if (enable && deviceConfig.Enabled == false)
                        {
                            _logger.Information("Modbus",
                                $"Control tag received requesting to enable device id {controlTag.TagConfig.DeviceId}...");
                            deviceConfig.Enabled = true;
                            updateChannelControlTag(controlTag.BaseTag.IdTag);
                            break;
                        }
                        else if (!enable && deviceConfig.Enabled == true)
                        {
                            _logger.Information("Modbus",
                                $"Control tag received requesting to disable device id {controlTag.TagConfig.DeviceId}...");
                            deviceConfig.Enabled = false;
                            updateChannelControlTag(controlTag.BaseTag.IdTag);
                            sendChannelOrDeviceDisabledData(controlTag.TagConfig.DeviceId);
                            break;
                        }
                        else
                        {
                            _logger.Warning("Modbus",
                                "'Enable' control tag does not change current device state: " + deviceConfig.Enabled);
                            break;
                        }
                    }
                    else
                    {
                        if (enable && State == BaseChannelStateOptions.Stopped)
                        {
                            _logger.Information("Modbus",
                                "Control tag received requesting to stop the channel... Stopping channel...");
                            Start();
                            break;
                        }

                        if (!enable && State == BaseChannelStateOptions.Started)
                        {
                            _logger.Information("Modbus",
                                "Control tag received requesting to start the channel... Starting channel...");
                            Stop();
                            break;
                        }

                        _logger.Warning("Modbus",
                            "'Enable' control tag does not change current channel state: " + State.ToString());
                    }
                    break;

                case "PollOnDemand":
                    var pollOnDemand = Convert.ToBoolean(newValue);
                    if (controlTag.TagConfig.DeviceId != null && pollOnDemand)
                    {
                        // This is a device control tag for manual poll on demand
                        if (!_config.Devices.TryGetValue(controlTag.TagConfig.DeviceId, out var deviceConfig))
                        {
                            _logger.Error("Modbus", $"Device ID {controlTag.TagConfig.DeviceId} is not configured in this channel.");
                            return;
                        }

                        if (deviceConfig.PollOnDemandConfig.Enabled && deviceConfig.Enabled)
                        {
                            _logger.Information("Modbus", $"Control tag received requesting poll-on-demand for device id {controlTag.TagConfig.DeviceId}...");

                            // Cancel current reads first to allow immediate prioritization
                            _modbusTagReader.CancelCurrentReads();

                            // Then set priority
                            _scheduler.SetDevicePriority(controlTag.TagConfig.DeviceId, deviceConfig.PollOnDemandConfig.Duration);

                            // Reset the control tag value to false after processing
                            await Task.Delay(100); // Small delay to ensure value is processed
                            PostNewEvent(false, QualityCodeOptions.Good_Non_Specific, controlTag.BaseTag.IdTag);
                        }
                        else
                        {
                            _logger.Warning("Modbus", $"Cannot trigger poll-on-demand for device {controlTag.TagConfig.DeviceId}: Enabled={deviceConfig.Enabled}, PollOnDemandEnabled={deviceConfig.PollOnDemandConfig.Enabled}");
                            // Reset the control tag value to false
                            PostNewEvent(false, QualityCodeOptions.Good_Non_Specific, controlTag.BaseTag.IdTag);
                        }
                    }
                    else
                    {
                        _logger.Warning("Modbus", "PollOnDemand control tag must be associated with a device and value must be true.");
                        // Reset the control tag value to false
                        PostNewEvent(false, QualityCodeOptions.Good_Non_Specific, controlTag.BaseTag.IdTag);
                    }
                    break;
                case "Restart":
                    bool restart;
                    if (!ModbusDataConverter.TryParseBool(newValue, out restart))
                    {
                        _logger.Error("Modbus", $"Failed to parse boolean value: {newValue}");
                        throw new ArgumentOutOfRangeException("newValue", $"Failed to parse boolean value: {newValue}");
                    }

                    if (restart)
                    {
                        _logger.Information("Modbus",
                            "Control tag received requesting to restart the channel... Restarting channel...");
                        await Restart();
                    }

                    break;
                default:
                    _logger.Warning("Modbus", "Unknown control tag name: " + controlTag.Name);
                    throw new ArgumentOutOfRangeException("newValue", "Unknown control tag name: " + controlTag.Name);
            }
        }

        public async Task Restart()
        {
            if (State == BaseChannelStateOptions.Started)
            {
                Stop();
                await Task.Delay(2000);
            }

            Start();
        }

        public override async Task<string> SetTagValue(Guid idTag, object newValue)
        {
            _logger.Debug("Modbus", $"Writing value [{newValue}] into tag id [{idTag}]");

            // Check if this is a diagnostic tag by examining the tag name
            if (_controlTagsDictionary.TryGetValue(idTag, out var controlTag))
            {
                if (controlTag.BaseTag.ClientAccess == ClientAccessOptions.ReadOnly)
                {
                    _logger.Warning("Modbus", $"Cannot write control tag. TagId {idTag} is readonly");
                    return $"Error, tag is read-only";
                }

                try
                {
                    await processSetControlTagValue(controlTag, newValue);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Modbus", $"Exception writing control tag {idTag}");
                    return $"Error, read logs for details";
                }

                return "Ok";
            }

            // From here we now it's a user tag (not control or diagnostics)
            if (!_tags.TryGetValue(idTag, out ModbusTagWrapper? tag))
            {
                _logger.Error("Modbus", $"Cannot write tag. TagId not found: {idTag}");
                return $"Error, tag id not found";
            }

            if (tag.Tag.ClientAccess == ClientAccessOptions.ReadOnly)
            {
                _logger.Warning("Modbus", $"Cannot write tag. TagId {idTag} is readonly");
                return $"Error, tag is read-only";
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                await _modbusTagReader.WriteTagAsync(tag, newValue);
                stopWatch.Stop();
                _channelControl.WriteCompleted(tag.Tag.IdTag, true, stopWatch.ElapsedMilliseconds);
                _logger.Debug("Modbus", $"Tag write success:. Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                PostNewEvent(newValue, QualityCodeOptions.Good_Non_Specific, tag.Tag.IdTag);

                // Write-triggered poll-on-demand
                if (_config.Devices.TryGetValue(tag.Config.DeviceId, out var deviceConfig))
                {
                    if (deviceConfig.PollOnDemandConfig.Enabled &&
                        deviceConfig.PollOnDemandConfig.TriggerOnWrite &&
                        deviceConfig.Enabled)
                    {
                        _logger.Debug("Modbus", $"Setting poll-on-demand priority for device {tag.Config.DeviceId}");
                        // Cancel current reads first to allow immediate prioritization
                        _modbusTagReader.CancelCurrentReads();
                        // Then set priority
                        _scheduler.SetDevicePriority(tag.Config.DeviceId, deviceConfig.PollOnDemandConfig.Duration);
                    }
                }
                //-----------------------------

                return "Ok";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is IOException)
            {
                stopWatch.Stop();
                _channelControl.WriteCompleted(tag.Tag.IdTag, false, stopWatch.ElapsedMilliseconds);
                _logger.Error("Modbus",
                    "Error writing tag: " + ex.Message + $". Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                return "Error:" + ex.Message;
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                _channelControl.WriteCompleted(tag.Tag.IdTag, false, stopWatch.ElapsedMilliseconds);
                _logger.Error(ex, "Modbus",
                    $"Unexpected error writing tag. Elapsed: {stopWatch.ElapsedMilliseconds}ms");
                return "Error: read log for details.";
            }
        }

        private void Scheduler_OnPollOnDemandStatusChanged(object? sender, PollOnDemandStatusEventArgs e)
        {            
            // Update status tag if registered
            if (_pollOnDemandStatusTags.TryGetValue(e.DeviceId, out var tagId))
            {
                _logger.Debug("Modbus", $"Updating PollOnDemandActive status tag for device {e.DeviceId} to {e.IsActive}");
                PostNewEvent(e.IsActive, QualityCodeOptions.Good_Non_Specific, tagId);
            }
        }

        public override void UpdateConfiguration(JsonObject config)
        {
            bool wasStarted = false;
            if (State == BaseChannelStateOptions.Started)
            {
                _logger.Information("Modbus", "UpdateConfiguration -> Stopping channel");
                wasStarted = true;
                Stop();
            }

            _logger.Information("Modbus", "UpdateConfiguration -> Reinitializing channel");
            initializeChannel(config);

            if (wasStarted)
            {
                _logger.Information("Modbus", "UpdateConfiguration -> Re-starting channel");
                Start();
            }
        }

        #region diagnostics

        public override DiagnosticTree GetChannelDiagnosticsTagsConfig(int id)
        {
            DiagnosticTree ret = new();

            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("TagsCount", "Total number of tags in this channel",
                    TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("TotalReads", "Total number read operations",
                    TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("TotalWrites", "Total number of write operations",
                    TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("FailedReads", "Number of failed read operations",
                    TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("Retries", "Number of retry attempts", TagDataTypeOptions.Int32,
                    0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("OverdueReads", "Number of overdue read operations",
                    TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("AvgOverdueTime",
                    "Average time a read is overdue in milliseconds", TagDataTypeOptions.Double, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("AvgWriteTime",
                    "Average time to complete a write operation in milliseconds", TagDataTypeOptions.Double, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("FailedConnectionAttempts",
                    "Number of failed connection attempts", TagDataTypeOptions.Int32, 0));
            ret.DiagnosticTags.Add(
                Diagnostics.Shared.CreateDiagnosticTag("ModbusMasterConnected",
                    "Indicates if the Modbus master is connected", TagDataTypeOptions.Boolean, false));

            DiagnosticTreeChild devicesTreeChild = new DiagnosticTreeChild { Name = "Slaves" };
            foreach (var device in _config.Devices)
            {
                DiagnosticTreeChild slaveTreeChild = new DiagnosticTreeChild { Name = "Slave " + device.Key };
                DiagnosticTag deviceTagCount = new();
                {
                    deviceTagCount.Name = "TagsCount";
                    deviceTagCount.Description = "Total number of tags is this device";
                    var obj = new { DeviceId = device.Key };
                    deviceTagCount.Config = System.Text.Json.JsonSerializer.Serialize(obj);
                    deviceTagCount.ClientAccess = ClientAccessOptions.ReadOnly;
                    deviceTagCount.TagDataType = TagDataTypeOptions.Int32;
                    deviceTagCount.InitialValue = 0;
                }
                DiagnosticTag deviceEnableControlTag = new();
                {
                    deviceEnableControlTag.Name = "Enable";
                    deviceEnableControlTag.Description = "Enables or disables the device";
                    var obj = new { DeviceId = device.Key };
                    deviceEnableControlTag.Config = System.Text.Json.JsonSerializer.Serialize(obj);
                    deviceEnableControlTag.ClientAccess = ClientAccessOptions.ReadWrite;
                    deviceEnableControlTag.TagDataType = TagDataTypeOptions.Boolean;
                    deviceEnableControlTag.InitialValue = true;
                }
                DiagnosticTag devicePollOnDemandControlTag = new();
                {
                    devicePollOnDemandControlTag.Name = "PollOnDemand";
                    devicePollOnDemandControlTag.Description = "Sets poll-on-demand for device";
                    var obj = new { DeviceId = device.Key };
                    devicePollOnDemandControlTag.Config = System.Text.Json.JsonSerializer.Serialize(obj);
                    devicePollOnDemandControlTag.ClientAccess = ClientAccessOptions.ReadWrite;
                    devicePollOnDemandControlTag.TagDataType = TagDataTypeOptions.Boolean;
                    devicePollOnDemandControlTag.InitialValue = false;
                }
                slaveTreeChild.DiagnosticTags.Add(deviceTagCount);
                slaveTreeChild.DiagnosticTags.Add(deviceEnableControlTag);
                slaveTreeChild.DiagnosticTags.Add(devicePollOnDemandControlTag);
                devicesTreeChild.Children.Add(slaveTreeChild);
            }

            ret.Children.Add(devicesTreeChild);
            _logger.Debug("Modbus", $"Returning control and diagnostic tags tree:\n{getDiagnosticTreeJson(ret)}");
            return ret;
        }

        private string getDiagnosticTreeJson(DiagnosticTree tree)
        {
            // Configure JSON serialization options for better readability
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true, // Equivalent to Formatting.Indented
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Equivalent to NullValueHandling.Ignore
                ReferenceHandler = ReferenceHandler.IgnoreCycles // Equivalent to ReferenceLoopHandling.Ignore
            };

            // Add a converter for any enums to make them appear as strings
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            // Serialize the diagnostic tree to JSON
            return JsonSerializer.Serialize(tree, jsonOptions);
        }

        private void _channelDiagnostics_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Check if the property is one of our metrics
            if (Shared.ChannelControlTagNames.Contains(e.PropertyName!))
            {
                // Update the corresponding diagnostic tag if registered
                updateChannelControlTag(e.PropertyName!);
            }
        }

        private void _channelDiagnostics_DevicePropertyChanged(object sender, DevicePropertyChangedEventArgs e)
        {
            // Handle device property changes that were forwarded by ChannelDiagnostics
            updateChannelControlTag(e.PropertyName, e.DeviceId);
        }

        private void deviceDiagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is DeviceDiagnostics deviceDiagnostics)
            {
                string deviceId = deviceDiagnostics.DeviceId;
                string propertyName = e.PropertyName!;

                // Update the corresponding diagnostic tag if registered
                updateChannelControlTag(propertyName, deviceId);
            }
        }

        private void updateChannelControlTag(string tagName, string? deviceId = null, object? forceValue = null,
            bool immediate = false)
        {
            if (immediate || forceValue != null)
            {
                // Update immediately
                UpdateTagByNameAndDevice(tagName, deviceId, forceValue);
            }
            else
            {
                // Queue for debounced update
                _updateDebouncer.QueueUpdate(deviceId, tagName);
            }
        }

        // Updates a diagnostic tag value if we have a tag registered for this metric
        private void updateChannelControlTag(Guid tagId, object? forceValue = null)
        {
            if (!_controlTagsDictionary.TryGetValue(tagId, out ModbusControlTag? tag))
            {
                _logger.Warning("Modbus", $"Control tag id {tagId} not found");
                return;
            }

            bool immediate = forceValue != null;
            updateChannelControlTag(tag.Name, tag.TagConfig.DeviceId, forceValue, immediate);
        }

        // This is called by the debouncer after the interval
        private void ProcessControlTagUpdates(string deviceKey, IEnumerable<string> tagNames)
        {
            string? deviceId = deviceKey == "channel" ? null : deviceKey;

            foreach (string tagName in tagNames)
            {
                // Find and update the control tag
                UpdateTagByNameAndDevice(tagName, deviceId);
            }
        }

        // Helper method to update a tag by name and device
        private void UpdateTagByNameAndDevice(string tagName, string? deviceId, object? forceValue = null)
        {
            ModbusControlTag? controlTag = _controlTagsDictionary.FirstOrDefault(p =>
                p.Value.Name == tagName &&
                (deviceId == null
                    ? string.IsNullOrEmpty(p.Value.TagConfig.DeviceId)
                    : p.Value.TagConfig.DeviceId == deviceId)
            ).Value;

            if (controlTag != null)
            {
                // Get the value to post (either forced or from the control tag)
                object? value = forceValue ?? GetControlTagValue(controlTag);
                if (value != null)
                {
                    DateTime timeStamp = DateTime.UtcNow;
                    PostNewEvent(value, QualityCodeOptions.Good_Non_Specific, controlTag.IdTag,
                        ((DateTimeOffset) timeStamp).ToUnixTimeMilliseconds());
                }
            }
        }

        // Helper method to get metric value by name using direct property access
        private object GetControlTagValue(ModbusControlTag controlTag)
        {
            DiagnosticMetrics metrics;
            string tagName;
            string deviceId;

            ModbusDeviceConfig? deviceConfig = null;
            if (controlTag.TagConfig.DeviceId != null &&
                !_config.Devices.TryGetValue(controlTag.TagConfig.DeviceId, out deviceConfig))
            {
                _logger.Error("Modbus",
                    $"Device ID {controlTag.TagConfig.DeviceId} is not configured in this channel.");
                return null; // Device not found
            }

            tagName = controlTag.Name;

            // First check for control tags
            if (tagName == "Enable")
            {
                if (controlTag.TagConfig.DeviceId == null)
                {
                    // This is a channel control tag, return state of the channel
                    return State == BaseChannelStateOptions.Started;
                }
                else
                {
                    return deviceConfig!.Enabled;
                }
            }
            else if (tagName == "Restart")
                return false;

            // Then check for diagnostic tags (metrics)

            // Check if this is a device-specific metric
            if (!string.IsNullOrEmpty(controlTag.TagConfig.DeviceId))
            {
                deviceId = controlTag.TagConfig.DeviceId;
                if (_channelControl.DeviceDiagnostics.TryGetValue(deviceId, out var deviceDiagnostics))
                {
                    metrics = deviceDiagnostics;
                }
                else
                {
                    _logger.Error("Modbus", $"Cannot get metric value, device [{deviceId}] not found");
                    return null; // Device not found
                }
            }
            else
            {
                metrics = _channelControl;
            }

            return DiagnosticMetrics.GetMetricValue(tagName, metrics);
        }

        #endregion

        private ModbusTagReader initializeModbusTagReader()
        {
            if (_config == null)
                throw new InvalidOperationException(
                    "Channel config must be initialized in order to initialize Modbus Tag Reader");

            var reader = new ModbusTagReader(_config, _channelControl, _logger);
            reader.PropertyChanged += Reader_PropertyChanged;
            reader.DeviceDemotionStatusChanged += Reader_DeviceDemotionStatusChanged;
            if (!reader.IsConnected)
            {
                handleTagReaderDisconnected();
            }

            return reader;
        }

        private void handleTagReaderConnected()
        {
            // start scheduler
            _logger.Debug("Modbus", "Modbus reader connected");
            if (!_scheduler.Running)
            {
                startScheduler();
            }
        }

        private void handleTagReaderDisconnected()
        {
            // stop scheduler
            _logger.Debug("Modbus", "Modbus reader disconnected");
            if (_scheduler.Running)
            {
                _logger.Debug("Modbus", "Stopping scheduler...");
                _scheduler.StopReading();
            }

            // putting all tags in out-of-service
            _logger.Debug("Modbus", "Sending out-of-service for all user tags");
            sendChannelOrDeviceDisabledData();
        }

        private void Reader_DeviceDemotionStatusChanged(object? sender, DeviceDemotionManager.DeviceDemotionEventArgs e)
        {
            // Handle device demotion status change
            if (e.IsDemoted)
            {
                // Device was demoted
                _logger.Warning("Modbus", $"Device {e.DeviceId} has been demoted until {e.DemotedUntil}");

                // Update tag qualities to communication failure
                handleDeviceDemoted(e.DeviceId);
            }
            else
            {
                // Device is no longer demoted
                _logger.Information("Modbus", $"Device {e.DeviceId} is no longer demoted");

                // Update tag qualities to uncertain
                handleDeviceUndemoted(e.DeviceId);
            }
        }

        private void handleDeviceDemoted(string deviceId)
        {
            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();

            // Set all tags for this device to Bad_Communication_Failure
            foreach (var tag in _tags.Values.Where(t => t.Config?.DeviceId == deviceId))
            {
                _logger.Trace("Modbus",
                    $"Setting tag to Bad_Communication_Failure due to device demotion: {tag.Tag.Name}");
                PostNewEvent(tag.CurrentValue, QualityCodeOptions.Bad_Communication_Failure, tag.Tag.IdTag, timeStamp);
            }
        }

        private void handleDeviceUndemoted(string deviceId)
        {
            long timeStamp = ((DateTimeOffset) DateTime.UtcNow).ToUnixTimeMilliseconds();

            // Set all tags for this device to Uncertain_Non_Specific
            foreach (var tag in _tags.Values.Where(t => t.Config?.DeviceId == deviceId))
            {
                _logger.Trace("Modbus",
                    $"Setting tag to Uncertain_Non_Specific after device undemotion: {tag.Tag.Name}");
                PostNewEvent(tag.CurrentValue, QualityCodeOptions.Uncertain_Non_Specific, tag.Tag.IdTag, timeStamp);
            }
        }

        private void Reader_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModbusTagReader.IsConnected))
            {
                lock (_channelLock)
                {
                    if (!_modbusTagReader.IsConnected)
                    {
                        handleTagReaderDisconnected();
                    }
                    else
                    {
                        handleTagReaderConnected();
                    }
                }
            }
        }

        public override DiagnosticTag GetChannelEnableControlTagConfig(int idChannel)
        {
            DiagnosticTag tag = new();
            {
                tag.Name = "Enable";
                tag.Description = "Enable/Disable the module";
                var obj = new { prueba = "prueba" };
                tag.Config = System.Text.Json.JsonSerializer.Serialize(obj);
                tag.ClientAccess = ClientAccessOptions.ReadWrite;
                tag.TagDataType = TagDataTypeOptions.Boolean;
                tag.InitialValue = true;
            }
            return tag;
        }

        public override DiagnosticTag GetChannelRestartControlTagConfig(int idChannel)
        {
            DiagnosticTag tag = new();
            {
                tag.Name = "Restart";
                tag.Description = "Restart the module";
                var obj = new { prueba = "prueba" };
                tag.Config = System.Text.Json.JsonSerializer.Serialize(obj);
                tag.ClientAccess = ClientAccessOptions.ReadWrite;
                tag.TagDataType = TagDataTypeOptions.Boolean;
                tag.InitialValue = false;
            }
            return tag;
        }

        /// <summary>
        /// Checks if the channel contains a tag with the specified ID.
        /// </summary>
        /// <param name="idTag">The ID of the tag to check.</param>
        /// <returns>True if the channel contains the tag, false otherwise.</returns>
        public override bool ContainsTag(Guid idTag)
        {
            return _tags.ContainsKey(idTag) || _controlTagsDictionary.ContainsKey(idTag);
        }

        private void PostNewEvent(object value, QualityCodeOptions quality, Guid idTag, long? timeStamp = null)
        {
            if (idTag == Guid.Empty)
            {
                // IdTag is empty, log an error and exit
                _logger.Error("Modbus", "Cannot post new event, idTag is empty");
                return;
            }

            if (State != BaseChannelStateOptions.Started)
            {
                // Channel is not started, exit early
                return;
            }

            string deviceId = "";
            bool isControlTag = false;
            string tagName;

            // Try to get tag info
            if (_tags.TryGetValue(idTag, out var tag))
            {
                // If it's a user tag, must check if either value or quality changed
                // We don't want to post the same value over and over again
                // UPDATE: No need to check for changes, we will always post the new value.
                //if (tag.CurrentValue != null && EqualityComparer<object>.Default.Equals(value, tag.CurrentValue) && tag.CurrentQuality==quality)
                //{
                //    // No change in value or quality, exit early
                //    return;
                //}
                deviceId = tag.Config?.DeviceId ?? "";
                tagName = tag.Tag.Name;
            }
            else if (_controlTagsDictionary.TryGetValue(idTag, out var controlTag))
            {
                isControlTag = true;
                deviceId = controlTag.TagConfig.DeviceId ?? "";
                tagName = controlTag.Name;
            }
            else
            {
                // Tag not found in either user tags or control tags, exit early.
                _logger.Warning("Modbus", $"Tag with ID {idTag} not found in regular or control tags");
                return;
            }
            /////////////////////


            // Prepare and log message
            string prefix = isControlTag ? "Control " : "";
            string logCategory;
            if (deviceId != "")
                logCategory = $"Modbus/{_config.NodeName}/{deviceId}/{tagName}";
            else
                logCategory = $"Modbus/{_config.NodeName}/{tagName}";

            // Format the value properly based on its type
            string valueStr = FormatValue(value);

            _logger.Trace(logCategory,
                $"{prefix}Data changed --> {{\"value\":{valueStr}, \"quality\":{(int) quality}, \"ts\":{timeStamp}}}");
            ///////////////////////////

            // Invoke the event
            InvokeOnPostNewEvent(new RawData(value, quality, idTag, timeStamp));

            // Update the tag with the new value and quality
            if (tag != null)
            {
                tag.CurrentValue = value;
                tag.CurrentQuality = quality;
            }
        }

        // Helper method to properly format values based on type
        private string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string strValue)
                return $"\"{strValue}\"";

            if (value is bool)
                return value.ToString().ToLowerInvariant(); // Use lowercase true/false for JSON

            if (value is DateTime dt)
                return $"\"{dt:yyyy-MM-ddTHH:mm:ss.fffZ}\""; // ISO 8601 format

            if (value is float || value is double || value is decimal)
                return Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);

            if (value is byte[] bytes)
                return $"[{string.Join(",", bytes)}]";

            return value.ToString();
        }
    }
}
