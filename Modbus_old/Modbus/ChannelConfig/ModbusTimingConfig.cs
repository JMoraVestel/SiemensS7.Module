using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModbusModule.ChannelConfig
{
    public class ModbusTimingConfig
    {
        /// <summary>
        /// Request timeout (in milliseconds).
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("ConnectTimeout")]
        public int ConnectTimeout { get; set; }
        /// <summary>
        /// Request timeout (in milliseconds).
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("RequestTimeout")]        
        public int RequestTimeout { get; set; }

        /// <summary>
        /// Max retries attempts.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("RetryAttempts")]
        public byte RetryAttempts { get; set; }

        /// <summary>
        /// Delay between requests.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("InterRequestDelay")]
        public int InterRequestDelay { get; set; }

        /// <summary>
        /// Delay between retry attempts.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("InterRetryDelay")]
        public int InterRetryDelay { get; set; }
    }
}
