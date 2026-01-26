namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Fishing hole collection entry.
    /// Maps to both 'collections' and 'fishing_holes' tables via JOIN.
    /// </summary>
    public class FishingHoleEntry : CollectionEntry
    {
        public int FishId { get; set; }
        public int FishingHoleId { get; set; }
        public string Zone { get; set; } = string.Empty;
        public string RecommendedBait { get; set; } = string.Empty;
        public bool IsBigFish { get; set; }
        public string? WeatherRequirement { get; set; }
        public string? TimeRequirement { get; set; }
    }
}
