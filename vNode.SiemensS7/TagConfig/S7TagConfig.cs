using System.IO;
using System.Text.Json.Serialization;
using S7.Net;
using S7.Net.Types;

namespace SiemensModule.TagConfig;

public class SiemensTagConfig
{
    [JsonRequired]
    public string DeviceId { get; set; }

    [JsonRequired]
#if PLCADDRESSCONVERTER
    [JsonConverter(typeof(PLCAddressConverter))]
#endif
    public PLCAddress Address { get; set; }

    public byte? BitNumber { get; set; }
    public byte StringSize { get; set; }
    public int ArraySize { get; set; } = 0;
    [JsonRequired]
    public int PollRate { get; set; } = -1;
    [JsonRequired]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VarType DataType { get; set; }

    public int GetSize()
    {
        int typeSize = DataType switch
        {
            VarType.Bit => 1,
            VarType.Byte => 1,
            VarType.Word => 2,
            VarType.Int => 2,
            VarType.DWord => 4,
            VarType.DInt => 4,
            VarType.Real => 4,
            VarType.LReal => 8,
            VarType.Timer => 2,
            VarType.Counter => 2,
            VarType.String => StringSize,
            VarType.S7String => StringSize,
            VarType.S7WString => StringSize * 2,
            _ => throw new InvalidDataException($"Don't know the size for DataType {DataType}")
        };

        if (ArraySize > 0)
        {
            return typeSize * ArraySize;
        }

        return typeSize;
    }

    public bool IsReadOnly => Address.DataType == DataType.Input;
}