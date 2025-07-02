using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using vNode.Sdk.Data;
using vNode.Sdk.Enum;

namespace ModbusModule.Diagnostics
{
    internal static class Shared
    {
        public static HashSet<string> ChannelControlTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Enable",
            "Restart",
            "Version", // Module version
            "Instances", // Total channel instances
            "TagsCount", // Total tags in all channels
            "TotalReads",
            "TotalWrites",
            "FailedReads",
            "Retries",
            "OverdueReads",
            "AvgOverdueTime",
            "AvgWriteTime",
            "FailedConnectionAttempts",
            "ModbusMasterConnected",
            "PollOnDemand"
        };

        public static HashSet<string> ModbusControlTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Enable",
            "Restart",
            "Version",
            "Instances",
            "TagsCount"
        };

        public static DiagnosticTag CreateDiagnosticTag(string name, string description, TagDataTypeOptions dataType, object defaultValue, string config="{}")
        {
            DiagnosticTag tag;

            if (config=="{}")
                config = @"{""hola"":""hola""}";

            tag = new();
            tag.Name = name;
            tag.Description = description;
            tag.Config = config;
            tag.ClientAccess = ClientAccessOptions.ReadOnly;
            tag.TagDataType = dataType;
            tag.InitialValue = defaultValue;

            return tag;
        }
    }


}
