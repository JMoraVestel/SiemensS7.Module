// Class to handle data conversion with swapping
using System;

using ModbusModule.ChannelConfig;
using ModbusModule.TagConfig;

using vNode.Sdk.Enum;
using vNode.Sdk.Logger;

public class ModbusDataConverter
{
    private readonly ModbusSwapConfig _swapConfig;
    private readonly ISdkLogger _logger;

    public static bool TryParseBool(object value, out bool result)
    {
        result = false;

        // Handle null
        if (value == null)
        {
            return false;
        }

        // If it's already a boolean, return it
        if (value is bool boolValue)
        {
            result = boolValue;
            return true;
        }

        // If it's a numeric type, 0 is false, anything else is true
        try
        {
            double numValue = Convert.ToDouble(value);
            result = numValue != 0;
            return true;
        }
        catch
        {
            // Not a numeric type, continue
        }

        // If it's a string, try standard boolean parsing
        if (value is string stringValue)
        {
            // Try standard boolean parsing first (true/false)
            if (bool.TryParse(stringValue, out result))
            {
                return true;
            }

            // Try numeric parsing (0/non-zero)
            if (double.TryParse(stringValue, out double numericValue))
            {
                result = numericValue != 0;
                return true;
            }
        }

        // Couldn't parse
        return false;
    }

    // Convenience method
    public static bool ParseBool(object value, bool defaultValue = false)
    {
        return TryParseBool(value, out bool result) ? result : defaultValue;
    }

