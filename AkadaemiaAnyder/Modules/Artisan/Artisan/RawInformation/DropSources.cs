using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace AkadaemiaAnyder.Modules.Artisan.RawInformation
{
    public class DropSources
    {
        public static List<DropSources>? Sources = DropList()?.ToList();

        public DropSources(uint Item, List<uint> monsterId)
        {
            ItemId = Item;
            MonsterId = monsterId;
            CanObtainFromRetainer = Svc.Data.GetExcelSheet<RetainerTaskNormal>()!.Any(x => x.Item.RowId == ItemId);
            UsedInRecipes = LuminaSheets.RecipeSheet.Values.Any(y => y.Ingredients().Any(x => x.Item.RowId == ItemId));
        }

        public bool CanObtainFromRetainer { get; set; }
        public uint ItemId { get; set; }

        public List<uint> MonsterId { get; set; }
        public bool UsedInRecipes { get; set; }

        /// <summary>
        /// Loads monster drop source data.
        /// </summary>
        /// <returns>Empty list - network call removed for privacy compliance.</returns>
        /// <remarks>
        /// PHASE 4 TODO: Implement local drop source database
        /// - Option 1: Bundle static JSON file from Teamcraft (one-time copy)
        /// - Option 2: Integrate with Akadaemia Anyder's database for drop tracking
        /// - Impact: Monster drop indicators won't show without this data
        /// </remarks>
        private static List<DropSources>? DropList()
        {
            // Network call removed for privacy (previously fetched from Teamcraft GitHub)
            return new List<DropSources>();
        }
    }
}