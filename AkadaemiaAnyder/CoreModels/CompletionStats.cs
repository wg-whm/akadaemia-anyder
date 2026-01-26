namespace AkadaemiaAnyder.Core.Models
{
    public class CompletionStats
    {
        public int TotalItems { get; set; }
        public int UnlockedCount { get; set; }
        public double CompletionPercentage => TotalItems > 0
            ? (double)UnlockedCount / TotalItems * 100
            : 0;
    }
}