    public ModbusDataConverter(ModbusSwapConfig config, ISdkLogger logger)
    {
        _swapConfig = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    // Try to convert OPCUA data to Modbus registers with two-step conversion
    public bool TryConvertToRegisters(object opcuaValue, TagDataTypeOptions opcuaType,
                                 ModbusTagDataTypeType modbusType, out ushort[] registers, bool isArray)
    {
        registers = null;

        // Handle array conversion
        if (isArray && opcuaValue is Array arrayValues)
        {
            int arraySize = arrayValues.Length;
            return TryConvertArrayToRegisters(arrayValues as object[], opcuaType, modbusType, arraySize, out registers, isArray);
        }

        // Step 1: Try to convert from OPCUA data type to Modbus data type
        object modbusValue;
        if (!TryConvertOpcuaToModbus(opcuaValue, opcuaType, modbusType, out modbusValue))
            return false;

        // Step 2: Try to convert from Modbus data type to registers
        return TryConvertModbusToRegisters(modbusValue, modbusType, out registers);
    }

    // Try to convert OPCUA data type to Modbus data type
    private bool TryConvertOpcuaToModbus(object value, TagDataTypeOptions sourceType,
                                        ModbusTagDataTypeType targetType, out object result)
    {
        result = null;

        if (value == null)
            return false;

        try
        {
            // Handle type conversion based on target Modbus type
            switch (targetType)
            {
                case ModbusTagDataTypeType.Boolean:
                    // String conversion to boolean
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (bool.TryParse(strVal, out bool boolResult))
                        {
                            result = boolResult;
                            return true;
                        }

                        // Try numeric conversion
                        if (double.TryParse(strVal, out double numResult))
                        {
                            result = numResult != 0;
                            return true;
                        }
                        return false;
                    }

                    // Other types to boolean
                    try
                    {
                        result = Convert.ToBoolean(value);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Int16:
                    // String conversion to Int16
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (short.TryParse(strVal, out short shortResult))
                        {
                            result = shortResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= short.MinValue && numResult <= short.MaxValue)
                        {
                            result = (short) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to Int16
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if (unixTime >= short.MinValue && unixTime <= short.MaxValue)
                            {
                                result = (short) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to Int16
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= short.MinValue && dblVal <= short.MaxValue)
                        {
                            result = (short) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Uint16:
                    // String conversion to UInt16
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (ushort.TryParse(strVal, out ushort ushortResult))
                        {
                            result = ushortResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= 0 && numResult <= ushort.MaxValue)
                        {
                            result = (ushort) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to UInt16
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if (unixTime >= 0 && unixTime <= ushort.MaxValue)
                            {
                                result = (ushort) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to UInt16
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= 0 && dblVal <= ushort.MaxValue)
                        {
                            result = (ushort) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Int32:
                    // String conversion to Int32
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (int.TryParse(strVal, out int intResult))
                        {
                            result = intResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= int.MinValue && numResult <= int.MaxValue)
                        {
                            result = (int) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to Int32
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if (unixTime >= int.MinValue && unixTime <= int.MaxValue)
                            {
                                result = (int) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to Int32
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= int.MinValue && dblVal <= int.MaxValue)
                        {
                            result = (int) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Int64:
                    // String conversion to Int64
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (int.TryParse(strVal, out int intResult))
                        {
                            result = intResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= Int64.MinValue && numResult <= Int64.MaxValue)
                        {
                            result = (Int64) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to Int64
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if (unixTime >= Int64.MinValue && unixTime <= Int64.MaxValue)
                            {
                                result = (Int64) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to Int64
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= Int64.MinValue && dblVal <= Int64.MaxValue)
                        {
                            result = (Int64) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Uint64:
                    // String conversion to Uint64
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (int.TryParse(strVal, out int intResult))
                        {
                            result = intResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= UInt64.MinValue && numResult <= UInt64.MaxValue)
                        {
                            result = (UInt64) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to UInt64
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if ((UInt64) unixTime >= UInt64.MinValue && (UInt64)unixTime <= UInt64.MaxValue)
                            {
                                result = (UInt64) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to UInt64
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= UInt64.MinValue && dblVal <= UInt64.MaxValue)
                        {
                            result = (UInt64) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Uint32:
                    // String conversion to UInt32
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (uint.TryParse(strVal, out uint uintResult))
                        {
                            result = uintResult;
                            return true;
                        }

                        // Try floating-point conversion with truncation
                        if (double.TryParse(strVal, out double numResult) &&
                            numResult >= 0 && numResult <= uint.MaxValue)
                        {
                            result = (uint) numResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to UInt32
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            long unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            // Check if within range
                            if (unixTime >= 0 && unixTime <= uint.MaxValue)
                            {
                                result = (uint) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to UInt32
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= 0 && dblVal <= uint.MaxValue)
                        {
                            result = (uint) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Float32:
                    // String conversion to Float
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (float.TryParse(strVal, out float floatResult))
                        {
                            result = floatResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to Float
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            double unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

                            if (unixTime >= float.MinValue && unixTime <= float.MaxValue)
                            {
                                result = (float) unixTime;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to Float
                    try
                    {
                        double dblVal = Convert.ToDouble(value);
                        if (dblVal >= float.MinValue && dblVal <= float.MaxValue)
                        {
                            result = (float) dblVal;
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.Double64:
                    // String conversion to Double
                    if (sourceType == TagDataTypeOptions.String)
                    {
                        string strVal = value.ToString();
                        if (double.TryParse(strVal, out double doubleResult))
                        {
                            result = doubleResult;
                            return true;
                        }
                        return false;
                    }

                    // DateTime conversion to Double
                    if (sourceType == TagDataTypeOptions.DateTime)
                    {
                        try
                        {
                            DateTime dt = Convert.ToDateTime(value);
                            double unixTime = new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();
                            result = unixTime;
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Other types to Double
                    try
                    {
                        result = Convert.ToDouble(value);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                case ModbusTagDataTypeType.String:
                    // All OPCUA types can be converted to string
                    result = value.ToString();
                    return true;

                default:
                    _logger.Error("ModbusDataConverter", "Cannot convert OPCUA to MODBUS because data type is not supported: " + targetType.ToString());
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    // Try to convert Modbus data type to registers
    private bool TryConvertModbusToRegisters(object value, ModbusTagDataTypeType dataType, out ushort[] registers)
    {
        registers = null;

        try
        {
            switch (dataType)
            {
                case ModbusTagDataTypeType.Boolean:
                    if (value is bool boolVal)
                    {
                        registers = ConvertBooleanToRegisters(boolVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Int16:
                    if (value is short shortVal)
                    {
                        registers = ConvertInt16ToRegisters(shortVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Uint16:
                    if (value is ushort ushortVal)
                    {
                        registers = ConvertUInt16ToRegisters(ushortVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Int32:
                    if (value is int intVal)
                    {
                        registers = ConvertInt32ToRegisters(intVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Uint32:
                    if (value is uint uintVal)
                    {
                        registers = ConvertUInt32ToRegisters(uintVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Int64:
                    if (value is Int64 int64Val)
                    {
                        registers = ConvertInt64ToRegisters(int64Val);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Uint64:
                    if (value is UInt64 uInt64Val)
                    {
                        registers = ConvertUint64ToRegisters(uInt64Val);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Float32:
                    if (value is float floatVal)
                    {
                        registers = ConvertFloatToRegisters(floatVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.Double64:
                    if (value is double doubleVal)
                    {
                        registers = ConvertDoubleToRegisters(doubleVal);
                        return true;
                    }
                    return false;

                case ModbusTagDataTypeType.String:
                    if (value is string stringVal)
                    {
                        registers = ConvertStringToRegisters(stringVal);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }
        catch
        {
            registers = null;
            return false;
        }
    }

    // Parse registers back to the specified type
    public object FromRegisters(ushort[] registers, ModbusTagConfig tagConfig, bool isArray)
    {
        if (tagConfig.DataType == ModbusTagDataTypeType.Boolean && !tagConfig.BitNumber.HasValue)
        {
            throw new InvalidDataException("BitNumber cannot be null if DataType is Boolean");
        }

        // Handle array type
        if (isArray)
        {
            return ParseArray(registers, tagConfig);
        }

        // Original single-value parsing logic
        return tagConfig.DataType switch
        {
            ModbusTagDataTypeType.Boolean => ParseBoolean(registers, tagConfig.BitNumber!.Value),
            ModbusTagDataTypeType.Int16 => ParseInt16(registers),
            ModbusTagDataTypeType.Uint16 => ParseUInt16(registers),
            ModbusTagDataTypeType.Int32 => ParseInt32(registers),
            ModbusTagDataTypeType.Uint32 => ParseUInt32(registers),
            ModbusTagDataTypeType.Float32 => ParseFloat(registers),
            ModbusTagDataTypeType.Double64 => ParseDouble(registers),
            ModbusTagDataTypeType.Uint64 => ParseUInt64(registers),
            ModbusTagDataTypeType.Int64 => ParseInt64(registers),
            ModbusTagDataTypeType.String => ParseString(registers),
            _ => throw new NotSupportedException($"Unsupported data type: {tagConfig.DataType}")
        };
    }

    public bool TryConvertArrayToRegisters(object[] arrayValues, TagDataTypeOptions opcuaType,
                                      ModbusTagDataTypeType modbusType, int arraySize, out ushort[] registers, bool isArray)
    {
        registers = null;

        try
        {
            // Calculate the total number of registers needed based on element type
            int registersPerElement = GetRegistersPerElement(modbusType);
            int totalRegisters = registersPerElement * arraySize;

            // Initialize the result array
            registers = new ushort[totalRegisters];

            // Process each array element
            for (int i = 0; i < arrayValues.Length && i < arraySize; i++)
            {
                // Convert this element to Modbus registers
                if (!TryConvertToRegisters(arrayValues[i], opcuaType, modbusType, out ushort[] elementRegisters, isArray))
                {
                    return false;
                }

                // Copy to the right position in the output array
                Array.Copy(elementRegisters, 0, registers, i * registersPerElement, elementRegisters.Length);
            }

            // Ensure remaining registers are zeroed if array is smaller than declared size
            if (arrayValues.Length < arraySize)
            {
                for (int i = arrayValues.Length * registersPerElement; i < totalRegisters; i++)
                {
                    registers[i] = 0;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("ModbusDataConverter", $"Error converting array to registers: {ex.Message}");
            registers = null;
            return false;
        }
    }

    // Helper method to get registers per element based on data type
    private int GetRegistersPerElement(ModbusTagDataTypeType dataType)
    {
        return dataType switch
        {
            ModbusTagDataTypeType.Boolean => 1,
            ModbusTagDataTypeType.Int16 => 1,
            ModbusTagDataTypeType.Uint16 => 1,
            ModbusTagDataTypeType.Int32 => 2,
            ModbusTagDataTypeType.Uint32 => 2,
            ModbusTagDataTypeType.Int64 => 4,
            ModbusTagDataTypeType.Uint64 => 4,
            ModbusTagDataTypeType.Float32 => 2,
            ModbusTagDataTypeType.Double64 => 4,
            ModbusTagDataTypeType.String => 1, // This would vary based on string length
            _ => throw new NotSupportedException($"Unsupported data type for array: {dataType}")
        };
    }

    // Method to parse array values from registers
    public object[] ParseArray(ushort[] registers, ModbusTagConfig tagConfig)
    {
        // Get registers per element based on data type
        int registersPerElement = GetRegistersPerElement(tagConfig.DataType);

        // Calculate array size from tag configuration
        int arraySize = tagConfig.ArraySize > 0 ? tagConfig.ArraySize : 1;

        // Calculate how many complete elements we can extract
        int elementCount = Math.Min(arraySize, registers.Length / registersPerElement);

        // Create the result array
        object[] result = new object[elementCount];

        // Process each element
        for (int i = 0; i < elementCount; i++)
        {
            // Extract registers for this element
            ushort[] elementRegisters = new ushort[registersPerElement];
            Array.Copy(registers, i * registersPerElement, elementRegisters, 0, registersPerElement);

            // Parse according to the data type
            result[i] = tagConfig.DataType switch
            {
                ModbusTagDataTypeType.Boolean => tagConfig.BitNumber.HasValue ?
                                                ParseBoolean(elementRegisters, tagConfig.BitNumber.Value) :
                                                ParseBoolean(elementRegisters, 0),
                ModbusTagDataTypeType.Int16 => ParseInt16(elementRegisters),
                ModbusTagDataTypeType.Uint16 => ParseUInt16(elementRegisters),
                ModbusTagDataTypeType.Int32 => ParseInt32(elementRegisters),
                ModbusTagDataTypeType.Uint32 => ParseUInt32(elementRegisters),
                ModbusTagDataTypeType.Int64 => ParseInt64(elementRegisters),
                ModbusTagDataTypeType.Uint64 => ParseUInt64(elementRegisters),
                ModbusTagDataTypeType.Float32 => ParseFloat(elementRegisters),
                ModbusTagDataTypeType.Double64 => ParseDouble(elementRegisters),
                ModbusTagDataTypeType.String => ParseString(elementRegisters),
                _ => throw new NotSupportedException($"Unsupported data type for array: {tagConfig.DataType}")
            };
        }

        return result;
    }
    #region Conversion Methods

    private ushort[] ConvertBooleanToRegisters(bool value)
    {
        ushort register = value ? (ushort) 1 : (ushort) 0;

        return swapNumericRegisters([register]);        
    }

    private ushort[] ConvertInt16ToRegisters(short value)
    {
        return swapNumericRegisters([(ushort) value ]);
    }

    private ushort[] ConvertUInt16ToRegisters(ushort value)
    {        
        return swapNumericRegisters([ value ]);
    }

    private ushort[] ConvertInt32ToRegisters(int value)
    {
        // Convert to bytes
        byte[] bytes = BitConverter.GetBytes(value);

        // Convert to registers (2 words for a 32-bit value)
        ushort[] registers = new ushort[2];
        registers[0] = BitConverter.ToUInt16(bytes, 0);
        registers[1] = BitConverter.ToUInt16(bytes, 2);

        return swapNumericRegisters(registers);        
    }
    
    private ushort[] ConvertUInt32ToRegisters(uint value)
    {
        // Similar to Int32
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[2];
        registers[0] = BitConverter.ToUInt16(bytes, 0);
        registers[1] = BitConverter.ToUInt16(bytes, 2);

        return swapNumericRegisters(registers);        
    }

    private ushort[] ConvertFloatToRegisters(float value)
    {
        // Similar to Int32 - float is 32 bits
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[2];
        registers[0] = BitConverter.ToUInt16(bytes, 0);
        registers[1] = BitConverter.ToUInt16(bytes, 2);

        return swapNumericRegisters(registers);
    }

    private ushort[] ConvertDoubleToRegisters(double value)
    {
        // Double is 64 bits (4 registers)
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[4];
        for (int i = 0; i < 4; i++)
        {
            registers[i] = BitConverter.ToUInt16(bytes, i * 2);
        }

        return swapNumericRegisters(registers);
    }

    private ushort[] ConvertInt64ToRegisters(Int64 value)
    {
        // Int64 is 64 bits (4 registers)
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[4];
        for (int i = 0; i < 4; i++)
        {
            registers[i] = BitConverter.ToUInt16(bytes, i * 2);
        }

        return swapNumericRegisters(registers);
    }
    private ushort[] ConvertUint64ToRegisters(UInt64 value)
    {
        // UInt64 is 64 bits (4 registers)
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[4];
        for (int i = 0; i < 4; i++)
        {
            registers[i] = BitConverter.ToUInt16(bytes, i * 2);
        }

        return swapNumericRegisters(registers);
    }
    private ushort[] ConvertStringToRegisters(string value)
    {
        // Calculate how many registers we need (2 bytes per register)
        int registerCount = (int) Math.Ceiling(value.Length / 2.0);
        ushort[] registers = new ushort[registerCount];

        // Convert string to bytes using ASCII encoding
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);

        // Pad with a null if odd length
        if (bytes.Length % 2 != 0)
        {
            Array.Resize(ref bytes, bytes.Length + 1);
        }

        // Convert bytes to registers
        for (int i = 0; i < bytes.Length; i += 2)
        {
            ushort register = (ushort) ((bytes[i] << 8) | bytes[i + 1]);

            // Apply byte swap if configured
            if (_swapConfig.BytesInStrings)
            {
                register = SwapBytes16(register);
            }

            registers[i / 2] = register;
        }

        return registers;
    }

    private ushort[] swapNumericRegisters(ushort[] registers)
    {
        // Work with a copy of the original array to avoid modifying the input
        ushort[] result = (ushort[]) registers.Clone();

        // Step 1: DWord swap for 64-bit values (Swap 32-bit chunks within a 64-bit value)
        if (result.Length == 4 && _swapConfig.DWordsIn64Bit)
        {
            // Swap first pair with second pair (0,1 with 2,3)
            ushort temp = result[0];
            result[0] = result[2];
            result[2] = temp;

            temp = result[1];
            result[1] = result[3];
            result[3] = temp;
        }

        // Step 2: Word swap for 32-bit values (Swap 16-bit chunks within each 32-bit value)
        if (_swapConfig.WordsIn32Bit)
        {
            if (result.Length == 4)
            {
                // For 64-bit values, swap words within each 32-bit chunk
                ushort temp = result[0];
                result[0] = result[1];
                result[1] = temp;

                temp = result[2];
                result[2] = result[3];
                result[3] = temp;
            }
            else if (result.Length == 2)
            {
                // For 32-bit values, swap the two words
                ushort temp = result[0];
                result[0] = result[1];
                result[1] = temp;
            }
        }

        // Step 3: Byte swap for each 16-bit register
        if (_swapConfig.BytesIn16Bit)
        {
            for (int i = 0; i < result.Length; i++)
            {
                // Swap the bytes within each 16-bit word (high and low byte)
                result[i] = (ushort) ((result[i] >> 8) | (result[i] << 8));
            }
        }

        // Step 4: Bit swap for each 16-bit register
        if (_swapConfig.BitsIn16Bit)
        {
            for (int i = 0; i < result.Length; i++)
            {
                // Swap the bits within each 16-bit word (mirror the bits)
                ushort value = result[i];
                ushort swapped = 0;

                for (int bit = 0; bit < 16; bit++)
                {
                    if ((value & (1 << bit)) != 0)
                    {
                        swapped |= (ushort) (1 << (15 - bit));
                    }
                }

                result[i] = swapped;
            }
        }

        return result;
    }
    #endregion

    #region Parsing Methods

    private bool ParseBoolean(ushort[] registers, byte bitNumber)
    {
        if (registers.Length < 1)
            throw new ArgumentException("Not enough registers for boolean value");

        ushort register = registers[0];

        // Apply bit swapping if configured
        if (_swapConfig.BitsIn16Bit)
        {
            register = SwapBits(register);
        }

        return Convert.ToBoolean(register & (1 << bitNumber));        
    }

    private short ParseInt16(ushort[] registers)
    {
        if (registers.Length < 1)
            throw new ArgumentException("Not enough registers for Int16 value");

        ushort register = registers[0];

        // Apply appropriate swaps for 16-bit values (in reverse order)
        if (_swapConfig.BytesIn16Bit)
        {
            register = SwapBytes16(register);
        }

        if (_swapConfig.BitsIn16Bit)
        {
            register = SwapBits(register);
        }

        return (short) register;
    }

    private ushort ParseUInt16(ushort[] registers)
    {
        if (registers.Length < 1)
            throw new ArgumentException("Not enough registers for UInt16 value");

        ushort register = registers[0];

        // Apply appropriate swaps for 16-bit values (in reverse order)
        if (_swapConfig.BytesIn16Bit)
        {
            register = SwapBytes16(register);
        }

        if (_swapConfig.BitsIn16Bit)
        {
            register = SwapBits(register);
        }

        return register;
    }

    private int ParseInt32(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Not enough registers for Int32 value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(2).ToArray();

        // Apply word swap if needed
        if (_swapConfig.WordsIn32Bit)
        {
            SwapWords(workingRegisters);
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[4];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 4);

        return BitConverter.ToInt32(bytes, 0);
    }

    private uint ParseUInt32(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Not enough registers for UInt32 value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(2).ToArray();

        // Apply word swap if needed
        if (_swapConfig.WordsIn32Bit)
        {
            SwapWords(workingRegisters);
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[4];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 4);

        return BitConverter.ToUInt32(bytes, 0);
    }

    private float ParseFloat(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Not enough registers for float value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(2).ToArray();

        // Apply word swap if needed
        if (_swapConfig.WordsIn32Bit)
        {
            SwapWords(workingRegisters);
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[4];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 4);

        return BitConverter.ToSingle(bytes, 0);
    }

    private double ParseDouble(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Not enough registers for double value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(4).ToArray();

        // Apply dword swap (64-bit)
        if (_swapConfig.DWordsIn64Bit)
        {
            SwapDWords(workingRegisters);
        }

        // Apply word swaps (32-bit chunks)
        if (_swapConfig.WordsIn32Bit)
        {
            // Swap words within each 32-bit chunk
            SwapWords(workingRegisters, 0, 2); // First 32-bit chunk
            SwapWords(workingRegisters, 2, 2); // Second 32-bit chunk
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[8];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 8);

        return BitConverter.ToDouble(bytes, 0);
    }

    private double ParseUInt64(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Not enough registers for double value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(4).ToArray();

        // Apply dword swap (64-bit)
        if (_swapConfig.DWordsIn64Bit)
        {
            SwapDWords(workingRegisters);
        }

        // Apply word swaps (32-bit chunks)
        if (_swapConfig.WordsIn32Bit)
        {
            // Swap words within each 32-bit chunk
            SwapWords(workingRegisters, 0, 2); // First 32-bit chunk
            SwapWords(workingRegisters, 2, 2); // Second 32-bit chunk
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[8];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 8);

        return BitConverter.ToUInt64(bytes, 0);
    }

    private double ParseInt64(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Not enough registers for double value");

        // Create a working copy
        ushort[] workingRegisters = registers.Take(4).ToArray();

        // Apply dword swap (64-bit)
        if (_swapConfig.DWordsIn64Bit)
        {
            SwapDWords(workingRegisters);
        }

        // Apply word swaps (32-bit chunks)
        if (_swapConfig.WordsIn32Bit)
        {
            // Swap words within each 32-bit chunk
            SwapWords(workingRegisters, 0, 2); // First 32-bit chunk
            SwapWords(workingRegisters, 2, 2); // Second 32-bit chunk
        }

        // Apply individual register swaps (in reverse order)
        for (int i = 0; i < workingRegisters.Length; i++)
        {
            if (_swapConfig.BytesIn16Bit)
            {
                workingRegisters[i] = SwapBytes16(workingRegisters[i]);
            }

            if (_swapConfig.BitsIn16Bit)
            {
                workingRegisters[i] = SwapBits(workingRegisters[i]);
            }
        }

        // Convert to bytes
        byte[] bytes = new byte[8];
        Buffer.BlockCopy(workingRegisters, 0, bytes, 0, 8);

        return BitConverter.ToInt64(bytes, 0);
    }

    private string ParseString(ushort[] registers)
    {
        // Initialize a byte array to hold all bytes
        byte[] bytes = new byte[registers.Length * 2];

        // Process each register
        for (int i = 0; i < registers.Length; i++)
        {
            ushort register = registers[i];

            // Apply byte swap if configured
            if (_swapConfig.BytesInStrings)
            {
                register = SwapBytes16(register);
            }

            // Extract the two bytes
            bytes[i * 2] = (byte) (register >> 8);
            bytes[i * 2 + 1] = (byte) (register & 0xFF);
        }

        // Find null terminator if present
        int stringLength = bytes.Length;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0)
            {
                stringLength = i;
                break;
            }
        }

        // Convert bytes to string
        return System.Text.Encoding.ASCII.GetString(bytes, 0, stringLength);
    }

    #endregion

    #region Helper Methods

    private ushort SwapBits(ushort value)
    {
        // Swap bits within a 16-bit word
        ushort result = 0;
        for (int i = 0; i < 16; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                result |= (ushort) (1 << (15 - i));
            }
        }
        return result;
    }

    private ushort SwapBytes16(ushort value)
    {
        // Swap the high and low bytes in a 16-bit word
        return (ushort) ((value >> 8) | (value << 8));
    }

    private void SwapWords(ushort[] words)
    {
        // Swap the entire array
        Array.Reverse(words);
    }

    private void SwapWords(ushort[] words, int startIndex, int count)
    {
        // Swap a subset of the array
        Array.Reverse(words, startIndex, count);
    }

    private void SwapDWords(ushort[] words)
    {
        // Swap two 32-bit chunks (4 registers total)
        if (words.Length < 4) return;

        // Swap first and third register
        ushort temp = words[0];
        words[0] = words[2];
        words[2] = temp;

        // Swap second and fourth register
        temp = words[1];
        words[1] = words[3];
        words[3] = temp;
    }

    #endregion
}
