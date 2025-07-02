namespace ModbusModule.Scheduler
{
    public class ReadingDueEventArgs : EventArgs
    {
        public ReadingDueEventArgs(TagReadBatch readBatch)
        {
            TagReadBatch = readBatch;
        }

        public TagReadBatch TagReadBatch { get; set; }
    }
}
