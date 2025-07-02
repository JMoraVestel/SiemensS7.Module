using System.Net.Sockets;

using ModbusModule.TagConfig;

namespace ModbusModule.Scheduler
{
    public class TagReadBatchItem
    {
        public TagReadBatchItem(ModbusTagWrapper tag, DateTime readDueTime)
        {
            Tag = tag;
            ReadDueTime = readDueTime;
        }

        public ModbusAddress Address => Tag.Config.RegisterAddress;

        public DateTime ReadDueTime { get; set; }
        public DateTime ActualReadTime { get; set; }

        public ModbusTagWrapper Tag { get; private set; }

        public ushort Size => Tag.Config.GetSize();

    }
}
