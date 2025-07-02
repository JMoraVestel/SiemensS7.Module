using System.ComponentModel;

namespace ModbusModule.ModbusCommLayer
{
    /// <summary>
    /// Interface for all Modbus communication operations
    /// </summary>
    public interface IModbusConnection : IDisposable, INotifyPropertyChanged
    {
        /// <summary>
        /// Indicates whether the connection is currently active
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Establish a connection to the Modbus device
        /// </summary>
        void Connect();

        /// <summary>
        /// Read coils from the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="registerAddress">The register address (1-based)</param>
        /// <param name="numberOfPoints">Number of points to read</param>
        /// <param name="retryCallback">Optional callback for retries</param>
        /// <returns>Array of boolean values</returns>
        Task<bool[]> ReadCoilsAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null);

        /// <summary>
        /// Read discrete inputs from the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="registerAddress">The register address (1-based)</param>
        /// <param name="numberOfPoints">Number of points to read</param>
        /// <param name="retryCallback">Optional callback for retries</param>
        /// <returns>Array of boolean values</returns>
        Task<bool[]> ReadDiscreteInputsAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null);

        /// <summary>
        /// Read input registers from the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="registerAddress">The register address (1-based)</param>
        /// <param name="numberOfPoints">Number of points to read</param>
        /// <param name="retryCallback">Optional callback for retries</param>
        /// <returns>Array of register values</returns>
        Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null);

        /// <summary>
        /// Read holding registers from the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="registerAddress">The register address (1-based)</param>
        /// <param name="numberOfPoints">Number of points to read</param>
        /// <param name="retryCallback">Optional callback for retries</param>
        /// <returns>Array of register values</returns>
        Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, uint registerAddress, ushort numberOfPoints, Action? retryCallback = null);

        /// <summary>
        /// Write coils to the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="coilAddress">The coil address (1-based)</param>
        /// <param name="values">Values to write</param>
        /// <param name="timeoutMs">Operation timeout in milliseconds</param>
        Task WriteOutputCoilsAsync(byte slaveAddress, uint coilAddress, bool[] values, int timeoutMs = 2000);

        /// <summary>
        /// Write holding registers to the Modbus device
        /// </summary>
        /// <param name="slaveAddress">The slave address</param>
        /// <param name="useFunction6">Use function code 6 for single register writes</param>
        /// <param name="registerAddress">The register address (1-based)</param>
        /// <param name="values">Values to write</param>
        /// <param name="timeoutMs">Operation timeout in milliseconds</param>
        Task WriteHoldingRegistersAsync(byte slaveAddress, bool useFunction6, uint registerAddress, ushort[] values, int timeoutMs = 2000);
    }
}
