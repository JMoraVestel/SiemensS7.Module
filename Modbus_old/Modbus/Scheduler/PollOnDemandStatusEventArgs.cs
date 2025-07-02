using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusModule.Scheduler
{
    public class PollOnDemandStatusEventArgs : EventArgs
    {
        public required string DeviceId { get; set; }
        public required bool IsActive { get; set; }
    }
}
