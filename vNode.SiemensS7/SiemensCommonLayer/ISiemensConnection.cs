using System.Threading.Tasks;

namespace vNode.SiemensS7.SiemensCommonLayer
{
    public interface ISiemensConnection
    {
        bool IsConnected { get; }
        Task ConnectAsync();
        void Disconnect();
        void Write(string address, object value);
        // Añade aquí otros métodos que necesites, como Read, etc.
    }
}