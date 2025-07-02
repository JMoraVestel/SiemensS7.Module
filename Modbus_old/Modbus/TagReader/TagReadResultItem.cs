using ModbusModule.Scheduler;

using static ModbusModule.TagReader.TagReadResult;

namespace ModbusModule.TagReader
{
    public class TagReadResultItem
    {
        public TagReadResultItem(TagReadBatchItem batchItem, TagReadResultType resultCode, object? value = null)
        {
            BatchItem = batchItem;
            TagId = batchItem.Tag.Tag.IdTag;
            Value = value;
            ResultCode = resultCode;
        }

        public TagReadBatchItem BatchItem { get; private set; }
        public TagReadResultType ResultCode { get; private set; }
        public Guid TagId { get; private set; }
        public object? Value { get; private set; }
    }
}
