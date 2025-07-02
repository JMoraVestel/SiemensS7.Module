using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ModbusModule.ChannelConfig
{    
    public class ModbusDeviceConfig
    {
        [JsonRequired] public required string DeviceId { get; set; }

        [JsonRequired]
        [Range(1, 255, ErrorMessage = "Modbus slave id must be between 1 and 255.")]
        public required byte ModbusSlaveId { get; set; }

        [JsonRequired] public required int ModbusAddressOffset { get; set; }

        [JsonRequired] public required bool EnableModbusFunction6 { get; set; }

        [JsonRequired] public required ModbusBlockSizeConfig BlockSize { get; set; }

        [JsonRequired] public required ModbusSwapConfig SwapConfig { get; set; }
        
        public ModbusPollOnDemandConfig PollOnDemandConfig { get; set; } = new ModbusPollOnDemandConfig();

        [JsonRequired] public required ModbusAutoDemotionConfig AutoDemotionConfig { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
