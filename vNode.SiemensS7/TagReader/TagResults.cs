﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.SiemensS7.Scheduler;

namespace vNode.SiemensS7.TagReader
{
    public class TagReadResult
    {
        public enum TagReadResultType
        {
            Success,
            CommsError,
            ParseError,
            OtherError
        }
        private TagReadResult(TagReadResultType result, List<TagReadResultItem> items)
        {
            ResultCode = result;
            Items = items;
        }

        public TagReadResultType ResultCode { get; private set; }
        public List<TagReadResultItem> Items { get; private set; }
        public bool Success => ResultCode == TagReadResultType.Success;

        public static TagReadResult CreateFailed(TagReadResultType result, List<TagReadBatchItem> failedItems)
        {
            return new TagReadResult(result, failedItems.Select(p => new TagReadResultItem(p, result, null)).ToList());
        }

        public static TagReadResult CreateSuccess(List<TagReadResultItem> items)
        {
            return new TagReadResult(TagReadResultType.Success, items);
        }
    }
}
