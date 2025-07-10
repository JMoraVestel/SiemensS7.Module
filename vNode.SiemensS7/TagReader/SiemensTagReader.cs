using System;
using System.Collections.Generic;
using System.Text;
using vNode.Sdk.Enum;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.TagReader
{
    public class SiemensTagReader
    {
        private readonly SiemensTcpStrategy _connection;

        public SiemensTagReader(SiemensTcpStrategy connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public Dictionary<string, object> ReadTags(IEnumerable<SiemensTagWrapper> tags)
        {
            var results = new Dictionary<string, object>();

            foreach (var tag in tags)
            {
                var tagName = tag.Name;

                try
                {
                    var raw = _connection.Read(tag.Config.Address);
                    var value = ConvertValue(raw, tag.Config.DataType.ToString());
                    results[tagName. = value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] {tagName} ({tag.Config.Address}): {ex.Message}");
                    results[tagName] = null;
                }
            }

            return results;
        }

        private object ConvertValue(object raw, string dataType)
        {
            if (raw == null) return null;

            switch (dataType)
            {
                case "BOOL":
                    return (bool)raw;

                case "BYTE":
                    return (byte)raw;

                case "WORD":
                    return (ushort)raw;

                case "DWORD":
                    return (uint)raw;

                case "INT":
                    return (short)raw;

                case "DINT":
                    return (int)raw;

                case "REAL":
                    {
                        byte[] bytes = BitConverter.GetBytes((uint)raw);
                        return BitConverter.ToSingle(bytes, 0);
                    }

                case "STRING":
                    {
                        // STRING S7: [MaxLength][CurrentLength][Chars...]
                        byte[] bytes = (byte[])raw;
                        if (bytes.Length < 2) return "";
                        int len = bytes[1]; // Current length
                        return Encoding.ASCII.GetString(bytes, 2, len);
                    }

                case "DATE":
                    {
                        // IEC DATE: days since 1990-01-01, stored as ushort
                        ushort days = (ushort)raw;
                        return new DateTime(1990, 1, 1).AddDays(days).ToString("yyyy-MM-dd");
                    }

                case "TIME":
                    {
                        // IEC TIME: duration in milliseconds, stored as uint
                        uint ms = (uint)raw;
                        TimeSpan ts = TimeSpan.FromMilliseconds(ms);
                        return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s {ts.Milliseconds}ms";
                    }

                case "DATE_AND_TIME":
                    {
                        // 8 bytes in BCD format: YY MM DD HH mm SS msec1 msec2
                        byte[] b = (byte[])raw;
                        if (b.Length != 8) return null;

                        int year = BcdToInt(b[0]) + 2000;
                        int month = BcdToInt(b[1]);
                        int day = BcdToInt(b[2]);
                        int hour = BcdToInt(b[3]);
                        int minute = BcdToInt(b[4]);
                        int second = BcdToInt(b[5]);
                        int millisecond = (BcdToInt(b[6]) * 10) + (BcdToInt(b[7]) / 10);

                        return new DateTime(year, month, day, hour, minute, second, millisecond)
                            .ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }

                case "S5TIME":
                    {
                        // WORD: 12 bits BCD + 2 bits timebase
                        ushort rawValue = (ushort)raw;
                        int baseCode = (rawValue >> 12) & 0x03;
                        int timebase = baseCode switch
                        {
                            0b00 => 10,      // 10 ms
                            0b01 => 100,     // 100 ms
                            0b10 => 1000,    // 1 sec
                            0b11 => 10000,   // 10 sec
                            _ => 1
                        };

                        int bcd = rawValue & 0x0FFF;
                        int value = BcdToInt(bcd);
                        return (value * timebase) + " ms";
                    }

                default:
                    throw new NotSupportedException($"Tipo de dato '{dataType}' no soportado.");
            }
        }

        private int BcdToInt(int bcd)
        {
            int result = 0;
            int multiplier = 1;

            while (bcd > 0)
            {
                int digit = bcd & 0xF;
                result += digit * multiplier;
                multiplier *= 10;
                bcd >>= 4;
            }

            return result;
        }

        //TODO: Continue here following anda adapting from the Modbus module
        public async Task<bool> WriteTagAsync(SiemensTagWrapper tag, object value)
        {

            if (tag..ClientAccess == ClientAccessOptions.ReadOnly)
            {
                throw new ArgumentException("Tag is read-only");
            }

            if (tag.Config.IsReadOnly)
            {
                throw new ArgumentException($"Modbus address {tag.Config.RegisterAddress} is read-only");
            }

            var deviceConfig = _channelConfig.Devices[tag.Config.DeviceId];

            if (deviceConfig == null) //device to read is not configured in this channel
            {
                _logger.Error("Modbus", $"Device ID {tag.Config.DeviceId} is not configured in this channel.");
                throw new ArgumentOutOfRangeException("Invalid Device Id {tag.Config.DeviceId}");

            }

            var modbusType = tag.Config.RegisterAddress.Type;
            ushort[] registers = Array.Empty<ushort>();
            bool coil = false;
            bool writeToRegister = modbusType == ModbusType.InputRegister || modbusType == ModbusType.HoldingRegister;

            if (writeToRegister)
            {
                if (!_parser.TryGetRegisters(tag, value, deviceConfig.SwapConfig, out registers))
                {
                    throw new InvalidDataException("Unable to parse value");
                }
            }
            else // is a coil
            {
                if (!ModbusDataConverter.TryParseBool(value, out coil))
                {
                    throw new InvalidDataException("Unable to parse value into a boolean");
                }
            }


            _writePending = true;
            await _modbusLock.WaitAsync();
            try
            {
                //var rawAddress = GetRawAddress(tag.Config.RegisterAddress);
                if (writeToRegister)
                {
                    await _modbusConnection.WriteHoldingRegistersAsync(deviceConfig.ModbusSlaveId,
                        deviceConfig.EnableModbusFunction6, (tag.Config.RegisterAddress.Offset), registers);
                }
                else
                {
                    await _modbusConnection.WriteOutputCoilsAsync(deviceConfig.ModbusSlaveId,
                        (tag.Config.RegisterAddress.Offset), new bool[] { coil });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusTagReader", $"Error writing to Modbus device: {ex.Message}");

            }

            finally
            {
                _modbusLock.Release();
                _writePending = false;
            }

            return true;
        }
    }
}
