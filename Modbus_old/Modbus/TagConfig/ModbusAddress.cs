using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusModule.TagConfig
{
    public class ModbusAddress : IComparable<ModbusAddress>, IComparable
    {
        public uint Offset { get; set; }
        public ModbusType Type { get; set; }
        public int CompareTo(ModbusAddress other)
        {
            if (other == null)
                return 1;

            // First compare by ModbusType
            int typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0)
                return typeComparison;

            // If types are equal, compare by Offset
            return Offset.CompareTo(other.Offset);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            if (obj is ModbusAddress other)
                return CompareTo(other);

            throw new ArgumentException("Object is not a ModbusAddress");
        }

        // Override Equals and GetHashCode for proper comparison
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            ModbusAddress other = (ModbusAddress) obj;
            return Type == other.Type && Offset == other.Offset;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Offset);
        }

    }

    public enum ModbusType
    {
        Unknown,
        OutputCoil,
        InputCoil,
        InputRegister,
        HoldingRegister
    }

    public class ModbusAddressConverter : JsonConverter<ModbusAddress>
    {
        public override ModbusAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Only accept string values
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Modbus address must be a string value");
            }

            // Read the address as string
            string addressStr = reader.GetString();

            if (string.IsNullOrEmpty(addressStr))
            {
                throw new JsonException("Modbus address cannot be null or empty");
            }

            // Parse the address to numeric value
            if (!uint.TryParse(addressStr, out uint numericAddress))
            {
                throw new JsonException($"Invalid Modbus address format: {addressStr}");
            }

            var result = new ModbusAddress();

            // Determine the type based on the first digit and calculate offset
            if ((addressStr.StartsWith("0") && numericAddress <= 65536) ||
                (numericAddress >= 0 && numericAddress <= 9999))
            {
                // Output Coils: "00000" to "09999" or "000000" to "065536"
                result.Type = ModbusType.OutputCoil;
                result.Offset = numericAddress;
            }
            else if (addressStr.StartsWith("1"))
            {
                // Input Coils: Any address starting with 1
                result.Type = ModbusType.InputCoil;

                // Calculate offset based on length
                if (addressStr.Length >= 6)
                {
                    // For 6-digit and beyond (e.g., 100000, 165536)
                    result.Offset = numericAddress - 100000;
                }
                else
                {
                    // For 5-digit (e.g., 10000, 19999)
                    result.Offset = numericAddress - 10000;
                }
            }
            else if (addressStr.StartsWith("3"))
            {
                // Input Registers: Any address starting with 3
                result.Type = ModbusType.InputRegister;

                // Calculate offset based on length
                if (addressStr.Length >= 6)
                {
                    // For 6-digit and beyond (e.g., 300000, 365535)
                    result.Offset = numericAddress - 300000;
                }
                else
                {
                    // For 5-digit (e.g., 30000, 39999)
                    result.Offset = numericAddress - 30000;
                }
            }
            else if (addressStr.StartsWith("4"))
            {
                // Holding Registers: Any address starting with 4
                result.Type = ModbusType.HoldingRegister;

                // Calculate offset based on length
                if (addressStr.Length >= 6)
                {
                    // For 6-digit and beyond (e.g., 400000, 465536)
                    result.Offset = numericAddress - 400000;
                }
                else
                {
                    // For 5-digit (e.g., 40000, 49999)
                    result.Offset = numericAddress - 40000;
                }
            }
            else
            {
                // Invalid address format
                throw new JsonException($"Invalid Modbus address format: {addressStr}. " +
                    "Valid formats are:\n" +
                    "- Output Coils: \"00000\" to \"09999\" or \"000000\" to \"065536\", or simply 0-9999\n" +
                    "- Digital Inputs: Addresses starting with \"1\" (e.g., \"10000\", \"165536\")\n" +
                    "- Analog Inputs: Addresses starting with \"3\" (e.g., \"30000\", \"365536\")\n" +
                    "- Holding Registers: Addresses starting with \"4\" (e.g., \"40000\", \"465536\")");
            }

            // Validate the offset doesn't exceed the maximum
            if (result.Offset > 65536)
            {
                throw new JsonException($"Invalid Modbus address offset: {result.Offset}. " +
                    "Offset must be between 0 and 65536");
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, ModbusAddress value, JsonSerializerOptions options)
        {
            // Format depends on the Modbus type - always write as string
            string formattedAddress;

            switch (value.Type)
            {
                case ModbusType.OutputCoil:
                    // Use 6-digit format for Output Coils (e.g., "000123")
                    formattedAddress = value.Offset.ToString().PadLeft(6, '0');
                    break;
                case ModbusType.InputCoil:
                    // Use 6-digit format for Input Coils (e.g., "100123")
                    formattedAddress = "10" + value.Offset.ToString().PadLeft(4, '0');
                    break;
                case ModbusType.InputRegister:
                    // Use 6-digit format for Input Registers (e.g., "300123")
                    formattedAddress = "30" + value.Offset.ToString().PadLeft(4, '0');
                    break;
                case ModbusType.HoldingRegister:
                    // Use 6-digit format for Holding Registers (e.g., "400123")
                    formattedAddress = "40" + value.Offset.ToString().PadLeft(4, '0');
                    break;
                default:
                    throw new JsonException($"Cannot serialize unknown ModbusType");
            }

            writer.WriteStringValue(formattedAddress);
        }
    }
}
