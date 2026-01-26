namespace AkadaemiaAnyder.Core.Models
{
    public class FishingHole : CollectionEntry
    {
        public uint FishId { get; set; }
        public uint FishingHoleId { get; set; }
        public string Zone { get; set; } = string.Empty;
        public string RecommendedBait { get; set; } = string.Empty;
        public bool IsBigFish { get; set; }
        public string? WeatherRequirement { get; set; }
        public string? TimeRequirement { get; set; }
    }
}
