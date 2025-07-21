using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;
using static vNode.SiemensS7.TagReader.TagReadResult;

namespace vNode.SiemensS7.TagReader
{
    public class SiemensTagReader
    {
        private readonly SiemensTcpStrategy _connection;
        private readonly SiemensChannelConfig _channelConfig;
        private readonly ISdkLogger _logger;
        private readonly SemaphoreSlim _s7Lock = new SemaphoreSlim(1, 1);

        public SiemensTagReader(SiemensTcpStrategy connection, SiemensChannelConfig channelConfig, ISdkLogger logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _channelConfig = channelConfig ?? throw new ArgumentNullException(nameof(channelConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    var value = SiemensDataConverter.ConvertFromPlc(tag.Config, raw);
                    results[tagName] = value;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "SiemensTagReader", $"Error al leer el tag '{tagName}' ({tag.Config.Address}).");
                    results[tagName] = null;
                }
            }

            return results;
        }

        /// <summary>
        /// Lee un lote de tags y devuelve los resultados en el formato esperado por el SDK.
        /// </summary>
        /// <param name="tagsToRead">Diccionario de tags a leer.</param>
        /// <returns>Un diccionario que contiene los resultados de la lectura.</returns>
        public Dictionary<Guid, TagReadResult> ReadManyForSdk(Dictionary<Guid, SiemensTagWrapper> tagsToRead)
        {
            var results = new Dictionary<Guid, TagReadResult>();
            var successfulItems = new List<TagReadResultItem>();
            var failedItems = new List<TagReadBatchItem>();

            // Nota: Para un rendimiento óptimo, esta lógica debería agrupar las variables y
            // leerlas en una sola solicitud usando `ReadMultipleVars` de S7.Net.
            // La implementación actual lee las variables una por una.
            foreach (var tagWrapper in tagsToRead.Values)
            {
                // El tiempo de lectura (`ReadDueTime`) debería venir idealmente del planificador.
                // Como no está disponible, usamos el tiempo actual.
                var batchItem = new TagReadBatchItem(tagWrapper, DateTime.UtcNow);

                try
                {
                    var rawValue = _connection.Read(tagWrapper.Config.Address);
                    var convertedValue = SiemensDataConverter.ConvertFromPlc(tagWrapper.Config, rawValue);
                    var resultItem = new TagReadResultItem(batchItem, (TagReadResultType) convertedValue, TagReadResult.TagReadResultType.Success);
                    successfulItems.Add(resultItem);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "SiemensTagReader", $"Error al leer el tag '{tagWrapper.Name}' ({tagWrapper.Config.Address}).");
                    failedItems.Add(batchItem);
                }
            }

            // Agrupa todos los resultados exitosos en un solo objeto TagReadResult.
            if (successfulItems.Any())
            {
                // La clave del diccionario no se usa en `processReadResult`, por lo que un nuevo Guid es suficiente.
                results[Guid.NewGuid()] = TagReadResult.CreateSuccess(successfulItems);
            }

            // Agrupa todos los fallos en otro objeto TagReadResult.
            if (failedItems.Any())
            {
                results[Guid.NewGuid()] = TagReadResult.CreateFailed(TagReadResult.TagReadResultType.OtherError, failedItems);
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

        public async Task<bool> WriteTagAsync(SiemensTagWrapper tag, object value)
        {
            if (tag.Tag.ClientAccess == ClientAccessOptions.ReadOnly)
            {
                _logger.Warning("SiemensTagReader", $"Intento de escritura en el tag de solo lectura '{tag.Name}'.");
                throw new ArgumentException("El tag es de solo lectura.");
            }

            if (tag.Config.IsReadOnly)
            {
                _logger.Warning("SiemensTagReader", $"Intento de escritura en la dirección de solo lectura '{tag.Config.Address}'.");
                throw new ArgumentException($"La dirección Siemens {tag.Config.Address} es de solo lectura.");
            }

            object valueToWrite;
            try
            {
                valueToWrite = SiemensDataConverter.ConvertToPlc(tag.Config, value);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SiemensTagReader", $"No se pudo convertir el valor '{value}' para el tag '{tag.Name}'.");
                throw new InvalidDataException("No se pudo procesar el valor para la escritura.", ex);
            }

            await _s7Lock.WaitAsync();
            try
            {
                await Task.Run(() => _connection.Write(tag.Config.Address, valueToWrite));
                _logger.Information("SiemensTagReader", $"Escritura exitosa en el tag '{tag.Name}' (Dirección: {tag.Config.Address}).");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SiemensTagReader", $"Error al escribir en el dispositivo S7: {ex.Message}");
                // Relanzar para que la capa superior pueda manejar el error de comunicación.
                throw;
            }
            finally
            {
                _s7Lock.Release();
            }

            return true;
        }

        /// <summary>
        /// Escribe varios tags en el PLC agrupando por lotes de hasta 200 bytes.
        /// </summary>
        /// <param name="tagValuePairs">Colección de pares (Tag, Valor) a escribir.</param>
        /// <returns>True si todas las escrituras fueron exitosas.</returns>
        public async Task<bool> WriteTagsBatchAsync(IEnumerable<(SiemensTagWrapper Tag, object Value)> tagValuePairs)
        {
            // Ordena por dirección para maximizar la agrupación contigua
            var ordered = tagValuePairs.OrderBy(x => x.Tag.Config.Address).ToList();
            var currentBatch = new List<(SiemensTagWrapper, object)>();
            int currentBatchSize = 0;
            bool allOk = true;

            foreach (var pair in ordered)
            {
                int tagSize = pair.Tag.Config.GetSize();
                if (currentBatchSize + tagSize > 200 && currentBatch.Count > 0)
                {
                    // Envía el lote actual
                    bool ok = await WriteBatchInternalAsync(currentBatch);
                    allOk &= ok;
                    currentBatch.Clear();
                    currentBatchSize = 0;
                }
                currentBatch.Add(pair);
                currentBatchSize += tagSize;
            }

            if (currentBatch.Count > 0)
            {
                bool ok = await WriteBatchInternalAsync(currentBatch);
                allOk &= ok;
            }

            return allOk;
        }

        /// <summary>
        /// Serializa y escribe un lote de tags en el PLC.
        /// </summary>
        private async Task<bool> WriteBatchInternalAsync(List<(SiemensTagWrapper Tag, object Value)> batch)
        {
            try
            {
                // Simula la escritura múltiple: en la práctica, aquí deberías usar WriteMultipleVars si tu driver lo soporta.
                await Task.Run(() =>
                {
                    foreach (var (tag, value) in batch)
                    {
                        var valueToWrite = SiemensDataConverter.ConvertToPlc(tag.Config, value);
                        _connection.Write(tag.Config.Address, valueToWrite);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SiemensTagReader", "Error al escribir lote de tags.");
                return false;
            }
        }
    }
}
