using System;

namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Base class for all collection entries.
    /// Maps to the 'collections' table.
    /// </summary>
    public class CollectionEntry
    {
        public int Id { get; set; }
        public int CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public string WorldName { get; set; } = string.Empty;
        public CollectionType Type { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// Collection types matching database schema.
    /// </summary>
    public enum CollectionType
    {
        Recipe = 1,
        GatheringNode = 2,
        FishingHole = 3
    }
}
