using System.ComponentModel;

using ModbusModule.ChannelConfig;
using ModbusModule.Diagnostics;
using ModbusModule.TagConfig;
using ModbusModule.Helper;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using ModbusModule.ModbusCommLayer;

using static ModbusModule.Helper.ModbusHelper;
using ModbusModule.Scheduler;
using System.IO.Ports;

namespace ModbusModule.TagReader
{
    public class ModbusTagReader : IDisposable, INotifyPropertyChanged
    {
        private readonly ModbusChannelConfig _channelConfig;
        private readonly ISdkLogger _logger;
        private ModbusParser _parser;
        private IModbusConnection _modbusConnection;
        private readonly SemaphoreSlim _modbusLock = new SemaphoreSlim(1, 1);
        private volatile bool _writePending = false;
        private bool _disposedValue;
        private readonly ChannelDiagnostics _channelDiagnostics;
        private readonly DeviceDemotionManager _demotionManager;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DeviceDemotionManager.DeviceDemotionEventArgs>? DeviceDemotionStatusChanged;
        private CancellationTokenSource _readCancellationTokenSource = new CancellationTokenSource();

        private void onPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnDeviceDemotionStatusChanged(object? sender, DeviceDemotionManager.DeviceDemotionEventArgs e)
        {            
            // Forward the event
            DeviceDemotionStatusChanged?.Invoke(this, e);
        }

        public ModbusTagReader(ModbusChannelConfig channelConfig, ChannelDiagnostics channelDiagnostics, ISdkLogger logger)
        {
            _channelConfig = channelConfig;
            _channelDiagnostics = channelDiagnostics;
            _logger = logger;

            _logger.Information("ModbusTagReader", "Initializing Modbus Reader...");
            if (!tryInitializeModbusReader())
            {
                _logger.Error("ModbusTagReader", "Error initializing Modbus reader");
            }

            _logger.Information("ModbusTagReader", "Initializing Modbus Read parser...");
            initializeModbusParser();

            _demotionManager = new DeviceDemotionManager(_logger);
            _demotionManager.DeviceDemotionStatusChanged += OnDeviceDemotionStatusChanged;

        }

