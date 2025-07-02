using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Windows.Markup;

using ModbusModule.ChannelConfig;
using ModbusModule.TagConfig;
using ModbusModule.Helper;

using vNode.Sdk.Logger;
using ModbusModule.Scheduler;

namespace ModbusModule.TagReader
{
    public class ModbusParser
    {
        private readonly ISdkLogger _logger;
        public ModbusParser(ISdkLogger logger)
        {
            _logger = logger;
        }

        public bool TryGetRegisters(ModbusTagWrapper tag, object value, ModbusSwapConfig swapConfig, out ushort[] registers)
        {
            ArgumentNullException.ThrowIfNull(value);
            registers = Array.Empty<ushort>();

            // Create a converter with the swap configuration from tagConfig
            ModbusDataConverter converter = new ModbusDataConverter(swapConfig, _logger);

            try
            {
                if (tag.Config.RegisterAddress.Type == ModbusType.InputCoil ||
                    tag.Config.RegisterAddress.Type == ModbusType.OutputCoil)
                {
                    _logger.Error("ModbusParser", $"Cannot parse into registers: this tag (modbus address {tag.Config.RegisterAddress}) targets a coil");
                    return false;
                }

                // First attempt to convert to registers
                var success = converter.TryConvertToRegisters(value, tag.Tag.TagDataType, tag.Config.DataType, out registers, tag.Tag.IsArray);

                if (!success || registers == null)
                {
                    return false;
                }

                // Apply any data-type specific adjustments
                // For example, if the tag is a string, we need to ensure the register array is sized correctly
                adjustRegistersForDataType(tag, ref registers);

                // Return success since we've already handled the conversion
                return true;

            }
            catch
            {
                // Handle any unexpected errors during conversion
                registers = null;
                return false;
            }
        }

        private void adjustRegistersForDataType(ModbusTagWrapper tag, ref ushort[] registers)
        {
            // Handle string-specific sizing requirements
            if (tag.Config.DataType == ModbusTagDataTypeType.String)
            {
                int requiredRegisterCount = (tag.Config.StringSize + 1) / 2; // Round up
                if (registers.Length != requiredRegisterCount)
                {
                    ushort[] adjustedRegisters = new ushort[requiredRegisterCount];
                    int registersToCopy = Math.Min(registers.Length, requiredRegisterCount);
                    Array.Copy(registers, adjustedRegisters, registersToCopy);
                    registers = adjustedRegisters;
                }
            }

            if (tag.Tag.IsArray)
            {
                // Handle array-specific sizing requirements
                int requiredRegisterCount = tag.Config.GetSize();
                if (registers.Length != requiredRegisterCount)
                {
                    ushort[] adjustedRegisters = new ushort[requiredRegisterCount];
                    int registersToCopy = Math.Min(registers.Length, requiredRegisterCount);
                    Array.Copy(registers, adjustedRegisters, registersToCopy);
                    registers = adjustedRegisters;
                }
            }
        }

        public List<TagReadResultItem> parseCoils(List<TagReadBatchItem> readItems, bool[] coilValues)
        {

            if (coilValues == null)
            {
                throw new InvalidDataException("coilValues is null, cannot parse read results.");
            }
            if (readItems.Count != coilValues.Count())
            {
                throw new InvalidDataException("coilValues count does not match itemsToRead count");
            }

            var parseResults = new List<TagReadResultItem>();
            for (var i = 0; i < readItems.Count; i++)
            {
                var item = readItems[i];
                parseResults.Add(new TagReadResultItem(item,
                    TagReadResult.TagReadResultType.Success,
                    ModbusHelper.ConvertModbusToOpcua(coilValues[i], ModbusTagDataTypeType.Boolean, item.Tag.Tag.TagDataType, item.Tag.Tag.IsArray)));
            }
            return parseResults;
        }

        public List<TagReadResultItem> ParseRegisters(List<TagReadBatchItem> readItems, uint firstReadAddress, uint lastReadAddress, ushort[] values,
            ModbusSwapConfig swapConfig)
        {
            ArgumentNullException.ThrowIfNull(readItems, nameof(readItems));
            ArgumentNullException.ThrowIfNull(values, nameof(values));

            var retVal = new List<TagReadResultItem>();
            var converter = new ModbusDataConverter(swapConfig, _logger);

            foreach (var item in readItems)
            {
                int valueIndex = (int) (item.Tag.Config.RegisterAddress.Offset - firstReadAddress);
                if (values.Length < valueIndex + item.Tag.Config.GetSize())
                {
                    throw new ArgumentException("Invalid values array size");
                }

                Span<ushort> span = values.AsSpan(valueIndex, item.Size);
                ushort[] subArray = span.ToArray();

                object modbusValue;
                try
                {
                    modbusValue = converter.FromRegisters(subArray, item.Tag.Config, item.Tag.Tag.IsArray);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "ModbusParser", $"Unable to parse registers.");
                    retVal.Add(new TagReadResultItem(item, TagReadResult.TagReadResultType.ParseError));
                    continue;
                }

                object opcuaValue;
                try
                {
                    opcuaValue = ModbusHelper.ConvertModbusToOpcua(modbusValue, item.Tag.Config.DataType, item.Tag.Tag.TagDataType, item.Tag.Tag.IsArray);
                }
                catch (Exception ex)
                {
                    _logger.Error("ModbusParser", $"Unable to convert modbus type {item.Tag.Config.DataType} to {item.Tag.Tag.TagDataType}: {ex.Message}");
                    retVal.Add(new TagReadResultItem(item, TagReadResult.TagReadResultType.ParseError));
                    continue;
                }

                retVal.Add(new TagReadResultItem(item, TagReadResult.TagReadResultType.Success, opcuaValue));
            }

            return retVal;
        }
    }
}
