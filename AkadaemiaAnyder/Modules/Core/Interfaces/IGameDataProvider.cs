using System.Collections.Generic;
using System.Threading.Tasks;

namespace AkadaemiaAnyder.Modules.Core.Interfaces
{
    /// <summary>
    /// Abstraction layer for game data access (Lumina, FFXIVClientStructs, Dalamud services).
    /// Decouples business logic from direct game memory access for testability.
    /// </summary>
    public interface IGameDataProvider
    {
        #region Item Data

        /// <summary>
        /// Gets item data by ID from game data sheets.
        /// </summary>
        GameItem? GetItem(uint itemId);

        /// <summary>
        /// Gets the localized name of an item.
        /// </summary>
        string GetItemName(uint itemId);

        /// <summary>
        /// Gets the icon ID for an item.
        /// </summary>
        uint GetItemIcon(uint itemId);

        #endregion

        #region Recipe Data

        /// <summary>
        /// Gets recipe data by recipe ID.
        /// </summary>
        GameRecipe? GetRecipe(uint recipeId);

        /// <summary>
        /// Gets all recipes that produce a specific item.
        /// </summary>
        List<GameRecipe> GetRecipesByItem(uint itemId);

        /// <summary>
        /// Gets ingredient information for a recipe.
        /// </summary>
        List<IngredientInfo> GetRecipeIngredients(uint recipeId);

        #endregion

        #region Character State

        /// <summary>
        /// Gets the current player's ClassJob ID.
        /// </summary>
        uint GetCurrentJob();

        /// <summary>
        /// Gets the current character's name.
        /// </summary>
        string GetCharacterName();

        /// <summary>
        /// Gets the current character's content ID (unique identifier).
        /// </summary>
        ulong GetCharacterContentId();

        /// <summary>
        /// Gets the current world ID.
        /// </summary>
        uint GetWorldId();

        /// <summary>
        /// Gets the world name by ID.
        /// </summary>
        string GetWorldName(uint worldId);

        /// <summary>
        /// Gets the data center name for the current world.
        /// </summary>
        string GetDataCenter();

        #endregion

        #region Inventory Queries

        /// <summary>
        /// Gets all items in the character's main inventory (bags 0-3).
        /// </summary>
        List<InventoryItemInfo> GetCharacterInventory();

        /// <summary>
        /// Gets all items in the armory chest.
        /// </summary>
        List<InventoryItemInfo> GetArmoryChest();

        /// <summary>
        /// Gets the total count of a specific item across all accessible inventories.
        /// </summary>
        int GetItemCount(uint itemId, bool includeHQ = false);

        /// <summary>
        /// Checks if the player is currently logged in.
        /// </summary>
        bool IsLoggedIn();

        #endregion

        #region Consumables

        /// <summary>
        /// Gets all food items in the player's inventory.
        /// </summary>
        List<GameItem> GetFoodItems();

        /// <summary>
        /// Gets all potion items in the player's inventory.
        /// </summary>
        List<GameItem> GetPotionItems();

        /// <summary>
        /// Gets all manual items (crafting manuals) in the player's inventory.
        /// </summary>
        List<GameItem> GetManualItems();

        #endregion

        #region Crafting State

        /// <summary>
        /// Checks if the player is currently in a crafting action.
        /// </summary>
        bool IsCrafting();

        /// <summary>
        /// Gets the current crafting state if crafting is active.
        /// </summary>
        CraftingStateInfo? GetCurrentCraftingState();

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Represents basic item information from game data.
    /// </summary>
    public class GameItem
    {
        public uint ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint Icon { get; set; }
        public uint StackSize { get; set; }
        public uint ItemLevel { get; set; }
        public bool IsUntradable { get; set; }
    }

    /// <summary>
    /// Represents recipe information from game data.
    /// </summary>
    public class GameRecipe
    {
        public uint RecipeId { get; set; }
        public uint ResultItemId { get; set; }
        public string ResultItemName { get; set; } = string.Empty;
        public byte CraftType { get; set; } // 0-7 for CRP through CUL
        public ushort RecipeLevelTable { get; set; }
        public uint RequiredCraftsmanship { get; set; }
        public uint RequiredControl { get; set; }
        public uint Durability { get; set; }
        public bool IsExpert { get; set; }
        public bool IsSpecializationRequired { get; set; }
    }

    /// <summary>
    /// Represents ingredient requirements for a recipe.
    /// </summary>
    public class IngredientInfo
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsHQRequired { get; set; }
    }

    /// <summary>
    /// Represents an item in inventory with quantity and flags.
    /// </summary>
    public class InventoryItemInfo
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsHQ { get; set; }
        public ushort Spiritbond { get; set; }
        public ushort Condition { get; set; }
        public byte InventoryType { get; set; }
        public short SlotIndex { get; set; }
    }

    /// <summary>
    /// Represents the current crafting state during synthesis.
    /// </summary>
    public class CraftingStateInfo
    {
        public uint RecipeId { get; set; }
        public uint Progress { get; set; }
        public uint MaxProgress { get; set; }
        public uint Quality { get; set; }
        public uint MaxQuality { get; set; }
        public uint Durability { get; set; }
        public uint MaxDurability { get; set; }
        public uint CP { get; set; }
        public uint MaxCP { get; set; }
        public byte Condition { get; set; }
        public byte Step { get; set; }
    }

    #endregion
}
