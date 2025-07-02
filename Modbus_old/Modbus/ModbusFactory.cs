using System.Text.Json;
using System.Text.Json.Nodes;

using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Helper;
using vNode.Sdk.Logger;

namespace ModbusModule
{
    public class ModbusFactory : BaseChannelFactory
    {
        private ModbusControl? _modbusControl;

        public override void InitializeControlChannel(ISdkLogger logger)
        {
            _modbusControl = new ModbusControl(logger);
        }

        public override BaseChannelControl CreateBaseChannelControl(ISdkLogger logger)
        {
            logger.Debug("ModbusFactory", $"{GetType().Assembly.GetName().Name!} CreateBaseChannelControl");
            return _modbusControl;
        }

        public override BaseChannel CreateBaseChannel(string nodeName, string config, ISdkLogger logger)
        {
            logger.Debug("ModbusFactory",
                $"{GetType().Assembly.GetName().Name!} CreateBaseChannel {JsonHelper.ObjectToString(config, logger)}");
            var newChannel = new Modbus(nodeName, JsonHelper.StringToJsonObject(config, logger), logger, _modbusControl);
            _modbusControl.RegisterChannel(newChannel);
            return newChannel;
        }

        public override string GetChannelSchema()
        {
            return @"
        {
            ""validator"": {
                ""rate"": {
                    ""required"": true,
                    ""type"": ""number"",
                    ""min"": 100
                }
            },
            ""result"": {
                ""rate"": [
                ""number"",
                {
                    ""label"": ""Random generation rate"",
                    ""defaultValue"": 1000,
                    ""help"": ""Rate for values random generation, in milliseconds.""
                }
                ]
            }
        }";
        }

        public override string GetTagSchema()
        {
            return @"
        {
            ""validator"": {
                ""address"": {
                    ""required"": false,
                    ""type"": ""string"",
                    ""in"": [
                        ""Random"",
                        ""Ramp"",
                        ""Sinusoidal"",
                        ""Triangle"",
                        ""Boolean"",
                        ""String"",
                        ""Static""
                    ]
                }
            },
            ""result"": {
                ""address"": [
                    ""string"",
                    {
                        ""label"": ""Address"",
                        ""defaultValue"": null,
                        ""help"": ""Datatype generated randomly at the specified rate"",
                        ""expression"": true,
                        ""control"": [
                            ""dropdown"",
                            {
                                ""valueList"": {
                                    ""Random"": ""Random"",
                                    ""Ramp"": ""Ramp"",
                                    ""Sinusoidal"": ""Sinusoidal"",
                                    ""Triangle"": ""Triangle"",
                                    ""Boolean"": ""Boolean"",
                                    ""String"": ""String"",
                                    ""Static"": ""Static""
                                }
                            }
                        ]
                    }
                ]
            }
        }";
        }

        public override DiagnosticTree GetModuleDiagnosticsTagsConfig(int idSource)
        {
            DiagnosticTree diagnosticTree = new();

            DiagnosticTag instancesTag = new();
            {
                instancesTag.Name = "Instances";
                instancesTag.Description = "Total count of channels instances";
                var obj = new { prueba = "prueba" };
                instancesTag.Config = JsonSerializer.Serialize(obj);
                instancesTag.ClientAccess = ClientAccessOptions.ReadOnly;
                instancesTag.TagDataType = TagDataTypeOptions.Int32;
                instancesTag.InitialValue = 0;
            }

            DiagnosticTag tagCount = new();
            {
                tagCount.Name = "TagsCount";
                tagCount.Description = "Total count of tags on all channels";
                var obj = new { prueba = "prueba" };
                tagCount.Config = JsonSerializer.Serialize(obj);
                tagCount.ClientAccess = ClientAccessOptions.ReadOnly;
                tagCount.TagDataType = TagDataTypeOptions.Int32;
                tagCount.InitialValue = 0;
            }

            DiagnosticTag versionTag = new();
            {
                versionTag.Name = "Version";
                versionTag.Description = "Module version";
                var obj = new { prueba = "prueba" };
                versionTag.Config = JsonSerializer.Serialize(obj);
                versionTag.ClientAccess = ClientAccessOptions.ReadOnly;
                versionTag.TagDataType = TagDataTypeOptions.String;
                versionTag.InitialValue = "0.0.0.0";
            }
            diagnosticTree.DiagnosticTags.Add(instancesTag);
            diagnosticTree.DiagnosticTags.Add(tagCount);
            diagnosticTree.DiagnosticTags.Add(versionTag);
            return diagnosticTree;
        }


        public override Result ChannelConfigurationIsValid(string configuration)
        {
            return Result.Return();
            try
            {
                JsonObject json = JsonSerializer.Deserialize<JsonObject>(configuration);

                if (json.Count == 0)
                {
                    return Result.Return(false,
                        "Channel Configuration Error: Configuration is empty. Expected parameters: Rate");
                }

                if (json.TryGetPropertyValue("Rate", out JsonNode rate))
                {
                    if (rate == null)
                    {
                        return Result.Return(false,
                            "Channel Configuration Error: Rate value not set. Example usage = \"Rate\":\"1000\" ");
                    }
                }

                return Result.Return();
            }
            catch (Exception ex)
            {
                return Result.Return(false, $"Channel Configuration Error: {ex.Message}");
            }
        }

        public override string SanitizeChannelConfiguration(string configuration)
        {
            return configuration;
        }

        /// <summary>
        /// Retrieves the configuration for the enable control tag of the module.
        /// </summary>
        /// <param name="idChannel">The ID of the channel.</param>
        /// <returns>A DiagnosticTag representing the enable control configuration.</returns>
        public override DiagnosticTag GetModuleEnableControlTagConfig(int idChannel)
        {
            DiagnosticTag tag = new();
            {
                tag.Name = "Enable";
                tag.Description = "Enable/Disable the module";
                var obj = new { prueba = "prueba" };
                tag.Config = JsonSerializer.Serialize(obj);
                tag.ClientAccess = ClientAccessOptions.ReadWrite;
                tag.TagDataType = TagDataTypeOptions.Boolean;
                tag.InitialValue = true;
            }
            return tag;
        }

        /// <summary>
        /// Retrieves the configuration for the restart control tag of the module.
        /// </summary>
        /// <param name="idChannel">The ID of the channel.</param>
        /// <returns>A DiagnosticTag representing the restart control configuration.</returns>
        public override DiagnosticTag GetModuleRestartControlTagConfig(int idChannel)
        {
            DiagnosticTag tag = new();
            {
                tag.Name = "Restart";
                tag.Description = "Restart the module";
                var obj = new { prueba = "prueba" };
                tag.Config = JsonSerializer.Serialize(obj);
                tag.ClientAccess = ClientAccessOptions.ReadWrite;
                tag.TagDataType = TagDataTypeOptions.Boolean;
                tag.InitialValue = false;
            }
            return tag;
        }
    }
}
