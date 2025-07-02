using NModbus;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Interface for different Modbus connection strategies
    /// </summary>
    public interface IModbusConnectionStrategy : IDisposable
    {
        /// <summary>
        /// Establishes a connection to the Modbus device
        /// </summary>
        /// <returns>True if connection was successful, otherwise false</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Checks if the connection is currently active
        /// </summary>
        /// <returns>True if connected, otherwise false</returns>
        bool IsConnected();

        /// <summary>
        /// Gets the Modbus master for the connection
        /// </summary>
        IModbusMaster GetModbusMaster();

        /// <summary>
        /// Closes the connection to the Modbus device
        /// </summary>
        void Close();
    }
}
