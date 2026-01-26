using System;

namespace AkadaemiaAnyder.Core.Models
{
    public class CollectionEntry
    {
        public int Id { get; set; }
        public uint CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public string WorldName { get; set; } = string.Empty;
        public CollectionType Type { get; set; }
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
