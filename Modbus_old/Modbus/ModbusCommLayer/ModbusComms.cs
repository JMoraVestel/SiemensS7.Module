using System.ComponentModel;
using System.Net.Sockets;

using ModbusModule.Diagnostics;

using NModbus;

using vNode.Sdk.Logger;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Main Modbus communication class that uses composition to handle different connection types
    /// </summary>
    public class ModbusComms : IModbusConnection
    {
        private readonly ISdkLogger _logger;
        private readonly ChannelDiagnostics _channelDiagnostics;
        private readonly IModbusConnectionStrategy _connectionStrategy;
        private readonly byte _retries;
        private readonly int _delayBetweenRetries;
        private readonly int _reconnectInterval;
        private bool _isConnected;
        private readonly object _connectionLock = new object();
        private bool _disposedValue;
        private System.Timers.Timer? _reconnectTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates a new ModbusComms instance with the specified connection strategy
        /// </summary>
        public ModbusComms(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            IModbusConnectionStrategy connectionStrategy,
            byte retries,
            int delayBetweenRetries,
            int reconnectInterval = 5000)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(connectionStrategy);

            _logger = logger;
            _channelDiagnostics = channelDiagnostics;
            _connectionStrategy = connectionStrategy;
            _retries = retries;
            _delayBetweenRetries = delayBetweenRetries;
            _reconnectInterval = reconnectInterval;
            _isConnected = false;

            _logger.Information("ModbusComms", "Initializing ModbusComms");
        }

        #region Connection Management

        /// <summary>
        /// Check if the Modbus client is connected
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Connect to the Modbus device
        /// </summary>
        public void Connect()
        {
            lock (_connectionLock)
            {
                ConnectAsync().Wait();
                UpdateConnectionStatus(forceEvent: true);
                if (!IsConnected)
                {
                    HandleDisconnection();
                }
            }
        }

        /// <summary>
        /// Connect to the Modbus device asynchronously
        /// </summary>
        private async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.Debug("ModbusComms", "Connecting...");
                bool connected = await _connectionStrategy.ConnectAsync();

                if (!connected)
                {
                    _logger.Error("ModbusComms", "Failed to connect");
                    return false;
                }

                _logger.Debug("ModbusComms", "Connection successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusComms", $"Exception during connection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update the connection status and notify if changed
        /// </summary>
        private void UpdateConnectionStatus(bool forceEvent = false)
        {
            lock (_connectionLock)
            {
                bool currentStatus = _connectionStrategy.IsConnected();

                if (_isConnected != currentStatus || forceEvent)
                {
                    _isConnected = currentStatus;
                    OnPropertyChanged(nameof(IsConnected));
                }
            }
        }

        /// <summary>
        /// Handle client disconnection and attempt to reconnect
        /// </summary>
        private void HandleDisconnection()
        {
            lock (_connectionLock)
            {
                _logger.Debug("ModbusComms", $"Client disconnected. Attempting reconnection in {_reconnectInterval}ms");

                UpdateConnectionStatus();

                _reconnectTimer?.Stop();
                _reconnectTimer?.Dispose();

                // Create and start a new timer
                _reconnectTimer = new System.Timers.Timer(_reconnectInterval);
                _reconnectTimer.AutoReset = false;
                _reconnectTimer.Elapsed += async (sender, e) =>
                {
                    _reconnectTimer.Stop();
                    if (_isConnected)
                    {
                        _logger.Warning("ModbusComms", "Client is already connected. No reconnection needed.");
                        return;
                    }

                    try
                    {
                        _logger.Debug("ModbusComms", "Reconnection timer elapsed. Attempting to reconnect...");
                        bool connected = await ConnectAsync();

                        if (connected)
                        {
                            _logger.Debug("ModbusComms", "Reconnection successful");
                            UpdateConnectionStatus();
                        }
                        else
                        {
                            _logger.Debug("ModbusComms", $"Reconnection failed. Scheduling another attempt in {_reconnectInterval}ms");
                            // If connection failed, try again
                            HandleDisconnection();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "ModbusComms", $"Exception during reconnection attempt: {ex.Message}. Scheduling another attempt in {_reconnectInterval}ms");
                        // If exception occurred during connection, try again
                        HandleDisconnection();
                    }
                };

                _reconnectTimer.Start();
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Read coils from the Modbus device
        /// </summary>
        public async Task<bool[]> ReadCoilsAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null)
        {
            return await ExecuteReadWithRetriesAsync(
                () => _connectionStrategy.GetModbusMaster().ReadCoilsAsync(slaveAddress, (ushort) (registerAddress - 1), numberOfPoints),
                "ReadCoils",
                slaveAddress, default, retryCallback);
        }

        /// <summary>
        /// Read discrete inputs from the Modbus device
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null)
        {
            return await ExecuteReadWithRetriesAsync(
                () => _connectionStrategy.GetModbusMaster().ReadInputsAsync(slaveAddress, (ushort) (registerAddress - 1), numberOfPoints),
                "ReadDiscreteInputs",
                slaveAddress, default, retryCallback);
        }

        /// <summary>
        /// Read input registers from the Modbus device
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null)
        {
            return await ExecuteReadWithRetriesAsync(
                () => _connectionStrategy.GetModbusMaster().ReadInputRegistersAsync(slaveAddress, (ushort) (registerAddress - 1), numberOfPoints),
                "ReadInputRegisters",
                slaveAddress, default, retryCallback);
        }

        /// <summary>
        /// Read holding registers from the Modbus device
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null)
        {
            return await ExecuteReadWithRetriesAsync(
                () => _connectionStrategy.GetModbusMaster().ReadHoldingRegistersAsync(slaveAddress, (ushort) (registerAddress - 1), numberOfPoints),
                "ReadHoldingRegisters",
                slaveAddress, default, retryCallback);
        }

        /// <summary>
        /// Generic wrapper method for Modbus operations with retries
        /// </summary>
        private async Task<T> ExecuteReadWithRetriesAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            byte slaveAddress,
            CancellationToken cancellationToken = default,
            Action? retryCallback = null
            )
        {
            int retryCount = 0;
            DateTime startTime = DateTime.UtcNow;
            Exception? lastException = null;

            if (!_connectionStrategy.IsConnected())
            {
                _logger.Error("ModbusComms", $"Error: Modbus connection is not established. Cannot perform read operation.");
                throw new IOException("Modbus connection is not established. Cannot perform read operation.");
            }

            while (true)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Debug("ModbusComms", "Operation cancelled");
                        throw new OperationCanceledException("Operation was cancelled", cancellationToken);
                    }

                    _logger.Trace("ModbusComms", $"Read attempt #{retryCount + 1}");
                    T result = await operation();

                    return result;
                }

                catch (Exception ex) when (
                    ex is IOException || ex is TimeoutException || ex is SocketException)
                {
                    lastException = ex;

                    _logger.Warning("ModbusComms", "Read Failed: " + GetSimpleErrorDescription(ex));

                    UpdateConnectionStatus();
                    if (!IsConnected)
                    {
                        HandleDisconnection();
                        throw new ClientDisconnectedException();
                    }

                    // If we've used all our retries, break out and throw
                    if (retryCount >= _retries)
                    {
                        break;
                    }

                    retryCount++;
                    retryCallback?.Invoke();

                    // Wait before retry
                    await Task.Delay(_delayBetweenRetries, cancellationToken);
                }
            }

            // If we get here, all retries have been exhausted but there was no TCP disconnection.
            _logger.Warning("ModbusComms",
                $"Read Failed. No retries left.");

            // Throw the last exception with additional context
            throw new IOException(
                $"{operationName} failed after {_retries} retries. Last error: {lastException?.Message}",
                lastException);
        }

        #endregion

        #region Write Methods

        /// <summary>
        /// Write coils to the Modbus device
        /// </summary>
        public async Task WriteOutputCoilsAsync(byte slaveAddress, uint coilAddress, bool[] values, int timeoutMs = 2000)
        {
            await WriteMultipleValuesAsync(
                slaveAddress,
                coilAddress,
                values,
                _connectionStrategy.GetModbusMaster().WriteMultipleCoilsAsync,
                timeoutMs);
        }

        /// <summary>
        /// Write holding registers to the Modbus device
        /// </summary>
        public async Task WriteHoldingRegistersAsync(byte slaveAddress, bool useFunction6, uint registerAddress, ushort[] values, int timeoutMs = 2000)
        {
            if (useFunction6 && values.Length == 1)
            {
                // Use a direct call to WriteSingleRegisterAsync
                await ExecuteWriteWithRetriesAsync(
                    () => _connectionStrategy.GetModbusMaster().WriteSingleRegisterAsync(slaveAddress, (ushort) (registerAddress - 1), values[0]),
                    "WriteSingleRegister",
                    slaveAddress);
            }
            else
            {
                // Use writeMultipleValuesAsync with WriteMultipleRegistersAsync
                await WriteMultipleValuesAsync(
                    slaveAddress,
                    registerAddress,
                    values,
                    _connectionStrategy.GetModbusMaster().WriteMultipleRegistersAsync,
                    timeoutMs);
            }
        }

        /// <summary>
        /// Write multiple values to the Modbus device with chunking if needed
        /// </summary>
        private async Task WriteMultipleValuesAsync<T>(
            byte slaveAddress,
            uint startAddress,
            T[] values,
            Func<byte, ushort, T[], Task> writeFunc,
            int timeoutMs = 2000)
        {
            // Maximum number of registers that can be written in a single call
            const int maxValuesPerWrite = 123;

            // If we're within the limit, perform a single write with retries
            if (values.Length <= maxValuesPerWrite)
            {
                await ExecuteWriteWithRetriesAsync(
                    () => writeFunc(slaveAddress, (ushort) (startAddress - 1), values),
                    $"WriteMultiple_{typeof(T).Name}",
                    slaveAddress);
                return;
            }

            // For larger arrays, split into multiple writes
            int currentOffset = 0;
            while (currentOffset < values.Length)
            {
                // Calculate how many values to write in this iteration
                int valuesToWrite = Math.Min(maxValuesPerWrite, values.Length - currentOffset);

                // Create a subarray of values for this write
                T[] subValues = new T[valuesToWrite];
                Array.Copy(values, currentOffset, subValues, 0, valuesToWrite);

                // Calculate the address for this write
                ushort currentAddress = (ushort) (startAddress + currentOffset);

                // Perform the write operation with retries
                await ExecuteWriteWithRetriesAsync(
                    () => writeFunc(slaveAddress, (ushort) (currentAddress - 1), subValues),
                    $"WriteChunk_{typeof(T).Name}",
                    slaveAddress);

                // Move to the next chunk
                currentOffset += valuesToWrite;
            }
        }

        /// <summary>
        /// Execute a write operation with retries
        /// </summary>
        private async Task ExecuteWriteWithRetriesAsync(
            Func<Task> operation,
            string operationName,
            byte slaveAddress,
            CancellationToken cancellationToken = default,
            Action? retryCallback = null
            )
        {
            int retryCount = 0;
            DateTime startTime = DateTime.UtcNow;
            Exception? lastException = null;

            if (!_connectionStrategy.IsConnected())
            {
                _logger.Error("ModbusComms", $"Error: Modbus connection is not established. Cannot perform write operation.");
                throw new IOException("Modbus connection is not established. Cannot perform write operation.");
            }

            while (true)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Debug("ModbusComms", "Operation cancelled");
                        throw new OperationCanceledException("Operation was cancelled", cancellationToken);
                    }

                    _logger.Trace("ModbusComms", $"Write attempt #{retryCount + 1}");
                    await operation();

                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is SocketException)
                {
                    lastException = ex;
                    _logger.Warning("ModbusComms", "Write Failed: " + GetSimpleErrorDescription(ex));

                    UpdateConnectionStatus();
                    if (!IsConnected)
                    {
                        HandleDisconnection();
                        throw new ClientDisconnectedException("Lost connection during write operation");
                    }

                    // If we've used all our retries, break out and throw
                    if (retryCount >= _retries)
                    {
                        // If we get here, all retries have been exhausted
                        _logger.Error("ModbusComms", $"{operationName} Failed");

                        // Throw the last exception with additional context
                        throw new IOException(
                            $"{operationName} failed after {_retries} retries. Last error: {lastException?.Message}",
                            lastException);
                    }

                    retryCount++;
                    retryCallback?.Invoke();

                    // Wait before retry
                    await Task.Delay(_delayBetweenRetries, cancellationToken);
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get a simple description of an error
        /// </summary>
        private string GetSimpleErrorDescription(Exception ex) => ex switch
        {
            TimeoutException => "timeout",
            SocketException socketEx => $"network error (code: {socketEx.ErrorCode})",
            IOException => "I/O error",
            ObjectDisposedException => "connection closed",
            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("port") => "port error",
            _ => $"error: {ex.GetType().Name}"
        };

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Debug("ModbusComms", "Disposing ModbusComms");

                    _reconnectTimer?.Stop();
                    _reconnectTimer?.Dispose();

                    _connectionStrategy.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
