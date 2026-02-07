using System;

namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Crafting history tracking entity.
    /// Maps to the 'crafting_history' table.
    /// Tracks total crafted, HQ count, and timestamps per recipe.
    /// </summary>
    public class CraftingHistoryEntity
    {
        /// <summary>
        /// Primary key (auto-increment).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Recipe ID being tracked.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Cached recipe name for quick display.
        /// </summary>
        public string RecipeName { get; set; } = string.Empty;

        /// <summary>
        /// Character content ID (character-specific tracking).
        /// </summary>
        public ulong CharacterContentId { get; set; }

        /// <summary>
        /// Total items crafted of this recipe (NQ + HQ).
        /// </summary>
        public int TotalCrafted { get; set; }

        /// <summary>
        /// Number of high-quality items crafted.
        /// </summary>
        public int HQCount { get; set; }

        /// <summary>
        /// Calculated property: number of normal-quality items.
        /// </summary>
        public int NQCount => TotalCrafted - HQCount;

        /// <summary>
        /// Calculated property: HQ percentage (0.0-100.0).
        /// </summary>
        public double HQPercentage => TotalCrafted > 0 ? (double)HQCount / TotalCrafted * 100 : 0;

        /// <summary>
        /// Timestamp of first craft of this recipe.
        /// </summary>
        public DateTime FirstCraftedAt { get; set; }

        /// <summary>
        /// Timestamp of most recent craft of this recipe.
        /// </summary>
        public DateTime LastCraftedAt { get; set; }
    }
}
