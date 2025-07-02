using ModbusModule.ChannelConfig;
using ModbusModule.TagConfig;
using ModbusModule.Helper;

using vNode.Sdk.Logger;

namespace ModbusModule.Scheduler;

public class ModbusScheduler
{
    private readonly object _lock = new(); // Ensure thread-safe queue updates
    private readonly ISdkLogger _logger;
    private readonly TagReadScheduleItems _scheduleItems;
    private bool _running = false;
    public EventHandler<ReadingDueEventArgs>? ReadingDue;
    public EventHandler<PollOnDemandStatusEventArgs>? OnPollOnDemandStatusChanged;


    // Poll-on-demand
    private string _priorityDeviceId = null;
    private DateTime _priorityExpiration = DateTime.MinValue;

    public ModbusScheduler(List<ModbusDeviceConfig> devices, ISdkLogger logger)
    {
        _logger = logger;
        _scheduleItems = new TagReadScheduleItems(devices.Select(p => p.DeviceId).ToArray());
    }

    public bool Running => _running;
    public void StopReading()
    {
        _running = false;
    }

    public void SetDevicePriority(string deviceId, int durationMs)
    {
        if (string.IsNullOrEmpty(deviceId) || durationMs <= 0)
            return;

        lock (_lock)
        {
            _priorityDeviceId = deviceId;
            _priorityExpiration = DateTime.UtcNow.AddMilliseconds(durationMs);
            _logger.Debug("ModbusScheduler", $"Set poll-on-demand priority for device {deviceId} for {durationMs}ms until {_priorityExpiration}");

            // Trigger the event to notify that poll-on-demand status has changed
            OnPollOnDemandStatusChanged?.Invoke(this, new PollOnDemandStatusEventArgs
            {
                DeviceId = deviceId,
                IsActive = true
            });

            Monitor.Pulse(_lock); // Wake up the scheduler if it's waiting
        }
    }

    public void AddTag(ModbusTagWrapper tag)
    {
        lock (_lock)
        {
            _scheduleItems.AddOrUpdateTag(tag);
            Monitor.Pulse(_lock); // Wake up the scheduler if it's waiting
        }
    }

    public void RemoveTag(Guid tagId)
    {
        _scheduleItems.RemoveTag(tagId);
    }

    private List<TagReadBatch> CreateDueItemsReadBatches()
    {
        List<TagReadBatch> retVal = new();
        TagReadBatch batch;

        bool hasPriorityDevice = false;
        string priorityDeviceId = null;

        // Check if there is a priority device set
        lock (_lock)
        {
            if (_priorityDeviceId != null && DateTime.UtcNow < _priorityExpiration)
            {
                // Priority is still valid
                hasPriorityDevice = true;
                priorityDeviceId = _priorityDeviceId;                
            }
            else if (_priorityDeviceId != null)
            {
                // Priority expired, reset it
                _logger.Debug("ModbusScheduler", $"Poll-on-demand priority for device {_priorityDeviceId} expired");

                // Update the PollOnDemandActive control tag to false when priority expires
                OnPollOnDemandStatusChanged?.Invoke(this, new PollOnDemandStatusEventArgs
                {
                    DeviceId = _priorityDeviceId,
                    IsActive = false
                });

                _priorityDeviceId = null;
            }
        }

        // If we have a priority device, only process batches for that device
        if (hasPriorityDevice && _scheduleItems.Items.ContainsKey(priorityDeviceId))
        {
            var deviceItems = _scheduleItems.Items[priorityDeviceId];
            foreach (Dictionary<Guid, TagReadScheduleItem> byRegisterTypeItems in deviceItems.Values)
            {
                batch = new TagReadBatch();
                foreach (TagReadScheduleItem tagItem in byRegisterTypeItems.Values.OrderBy(p => p.Tag.Config.RegisterAddress))
                {
                    if (tagItem.IsDue())
                    {
                        if (!batch.BelongsToThisBatch(tagItem.Tag))
                        {
                            if (!batch.IsEmpty)
                                retVal.Add(batch);
                            batch = new TagReadBatch();
                        }
                        batch.AddTag(tagItem.Tag, tagItem.NextReadTime);
                    }
                }

                if (!batch.IsEmpty)
                {
                    retVal.Add(batch);
                }
            }

            return retVal;            
        }
        //-----------------------------------
        // No priority device, expired priority, or no due items for priority device - process all devices

        //TODO: Investigar a partir de aquí
        foreach (Dictionary<ModbusType, Dictionary<Guid, TagReadScheduleItem>> byDeviceItems in
                 _scheduleItems.Items.Values)
        {
            foreach (Dictionary<Guid, TagReadScheduleItem> byRegisterTypeItems in byDeviceItems.Values)
            {
                batch = new TagReadBatch();
                foreach (TagReadScheduleItem tagItem in byRegisterTypeItems.Values.OrderBy(p => p.Tag.Config.RegisterAddress))
                {
                    if (tagItem.IsDue())
                    {
                        if (!batch.BelongsToThisBatch(tagItem.Tag))
                        {
                            retVal.Add(batch);
                            batch = new TagReadBatch();
                        }
                        batch.AddTag(tagItem.Tag, tagItem.NextReadTime);
                    }


                }

                if (!batch.IsEmpty)
                {
                    retVal.Add(batch);
                }
            }
        }
        return retVal;
    }

    public async Task StartReadingAsync(CancellationToken cancellationToken)
    {
        // It's important to set the due times for the tags when the scheduler starts
        // Otherwise the default due time is when the tag is registered, and that could be several seconds before.
        // That messes un the read overdue average.
        _scheduleItems.ResetNextReadTimes();

        _running = true;
        // Create a lightweight timer that will check for due items
        //IMPORTANTE: Previene CPU overload
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));

        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // to prevent CPU overload, but it creates an offset of 10ms in reading due times.
                // Meaning that each read will show to be at least ~10ms overdue in the log.
                await periodicTimer.WaitForNextTickAsync(cancellationToken);

                List<TagReadBatch> readBatches;
                lock (_lock)
                {
                    readBatches = CreateDueItemsReadBatches();
                    if (readBatches.Count == 0)
                    {
                        continue;
                    }
                }

                // Iterate batches, trigger batch processing, mark processed tags as read.
                foreach (TagReadBatch? batchItem in readBatches)
                {
                    // Next read time will be calculated over the time the reading started, not when ended.
                    _scheduleItems.IncrementNextReadTime(batchItem.ReadItems.Select(p => p.Tag).ToList());
                    OnReadingDue(batchItem);
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "ModbusScheduler", "Unhandled error in scheduler loop");
            }
        }
    }


    private void OnReadingDue(TagReadBatch readBatch)
    {
        // Trigger the event when a read is due
        ReadingDue?.Invoke(this, new ReadingDueEventArgs(readBatch));
    }
}
