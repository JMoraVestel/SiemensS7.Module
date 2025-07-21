using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Scheduler
{
    public class TagScheduleInfo
    {
        public SiemensTagWrapper Tag { get; set; }
        public int TickInterval { get; set; }
        public long NextFireTick { get; set; }
        public int PollRateMs { get; set; }
        public int ActualIntervalMs { get; set; }
    }
}
