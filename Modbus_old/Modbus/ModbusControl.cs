using System.Reflection;

using ModbusModule.TagConfig;

using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;

namespace ModbusModule;

public class ModbusControl : BaseChannelControl
{
    private ISdkLogger _logger;

    private readonly List<Modbus> _channels = new();
    private readonly Dictionary<Guid, TagModelBase> _controlTagsDictionary = [];

    private long tagsCount;

    public ModbusControl(ISdkLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts the module and all registered channels.
    /// </summary>
    /// <returns>True if the module was successfully stopped, false otherwise.</returns>
    public override bool Start()
    {
        _logger.Information("ModbusControl", "Starting Modbus control");
        State = BaseChannelStateOptions.Started;
        PostNewEvent("Instances", _channels.Count);
        PostNewEvent("TagsCount", tagsCount);
        PostNewEvent("Version", Assembly.GetExecutingAssembly().GetName().Version!.ToString());
        PostNewEvent("Enable", true);
        PostNewEvent("Restart", false);
        return true;


        //try
        //{
        //    foreach (var channel in _channels)
        //    {
        //        if (channel.State == BaseChannelStateOptions.Stopped)
        //        {
        //            _logger.Information("ModbusControl", $"Starting channel [{channel.ModuleName}]");
        //            channel.Start();
        //        }
        //    }

        //    return true;
        //}
        //catch (Exception ex)
        //{
        //    _logger.Error(ex, "ModbusControl", "Exception ocurred stopping all channels");
        //    return false;
        //}
    }

    /// <summary>
    /// Increases the count of tags by 1 and posts the new tag count as a value.
    /// </summary>
    /// <remarks>
    /// This method increments the count of tags by 1 and then posts the new tag count as a value using the private method PostNewValue.
    /// It is typically called when registering a new tag to update the tag count.
    /// </remarks>
    public void UserTagRegistered(ModbusTagWrapper modbusTag)
    {
        _logger.Debug("ModbusControl",
            $"New user tag registered (DeviceId=[{modbusTag.Config.DeviceId}], TagName=[{modbusTag.Tag.Name}], TagId=[{modbusTag.Tag.IdTag}]) ");
        PostNewEvent("TagsCount", ++tagsCount);
    }

    public void UserTagUnregistered(Guid tagId)
    {
        _logger.Information("ModbusControl", $"User tag un-registered (TagId=[{tagId}]) ");
        PostNewEvent("TagsCount", --tagsCount);
    }

    private readonly object _lock = new object();

    public override bool RegisterTag(TagModelBase tagObject)
    {
        ArgumentNullException.ThrowIfNull(tagObject);
        string tagName = tagObject.Name.Trim();
        ArgumentException.ThrowIfNullOrEmpty(tagName);

        lock (_lock)
        {
            _logger.Debug("ModbusControl", $"Registering tag (Name=[{tagName}], Id=[{tagObject.IdTag}])");

            try
            {
                if (_controlTagsDictionary.ContainsKey(tagObject.IdTag))
                {
                    _logger.Warning("ModbusControl",
                        $"Control tag '{tagName}' with ID '{tagObject.IdTag}' was already present in controlTagsDictionary");
                    _controlTagsDictionary[tagObject.IdTag] = tagObject;
                }
                else
                {
                    _controlTagsDictionary.Add(tagObject.IdTag, tagObject);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusControl",
                    $"Exception registering modbus control tag '{tagName}' with Id {tagObject.IdTag}");
                return false;
            }

            return true;
        }
    }

    public async Task processSetControlTagValue(TagModelBase controlTag, object newValue)
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

                if (enable && State == BaseChannelStateOptions.Stopped)
                {
                    _logger.Warning("ModbusControl", "Control tag received requesting to start the channel... Stopping channel...");
                    Start();
                    break;
                }

                if (!enable && State == BaseChannelStateOptions.Started)
                {
                    _logger.Warning("ModbusControl",
                        "Control tag received requesting to stop the channel... Starting channel...");
                    Stop();
                    break;
                }

                _logger.Warning("ModbusControl",
                    "'Enable' control tag does not change current channel state: " + State.ToString());
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
                    _logger.Warning("ModbusControl",
                        "Control tag received requesting to restart the channel... Restarting channel...");
                    await Restart();
                }

