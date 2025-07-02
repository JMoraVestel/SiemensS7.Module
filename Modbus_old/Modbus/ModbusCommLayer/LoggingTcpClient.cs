using NModbus.IO;
using System.Net.Sockets;
using System.Text;
using vNode.Sdk.Logger;

public class LoggingTcpClient : TcpClient, IStreamResource
{
    private readonly ISdkLogger _logger;
    private LoggingStream _loggingStream;

    public LoggingTcpClient(ISdkLogger logger)
    {
        _logger = logger;
    }

    // IStreamResource implementation
    public int InfiniteTimeout => System.Threading.Timeout.Infinite;

    // Forward timeouts to the underlying socket
    public int ReadTimeout
    {
        get => Client.ReceiveTimeout;
        set => Client.ReceiveTimeout = value;
    }

    public int WriteTimeout
    {
        get => Client.SendTimeout;
        set => Client.SendTimeout = value;
    }

    // This is the key method NModbus uses for IStreamResource
    public Stream GetStream()
    {
        if (_loggingStream == null)
        {
            var baseStream = base.GetStream();
            _loggingStream = new LoggingStream(baseStream, _logger);
        }
        return _loggingStream;
    }

    public void DiscardInBuffer()
    {
        // Implement if needed - often NModbus doesn't use this
    }

    // These methods are likely used by NModbus instead of calling GetStream directly
    public int Read(byte[] buffer, int offset, int size)
    {
        var stream = GetStream();
        return stream.Read(buffer, offset, size);
    }

    public void Write(byte[] buffer, int offset, int size)
    {
        var stream = GetStream();
        stream.Write(buffer, offset, size);
    }

    private class LoggingStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly ISdkLogger _logger;

        public LoggingStream(Stream baseStream, ISdkLogger logger)
        {
            _baseStream = baseStream;
            _logger = logger;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _baseStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                byte[] readBytes = new byte[bytesRead];
                Array.Copy(buffer, offset, readBytes, 0, bytesRead);
                LogBytes("RX", readBytes);
            }
            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Log before writing
            byte[] writeBytes = new byte[count];
            Array.Copy(buffer, offset, writeBytes, 0, count);
            LogBytes("TX", writeBytes);

            _baseStream.Write(buffer, offset, count);
        }

        private void LogBytes(string direction, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            StringBuilder sb = new StringBuilder();
            sb.Append($"{direction}: ");

            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    sb.Append('-');
                sb.Append(bytes[i].ToString("X2"));
            }

            _logger.Trace("TransportTcp", sb.ToString());
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);
    }
}
