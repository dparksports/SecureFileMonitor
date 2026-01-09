namespace SecureFileMonitor.Core.Models
{
    public class UsnJournalData
    {
        public ulong UsnJournalID { get; set; }
        public long FirstUsn { get; set; }
        public long NextUsn { get; set; }
        public long LowestValidUsn { get; set; }
        public long MaxUsn { get; set; }
        public long MaximumSize { get; set; }
        public long AllocationDelta { get; set; }
    }
}
