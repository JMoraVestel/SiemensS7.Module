using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModbusModule.ChannelConfig
{
    public class ModbusPollOnDemandConfig
    {
        [JsonRequired] public bool Enabled { get; set; } = false;
        [JsonRequired] public bool TriggerOnWrite { get; set; } = false;
        [JsonRequired] public int Duration { get; set; } = 20000; // 20 seconds
    } 
}
