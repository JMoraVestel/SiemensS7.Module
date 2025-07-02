namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Exception thrown when a Modbus client is disconnected
    /// </summary>
    public class ClientDisconnectedException : IOException
    {
        public ClientDisconnectedException() : base("Modbus client is disconnected")
        {
        }

        public ClientDisconnectedException(string message) : base(message)
        {
        }

        public ClientDisconnectedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
