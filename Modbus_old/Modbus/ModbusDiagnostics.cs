//using System.Text.Json.Nodes;

//using vNode.Sdk.Base;
//using vNode.Sdk.Data;
//using vNode.Sdk.Enum;
//using vNode.Sdk.Filter;
//using vNode.Sdk.Logger;

//namespace ModbusModule;

//public class ModbusDiagnostics(string _nodeName, ISdkLogger _logger) : BaseChannelDiagnostics
//{
//    private readonly HashSet<string> _diagnosticTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
//        {
//        "Version", // Module version
//        "Instances", // Total channel instances
//        "Tags", // Total tags in all channels
//        "FailedReads",
//        "Retries",
//        "OverdueReads",
//        "AvgOverdueTime",
//        "AvgWriteTime",
//        "FailedConnectionAttempts",
//        "ModbusMasterConnected"
//        };
//    private readonly Dictionary<string, Guid> _diagnosticTags = [];

//    public override BaseChannelStateOptions State { get; }

//    public override void Dispose()
//    {
//        // throw new NotImplementedException();
//    }

//    public override bool RegisterTag(TagModelBase tagObject)
//    {
//        ArgumentNullException.ThrowIfNull(tagObject);

//        // Check if this is a diagnostic tag by examining the tag name
//        string tagName = tagObject.Name.Trim();
//        if (!string.IsNullOrEmpty(tagName) && _diagnosticTagNames.Contains(tagName))
//        {
//            // This is a diagnostic tag - store it in our dictionary
//            _diagnosticTags[tagName] = tagObject.IdTag;
//            _logger.Information("ModbusDiagnostics", $"Registered diagnostic tag '{tagName}' with ID {tagObject.IdTag}");
//            return true;
//        }
//        else
//        {
//            _logger.Error("ModbusDiagnostics", $"'{tagName}' is not a valid mdoule diagnostics tag name");
//            return false; // Name is not a valid diagnostics tag name
//        }

//    }

//    public override bool RemoveTag(Guid idTag)
//    {
//        return true;
//    }

//    public override bool Stop()
//    {
//        return true;
//    }

//    public override bool Start()
//    {
//        return true;
//    }

//    public override bool Terminate()
//    {
//        return true;
//    }

//    public override TagPathFilterBase GetSubscribeFilter()
//    {
//        return TagPathFilterBase.EmptySubscribe();
//    }

//    public override void ProcessData(RawData rawData)
//    {
//        throw new NotImplementedException();
//    }

//    public override Task<string> SetTagValue(Guid idTag, object newValue)
//    {
//        throw new NotImplementedException();
//    }

//    public override void UpdateConfiguration(JsonObject config)
//    {
//        throw new NotImplementedException();
//    }

//    public override DiagnosticTree GetChannelDiagnosticsTagsConfig(int id)
//    {
//        DiagnosticTree ret = new();
//        // TagModelBase instancesTag = new();
//        // {
//        //     instancesTag.Name = "Instances";
//        //     instancesTag.Description = "Total count of channels instances";
//        //     instancesTag.Config = new TagConfig(TagTypeOptions.Instances, _logger, idSource).ToString();
//        //     instancesTag.ClientAccess = ClientAccessOptions.ReadOnly;
//        //     instancesTag.TagDataType = TagDataTypeOptions.Int32;
//        //     instancesTag.InitialValue = "0";
//        // }
//        //
//        // TagModelBase tagCount = new();
//        // {
//        //     tagCount.Name = "Tags";
//        //     tagCount.Description = "Total count of tags on all channels";
//        //     tagCount.Config = new TagConfig(TagTypeOptions.Tags, _logger, idSource).ToString();
//        //     tagCount.ClientAccess = ClientAccessOptions.ReadOnly;
//        //     tagCount.TagDataType = TagDataTypeOptions.Int32;
//        //     tagCount.InitialValue = "0";
//        // }
//        //
//        // TagModelBase versionTag = new();
//        // {
//        //     versionTag.Name = "Version";
//        //     versionTag.Description = "Module version";
//        //     versionTag.Config = new TagConfig(TagTypeOptions.Version, _logger, idSource).ToString();
//        //     versionTag.ClientAccess = ClientAccessOptions.ReadOnly;
//        //     versionTag.TagDataType = TagDataTypeOptions.String;
//        //     versionTag.InitialValue = "0.0.0.0";
//        // }
//        // ret.Add(instancesTag);
//        // ret.Add(tagCount);
//        // ret.Add(versionTag);
//        return ret;
//    }
//}
