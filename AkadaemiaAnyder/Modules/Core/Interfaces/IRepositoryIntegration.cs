using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AkadaemiaAnyder.Modules.Core.Interfaces
{
    /// <summary>
    /// Integration interface for Akadaemia Anyder database operations.
    /// Replaces Universalis/Teamcraft with local-only data access.
    /// All methods are async to support non-blocking database operations.
    /// </summary>
    public interface IRepositoryIntegration
    {
        #region Material Availability (Replaces Universalis)

        /// <summary>
        /// Gets material availability across all local storage locations.
        /// </summary>
        Task<MaterialAvailability> GetMaterialAvailabilityAsync(uint itemId);

        /// <summary>
        /// Finds all locations where a specific item exists.
        /// </summary>
        Task<List<MaterialLocation>> FindMaterialLocationsAsync(uint itemId);

        /// <summary>
        /// Gets aggregated material availability for multiple items.
        /// </summary>
        Task<Dictionary<uint, MaterialAvailability>> GetBulkMaterialAvailabilityAsync(IEnumerable<uint> itemIds);

        #endregion

        #region Collection Bindings

        /// <summary>
        /// Checks if an item is collected for a specific collection type.
        /// </summary>
        Task<bool> IsItemCollectedAsync(uint itemId, CollectionCategory category);

        /// <summary>
        /// Gets overall progress for a collection category.
        /// </summary>
        Task<CollectionProgress> GetCollectionProgressAsync(CollectionCategory category);

        /// <summary>
        /// Gets progress for all collection categories.
        /// </summary>
        Task<Dictionary<CollectionCategory, CollectionProgress>> GetAllCollectionProgressAsync();

        #endregion

        #region Crafting List Persistence (Local Database)

        /// <summary>
        /// Saves a crafting list to the database.
        /// </summary>
        Task SaveCraftingListAsync(CraftingListData list);

        /// <summary>
        /// Loads a crafting list by ID.
        /// </summary>
        Task<CraftingListData?> LoadCraftingListAsync(string listId);

        /// <summary>
        /// Loads all crafting lists for the current character.
        /// </summary>
        Task<List<CraftingListData>> LoadAllCraftingListsAsync();

        /// <summary>
        /// Deletes a crafting list by ID.
        /// </summary>
        Task DeleteCraftingListAsync(string listId);

        #endregion

        #region Recipe Tracking

        /// <summary>
        /// Gets all recipe IDs that have been crafted.
        /// </summary>
        Task<List<uint>> GetCraftedRecipesAsync();

        /// <summary>
        /// Records a crafted recipe with HQ status.
        /// </summary>
        Task RecordCraftedRecipeAsync(uint recipeId, bool wasHQ);

        /// <summary>
        /// Gets crafting history for a specific recipe.
        /// </summary>
        Task<CraftingHistory> GetCraftingHistoryAsync(uint recipeId);

        #endregion

        #region Session Tracking

        /// <summary>
        /// Records a crafting session with summary statistics.
        /// </summary>
        Task RecordCraftingSessionAsync(CraftingSessionData session);

        /// <summary>
        /// Gets recent crafting sessions within the specified number of days.
        /// </summary>
        Task<List<CraftingSessionData>> GetRecentSessionsAsync(int days);

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Represents material availability across storage locations.
    /// </summary>
    public class MaterialAvailability
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int InInventory { get; set; }
        public int InSaddlebag { get; set; }
        public int InRetainers { get; set; }
        public int InGlamourDresser { get; set; }
        public int InArmoryChest { get; set; }

        /// <summary>
        /// Total items available across all locations.
        /// </summary>
        public int Total => InInventory + InSaddlebag + InRetainers + InGlamourDresser + InArmoryChest;

        /// <summary>
        /// Breakdown by specific location name.
        /// </summary>
        public Dictionary<string, int> ByLocation { get; set; } = new();
    }

    /// <summary>
    /// Represents a specific location where an item was found.
    /// </summary>
    public class MaterialLocation
    {
        /// <summary>
        /// Location identifier (e.g., "inventory", "saddlebag", "retainer_1").
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Slot index within the location.
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Quantity at this specific location.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Whether the item is HQ.
        /// </summary>
        public bool IsHQ { get; set; }
    }

    /// <summary>
    /// Collection categories for tracking progress.
    /// </summary>
    public enum CollectionCategory
    {
        Recipe,
        Gathering,
        Fishing,
        Mount,
        Minion,
        TripleTriadCard,
        OrchestrionRoll,
        Emote,
        Hairstyle,
        Barding,
        BlueMageSpell
    }

    /// <summary>
    /// Represents progress within a collection category.
    /// </summary>
    public class CollectionProgress
    {
        public CollectionCategory Category { get; set; }
        public int Unlocked { get; set; }
        public int Total { get; set; }

        /// <summary>
        /// Completion percentage (0-100).
        /// </summary>
        public double Percentage => Total > 0 ? (double)Unlocked / Total * 100 : 0;
    }

    /// <summary>
    /// Represents a saved crafting list.
    /// </summary>
    public class CraftingListData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ulong CharacterContentId { get; set; }
        public List<CraftingListItemData> Items { get; set; } = new();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an item within a crafting list.
    /// </summary>
    public class CraftingListItemData
    {
        public uint RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int QuantityCrafted { get; set; }
        public byte CraftType { get; set; }
    }

    /// <summary>
    /// Represents crafting history for a specific recipe.
    /// </summary>
    public class CraftingHistory
    {
        public uint RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public int TotalCrafted { get; set; }
        public int HQCount { get; set; }
        public int NQCount => TotalCrafted - HQCount;
        public double HQPercentage => TotalCrafted > 0 ? (double)HQCount / TotalCrafted * 100 : 0;
        public DateTime FirstCrafted { get; set; }
        public DateTime LastCrafted { get; set; }
    }

    /// <summary>
    /// Represents a crafting session with summary statistics.
    /// </summary>
    public class CraftingSessionData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ulong CharacterContentId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Duration => End - Start;
        public int ItemsCrafted { get; set; }
        public int HQCount { get; set; }
        public int NQCount => ItemsCrafted - HQCount;
        public List<uint> RecipeIds { get; set; } = new();
    }

    #endregion
}
