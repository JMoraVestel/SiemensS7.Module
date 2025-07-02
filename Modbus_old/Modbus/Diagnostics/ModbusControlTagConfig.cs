using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModbusModule.Diagnostics
{    
    public class ModbusControlTagConfig
    {
        [JsonPropertyName("DeviceId")]
        public string? DeviceId { get; init; }
    }
}
