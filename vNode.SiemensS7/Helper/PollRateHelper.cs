using System;
using System.Text.Json;
using vNode.SiemensS7.TagConfig;
using vNode.Sdk.Data;

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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                SiemensTagConfig? cfg = JsonSerializer.Deserialize<SiemensTagConfig>(tag.Config, options);
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
