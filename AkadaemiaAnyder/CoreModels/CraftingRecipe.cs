namespace AkadaemiaAnyder.Core.Models
{
    public class CraftingRecipe : CollectionEntry
    {
        public uint RecipeId { get; set; }
        public int RecipeLevel { get; set; }
        public CraftingClass CraftingClass { get; set; }
        public bool IsMasterRecipe { get; set; }
        public uint? MasterBookId { get; set; }
        public int ItemLevel { get; set; }
    }

    public enum CraftingClass
    {
        CRP = 0, BSM = 1, ARM = 2, GSM = 3,
        LTW = 4, WVR = 5, ALC = 6, CUL = 7
    }
}
