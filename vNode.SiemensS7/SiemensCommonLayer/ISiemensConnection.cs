using System.Threading.Tasks;

namespace vNode.SiemensS7.SiemensCommonLayer
{
    public interface ISiemensConnection
    {
        bool IsConnected { get; }
        Task ConnectAsync();
        void Disconnect();
        void Write(string address, object value);
        // A�ade aqu� otros m�todos que necesites, como Read, etc.
    }
}