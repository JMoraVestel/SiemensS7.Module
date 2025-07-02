using ModbusModule.TagConfig;

using static ModbusModule.Helper.ModbusHelper;

namespace ModbusModule.Scheduler
{
    /// <summary>
    /// This class represents a matrix-like structure, where the rows are the different Modbus slave Ids
    /// and the columns are the different register addresses
    /// Each position of the matrix contains a <see cref="TagReadScheduleItem"/>
    /// </summary>
    public class TagReadScheduleItems
    {
        private readonly object _lock = new();
        private Dictionary<string, Dictionary<ModbusType, Dictionary<Guid, TagReadScheduleItem>>> _items;
        private Dictionary<Guid, ModbusTagWrapper> _tagIndex = new();

        public TagReadScheduleItems(string[] modbusDeviceIds)
        {
            initializeStructure(modbusDeviceIds);
        }

        public void ResetNextReadTimes()
        {
            foreach (var deviceItem in _items)
            {
                foreach (var typeItem in deviceItem.Value.Values)
                {
                    foreach (var tagItem in typeItem.Values)
                    {
                        tagItem.ResetNextReadTime();
                    }
                }
            }
        }

        public Dictionary<string, Dictionary<ModbusType, Dictionary<Guid, TagReadScheduleItem>>> Items => _items;

        private void initializeStructure(string[] modbusDeviceIds)
        {
            _items = new();

            // Initialize structure
            foreach (string item in modbusDeviceIds)
            {
                var modbusTypeDic = new Dictionary<ModbusType, Dictionary<Guid, TagReadScheduleItem>>
                {
                    [ModbusType.InputCoil] = new Dictionary<Guid, TagReadScheduleItem>(),
                    [ModbusType.OutputCoil] = new Dictionary<Guid, TagReadScheduleItem>(),
                    [ModbusType.InputRegister] = new Dictionary<Guid, TagReadScheduleItem>(),
                    [ModbusType.HoldingRegister] = new Dictionary<Guid, TagReadScheduleItem>()
                };
                _items.TryAdd(item, modbusTypeDic);
            }
        }

        public void AddOrUpdateTag(ModbusTagWrapper tag)
        {
            lock (_lock)
            {
                if (_items[tag.Config.DeviceId][tag.Config.RegisterAddress.Type].ContainsKey(tag.Tag.IdTag))
                {
                    _items[tag.Config.DeviceId][tag.Config.RegisterAddress.Type].Remove(tag.Tag.IdTag);
                }

                _items[tag.Config.DeviceId][tag.Config.RegisterAddress.Type]
                    .Add(tag.Tag.IdTag, new TagReadScheduleItem(tag));

                if (_tagIndex.ContainsKey(tag.Tag.IdTag))
                {
                    _tagIndex.Remove(tag.Tag.IdTag);
                }

                _tagIndex.Add(tag.Tag.IdTag, tag);
            }
        }

        public void RemoveTag(Guid tagId)
        {
            if (_tagIndex.TryGetValue(tagId, out var tagToRemove))
            {
                RemoveTag(tagToRemove);
            }
        }

        public void RemoveTag(ModbusTagWrapper tag)
        {
            lock (_lock)
            {
                if (_items[tag.Config.DeviceId][tag.Config.RegisterAddress.Type].ContainsKey(tag.Tag.IdTag))
                {
                    _items[tag.Config.DeviceId][tag.Config.RegisterAddress.Type].Remove(tag.Tag.IdTag);
                }

                if (_tagIndex.ContainsKey(tag.Tag.IdTag))
                {
                    _tagIndex.Remove(tag.Tag.IdTag);
                }
            }
        }

        public void IncrementNextReadTime(List<ModbusTagWrapper> tags)
        {
            foreach (var tag in tags)
            {
                if (_items.TryGetValue(tag.Config.DeviceId, out var deviceTags))
                {
                    if (deviceTags.TryGetValue(tag.Config.RegisterAddress.Type, out var modbusTypeTags))
                    {
                        if (modbusTypeTags.TryGetValue(tag.Tag.IdTag, out var scheduleItem))
                        {
                            scheduleItem.IncrementNextReadTime();
                        }
                    }
                }
            }
        }
    }
}