                break;
            default:
                _logger.Warning("Modbus", "Unknown control tag name: " + controlTag.Name);
                break;
        }
    }

    /// <summary>
    /// Registers a channel to the control.
    /// </summary>
    /// <param name="channel">The channel to be registered.</param>
    public void RegisterChannel(Modbus channel)
    {
        _channels.Add(channel);
        PostNewEvent("Instances", _channels.Count);
    }

    /// <summary>
    /// Unregisters a channel from the control.
    /// </summary>
    /// <param name="channel">The channel to be unregistered.</param>
    public void UnregisterChannel(Modbus channel)
    {
        _channels.Remove(channel);
        PostNewEvent("Instances", _channels.Count);
    }

    /// <summary>
    /// Restart all registered channels
    /// </summary>
    protected async Task Restart()
    {
        _logger.Information("ModbusControl", $"Restarting all {_channels.Count} channels");

        try
        {
            foreach (var channel in _channels)
            {
                await channel.Restart();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ModbusControl", "Exception ocurred restarting all channels");
        }
    }

    public override bool Stop()
    {
        _logger.Information("ModbusControl", $"Stopping all {_channels.Count} channels");
        PostNewEvent("Enable", false);
        try
        {
            foreach (var channel in _channels)
            {
                if (channel.State == BaseChannelStateOptions.Started)
                {
                    _logger.Information("ModbusControl", $"Stopping channel [{channel.ModuleName}]");
                    channel.Stop();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ModbusControl", "Exception ocurred stopping all channels");
            return false;
        }
        finally
        {
            State = BaseChannelStateOptions.Stopped;
        }
    }

    //public override bool Start()
    //{
    //    _logger.Information("ModbusControl", $"Starting all {_channels.Count} channels");

    //    try
    //    {
    //        foreach (var channel in _channels)
    //        {
    //            if (channel.State == BaseChannelStateOptions.Stopped)
    //                channel.Start();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.Error(ex, "ModbusControl", "Exception ocurred starting all channels");
    //        return false;
    //    }
    //    return true;
    //}

    public override async Task<string> SetTagValue(Guid idTag, object newValue)
    {
        _logger.Debug("ModbusControl", $"Writing value [{newValue}] into tag id [{idTag}]");
        if (!_controlTagsDictionary.TryGetValue(idTag, out var controlTag))
        {
            _logger.Error("ModbusControl", $"Cannot write tag. TagId not found: {idTag}");
            return $"Error, Tag id not found";
        }

        if (controlTag.ClientAccess == ClientAccessOptions.ReadOnly)
        {
            _logger.Warning("ModbusControl", $"Cannot write control tag. TagId {idTag} is readonly");
            return $"Error, tag is read-only";
        }

        try
        {
            await processSetControlTagValue(controlTag, newValue);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "ModbusControl", $"Exception writing control tag.");
            return $"Error, read log for details";
        }

        return "Ok";
    }

    /// <summary>
    /// Checks if the channel contains a tag with the specified ID.
    /// </summary>
    /// <param name="idTag">The ID of the tag to check.</param>
    /// <returns>True if the channel contains the tag, false otherwise.</returns>
    public override bool ContainsTag(Guid idTag)
    {
        return _controlTagsDictionary.ContainsKey(idTag);
    }

    private void PostNewEvent(string tagName, object value)
    {
        var controlTag = _controlTagsDictionary.Values.FirstOrDefault(tag => tag.Name == tagName);
        if (controlTag == null)
        {
            _logger.Error("ModbusControl", $"Control tag [{tagName}] is not registered.");
            return;
        }

        _logger.Trace("ModbusControl", $"Module diag update: [{tagName}]. New value: [{value}]");
        if (State == BaseChannelStateOptions.Started)
        {
            PostNewEvent(controlTag.IdTag, value);
        }
    }

    private void PostNewEvent(Guid idTag, object value)
    {
        if (idTag == Guid.Empty)
        {
            _logger.Trace("ModbusControl", $"tag id not set for value {value}");
            return;
        }

        InvokeOnPostNewEvent(new RawData(value, QualityCodeOptions.Good_Non_Specific, idTag));
    }

    public override void Dispose()
    {
        _logger.Warning("ModbusControl", "Disposing, stopping all channels");
        foreach (BaseChannel channel in _channels)
        {
            if (channel.State == BaseChannelStateOptions.Started)
            {
                _logger.Warning("ModbusControl", $"Dispose: stopping channel [{channel.ModuleName}]");
                channel.Stop();
            }

            channel.Dispose();
        }
    }
}
