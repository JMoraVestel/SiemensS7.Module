using System.IO.Ports;

using ModbusModule.Diagnostics;

using NModbus;
using NModbus.Serial;

using vNode.Sdk.Logger;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Serial implementation of Modbus connection strategy
    /// </summary>
    public class ModbusSerialStrategy : IModbusConnectionStrategy
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;
        private readonly int _requestTimeout;
        private readonly int _connectTimeoutMs;
        private readonly int _delayBetweenRetries;
        private readonly ISdkLogger _logger;
        private readonly ChannelDiagnostics _channelDiagnostics;
        private SerialPort? _serialPort;
        private IModbusMaster? _modbusMaster;
        private bool _disposedValue;
        private readonly bool _isAscii; // Determines if we use Modbus ASCII instead of RTU
        private readonly int _bufferSize;

        public ModbusSerialStrategy(
        ISdkLogger logger,
        ChannelDiagnostics channelDiagnostics,
        string portName,
        int baudRate,
        Parity parity,
        int dataBits,
        StopBits stopBits,
        int bufferSize,
        int requestTimeout,
        int delayBetweenRetries,
        int connectTimeout,
        bool isAscii = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(portName);
            ArgumentNullException.ThrowIfNull(logger);

            _portName = portName;
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _bufferSize = bufferSize;
            _requestTimeout = requestTimeout;
            _delayBetweenRetries = delayBetweenRetries;
            _connectTimeoutMs = connectTimeout;
            _logger = logger;
            _channelDiagnostics = channelDiagnostics;
            _isAscii = isAscii;

            string mode = _isAscii ? "ASCII" : "RTU";
            _logger.Information("ModbusSerialStrategy", $"Initializing Modbus Serial {mode} strategy for port {portName}");
        }

        /// <summary>
        /// Establish a connection to the Modbus device over serial
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _logger.Debug("ModbusSerialStrategy", "Closing existing serial connection...");
                _serialPort.Close();
                _logger.Debug("ModbusSerialStrategy", "Serial port closed");
            }

            _logger.Debug("ModbusSerialStrategy", $"Opening serial port {_portName}...");

            try
            {
                // Create and configure the serial port
                _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
                {
                    ReadTimeout = _requestTimeout,
                    WriteTimeout = _requestTimeout,
                    ReadBufferSize = _bufferSize,
                    WriteBufferSize = _bufferSize
                };

                // Create a cancellation token source with the specified timeout
                using (var cts = new CancellationTokenSource(_connectTimeoutMs))
                {
                    // Open the serial port
                    await Task.Run(() => _serialPort.Open(), cts.Token);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error(ex, "ModbusSerialStrategy", $"Access denied to serial port {_portName}: {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error(ex, "ModbusSerialStrategy", $"Invalid serial port parameters for {_portName}: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error(ex, "ModbusSerialStrategy", $"I/O error accessing serial port {_portName}: {ex.Message}");
                return false;
            }
            catch (OperationCanceledException)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Warning("ModbusSerialStrategy", $"{_connectTimeoutMs}ms timeout connecting to serial port {_portName}");
                return false;
            }
            catch (Exception ex)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error(ex, "ModbusSerialStrategy", $"Error opening serial port {_portName}: {ex.Message}");
                return false;
            }

            if (_serialPort == null || !_serialPort.IsOpen)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error("ModbusSerialStrategy", $"Error opening serial port {_portName}: Port is not open");
                return false;
            }

            _logger.Debug("ModbusSerialStrategy", $"Serial port {_portName} opened successfully");
            _channelDiagnostics.ConnectionSuccess();

            // Create the Modbus master
            var factory = new NModbus.ModbusFactory();
            var adapter = new SerialPortAdapter(_serialPort);

            // Create either ASCII or RTU master based on configuration
            if (_isAscii)
            {
                _modbusMaster = factory.CreateAsciiMaster(adapter);
                _logger.Debug("ModbusSerialStrategy", "Created Modbus ASCII master");
            }
            else
            {
                _modbusMaster = factory.CreateRtuMaster(adapter);
                _logger.Debug("ModbusSerialStrategy", "Created Modbus RTU master");
            }

            // Configure the transport
            _modbusMaster.Transport.ReadTimeout = _requestTimeout;
            _modbusMaster.Transport.WriteTimeout = _requestTimeout;
            _modbusMaster.Transport.Retries = 0; // We handle retries at a higher level
            _modbusMaster.Transport.WaitToRetryMilliseconds = _delayBetweenRetries;
            
            return true;
        }

        /// <summary>
        /// Check if the serial connection is active
        /// </summary>
        public bool IsConnected()
        {
            return _modbusMaster != null && _serialPort != null && _serialPort.IsOpen;
        }

        /// <summary>
        /// Get the Modbus master for this connection
        /// </summary>
        public IModbusMaster GetModbusMaster()
        {
            if (_modbusMaster == null)
            {
                throw new InvalidOperationException("Modbus master is not initialized. Connect first.");
            }
            return _modbusMaster;
        }

        /// <summary>
        /// Close the serial connection
        /// </summary>
        public void Close()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _logger.Debug("ModbusSerialStrategy", "Closing serial port");
                _serialPort.Close();
            }
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Debug("ModbusSerialStrategy", "Disposing serial connection resources");

                    if (_modbusMaster != null)
                    {
                        _modbusMaster.Dispose();
                        _modbusMaster = null;
                    }

                    if (_serialPort != null)
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
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
