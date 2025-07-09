using SiemensModule;
using System.Text.Json.Nodes;
using vNode.Sdk.Base;
using vNode.Sdk.Data;
using vNode.Sdk.Logger;

namespace vNode.SiemensS7
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
            // Crea la instancia de control del canal
            return _control ?? new SiemensControl(logger);
        }

        public override BaseChannel CreateBaseChannel(string nodeName, string config, ISdkLogger loggerEx)
        {
            // Crea una nueva instancia del canal Siemens
            var json = JsonNode.Parse(config) as JsonObject ?? new JsonObject();
            return new Siemens(nodeName, json, loggerEx, _control ?? new SiemensControl(loggerEx));
        }

        public override string GetChannelSchema()
        {
            // Return the channel schema  
            return "<ChannelSchema>";
        }

        public override string GetTagSchema()
        {
            // Return the tag schema  
            return "<TagSchema>";
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
    }
}
