using System.Text.Json.Serialization;
using System.Text.Json;

namespace ModbusModule.TagConfig;

public class ModbusAddressConverter : JsonConverter<UInt32>
{
    public override UInt32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        UInt32 address = reader.TokenType == JsonTokenType.String
            ? UInt32.Parse(reader.GetString())
            : reader.GetUInt32();

        // Convert 5-digit addresses to 6-digit format
        if (address >= 10000 && address < 100000)
        {
            // Determine the first digit to know the type
            uint firstDigit = address / 10000;
            uint remainingDigits = address % 10000;

            switch (firstDigit)
            {
                case 1: // InputCoil: Convert 1xxxx to 10xxxx
                    return 100000 + remainingDigits;
                case 3: // InputRegister: Convert 3xxxx to 30xxxx
                    return 300000 + remainingDigits;
                case 4: // HoldingRegister: Convert 4xxxx to 40xxxx
                    return 400000 + remainingDigits;
                default:
                    return address; // Keep original if it's an unknown format
            }
        }
        // For addresses < 10000 (0-9999 range), keep as is (they're already in the correct format)
        // For addresses >= 100000, keep as is (they're already in 6-digit format)
        return address;
    }

    public override void Write(Utf8JsonWriter writer, UInt32 value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
