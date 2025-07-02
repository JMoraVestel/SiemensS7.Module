using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace vNode.SiemensS7.Helper
{
    internal class PollRateHelper
    {
        /// <summary>
        /// Intenta obtener el poll rate configurado para una etiqueta.
        /// </summary>
        /// <param name="tag">Etiqueta que contiene la configuración JSON.</param>
        /// <param name="pollRate">Devuelve el poll rate en milisegundos cuando tiene éxito.</param>
        /// <returns>True si se pudo leer el poll rate, false en caso contrario.</returns>
        public static bool TryGetPollRate(TagModelBase tag, out int pollRate)
        {
            pollRate = -1;
            if (tag == null || string.IsNullOrWhiteSpace(tag.Config))
            {
                return false;
            }

            try
            {
                ModbusTagConfig? cfg = JsonSerializer.Deserialize<ModbusTagConfig>(tag.Config);
                if (cfg == null || cfg.PollRate < 0)
                {
                    return false;
                }

                pollRate = cfg.PollRate;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
