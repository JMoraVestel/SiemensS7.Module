using System.Collections.Concurrent;
using System.Reflection;
using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;

namespace SiemensModule
{
    public class SiemensControl : BaseChannelControl
    {
        private readonly ISdkLogger _logger;
        private readonly ConcurrentDictionary<Guid, Siemens> _channels = new();
        private readonly Dictionary<Guid, TagModelBase> _controlTagsDictionary = new Dictionary<Guid, TagModelBase>();

        private int tagsCount;

        public SiemensControl(ISdkLogger logger)
        {
            _logger = logger;
        }

        public void RegisterChannel(Guid channelId, Siemens channel)
        {
            _channels.TryAdd(channelId, channel);
        }

        public void UnregisterChannel(Siemens channel)
        {
            if (_channels.TryRemove(channel.IdChannel, out var removedChannel))
            {
                _logger.Information("SiemensControl", $"Channel with ID {channel.IdChannel} has been unregistered.");
                tagsCount -= removedChannel.TagsCount;
                PostNewEvent("Instances", _channels.Count);
                PostNewEvent("TagsCount", tagsCount);
            }
            else
            {
                _logger.Warning("SiemensControl", $"Attempted to unregister a channel with ID {channel.IdChannel} that was not found.");
            }
        }

        public override void Dispose()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }
            _channels.Clear();
            GC.SuppressFinalize(this);
        }

        public override bool ContainsTag(Guid idTag)
        {
            // Add logic to check if the tag exists  
            return false; // Placeholder implementation  
        }

        public override Task<string> SetTagValue(Guid idTag, object newValue)
        {
            // Add logic to set the tag value  
            return Task.FromResult("Success"); // Placeholder implementation  
        }

        public override bool Start()
        {
            _logger.Info("SiemensControl", "Starting Siemens channel...");
            State = BaseChannelStateOptions.Started;
            PostNewEvent("Instances", _channels.Count);
            PostNewEvent("TagsCount", tagsCount);
            PostNewEvent("Version", Assembly.GetExecutingAssembly().GetName().Version!.ToString()); 
            PostNewEvent("Enabled", true);
            PostNewEvent("Restart", false);

            return true;
        }

        private void PostNewEvent(string tagName, object value)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                _logger.Error("SiemensControl", "Tag name cannot be null or empty.");
                return;
            }

            var controlTag = _controlTagsDictionary.Values.FirstOrDefault(t => t.Name == tagName);
            if (controlTag != null)
            {
                _logger.Info("SiemensControl", $"Posting new event for tag '{tagName}': {value}");
                return;
            }

            _logger.Trace("SiemensControl", $"No control tag found for '{tagName}'. Posting event anyway.");

            if (tagName == "Enabled")
            {
                if (value is bool enableValue)
                {
                    _logger.Debug("SiemensControl", $"Posting event for tag '{tagName}': {enableValue}");
                }
                else
                {
                    _logger.Warn("SiemensControl", $"Unknown tag '{tagName}' with value: {value.GetType()}");
                }
            }
            else if (tagName == "Restart")
            {
                if (value is int restartValue)
                {
                    _logger.Debug("SiemensControl", $"Posting event for tag '{tagName}': {restartValue}");
                }
                else
                {
                    _logger.Warn("SiemensControl", $"Unknown tag '{tagName}' with value: {value.GetType()}");
                }
            }
            else if (tagName == "Version")
            {
                _logger.Debug("SiemensControl", $"Processing 'Version' for tag '{tagName}': {value}");
            }
            else
            {
                _logger.Debug("SiemensControl", $"Generic processing for tag '{tagName}': {value}");
            }

            if (State == BaseChannelStateOptions.Started)
            {
                PostNewEvent(controlTag.IdTag, value);
            }
        }

        private void PostNewEvent(Guid idTag, object value)
        {
            if (idTag == Guid.Empty)
            {
                _logger.Error("SiemensControl", "Tag ID cannot be empty.");
                return;
            }
            InvokeOnPostNewEvent(new RawData(value, QualityCodeOptions.Good_Non_Specific, idTag));
        }   

        public override bool RegisterTag(TagModelBase tagObject)
        {
            // Add logic to register the tag  
            return true; // Placeholder implementation  
        }

        public override bool Stop()
        {
            // Add logic to stop the channel  
            return true; // Placeholder implementation  
        }
    }
}