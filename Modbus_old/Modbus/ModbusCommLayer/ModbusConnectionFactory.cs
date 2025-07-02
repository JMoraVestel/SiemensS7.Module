using System.IO.Ports;

using ModbusModule.Diagnostics;

using vNode.Sdk.Logger;

namespace ModbusModule.ModbusCommLayer
{
    public static class ModbusConnectionFactory
    {
        /// <summary>
        /// Create a TCP Modbus connection
        /// </summary>
        public static IModbusConnection CreateTcp(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            string host,
            int port,
            int requestTimeout,
            byte retries,
            int delayBetweenRetries,
            int connectTimeout)
        {
            var strategy = new ModbusTcpStrategy(
                logger,
                channelDiagnostics,
                host,
                port,
                requestTimeout,
                delayBetweenRetries,
                connectTimeout);

            return new ModbusComms(
                logger,
                channelDiagnostics,
                strategy,
                retries,
                delayBetweenRetries,
                connectTimeout);
        }

        /// <summary>
        /// Create a TCP RTU Modbus connection (RTU over TCP/IP)
        /// </summary>
        public static IModbusConnection CreateTcpRtu(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            string host,
            int port,
            int requestTimeout,
            byte retries,
            int delayBetweenRetries,
            int connectTimeout)
        {
            var strategy = new ModbusTcpStrategy(
                logger,
                channelDiagnostics,
                host,
                port,
                requestTimeout,
                delayBetweenRetries,
                connectTimeout,
                useRtuProtocol: true); // Use RTU protocol over TCP

            return new ModbusComms(
                logger,
                channelDiagnostics,
                strategy,
                retries,
                delayBetweenRetries,
                connectTimeout);
        }

        /// <summary>
        /// Create a serial RTU Modbus connection
        /// </summary>
        public static IModbusConnection CreateRtu(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            string portName,
            int baudRate,
            Parity parity,
            int dataBits,
            StopBits stopBits,
            int bufferSize,
            int requestTimeout,
            byte retries,
            int delayBetweenRetries,
            int connectTimeout)
        {
            var strategy = new ModbusSerialStrategy(
                logger,
                channelDiagnostics,
                portName,
                baudRate,
                parity,
                dataBits,
                stopBits,
                bufferSize,
                requestTimeout,
                delayBetweenRetries,
                connectTimeout,
                isAscii: false);

            return new ModbusComms(
                logger,
                channelDiagnostics,
                strategy,
                retries,
                delayBetweenRetries,
                connectTimeout);
        }

        /// <summary>
        /// Create a serial ASCII Modbus connection
        /// </summary>
        public static IModbusConnection CreateAscii(
            ISdkLogger logger,
            ChannelDiagnostics channelDiagnostics,
            string portName,
            int baudRate,
            Parity parity,
            int dataBits,
            StopBits stopBits,
            int bufferSize,
            int requestTimeout,
            byte retries,
            int delayBetweenRetries,
            int connectTimeout)
        {
            var strategy = new ModbusSerialStrategy(
                logger,
                channelDiagnostics,
                portName,
                baudRate,
                parity,
                dataBits,
                stopBits,
                bufferSize,
                requestTimeout,
                delayBetweenRetries,
                connectTimeout,
                isAscii: true);

            return new ModbusComms(
                logger,
                channelDiagnostics,
                strategy,
                retries,
                delayBetweenRetries,
                connectTimeout);
        }
    }
}
