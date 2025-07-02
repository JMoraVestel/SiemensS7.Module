using ModbusModule.Helper;
using ModbusModule.TagConfig;

namespace ModbusModule.ChannelConfig
{
    public class ModbusBlockSizeConfig
    {
        public ushort OutputCoils { get; set; }
        public ushort InputCoils { get; set; }
        public ushort InputRegisters { get; set; }
        public ushort HoldingRegisters { get; set; }

        public ushort GetForModbusType(ModbusType modbusType)
        {
            return modbusType switch
            {
                ModbusType.OutputCoil => OutputCoils,
                ModbusType.InputCoil => InputCoils,
                ModbusType.InputRegister => InputRegisters,
                ModbusType.HoldingRegister => HoldingRegisters,
                _ => throw new ArgumentOutOfRangeException("modbusType",
                    "Unknown modbus type: " + modbusType.ToString())
            };
        }
    }
}
