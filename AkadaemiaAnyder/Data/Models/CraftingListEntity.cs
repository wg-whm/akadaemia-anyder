using System;
using System.Collections.Generic;

namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Crafting to-do list entity.
    /// Maps to the 'crafting_lists' table.
    /// Contains recipe queues with quantities to craft.
    /// </summary>
    public class CraftingListEntity
    {
        /// <summary>
        /// Unique identifier for this crafting list.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User-defined name for this crafting list.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Character content ID (character-specific list).
        /// </summary>
        public ulong CharacterContentId { get; set; }

        /// <summary>
        /// Timestamp when this list was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this list was last modified.
        /// </summary>
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property for items in this list.
        /// </summary>
        public List<CraftingListItemEntity> Items { get; set; } = new();
    }
}
