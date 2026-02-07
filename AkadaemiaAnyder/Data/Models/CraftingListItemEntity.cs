namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Individual item in a crafting list.
    /// Maps to the 'crafting_list_items' table.
    /// Tracks recipe ID, quantity to craft, and progress.
    /// </summary>
    public class CraftingListItemEntity
    {
        /// <summary>
        /// Primary key (auto-increment).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Foreign key reference to the parent CraftingList.
        /// </summary>
        public string ListId { get; set; } = string.Empty;

        /// <summary>
        /// Recipe ID for this item.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Cached recipe name for quick display.
        /// </summary>
        public string RecipeName { get; set; } = string.Empty;

        /// <summary>
        /// Quantity of this recipe to craft.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Number of this recipe already crafted.
        /// </summary>
        public int QuantityCrafted { get; set; }

        /// <summary>
        /// Crafting class for this recipe (8=CRP, 9=BSM, etc).
        /// </summary>
        public byte CraftType { get; set; }

        /// <summary>
        /// Navigation property for parent list.
        /// </summary>
        public CraftingListEntity? List { get; set; }
    }
}
