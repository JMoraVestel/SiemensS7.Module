using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.SiemensS7.Scheduler;
using static vNode.SiemensS7.TagReader.TagReadResult;

namespace vNode.SiemensS7.TagReader
{
    public class TagReadResultItem
    {
        public TagReadResultItem(TagReadBatchItem batchItem, TagReadResultType resultCode, object? value = null)
        {
            BatchItem = batchItem;
            TagId = batchItem.Tag.Config.TagId;
            Value = value;
            ResultCode = resultCode;
        }

        public TagReadBatchItem BatchItem { get; private set; }
        public TagReadResultType ResultCode { get; private set; }
        public Guid TagId { get; private set; }
        public object? Value { get; private set; }
    }
}
