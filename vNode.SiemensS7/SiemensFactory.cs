using S7.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Logger;
using vNode.SiemensS7.ChannelConfig;
using vNode.SiemensS7.Scheduler;

namespace SiemensModule
{
    public class SiemensFactory : BaseChannelFactory
    {
        private SiemensControl? _control;

        // Inicializa el control del módulo
        public override void InitializeControlChannel(ISdkLogger logger)
        {
            _control = new SiemensControl(logger);
        }

        public override string GetModuleName()
        {
            return "SiemensModule";
        }

        public override BaseChannelControl CreateBaseChannelControl(ISdkLogger logger)
        {
            // Lógica de control del canal Siemens
            return new SiemensControl(logger);
        }

        public override BaseChannel CreateBaseChannel(string nodeName, string config, ISdkLogger logger)
        {
            var jsonConfig = JsonSerializer.Deserialize<JsonObject>(config);
            if (_control == null)
                _control = new SiemensControl(logger);

            var channelConfig = SiemensChannelConfig.FromJson(jsonConfig);

            // Siemens debe heredar de BaseChannel
            var channel = new Siemens(channelConfig, logger, _control);
            return channel;
        }

        /// <summary>
        /// Devuelve el esquema JSON para la configuración de un canal Siemens.
        /// Útil para que el frontend genere formularios dinámicos.
        /// </summary>
        public override string GetChannelSchema()
        {
            return @"
            [
                {
                    $formkit: 'group',
                    name: 'Connection',
                    children: [
                        {
                            $formkit: 'text',
                            name: 'IpAddress',
                            label: 'IP Address',
                            validation: 'required|ipv4'
                        },
                        {
                            $formkit: 'select',
                            name: 'CpuType',
                            label: 'CPU Type',
                            options: ['S7300', 'S7400', 'S71200', 'S71500'],
                            validation: 'required'
                        },
                        {
                            $formkit: 'number',
                            name: 'Rack',
                            label: 'Rack',
                            validation: 'required|min:0'
                        },
                        {
                            $formkit: 'number',
                            name: 'Slot',
                            label: 'Slot',
                            validation: 'required|min:0'
                        }
                    ]
                }
            ]";
        }

        /// <summary>
        /// Devuelve el esquema JSON para la configuración de un tag Siemens.
        /// Útil para que el frontend genere formularios dinámicos.
        /// </summary>
        public override string GetTagSchema()
        {
            // Ejemplo de esquema, ajusta según SiemensTagConfig
            var schema = new JsonObject
            {
                ["TagId"] = "guid",
                ["Name"] = "string",
                ["Address"] = "string",
                ["DataType"] = "enum",
                ["PollRate"] = "int",
                ["BitNumber"] = "byte?",
                ["StringSize"] = "byte",
                ["ArraySize"] = "int",
                ["IsReadOnly"] = "bool",
                ["DeviceId"] = "string"
            };
            return schema.ToJsonString();
        }

        public override DiagnosticTree GetModuleDiagnosticsTagsConfig(int idChannel)
        {
            // Implement logic for diagnostics tags configuration  
            return new DiagnosticTree();
        }

        public override DiagnosticTag GetModuleEnableControlTagConfig(int idChannel)
        {
            // Implement logic for enable control tag configuration  
            return new DiagnosticTag();
        }

        public override DiagnosticTag GetModuleRestartControlTagConfig(int idChannel)
        {
            // Implement logic for restart control tag configuration  
            return new DiagnosticTag();
        }

        public override Result ChannelConfigurationIsValid(string configuration)
        {
            // Implement logic to validate channel configuration  
            return new Result(true, "Configuration is valid.");
        }

        public override string SanitizeChannelConfiguration(string configuration)
        {
            // Implement logic to sanitize channel configuration
            return configuration.Trim();
        }

        public SiemensChannelConfig? CreateChannelConfig(string jsonConfig, ISdkLogger logger)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            try
            {
                var config = JsonSerializer.Deserialize<SiemensChannelConfig>(jsonConfig, options);
                if (config == null)
                    logger.Error("SiemensFactory", "La configuración deserializada es nula.");
                return config;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "SiemensFactory", $"Error al deserializar la configuración del canal: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Valida la configuración del canal recibida en formato JSON.
        /// Devuelve un resultado en formato JSON para el frontend.
        /// </summary>
        public string ValidateChannelConfig(string jsonConfig)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var config = JsonSerializer.Deserialize<SiemensChannelConfig>(jsonConfig, options);

                // Validaciones personalizadas
                if (config == null)
                    throw new ArgumentException("La configuración es nula.");
                if (string.IsNullOrWhiteSpace(config.IpAddress))
                    throw new ArgumentException("La dirección IP es obligatoria.");
                if (config.Rack < 0)
                    throw new ArgumentException("El valor de Rack debe ser mayor o igual a 0.");
                if (config.Slot < 0)
                    throw new ArgumentException("El valor de Slot debe ser mayor o igual a 0.");

                // Puedes agregar más validaciones según tus necesidades

                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }
    }
}
