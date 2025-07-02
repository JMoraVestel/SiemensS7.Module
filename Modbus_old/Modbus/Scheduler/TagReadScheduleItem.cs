using ModbusModule.TagConfig;

namespace ModbusModule.Scheduler
{
    public class TagReadScheduleItem
    {
        public TagReadScheduleItem(ModbusTagWrapper tag)
        {
            Tag = tag;
        }

        public ModbusTagWrapper Tag { get; set; }
        public DateTime NextReadTime { get; set; } = DateTime.UtcNow;
        public bool IsDue() => DateTime.UtcNow >= NextReadTime;

        public void IncrementNextReadTime()
        {
            NextReadTime = DateTime.UtcNow.AddMilliseconds(Tag.Config.PollRate);
        }

        public void ResetNextReadTime()
        {
            NextReadTime = DateTime.UtcNow;
        }
    }
}
