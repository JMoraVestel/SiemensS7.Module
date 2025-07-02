using System.Globalization;
using System.Text;

using ModbusModule.TagConfig;

using vNode.Sdk.Enum;

namespace ModbusModule.Helper
{
    public static class ModbusHelper
    {        
        public static bool AreDifferentModbusTypes(uint address1, uint address2) =>
            GetModbusType(address1) != GetModbusType(address2);

        public static bool AreSameModbusTypes(uint address1, uint address2) =>
            GetModbusType(address1) != GetModbusType(address2);

        public static Type GetModbusTypeDataType(ModbusType modbusType)
        {
            if (modbusType == ModbusType.InputCoil || modbusType == ModbusType.OutputCoil)
                return typeof(bool);
            else if (modbusType == ModbusType.InputRegister || modbusType == ModbusType.HoldingRegister)
                return typeof(ushort);
            else
                throw new ArgumentOutOfRangeException(nameof(modbusType));
        }

        // Get the base address based on the register address pattern
        //private static UInt32 GetBaseAddress(UInt32 registerAddress)
        //{
        //    if (registerAddress >= 400000 && registerAddress <= 465536)
        //        return 400000;
        //    else if (registerAddress >= 40000 && registerAddress <= 49999)
        //        return 40000;
        //    else if (registerAddress >= 300000 && registerAddress <= 365536)
        //        return 300000;
        //    else if (registerAddress >= 30000 && registerAddress <= 39999)
        //        return 30000;
        //    else if (registerAddress >= 100000 && registerAddress <= 165536)
        //        return 100000;
        //    else if (registerAddress >= 10000 && registerAddress <= 19999)
        //        return 10000;
        //    else if (registerAddress >= 0 && registerAddress <= 9999)
        //        return 0;            
        //    else
        //        throw new ArgumentOutOfRangeException("registerAddress", "Invalid registerAddress: " + registerAddress);
        //}

        // Get the raw address from the logical address
        //public static ushort GetRawAddress(UInt32 registerAddress)
        //{
        //    UInt32 baseAddress = GetBaseAddress(registerAddress);
        //    // Subtract the base address and 1 (because Modbus addresses are 0-based internally)
        //    return (ushort) (registerAddress - baseAddress - 1);
        //}

        public static ModbusType GetModbusType(uint address) => address switch
        {
            var addr when (addr >= 0 && addr <= 9999) => ModbusType.OutputCoil,
            var addr when (addr >= 10000 && addr <= 19999) || (addr >= 100000 && addr <= 165536) => ModbusType.InputCoil,
            var addr when (addr >= 30000 && addr <= 39999) || (addr >= 300000 && addr <= 365536) => ModbusType.InputRegister,
            var addr when (addr >= 40000 && addr <= 49999) || (addr >= 400000 && addr <= 465536) => ModbusType.HoldingRegister,
            _ => ModbusType.Unknown
        };

        public static string ModbusRegistersToString(ushort[] registers)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ushort register in registers)
            {
                // Extract high byte (first character)
                char highByte = (char) ((register >> 8) & 0xFF);

                // Extract low byte (second character)
                char lowByte = (char) (register & 0xFF);

                // Add the characters to the string, but only if they're printable
                // Often Modbus strings are null-terminated or padded with zeros/spaces
                if (highByte != 0 && !char.IsControl(highByte))
                    sb.Append(highByte);

                if (lowByte != 0 && !char.IsControl(lowByte))
                    sb.Append(lowByte);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Overload of ConvertModbusToOpcua that handles both scalar and array values
        /// </summary>
        /// <param name="value">The value to convert (can be a scalar or an array)</param>
        /// <param name="sourceType">The Modbus data type of the source value</param>
        /// <param name="targetType">The OPCUA data type to convert to</param>
        /// <param name="isArray">Indicates whether the value is an array</param>
        /// <returns>The converted value or array of values</returns>
        public static object ConvertModbusToOpcua(object value, ModbusTagDataTypeType sourceType, TagDataTypeOptions targetType, bool isArray)
        {
            if (isArray && value is object[] arrayValues)
            {
                return ConvertModbusArrayToOpcua(arrayValues, sourceType, targetType);
            }

            return ConvertModbusToOpcua(value, sourceType, targetType);
        }

        /// <summary>
        /// Converts an array of Modbus values to a strongly-typed array of OPCUA values
        /// </summary>
        /// <param name="values">The array of values to convert</param>
        /// <param name="sourceType">The Modbus data type of the source values</param>
        /// <param name="targetType">The OPCUA data type to convert to</param>
        /// <returns>A strongly-typed array of converted values based on the target type</returns>
        public static object ConvertModbusArrayToOpcua(object[] values, ModbusTagDataTypeType sourceType, TagDataTypeOptions targetType)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<object>();

            // Convert all values to the target OPCUA type
            var convertedValues = values.Select(v => ConvertModbusToOpcua(v, sourceType, targetType)).ToArray();

            // Create appropriate typed array based on target type
            switch (targetType)
            {
                case TagDataTypeOptions.Boolean:
                    return Array.ConvertAll(convertedValues, item => (bool) item);
                case TagDataTypeOptions.SByte:
                    return Array.ConvertAll(convertedValues, item => (sbyte) item);
                case TagDataTypeOptions.Byte:
                    return Array.ConvertAll(convertedValues, item => (byte) item);
                case TagDataTypeOptions.Int16:
                    return Array.ConvertAll(convertedValues, item => (short) item);
                case TagDataTypeOptions.UInt16:
                    return Array.ConvertAll(convertedValues, item => (ushort) item);
                case TagDataTypeOptions.Int32:
                    return Array.ConvertAll(convertedValues, item => (int) item);
                case TagDataTypeOptions.UInt32:
                    return Array.ConvertAll(convertedValues, item => (uint) item);
                case TagDataTypeOptions.Int64:
                    return Array.ConvertAll(convertedValues, item => (long) item);
                case TagDataTypeOptions.UInt64:
                    return Array.ConvertAll(convertedValues, item => (ulong) item);
                case TagDataTypeOptions.Float:
                    return Array.ConvertAll(convertedValues, item => (float) item);
                case TagDataTypeOptions.Double:
                    return Array.ConvertAll(convertedValues, item => (double) item);
                case TagDataTypeOptions.String:
                    return Array.ConvertAll(convertedValues, item => (string) item);
                case TagDataTypeOptions.DateTime:
                    return Array.ConvertAll(convertedValues, item => (DateTime) item);
                default:
                    return convertedValues;
            }
        }        

