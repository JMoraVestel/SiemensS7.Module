using System.Net.Sockets;

using NModbus.IO;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Adapter to wrap NetworkStream for use with NModbus RTU over TCP
    /// </summary>
    public class NetworkStreamAdapter : IStreamResource
    {
        private readonly NetworkStream _networkStream;

        public NetworkStreamAdapter(NetworkStream networkStream)
        {
            _networkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
        }

        public int InfiniteTimeout => System.Threading.Timeout.Infinite;

        public int ReadTimeout
        {
            get => _networkStream.ReadTimeout;
            set => _networkStream.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _networkStream.WriteTimeout;
            set => _networkStream.WriteTimeout = value;
        }

        public void DiscardInBuffer()
        {
            // For network streams, we can't directly discard the buffer like serial ports
            // This is typically not needed for TCP connections, but we implement it for compatibility
            if (_networkStream.DataAvailable)
            {
                byte[] buffer = new byte[1024];
                while (_networkStream.DataAvailable)
                {
                    _networkStream.Read(buffer, 0, buffer.Length);
                }
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _networkStream.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _networkStream.Write(buffer, offset, count);
        }

        public void Dispose()
        {
            _networkStream?.Dispose();
        }
    }
}
