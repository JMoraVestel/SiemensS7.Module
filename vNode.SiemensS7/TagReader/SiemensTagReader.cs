using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using vNode.SiemensS7.Scheduler;
using vNode.SiemensS7.SiemensCommonLayer;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.TagReader
{
    /// <summary>
    /// Gestiona las operaciones de lectura y escritura de tags con un PLC Siemens.
    /// </summary>
    public class SiemensTagReader : IDisposable
    {
        private readonly ISdkLogger _logger;
        private readonly SiemensTcpStrategy _plcConnection;
        private readonly SemaphoreSlim _plcLock = new(1, 1);
        private bool _disposedValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        // The error CS1061 indicates that the `Plc` class does not have a `PropertyChanged` event.
        // To fix this, you need to ensure that the `Plc` class implements `INotifyPropertyChanged`
        // and defines the `PropertyChanged` event. If the `Plc` class is part of an external library,
        // you cannot modify it directly. In that case, you can use a wrapper class or remove the
        // event subscription.

        public SiemensTagReader(SiemensTcpStrategy plcConnection, ISdkLogger logger)
        {
            _plcConnection = plcConnection ?? throw new ArgumentNullException(nameof(plcConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Check if the Plc class has the PropertyChanged event before subscribing.
            if (_plcConnection.Plc is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += _plc_PropertyChanged;
            }
            else
            {
                _logger.Warning("SiemensTagReader", "El objeto Plc no implementa INotifyPropertyChanged. No se puede suscribir al evento PropertyChanged.");
            }
        }

        /// <summary>
        /// Indica si la conexión con el PLC está activa.
        /// </summary>
        public bool IsConnected => _plcConnection.Plc.IsConnected;

        /// <summary>
        /// Lee el valor de un único tag del PLC.
        /// </summary>
        /// <param name="tagConfig">Configuración del tag a leer.</param>
        /// <returns>El valor leído del tag.</returns>
        public object ReadTag(SiemensTagConfig tagConfig)
        {
            if (!IsConnected)
            {
                _logger.Warning("SiemensTagReader", "No se puede leer el tag porque no hay conexión con el PLC.");
                throw new InvalidOperationException("No conectado al PLC.");
            }

            _plcLock.Wait();
            try
            {
                return _plcConnection.Plc.Read(tagConfig.Address);
            }
            finally
            {
                _plcLock.Release();
            }
        }

        // Fix for CS1729: 'TagReadResult' no contiene un constructor que tome 3 argumentos.
        // The error occurs because the `TagReadResult` class does not have a constructor that accepts three arguments.
        // Based on the provided type signatures, we need to use the static methods `CreateFailed` or `CreateSuccess` to create instances of `TagReadResult`.

        public Dictionary<Guid, TagReadResult> ReadManyForSdk(Dictionary<Guid, SiemensTagWrapper> tagsToRead)
        {
            var results = new Dictionary<Guid, TagReadResult>();
            var stopWatch = new Stopwatch();

            if (!IsConnected)
            {
                _logger.Warning("SiemensTagReader", "No se puede realizar la lectura múltiple porque no hay conexión con el PLC.");
                foreach (var tagId in tagsToRead.Keys)
                {
                    // Use CreateFailed to create a failed TagReadResult.
                    results[tagId] = TagReadResult.CreateFailed(TagReadResult.TagReadResultType.CommsError, new List<TagReadBatchItem>());
                }
                return results;
            }

            // Prepara la lista de variables para S7NetPlus.
            var s7Vars = tagsToRead.Select(kvp => new S7.Net.Types.DataItem
            {
                DataType = (S7.Net.Types.DataType)Enum.Parse(typeof(S7.Net.Types.DataType), kvp.Value.Config.DataType.ToString()),
                VarType = S7.Net.Types.VarType.DB, // Asumimos DB, se podría extender.
                DB = 1, // Esto debería ser parte de la dirección parseada.
                StartByteAdr = 0, // Esto debería ser parte de la dirección parseada.
                Count = kvp.Value.Config.ArraySize > 0 ? kvp.Value.Config.ArraySize : 1
            }).ToList();

            _plcLock.Wait();
            stopWatch.Start();
            try
            {
                // Realiza la lectura múltiple.
                var s7Results = _plcConnection.Plc.ReadMultipleVars(s7Vars);
                stopWatch.Stop();

                // Mapea los resultados de S7NetPlus a los resultados del SDK.
                int i = 0;
                foreach (var kvp in tagsToRead)
                {
                    var s7Result = s7Results[i++];
                    var quality = s7Result.IsGood ? QualityCodeOptions.Good_Non_Specific : QualityCodeOptions.Bad_Non_Specific;

                    // Use CreateSuccess to create a successful TagReadResult.
                    var batchItem = new TagReadBatchItem(kvp.Value, DateTime.UtcNow)
                    {
                        ActualReadTime = DateTime.UtcNow
                    };

                    results[kvp.Key] = TagReadResult.CreateSuccess(new List<TagReadResultItem>
                    {
                        new TagReadResultItem(batchItem, TagReadResult.TagReadResultType.Success, s7Result.Value)
                    });
                }
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                _logger.Error(ex, "SiemensTagReader", "Error durante la lectura múltiple de tags.");
                foreach (var tagId in tagsToRead.Keys)
                {
                    // Use CreateFailed to create a failed TagReadResult.
                    results[tagId] = TagReadResult.CreateFailed(TagReadResult.TagReadResultType.CommsError, new List<TagReadBatchItem>());
                }
            }
            finally
            {
                _plcLock.Release();
            }

            _logger.Debug("SiemensTagReader", $"Lectura múltiple de {tagsToRead.Count} tags completada en {stopWatch.ElapsedMilliseconds}ms.");
            return results;
        }

        /// <summary>
        /// Escribe un valor en un tag del PLC de forma asíncrona.
        /// </summary>
        /// <param name="tagConfig">Configuración del tag a escribir.</param>
        /// <param name="value">Valor a escribir.</param>
        public async Task WriteTagAsync(SiemensTagConfig tagConfig, object value)
        {
            if (!IsConnected)
            {
                _logger.Warning("SiemensTagReader", "No se puede escribir el tag porque no hay conexión con el PLC.");
                throw new InvalidOperationException("No conectado al PLC.");
            }

            await _plcLock.WaitAsync();
            try
            {
                await _plcConnection.Plc.WriteAsync(tagConfig.Address, value);
            }
            finally
            {
                _plcLock.Release();
            }
        }

        /// <summary>
        /// Manejador para los cambios de propiedad en la conexión del PLC.
        /// </summary>
        private void _plc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Plc.IsConnected))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
            }
        }

        /// <summary>
        /// Libera los recursos utilizados por el lector de tags.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_plcConnection?.Plc != null)
                    {
                        _plcConnection.Plc.PropertyChanged -= _plc_PropertyChanged;
                    }
                    _plcLock.Dispose();
                }
                _disposedValue = true;
            }
        }
    }
}
