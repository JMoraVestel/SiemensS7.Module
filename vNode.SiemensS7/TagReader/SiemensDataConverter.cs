using System;
using System.Globalization;
using System.Text;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.TagReader
{
    public static class SiemensDataConverter
    {
        /// <summary>
        /// Convierte un valor leído del PLC a un tipo de dato de .NET.
        /// </summary>
        public static object ConvertFromPlc(SiemensTagConfig config, object rawValue)
        {
            if (rawValue == null) return null;

            switch (config.DataType)
            {
                case SiemensTagDataType.Bool:
                    return (bool)rawValue;
                case SiemensTagDataType.Byte:
                    return (byte)rawValue;
                case SiemensTagDataType.Word:
                    return (ushort)rawValue;
                case SiemensTagDataType.DWord:
                    return (uint)rawValue;
                case SiemensTagDataType.Int:
                    return (short)rawValue;
                case SiemensTagDataType.DInt:
                    return (int)rawValue;
                case SiemensTagDataType.Real:
                    // El valor viene como uint, hay que convertirlo a array de bytes y luego a float.
                    if (rawValue is uint uintVal)
                    {
                        return BitConverter.ToSingle(BitConverter.GetBytes(uintVal), 0);
                    }
                    return Convert.ToSingle(rawValue);
                case SiemensTagDataType.String:
                    if (rawValue is byte[] bytes)
                    {
                        if (bytes.Length < 2) return "";
                        int len = bytes[1]; // Longitud actual de la cadena.
                        return Encoding.ASCII.GetString(bytes, 2, len);
                    }
                    return rawValue.ToString();
                // Añadir aquí otros tipos de datos si es necesario (DATE, TIME, etc.)
                default:
                    return rawValue;
            }
        }

        /// <summary>
        /// Convierte un valor de .NET al tipo de dato esperado por el PLC para su escritura.
        /// </summary>
        public static object ConvertToPlc(SiemensTagConfig config, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "El valor a escribir no puede ser nulo.");

            try
            {
                switch (config.DataType)
                {
                    case SiemensTagDataType.Bool:
                        return Convert.ToBoolean(value);
                    case SiemensTagDataType.Byte:
                        return Convert.ToByte(value);
                    case SiemensTagDataType.Word:
                        return Convert.ToUInt16(value);
                    case SiemensTagDataType.DWord:
                        return Convert.ToUInt32(value);
                    case SiemensTagDataType.Int:
                        return Convert.ToInt16(value);
                    case SiemensTagDataType.DInt:
                        return Convert.ToInt32(value);
                    case SiemensTagDataType.Real:
                        return Convert.ToSingle(value);
                    case SiemensTagDataType.String:
                        return value.ToString();
                    // Añadir aquí otros tipos de datos si es necesario
                    default:
                        throw new NotSupportedException($"El tipo de dato '{config.DataType}' no está soportado para escritura.");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"No se pudo convertir el valor '{value}' al tipo de dato Siemens '{config.DataType}'.", ex);
            }
        }
    }
}
