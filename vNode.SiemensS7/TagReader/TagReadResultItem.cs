using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.SiemensS7.Scheduler;
using static vNode.SiemensS7.TagReader.TagReadResult;
using vNode.Sdk.Enum; // Para QualityCodeOptions

namespace vNode.SiemensS7.TagReader
{
    public class TagReadResultItem
    {
        public TagReadResultItem(TagReadBatchItem batchItem, TagReadResultType resultCode, object? value = null, QualityCodeOptions quality = QualityCodeOptions.Good_Non_Specific)
        {
            BatchItem = batchItem;
            TagId = batchItem.Tag.Config.TagId;
            Value = value;
            ResultCode = resultCode;
            Quality = quality;
        }

        public TagReadBatchItem BatchItem { get; private set; }
        public TagReadResultType ResultCode { get; private set; }
        public Guid TagId { get; private set; }
        public object? Value { get; private set; }
        public QualityCodeOptions Quality { get; private set; }
    }
}
