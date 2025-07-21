using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vNode.SiemensS7.Scheduler
{
    public class IntervalInfo
    {
        public int RequestedIntervalMs { get; set; }
        public int ActualIntervalMs { get; set; }
        public int TagCount { get; set; }
    }
}