        public void CancelCurrentReads()
        {
            try
            {
                // Cancel existing token
                if (!_readCancellationTokenSource.IsCancellationRequested)
                {
                    _logger.Debug("ModbusTagReader", "Cancelling current read operations");
                    _readCancellationTokenSource.Cancel();

                    // Create new token for future operations
                    _readCancellationTokenSource = new CancellationTokenSource();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusTagReader", "Error cancelling read operations");
            }
        }

        public async Task<bool> WriteTagAsync(ModbusTagWrapper tag, object value)
        {

            if (tag.Tag.ClientAccess == ClientAccessOptions.ReadOnly)
            {
                throw new ArgumentException("Tag is read-only");
            }

            if (tag.Config.IsReadOnly)
            {
                throw new ArgumentException($"Modbus address {tag.Config.RegisterAddress} is read-only");
            }

            var deviceConfig = _channelConfig.Devices[tag.Config.DeviceId];
            
            if (deviceConfig == null) //device to read is not configured in this channel
            {
                _logger.Error("Modbus", $"Device ID {tag.Config.DeviceId} is not configured in this channel.");
                throw new ArgumentOutOfRangeException("Invalid Device Id {tag.Config.DeviceId}");

            }

            var modbusType = tag.Config.RegisterAddress.Type;
            ushort[] registers = Array.Empty<ushort>();
            bool coil = false;
            bool writeToRegister = modbusType == ModbusType.InputRegister || modbusType == ModbusType.HoldingRegister;

            if (writeToRegister)
            {
                if (!_parser.TryGetRegisters(tag, value, deviceConfig.SwapConfig, out registers))
                {
                    throw new InvalidDataException("Unable to parse value");
                }
            }
            else // is a coil
            {
                if (!ModbusDataConverter.TryParseBool(value, out coil))
                {
                    throw new InvalidDataException("Unable to parse value into a boolean");
                }
            }


            _writePending = true;
            await _modbusLock.WaitAsync();
            try
            {
                //var rawAddress = GetRawAddress(tag.Config.RegisterAddress);
                if (writeToRegister)
                {
                    await _modbusConnection.WriteHoldingRegistersAsync(deviceConfig.ModbusSlaveId,
                        deviceConfig.EnableModbusFunction6, (tag.Config.RegisterAddress.Offset), registers);
                }
                else
                {
                    await _modbusConnection.WriteOutputCoilsAsync(deviceConfig.ModbusSlaveId,
                        (tag.Config.RegisterAddress.Offset), new bool[] { coil });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusTagReader", $"Error writing to Modbus device: {ex.Message}");

            }

            finally
            {
                _modbusLock.Release();
                _writePending = false;
            }

            return true;
        }

        public void ReadBatch(TagReadBatch batch, ModbusDeviceConfig deviceConfig,
    Action<TagReadResult> callback)
        {
            ArgumentNullException.ThrowIfNull(nameof(batch));
            ArgumentNullException.ThrowIfNull(nameof(deviceConfig));
            ArgumentNullException.ThrowIfNull(nameof(callback));

            // Capture the cancellation token at the start to avoid disposed token issues
            var cancellationToken = _readCancellationTokenSource.Token;

            if (_modbusConnection == null || _modbusConnection.IsConnected == false)
            {
                _logger.Error("ModbusTagReader",
                    "Reader is not connected, batch read will return as failed for all tags");
                callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.CommsError, batch.ReadItems.Select(p => p).ToList()));
                return;
            }

            if (batch.ModbusType == ModbusType.Unknown)
            {
                _logger.Error("ModbusTagReader",
                    "Cannot read batch: modbus type is 'unknown'.");
                callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.OtherError, batch.ReadItems.Select(p => p).ToList()));
                return;
            }

            ushort blockSize = batch.ModbusType switch
            {
                ModbusType.HoldingRegister => deviceConfig.BlockSize.HoldingRegisters,
                ModbusType.InputRegister => deviceConfig.BlockSize.InputRegisters,
                ModbusType.InputCoil => deviceConfig.BlockSize.InputCoils,
                ModbusType.OutputCoil => deviceConfig.BlockSize.OutputCoils,
                _ => 64
            };

            // Cap the last address to the maximum of 65536
            uint lastAddress = Math.Min(batch.LastAddress, 65536);

            // Calculate address count and block count based on the capped last address
            var addressCountToRead = lastAddress - batch.FirstAddress + 1;
            ushort blockCount = (ushort) ((addressCountToRead + blockSize - 1) / blockSize);

            // Read blocks
            uint currentAddress = batch.FirstAddress;

            for (int i = 0; i < blockCount; i++)
            {
                // Check for cancellation using the local token reference
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Trace("ModbusTagReader", $"Batch read for device {deviceConfig.DeviceId} was cancelled");
                    break;
                }

                if (_demotionManager.IsDemoted(deviceConfig.DeviceId))
                {
                    _logger.Trace("ModbusTagReader", $"Device {deviceConfig.DeviceId} is demoted, skipping read.");
                    break;
                }

                uint startAddress = currentAddress;
                uint endAddress = Math.Min(currentAddress + blockSize - 1, lastAddress);

                var block = batch.GetBlock(startAddress, endAddress);
                if (block.Count != 0)
                {
                    // Pass the cancellation token to readBlock
                    readBlock(batch.ModbusType, block, deviceConfig, callback, cancellationToken).Wait(cancellationToken);
                }

                // If we've reached the last address, break out of the loop
                if (endAddress >= lastAddress)
                {
                    break;
                }

