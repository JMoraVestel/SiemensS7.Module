using System;
using S7.Net;

namespace vNode.SiemensS7.SiemensCommonLayer
{
    public class SiemensTcpStrategy
    {
        public Plc Plc { get; set; }
        public string Ip { get; }
        public short Rack { get; }
        public short Slot { get; }

        public SiemensTcpStrategy(string ip, short rack = 0, short slot = 1)
        {
            Ip = ip;
            Rack = rack;
            Slot = slot;
            Plc = new Plc(CpuType.S71500, Ip, Rack, Slot);
        }

        public void Connect()
        {
            if (!Plc.IsConnected)
            {
                Plc.Open();
            }
        }

        public object Read(string address)
        {
            Connect();
            return Plc.Read(address);
        }

        /// <summary>
        /// Escribe un valor en la dirección especificada del PLC.
        /// </summary>
        /// <param name="address">La dirección de memoria del PLC (ej. "DB1.DBW20").</param>
        /// <param name="value">El valor a escribir.</param>
        public void Write(string address, object value)
        {
            Connect();
            Plc.Write(address, value);
        }

        public void Disconnect()
        {
            if (Plc.IsConnected)
            {
                Plc.Close();
            }
        }

        public bool IsConnected()
        {
            return Plc.IsConnected;
        }

        public void Reconnect()
        {
            Disconnect();
            Connect();
        }
    }
}
