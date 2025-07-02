namespace ModbusModule.ChannelConfig
{    
    public class ModbusSwapConfig
    {
        public bool BitsIn16Bit { get; set; }
        public bool BytesIn16Bit { get; set; }
        public bool WordsIn32Bit { get; set; }
        public bool DWordsIn64Bit { get; set; }
        public bool BytesInStrings { get; set; }
    }
}