                currentAddress = endAddress + 1;
            }
        }

        private async Task readBlock(ModbusType modbusType, List<TagReadBatchItem> itemsToRead, ModbusDeviceConfig deviceConfig,
    Action<TagReadResult> callback, CancellationToken cancellationToken = default)
        {
            // Check for cancellation at the start
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Trace("ModbusTagReader", "Read block cancelled before execution");
                return;
            }

            bool[]? coilValues = null;
            ushort[] readValues = [];
            uint firstAddress = (uint) (itemsToRead.First().Address.Offset);
            uint lastAddress = itemsToRead.Last().Address.Offset + itemsToRead.Last().Tag.Config.GetSize() - 1;
            ushort addressCount = (ushort) (lastAddress - firstAddress + 1);

            Action retryCallback = () => _channelDiagnostics.ReadRetry(deviceConfig.DeviceId);

            try
            {
                // Check for cancellation before waiting for write operations
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Trace("ModbusTagReader", "Read block cancelled during write wait");
                    return;
                }

                // Check for write operations periodically with cancellation support
                while (_writePending && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10, cancellationToken); // Short delay to let writes proceed
                }

                // Final cancellation check before acquiring lock
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Trace("ModbusTagReader", "Read block cancelled before acquiring lock");
                    return;
                }

                await _modbusLock.WaitAsync(cancellationToken);
                _logger.Debug($"ModbusTagReader", $"Reading block: [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");

                try
                {
                    // Check for cancellation after acquiring lock
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Trace("ModbusTagReader", "Read block cancelled after acquiring lock");
                        return;
                    }

                    switch (modbusType)
                    {
                        case ModbusType.OutputCoil:
                            coilValues = _modbusConnection.ReadCoilsAsync(deviceConfig.ModbusSlaveId, firstAddress,
                                addressCount, retryCallback).Result.ToArray();
                            _demotionManager.ReadSuccess(deviceConfig.DeviceId);
                            break;
                        case ModbusType.InputCoil:
                            coilValues = _modbusConnection.ReadDiscreteInputsAsync(deviceConfig.ModbusSlaveId,
                                firstAddress, addressCount, retryCallback).Result.ToArray();
                            _demotionManager.ReadSuccess(deviceConfig.DeviceId);
                            break;
                        case ModbusType.InputRegister:
                            readValues = _modbusConnection.ReadInputRegistersAsync(deviceConfig.ModbusSlaveId,
                                firstAddress, addressCount, retryCallback).Result.ToArray();
                            _demotionManager.ReadSuccess(deviceConfig.DeviceId);
                            break;
                        case ModbusType.HoldingRegister:
                            readValues = _modbusConnection.ReadHoldingRegistersAsync(deviceConfig.ModbusSlaveId,
                                firstAddress, addressCount, retryCallback).Result.ToArray();
                            _demotionManager.ReadSuccess(deviceConfig.DeviceId);
                            break;
                        default:
                            _logger.Error("ModbusTagReader",
                                "Block read error: unknown modbus type: " + modbusType.ToString());
                            callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.OtherError, itemsToRead.Select(p => p).ToList()));
                            return;
                    }
                }
                finally
                {
                    _modbusLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Trace("ModbusTagReader", $"Read block operation was cancelled: [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");
                return; // Don't invoke callback for cancelled operations
            }
            catch (ObjectDisposedException)
            {
                _logger.Debug("ModbusTagReader", $"Read block operation aborted due to disposal: [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");
                return; // Don't invoke callback for disposed objects
            }
            catch (Exception ex)
            {
                if (ex is ClientDisconnectedException || (ex is AggregateException && ex.InnerException is ClientDisconnectedException))
                {
                    // In this case no need to signal Modbus channel since disconnected event already went through.
                    _logger.Error("ModbusTagReader", $"Block read error (Client disconnected): [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");
                    return;
                }
                if (ex is IOException || ex is AggregateException && ex.InnerException is IOException)
                {
                    _logger.Warning($"ModbusTagReader", $"Block read error ({ex.Message}): [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");
                }
                else
                {
                    _logger.Error(ex, "ModbusTagReader", $"Unexpected error reading Modbus block: [{_channelConfig.NodeName}/{deviceConfig.DeviceId}/{firstAddress}:{addressCount}]");
                }
                _demotionManager.ReadFail(deviceConfig.DeviceId, deviceConfig.AutoDemotionConfig);
                callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.CommsError, itemsToRead.Select(p => p).ToList()));
                return;
            }

            // Check for cancellation before processing results
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Trace("ModbusTagReader", "Read block cancelled before processing results");
                return;
            }

            itemsToRead.ForEach(p => p.ActualReadTime = DateTime.UtcNow);

            // Parse results
            if (modbusType == ModbusType.InputRegister ||
                modbusType == ModbusType.HoldingRegister)
            {
                try
                {
                    List<TagReadResultItem> parseResults =
                    _parser.ParseRegisters(itemsToRead, firstAddress, lastAddress, readValues, deviceConfig.SwapConfig);
                    var parseOk = parseResults.Where(p => p.ResultCode == TagReadResult.TagReadResultType.Success).ToList();
                    var parseFailed = parseResults.Where(p => p.ResultCode == TagReadResult.TagReadResultType.ParseError).ToList();
                    if (parseOk.Count > 0)
                        callback.Invoke(TagReadResult.CreateSuccess(parseOk));
                    if (parseFailed.Count > 0)
                        callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.ParseError, parseFailed.Select(p => p.BatchItem).ToList()));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ModbusTagReader", "Block read error: Error parsing register values");
                    callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.ParseError, itemsToRead.Select(p => p).ToList()));
                    return;
                }
            }
            else //Coils
            {
                try
                {
                    var parseResults = _parser.parseCoils(itemsToRead, coilValues!);
                    callback.Invoke(TagReadResult.CreateSuccess(parseResults));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ModbusTagReader", "Block read error: Error parsing coil values");
                    callback.Invoke(TagReadResult.CreateFailed(TagReadResult.TagReadResultType.ParseError, itemsToRead.Select(p => p).ToList()));
                    return;
                }
            }
        }

        private void initializeModbusParser()
        {
            _parser = new ModbusParser(_logger);
        }

        private bool tryInitializeModbusReader()
        {
            if (_channelConfig == null)
            {
                _logger.Error("ModbusTagReader", "Cannot initialize Modbus reader if configuration is not set");
                return false;
            }

            if (_modbusConnection != null)
            {
                _modbusConnection.Dispose();
            }
            
            if (_channelConfig.Connection.ActiveConfig.ConnectionType == ModbusConnectionTypeType.Tcp)
            {
                if (_channelConfig.Connection.ModbusMode == ModbusModeType.Tcp ||
    _channelConfig.Connection.ModbusMode == ModbusModeType.TcpRtuEncapsulated)
                {
                    var tcpConfig = _channelConfig.Connection.TcpConfig
                                    ?? throw new InvalidCastException("Invalid TCP protocol configuration");

                    // Determine connection type and logging message
                    bool isRtuEncapsulated = _channelConfig.Connection.ModbusMode == ModbusModeType.TcpRtuEncapsulated;
                    string connectionType = isRtuEncapsulated ? "TCP RTU" : "TCP";

                    _logger.Information("ModbusTagReader", $"Creating {connectionType} Modbus reader");

                    try
                    {
                        // Create appropriate connection based on mode
                        _modbusConnection = isRtuEncapsulated
                            ? ModbusConnectionFactory.CreateTcpRtu(_logger, _channelDiagnostics, tcpConfig.Host, tcpConfig.Port,
                                requestTimeout: _channelConfig.Timing.RequestTimeout,
                                retries: _channelConfig.Timing.RetryAttempts,
                                delayBetweenRetries: _channelConfig.Timing.InterRetryDelay,
                                connectTimeout: _channelConfig.Timing.ConnectTimeout)
                            : ModbusConnectionFactory.CreateTcp(_logger, _channelDiagnostics, tcpConfig.Host, tcpConfig.Port,
                                requestTimeout: _channelConfig.Timing.RequestTimeout,
                                retries: _channelConfig.Timing.RetryAttempts,
                                delayBetweenRetries: _channelConfig.Timing.InterRetryDelay,
                                connectTimeout: _channelConfig.Timing.ConnectTimeout);

                        _modbusConnection.PropertyChanged += _reader_PropertyChanged;
                        _modbusConnection.Connect();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("ModbusTagReader", $"Error creating {connectionType} Modbus reader: " + ex.Message);
                        return false;
                    }
                }
                else
                {
                    _logger.Error("ModbusTagReader", "Unsupported modbus mode: " + _channelConfig.Connection.ModbusMode);
                    return false;
                }
            }
            
            else if (_channelConfig.Connection.ActiveConfig.ConnectionType == ModbusConnectionTypeType.Serial)
            {
                var serialConfig = _channelConfig.Connection.SerialConfig
                                ?? throw new InvalidCastException("Invalid Serial protocol configuration");

                _logger.Information("ModbusTagReader", "Creating Serial Modbus reader");
                try
                {
                    // Map enum values from our config to System.IO.Ports enums
                    Parity parity = (Parity) serialConfig.PortSettings.Parity;
                    StopBits stopBits = (StopBits) serialConfig.PortSettings.StopBits;

                    // Determine if we should use ASCII or RTU based on ModbusMode
                    bool isAscii = _channelConfig.Connection.ModbusMode != ModbusModeType.Rtu;

                    if (isAscii)
                    {
                        _modbusConnection = ModbusConnectionFactory.CreateAscii(
                            _logger,
                            _channelDiagnostics,
                            serialConfig.PortName,
                            baudRate: serialConfig.PortSettings.BaudRate,
                            parity: parity,
                            dataBits: serialConfig.PortSettings.DataBits,
                            stopBits: stopBits,
                            bufferSize: serialConfig.PortSettings.BufferSize,
                            requestTimeout: _channelConfig.Timing.RequestTimeout,
                            retries: _channelConfig.Timing.RetryAttempts,
                            delayBetweenRetries: _channelConfig.Timing.InterRetryDelay,
                            connectTimeout: _channelConfig.Timing.ConnectTimeout
                        );
                    }
                    else
                    {
                        _modbusConnection = ModbusConnectionFactory.CreateRtu(
                            _logger,
                            _channelDiagnostics,
                            serialConfig.PortName,
                            baudRate: serialConfig.PortSettings.BaudRate,
                            parity: parity,
                            dataBits: serialConfig.PortSettings.DataBits,
                            stopBits: stopBits,
                            bufferSize: serialConfig.PortSettings.BufferSize,
                            requestTimeout: _channelConfig.Timing.RequestTimeout,
                            retries: _channelConfig.Timing.RetryAttempts,
                            delayBetweenRetries: _channelConfig.Timing.InterRetryDelay,
                            connectTimeout: _channelConfig.Timing.ConnectTimeout
                        );
                    }

                    _modbusConnection.PropertyChanged += _reader_PropertyChanged;
                    _modbusConnection.Connect();
                }
                catch (Exception ex)
                {
                    _logger.Error("ModbusTagReader", "Error creating Serial Modbus reader: " + ex.Message);
                    return false;
                }
            }
            else
            {
                _logger.Error("ModbusTagReader", "Unsupported connection type: " + _channelConfig.Connection.ActiveConfig.ConnectionType);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Indicates if the reader is connected to the Modbus device.
        /// </summary>
        public bool IsConnected
        {
            get => _modbusConnection.IsConnected;
        }

        private void _reader_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModbusComms.IsConnected))
            {
                // Notify that the connection status has changed
                onPropertyChanged(nameof(IsConnected));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Debug("ModbusTagReader", "Disposing Modbus comm layer");
                    _modbusConnection?.Dispose();
                    _demotionManager.DeviceDemotionStatusChanged -= OnDeviceDemotionStatusChanged;

                    // Dispose the cancellation token source
                    _readCancellationTokenSource?.Dispose();
                }

                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ModbusTagReader()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
