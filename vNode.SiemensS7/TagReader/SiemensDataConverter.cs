using System;
using System.Text;
using vNode.SiemensS7.TagConfig;
using S7.Net;
using S7.Net.Types;

namespace vNode.SiemensS7.TagReader
{
    public static class SiemensDataConverter
    {
        /// <summary>
        /// Convierte un valor leído de un buffer de bytes a un tipo de dato de .NET.
        /// Este método está diseñado para extraer un valor de un buffer más grande usando un offset.
        /// </summary>
        public static object ConvertFromPlc(
            SiemensTagConfig config,
            byte[] rawBytes,
            int byteIndex = 0,
            int bitIndex = 0)
        {
            if (rawBytes == null)
                throw new ArgumentNullException(nameof(rawBytes), "El array de bytes no puede ser nulo.");

            // El tipo Bool se maneja a nivel de bit dentro de un byte.
            if (config.DataType == SiemensTagDataType.Bool)
            {
                if (rawBytes.Length <= byteIndex)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex), "El índice de byte está fuera del rango del buffer.");
                // Se extrae el bit específico del byte correspondiente.
                return (rawBytes[byteIndex] & (1 << bitIndex)) != 0;
            }

            // Para otros tipos, se extrae el número exacto de bytes del buffer.
            int size = config.GetSize();
            if (rawBytes.Length < byteIndex + size)
                throw new ArgumentException($"Buffer de bytes insuficiente para el tipo {config.DataType} en el offset {byteIndex}.");

            byte[] dataBytes = new byte[size];
            Array.Copy(rawBytes, byteIndex, dataBytes, 0, size);

            switch (config.DataType)
            {
                case SiemensTagDataType.String:
                    // S7 String tiene 2 bytes de cabecera (MaxLen, ActualLen)
                    // que deben ser omitidos para obtener la cadena de caracteres.
                    if (dataBytes.Length < 2)
                        return string.Empty;
                    var actualLen = dataBytes[1];
                    if (actualLen == 0)
                        return string.Empty;
                    // Asegurarse de no leer más allá del buffer si la longitud real es incorrecta.
                    if (actualLen > dataBytes.Length - 2)
                        actualLen = (byte)(dataBytes.Length - 2);
                    return Encoding.ASCII.GetString(dataBytes, 2, actualLen);
                case SiemensTagDataType.Byte:
                    return dataBytes[0]; // Para un solo byte, la conversión es directa.
                case SiemensTagDataType.Word:
                    return Word.FromByteArray(dataBytes);
                case SiemensTagDataType.DWord:
                    return DWord.FromByteArray(dataBytes);
                case SiemensTagDataType.Int:
                    return Int.FromByteArray(dataBytes);
                case SiemensTagDataType.DInt:
                    return DInt.FromByteArray(dataBytes);
                case SiemensTagDataType.Real:
                    return Real.FromByteArray(dataBytes);
                default:
                    throw new NotSupportedException($"Tipo de dato '{config.DataType}' no soportado para lectura desde buffer.");
            }
        }

        /// <summary>
        /// Lee un tag del PLC y convierte su valor a un tipo de dato de .NET.
        /// </summary>
        public static object ReadAndConvertFromPlc(
            Plc plc,
            SiemensTagConfig config)
        {
            var (dataType, db, startByteAdr, count, bitAdr) = S7Address.Parse(config.Address, config);

            if (config.DataType == SiemensTagDataType.Bool)
            {
                var byteValue = plc.ReadBytes(dataType, db, startByteAdr, 1);
                return (byteValue[0] & (1 << bitAdr)) != 0;
            }

            var rawBytes = plc.ReadBytes(dataType, db, startByteAdr, count);

            // Cuando se lee directamente, el array de bytes ya tiene el tamaño exacto.
            switch (config.DataType)
            {
                case SiemensTagDataType.String:
                    // S7 String tiene 2 bytes de cabecera (MaxLen, ActualLen)
                    // que deben ser omitidos para obtener la cadena de caracteres.
                    if (rawBytes.Length < 2)
                        return string.Empty;
                    var actualLen = rawBytes[1];
                    if (actualLen == 0)
                        return string.Empty;
                    // Asegurarse de no leer más allá del buffer si la longitud real es incorrecta.
                    if (actualLen > rawBytes.Length - 2)
                        actualLen = (byte)(rawBytes.Length - 2);
                    return Encoding.ASCII.GetString(rawBytes, 2, actualLen);
                case SiemensTagDataType.Byte:
                    return rawBytes[0];
                case SiemensTagDataType.Word:
                    return Word.FromByteArray(rawBytes);
                case SiemensTagDataType.DWord:
                    return DWord.FromByteArray(rawBytes);
                case SiemensTagDataType.Int:
                    return Int.FromByteArray(rawBytes);
                case SiemensTagDataType.DInt:
                    return DInt.FromByteArray(rawBytes);
                case SiemensTagDataType.Real:
                    return Real.FromByteArray(rawBytes);
                default:
                    throw new NotSupportedException($"Tipo de dato '{config.DataType}' no soportado para lectura.");
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
