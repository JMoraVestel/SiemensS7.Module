using NModbus.IO;
using NModbus;
using System.Reflection;
using System.Text;
using vNode.Sdk.Logger;

public class LoggingModbusTransport : IModbusTransport
{
    private readonly IModbusTransport _innerTransport;
    private readonly ISdkLogger _logger;

    public LoggingModbusTransport(IModbusTransport innerTransport, ISdkLogger logger)
    {
        _innerTransport = innerTransport;
        _logger = logger;
    }

    // Add the missing ReadRequest method
    public byte[] ReadRequest()
    {
        var result = _innerTransport.ReadRequest();
        LogBytes("RX", result);
        return result;
    }

    // Other property implementations
    public int Retries
    {
        get => _innerTransport.Retries;
        set => _innerTransport.Retries = value;
    }

    public int ReadTimeout
    {
        get => _innerTransport.ReadTimeout;
        set => _innerTransport.ReadTimeout = value;
    }

    public int WriteTimeout
    {
        get => _innerTransport.WriteTimeout;
        set => _innerTransport.WriteTimeout = value;
    }

    public int WaitToRetryMilliseconds
    {
        get => _innerTransport.WaitToRetryMilliseconds;
        set => _innerTransport.WaitToRetryMilliseconds = value;
    }

    public uint RetryOnOldResponseThreshold
    {
        get => _innerTransport.RetryOnOldResponseThreshold;
        set => _innerTransport.RetryOnOldResponseThreshold = value;
    }

    public bool SlaveBusyUsesRetryCount
    {
        get => _innerTransport.SlaveBusyUsesRetryCount;
        set => _innerTransport.SlaveBusyUsesRetryCount = value;
    }

    public IStreamResource StreamResource => _innerTransport.StreamResource;

    // Method implementations
    public byte[] BuildMessageFrame(IModbusMessage message)
    {
        return _innerTransport.BuildMessageFrame(message);
    }

    public void Write(IModbusMessage message)
    {
        // Log the frame being sent
        byte[] frame = BuildMessageFrame(message);
        LogBytes("TX", frame);

        _innerTransport.Write(message);
    }

    public T UnicastMessage<T>(IModbusMessage message) where T : IModbusMessage, new()
    {
        // Log the request frame
        byte[] requestFrame = BuildMessageFrame(message);
        LogBytes("TX", requestFrame);

        // Replace the StreamResource with our logging version
        var originalStream = StreamResource;
        var loggingStream = new LoggingStreamResource(originalStream, _logger);

        // Need to use reflection to set the internal stream resource on the actual transport
        SetStreamResourceOnTransport(_innerTransport, loggingStream);

        try
        {
            // Perform the operation
            var response = _innerTransport.UnicastMessage<T>(message);

            // At this point, our logging stream should have captured the response bytes
            return response;
        }
        finally
        {
            // Restore the original stream
            SetStreamResourceOnTransport(_innerTransport, originalStream);
        }
    }

    private void SetStreamResourceOnTransport(IModbusTransport transport, IStreamResource streamResource)
    {
        try
        {
            // Try to get the field that holds the stream resource
            var transportType = transport.GetType();
            var fieldNames = new[] {
                "streamResource", "_streamResource", "stream" };

            foreach (var fieldName in fieldNames)
            {
                var field = transportType.GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(transport, streamResource);
                    return;
                }
            }

            _logger.Warning("LoggingModbusTransport",
                "Could not find StreamResource field on transport");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "LoggingModbusTransport",
                "Error setting StreamResource: " + ex.Message);
        }
    }

    public void Dispose()
    {
        _innerTransport.Dispose();
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

    // Wrapper for IStreamResource to log reads and writes
    private class LoggingStreamResource : IStreamResource, IDisposable
    {
        private readonly IStreamResource _innerResource;
        private readonly ISdkLogger _logger;

        public LoggingStreamResource(IStreamResource innerResource, ISdkLogger logger)
        {
            _innerResource = innerResource;
            _logger = logger;
        }

        public int ReadTimeout
        {
            get => _innerResource.ReadTimeout;
            set => _innerResource.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _innerResource.WriteTimeout;
            set => _innerResource.WriteTimeout = value;
        }

        public int InfiniteTimeout => _innerResource.InfiniteTimeout;

        public void DiscardInBuffer()
        {
            _innerResource.DiscardInBuffer();
        }

        public int Read(byte[] buffer, int offset, int size)
        {
            int bytesRead = _innerResource.Read(buffer, offset, size);
            if (bytesRead > 0)
            {
                byte[] readBytes = new byte[bytesRead];
                Array.Copy(buffer, offset, readBytes, 0, bytesRead);
                LogBytes("RX", readBytes);
            }
            return bytesRead;
        }

        public void Write(byte[] buffer, int offset, int size)
        {
            byte[] writeBytes = new byte[size];
            Array.Copy(buffer, offset, writeBytes, 0, size);
            LogBytes("TX", writeBytes);

            _innerResource.Write(buffer, offset, size);
        }

        // Add the missing Dispose method
        public void Dispose()
        {
            if (_innerResource is IDisposable disposable)
            {
                disposable.Dispose();
            }
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
    }
}
