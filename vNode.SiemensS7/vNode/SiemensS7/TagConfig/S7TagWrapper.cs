using SiemensModule.TagConfig;
using System.Text.Json;

using vNode.Sdk.Data;
using vNode.Sdk.Enum;
using vNode.Sdk.Logger;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.vNode.SiemensS7.TagConfig
{
    public class S7TagWrapper
    {
        public enum SiemensTagStatus
        {
            Notinitialized,
            ConfigError,
            Ok
        }

        private readonly S7TagConfig _config;
        private readonly TagModelBase _tag;

        public object? CurrentValue { get; set; }
        public QualityCodeOptions CurrentQuality { get; set; }
        public TagModelBase Tag => _tag;
        public SiemensTagConfig Config => _config;
        public SiemensTagStatus Status { get; private set; } = SiemensTagStatus.NotInitialized;

        private S7TagWrapper(TagModelBase tagObject, S7TagConfig s7Config)
        {
            _tag = tagObject ?? throw new ArgumentNullException(nameof(tagObject), "Tag object cannot be null.");
            _config = s7Config ?? throw new ArgumentNullException(nameof(s7Config), "S7TagConfig cannot be null.");
            Status = SiemensTagStatus.Ok;
            CurrentValue = tagObject.InitialValue;

        }
    }
}
