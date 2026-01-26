namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Recipe collection entry.
    /// Maps to both 'collections' and 'recipes' tables via JOIN.
    /// </summary>
    public class RecipeEntry : CollectionEntry
    {
        public int RecipeId { get; set; }
        public int RecipeLevel { get; set; }
        public CraftingClass CraftingClass { get; set; }
        public bool IsMasterRecipe { get; set; }
        public int? MasterBookId { get; set; }
        public int ItemLevel { get; set; }
    }

    /// <summary>
    /// FFXIV crafting classes.
    /// </summary>
    public enum CraftingClass
    {
        Carpenter = 8,
        Blacksmith = 9,
        Armorer = 10,
        Goldsmith = 11,
        Leatherworker = 12,
        Weaver = 13,
        Alchemist = 14,
        Culinarian = 15
    }
}
