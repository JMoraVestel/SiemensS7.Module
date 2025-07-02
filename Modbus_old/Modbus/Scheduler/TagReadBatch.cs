using ModbusModule.TagConfig;
using ModbusModule.Helper;

namespace ModbusModule.Scheduler
{
    /// <summary>
    /// A TagReadBatch represents a group of tags that could be read in a batch.
    /// Conditions to belong to de same batch are
    /// - Belong to the same DeviceId
    /// - Have consecutive addresses.
    /// - All addresses must correspond to the same read type (coil, discret input, input register, output register, etc)
    /// </summary>
    public class TagReadBatch
    {
        private bool _isInitialized;

        public TagReadBatch()
        {
            _isInitialized = false;
        }

        public string DeviceId { get; private set; }
        public uint FirstAddress { get; private set; }
        public uint LastAddress { get; private set; }

        // All tags are of the same ModbusType in a batch
        public ModbusType ModbusType { get; private set; }
        public List<TagReadBatchItem> ReadItems { get; } = new List<TagReadBatchItem>();

        public bool IsEmpty => ReadItems.Count == 0;

        private void initialize(ModbusTagWrapper firstTag, DateTime readDueTime)
        {
            DeviceId = firstTag.Config.DeviceId;
            ModbusType = firstTag.Config.RegisterAddress.Type;
            FirstAddress = firstTag.Config.RegisterAddress.Offset;
            LastAddress = FirstAddress + firstTag.Config.GetSize() - 1;

            ReadItems.Add(new TagReadBatchItem(firstTag, readDueTime));
            _isInitialized = true;
        }

        public List<TagReadBatchItem> GetBlock(uint startAddress, uint endAddress)
        {
            return ReadItems.Where(p =>
                p.Tag.Config.RegisterAddress.Offset >= startAddress && p.Tag.Config.RegisterAddress.Offset <= endAddress).ToList();
        }

        public void AddTag(ModbusTagWrapper tag, DateTime readDueTime)
        {
            if (!_isInitialized)
            {
                initialize(tag, readDueTime);
                return;
            }

            if (!BelongsToThisBatch(tag, out var reason))
            {
                throw new InvalidOperationException(reason);
            }

            LastAddress = tag.Config.RegisterAddress.Offset + tag.Config.GetSize() - 1; // Move lastaddress pointer
            ReadItems.Add(new TagReadBatchItem(tag, readDueTime));
        }

        public bool BelongsToThisBatch(ModbusTagWrapper tag)
        {
            return BelongsToThisBatch(tag, out _);
        }

        public bool BelongsToThisBatch(ModbusTagWrapper tag, out string reason)
        {
            reason = "";
            if (!_isInitialized)
                return true;

            if (tag.Config.DeviceId != DeviceId)
            {
                reason = "Read batch must contain tags with the same DeviceId";
                return false;
            }

            var tagModbusType = tag.Config.RegisterAddress.Type;
            if (tagModbusType != ModbusType)
            {
                reason =
                    "Every tag in a batch must belong to the same modbus type (coil, input register, output register, etc";
                return false;
            }

            // Get the raw addresses (without type prefix and zero-based)
            uint currentRawAddress = tag.Config.RegisterAddress.Offset;
            uint lastRawAddress = LastAddress;

            // Check if the raw addresses are the same or consecutive
            if (currentRawAddress != lastRawAddress && currentRawAddress != lastRawAddress + 1)
            {
                reason = "Read batch must contain tags with the same or consecutive addresses";
                return false;
            }

            return true;
        }
    }
}
