using System;
using S7.Net;

namespace vNode.SiemensS7.SiemensCommonLayer
{
    public class SiemensTcpStrategy
    {
        public Plc Plc { get; private set; }

        public string Ip { get; }
        public short Rack { get; }
        public short Slot { get; }

        public SiemensTcpStrategy(string ip, short rack = 0, short slot = 1)
        {
            Ip = ip;
            Rack = rack;
            Slot = slot;

            Plc = new Plc(CpuType.S71200, ip, rack, slot);
        }

        public void Connect()
        {
            if (Plc.IsConnected)
                return;

            try
            {
                Plc.Open();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo establecer conexión con el PLC: {ex.Message}", ex);
            }
        }

        public object Read(string address)
        {
            if (!Plc.IsConnected)
                throw new InvalidOperationException("No hay conexión con el PLC.");
            try
            {
                return Plc.Read(address);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al leer el tag '{address}': {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                if (Plc.IsConnected)
                    Plc.Close();
            }
            catch (Exception ex)
            {
                // Se permite fallo silencioso en desconexión
                Console.WriteLine("Error al cerrar conexión: " + ex.Message);
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
