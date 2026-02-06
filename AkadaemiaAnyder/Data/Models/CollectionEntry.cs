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
    /// Extended to support all trackable collection categories.
    /// </summary>
    public enum CollectionType
    {
        Recipe = 1,
        GatheringNode = 2,
        FishingHole = 3,

        // Additional collection types for comprehensive tracking
        Gathering = 4,
        Fishing = 5,
        Mount = 6,
        Minion = 7,
        TripleTriadCard = 8,
        OrchestrionRoll = 9,
        Emote = 10,
        Hairstyle = 11,
        Barding = 12,
        BlueMageSpell = 13
    }
}
