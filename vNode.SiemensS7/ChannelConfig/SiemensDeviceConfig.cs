using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vNode.SiemensS7.ChannelConfig
{
    /// <summary>
    /// Define la configuración para un dispositivo PLC Siemens S7 dentro de un canal.
    /// </summary>
    public class SiemensDeviceConfig
    {
        /// <summary>
        /// Identificador único del dispositivo.
        /// </summary>
        [JsonRequired]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Dirección IP del PLC Siemens.
        /// </summary>
        [JsonRequired]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", ErrorMessage = "El formato de la dirección IP no es válido.")]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Número de Rack del PLC. Generalmente 0 para la mayoría de las CPUs S7-1200/1500.
        /// </summary>
        [JsonRequired]
        [Range(0, 7, ErrorMessage = "El número de Rack debe estar entre 0 y 7.")]
        public int Rack { get; set; }

        /// <summary>
        /// Número de Slot del PLC. Generalmente 1 para S7-1200/1500, y 2 para S7-300/400.
        /// </summary>
        [JsonRequired]
        [Range(0, 31, ErrorMessage = "El número de Slot debe estar entre 0 y 31.")]
        public int Slot { get; set; }

        /// <summary>
        /// Indica si el dispositivo está habilitado para la comunicación.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
