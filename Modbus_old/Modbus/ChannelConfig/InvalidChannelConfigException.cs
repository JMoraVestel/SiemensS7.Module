namespace ModbusModule.ChannelConfig
{
    public class InvalidChannelConfigException : ApplicationException
    {
        public InvalidChannelConfigException(Exception ex) : base("Invalid Channel Configuration", ex)
        {
        }
    }
}
