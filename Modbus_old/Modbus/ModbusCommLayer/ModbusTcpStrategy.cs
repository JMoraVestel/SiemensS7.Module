using System.Net.Sockets;

using ModbusModule.Diagnostics;

using NModbus;

using vNode.Sdk.Logger;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// TCP implementation of Modbus connection strategy (supports both TCP and RTU over TCP)
    /// </summary>
    public class ModbusTcpStrategy : IModbusConnectionStrategy
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _requestTimeout;
        private readonly int _connectTimeoutMs;
        private readonly int _delayBetweenRetries;
        private readonly ISdkLogger _logger;
        private readonly ChannelDiagnostics _channelDiagnostics;
        private readonly bool _useRtuProtocol; // Flag to determine if we use RTU over TCP
        private TcpClient? _client;
        private IModbusMaster? _modbusMaster;
        private bool _disposedValue;

        public ModbusTcpStrategy(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            string host,
            int port,
            int requestTimeout,
            int delayBetweenRetries,
            int connectTimeout,
            bool useRtuProtocol = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(host);
            ArgumentNullException.ThrowIfNull(logger);

            _host = host;
            _port = port;
            _requestTimeout = requestTimeout;
            _delayBetweenRetries = delayBetweenRetries;
            _connectTimeoutMs = connectTimeout;
            _logger = logger;
            _channelDiagnostics = channelDiagnostics;
            _useRtuProtocol = useRtuProtocol;

            string protocolType = _useRtuProtocol ? "TCP RTU" : "TCP";
            _logger.Information("ModbusTcpStrategy", $"Initializing Modbus {protocolType} strategy for {host}:{port}");
        }

        /// <summary>
        /// Establish a connection to the Modbus TCP server
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (_client != null && _client.Connected)
            {
                string protocolType = _useRtuProtocol ? "TCP RTU" : "TCP";
                _logger.Debug("ModbusTcpStrategy", "Disconnecting existing connection...");
                _client.Close();
                _logger.Debug("ModbusTcpStrategy", $"{protocolType} connection closed");
            }

            string protocolDesc = _useRtuProtocol ? "RTU over TCP" : "TCP";
            _logger.Debug("ModbusTcpStrategy", $"Connecting to {_host}:{_port} ({protocolDesc})...");
            _client = new LoggingTcpClient(_logger);
            try
            {
                // Create a cancellation token source with the specified timeout
                using (var cts = new CancellationTokenSource(_connectTimeoutMs))
                {
                    // Start the connection task
                    var connectTask = _client.ConnectAsync(_host, _port);

                    // Wait for the connection to complete or timeout
                    await connectTask.WaitAsync(cts.Token);
                }
            }
            catch (SocketException ex)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error(ex, "ModbusTcpStrategy", $"Error connecting to {_host}:{_port}: {ex.Message}");
                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("ModbusTcpStrategy", $"{_connectTimeoutMs}ms timeout connecting to {_host}:{_port}");
                _channelDiagnostics.ConnectionFailed();
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ModbusTcpStrategy", $"Error connecting to {_host}:{_port}: {ex.Message}");
                _channelDiagnostics.ConnectionFailed();
                return false;
            }

            if (!_client.Connected)
            {
                _channelDiagnostics.ConnectionFailed();
                _logger.Error("ModbusTcpStrategy", $"Error connecting to modbus host {_host} on port {_port}: Connected status is still FALSE");
                return false;
            }

            string successProtocol = _useRtuProtocol ? "TCP RTU" : "TCP";
            _logger.Debug("ModbusTcpStrategy", $"Modbus {successProtocol} connection success to host {_host} on port {_port}");
            _channelDiagnostics.ConnectionSuccess();

            var factory = new NModbus.ModbusFactory();

            // Create the appropriate master based on protocol type
            if (_useRtuProtocol)
            {
                // For RTU over TCP, we need to create an RTU master using a stream adapter
                // Create a network stream adapter (similar to SerialPortAdapter)
                var networkStream = _client.GetStream();
                var streamAdapter = new NetworkStreamAdapter(networkStream);
                _modbusMaster = factory.CreateRtuMaster(streamAdapter);
                _logger.Debug("ModbusTcpStrategy", "Created RTU master over TCP connection");
            }
            else
            {
                // For standard Modbus TCP, use the TcpClient directly
                _modbusMaster = factory.CreateMaster(_client);
                _logger.Debug("ModbusTcpStrategy", "Created standard TCP master");
            }

            _modbusMaster.Transport.ReadTimeout = _requestTimeout;
            _modbusMaster.Transport.WriteTimeout = _requestTimeout;
            _modbusMaster.Transport.Retries = 0; // We handle retries at a higher level
            _modbusMaster.Transport.WaitToRetryMilliseconds = _delayBetweenRetries;

            return true;
        }

        /// <summary>
        /// Check if the TCP connection is active
        /// </summary>
        public bool IsConnected()
        {
            return _modbusMaster != null && _client != null && _client.Connected;
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
        /// Close the TCP connection
        /// </summary>
        public void Close()
        {
            if (_client != null && _client.Connected)
            {
                _logger.Debug("ModbusTcpStrategy", "Closing TCP connection");
                _client.Close();
            }
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Debug("ModbusTcpStrategy", "Disposing TCP connection resources");

                    if (_modbusMaster != null)
                    {
                        _modbusMaster.Dispose();
                        _modbusMaster = null;
                    }

                    if (_client != null)
                    {
                        if (_client.Connected)
                        {
                            _client.Close();
                        }
                        _client.Dispose();
                        _client = null;
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