        /// <summary>
        /// Converts a value from a Modbus data type to the specified OPCUA data type
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="sourceType">The Modbus data type of the source value</param>
        /// <param name="targetType">The OPCUA data type to convert to</param>
        /// <returns>The converted value</returns>
        private static object ConvertModbusToOpcua(object value, ModbusTagDataTypeType sourceType, TagDataTypeOptions targetType)
        {
            // Handle null values
            if (value == null)
            {
                return GetDefaultValueForType(targetType);
            }

            try
            {
                // Direct conversion based on target type
                switch (targetType)
                {
                    case TagDataTypeOptions.Boolean:
                        return Convert.ToBoolean(value);

                    case TagDataTypeOptions.SByte:
                        return Convert.ToSByte(value);

                    case TagDataTypeOptions.Byte:
                        return Convert.ToByte(value);

                    case TagDataTypeOptions.Int16:
                        return Convert.ToInt16(value);

                    case TagDataTypeOptions.UInt16:
                        return Convert.ToUInt16(value);

                    case TagDataTypeOptions.Int32:
                        return Convert.ToInt32(value);

                    case TagDataTypeOptions.UInt32:
                        return Convert.ToUInt32(value);

                    case TagDataTypeOptions.Int64:
                        return Convert.ToInt64(value);

                    case TagDataTypeOptions.UInt64:
                        return Convert.ToUInt64(value);

                    case TagDataTypeOptions.Float:
                        return Convert.ToSingle(value);

                    case TagDataTypeOptions.Double:
                        return Convert.ToDouble(value);

                    case TagDataTypeOptions.String:
                        return value.ToString();

                    case TagDataTypeOptions.DateTime:
                        // Interpret various Modbus types as DateTime
                        if (sourceType == ModbusTagDataTypeType.Uint32 || sourceType == ModbusTagDataTypeType.Int32)
                            // Interpret as Unix timestamp (seconds since epoch)
                            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(value));
                        else if (sourceType == ModbusTagDataTypeType.Uint64 || sourceType == ModbusTagDataTypeType.Int64)
                            // Interpret as Windows file time
                            return DateTime.FromFileTimeUtc(Convert.ToInt64(value));
                        else if (sourceType == ModbusTagDataTypeType.String)
                            // Parse from string representation
                            return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                        else
                            throw new ArgumentException($"Cannot convert from Modbus {sourceType} to DateTime");

                    case TagDataTypeOptions.Json:
                        // For JSON, ensure it's a valid JSON string
                        if (sourceType == ModbusTagDataTypeType.String)
                            return value.ToString();
                        else
                            throw new ArgumentException("JSON can only be converted from string type");

                    default:
                        throw new ArgumentException($"Unsupported OPCUA data type: {targetType}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Failed to convert from Modbus {sourceType} to OPCUA {targetType}", ex);
            }
        }
        private static object GetDefaultValueForType(TagDataTypeOptions targetType)
        {
            switch (targetType)
            {
                case TagDataTypeOptions.Boolean:
                    return false;
                case TagDataTypeOptions.SByte:
                    return (sbyte) 0;
                case TagDataTypeOptions.Byte:
                    return (byte) 0;
                case TagDataTypeOptions.Int16:
                    return (short) 0;
                case TagDataTypeOptions.UInt16:
                    return (ushort) 0;
                case TagDataTypeOptions.Int32:
                    return 0;
                case TagDataTypeOptions.UInt32:
                    return 0U;
                case TagDataTypeOptions.Int64:
                    return 0L;
                case TagDataTypeOptions.UInt64:
                    return 0UL;
                case TagDataTypeOptions.Float:
                    return 0.0f;
                case TagDataTypeOptions.Double:
                    return 0.0d;
                case TagDataTypeOptions.String:
                    return string.Empty;
                case TagDataTypeOptions.DateTime:
                    return DateTime.MinValue;
                case TagDataTypeOptions.Json:
                    return "{}";
                default:
                    return null;
            }
        }
    }
}
