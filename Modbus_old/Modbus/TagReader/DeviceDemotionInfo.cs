namespace ModbusModule.TagReader
{
    internal class DeviceDemotionInfo
    {
        public string DeviceId { get; }
        public int ConsecutiveFailures { get; set; }
        public bool IsDemoted => DemotedUntil != null;
        public DateTime? DemotedUntil { get; set; }

        public DeviceDemotionInfo(string deviceId)
        {
            DeviceId = deviceId;
            ConsecutiveFailures = 0;            
        }
    }
}
