using vNode.SiemensS7.SiemensCommonLayer;

namespace vNode.SiemensS7
{
    public class SiemensFactory
    {
        public static S7TcpStrategy CreateTcpConnection(string ip)
        {
            var connection = new S7TcpStrategy(ip);
            connection.Connect();

            if (connection.IsConnected())
                return connection;  
            else
                throw new System.Exception("No se pudo conectar al PLC Siemens.");
        }
    }
}
