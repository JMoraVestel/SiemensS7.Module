using System;
using System.Collections.Generic;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.TagReader
{
    public class SiemensDataConverter
    {
        private readonly ISdkLogger _logger;

        public SiemensDataConverter(ISdkLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static bool TryParseBool(object value, out bool result)
        {
            result = false;

            if (value == null)
                return false;

            if (value is bool boolValue)
            {
                result = boolValue;
                return true;
            }

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

            if (value is string stringValue)
            {
                if (bool.TryParse(stringValue, out result))
                    return true;

                if (double.TryParse(stringValue, out double numericValue))
                {
                    result = numericValue != 0;
                    return true;
                }
            }

            return false;
        }

        public static bool ParseBool(object value, bool defaultValue = false)
        {
            return TryParseBool(value, out bool result) ? result : defaultValue;
        }

        public object ConvertToSiemensType(object value, SiemensTagDataType targetType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                return targetType switch
                {
                    SiemensTagDataType.Bool => Convert.ToBoolean(value),
                    SiemensTagDataType.Byte => Convert.ToByte(value),
                    SiemensTagDataType.Word => Convert.ToUInt16(value),
                    SiemensTagDataType.DWord => Convert.ToUInt32(value),
                    SiemensTagDataType.Int => Convert.ToInt16(value),
                    SiemensTagDataType.DInt => Convert.ToInt32(value),
                    SiemensTagDataType.Real => Convert.ToSingle(value),
                    SiemensTagDataType.String => value.ToString(),
                    _ => throw new NotSupportedException($"Unsupported Siemens data type: {targetType}")
                };
            }
            catch (Exception ex)
            {
                _logger.Error("SiemensDataConverter", $"Error converting value to Siemens type {targetType}: {ex.Message}");
                throw;
            }
        }

        public object ParseFromSiemensType(object value, SiemensTagDataType sourceType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                return sourceType switch
                {
                    SiemensTagDataType.Bool => Convert.ToBoolean(value),
                    SiemensTagDataType.Byte => Convert.ToByte(value),
                    SiemensTagDataType.Word => Convert.ToUInt16(value),
                    SiemensTagDataType.DWord => Convert.ToUInt32(value),
                    SiemensTagDataType.Int => Convert.ToInt16(value),
                    SiemensTagDataType.DInt => Convert.ToInt32(value),
                    SiemensTagDataType.Real => Convert.ToSingle(value),
                    SiemensTagDataType.String => value.ToString(),
                    _ => throw new NotSupportedException($"Unsupported Siemens data type: {sourceType}")
                };
            }
            catch (Exception ex)
            {
                _logger.Error("SiemensDataConverter", $"Error parsing value from Siemens type {sourceType}: {ex.Message}");
                throw;
            }
        }

        public bool TryConvertArrayToSiemensType(object[] arrayValues, SiemensTagDataType targetType, out object[] convertedValues)
        {
            convertedValues = null;

            try
            {
                convertedValues = new object[arrayValues.Length];
                for (int i = 0; i < arrayValues.Length; i++)
                {
                    convertedValues[i] = ConvertToSiemensType(arrayValues[i], targetType);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("SiemensDataConverter", $"Error converting array to Siemens type {targetType}: {ex.Message}");
                return false;
            }
        }

        internal static object ConvertToPlc(SiemensTagConfig config, object newValue)
        {
            throw new NotImplementedException();
        }
    }
}
