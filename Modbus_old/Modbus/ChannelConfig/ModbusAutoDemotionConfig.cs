using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig
{    
    public class ModbusAutoDemotionConfig
    {
        [JsonRequired] public bool Enabled { get; set; }

        [JsonRequired] public int Failures { get; set; } // Min = 1

        [JsonRequired] public int Delay { get; set; } // Min = 1
    }
}
