using System.Collections.Generic;
using AkadaemiaAnyder.Modules.Core.Interfaces;

namespace AkadaemiaAnyder.Modules.Core.Implementations
{
    /// <summary>
    /// Mock implementation of IGameDataProvider for testing without game running.
    /// Returns realistic test data for unit testing and offline development.
    /// </summary>
    public class MockGameDataProvider : IGameDataProvider
    {
        private readonly Dictionary<uint, GameItem> _testItems;
        private readonly Dictionary<uint, GameRecipe> _testRecipes;

        public MockGameDataProvider()
        {
            // Initialize with some test data
            _testItems = new Dictionary<uint, GameItem>
            {
                { 5057, new GameItem { ItemId = 5057, Name = "Iron Ingot", Icon = 20401, StackSize = 999, ItemLevel = 14, IsUntradable = false } },
                { 5111, new GameItem { ItemId = 5111, Name = "Iron Ore", Icon = 21202, StackSize = 999, ItemLevel = 11, IsUntradable = false } },
                { 5106, new GameItem { ItemId = 5106, Name = "Fire Shard", Icon = 20001, StackSize = 9999, ItemLevel = 1, IsUntradable = false } },
                { 19925, new GameItem { ItemId = 19925, Name = "Chondrite Ingot", Icon = 20466, StackSize = 999, ItemLevel = 560, IsUntradable = false } },
            };

            _testRecipes = new Dictionary<uint, GameRecipe>
            {
                { 33, new GameRecipe { RecipeId = 33, ResultItemId = 5057, ResultItemName = "Iron Ingot", CraftType = 1, RecipeLevelTable = 14, RequiredCraftsmanship = 44, RequiredControl = 0, Durability = 40, IsExpert = false, IsSpecializationRequired = false } },
                { 34567, new GameRecipe { RecipeId = 34567, ResultItemId = 19925, ResultItemName = "Chondrite Ingot", CraftType = 1, RecipeLevelTable = 560, RequiredCraftsmanship = 2805, RequiredControl = 2635, Durability = 70, IsExpert = false, IsSpecializationRequired = false } },
            };
        }

        #region Item Data

        public GameItem? GetItem(uint itemId)
        {
            return _testItems.TryGetValue(itemId, out var item) ? item : null;
        }

        public string GetItemName(uint itemId)
        {
            return _testItems.TryGetValue(itemId, out var item) ? item.Name : $"Test Item {itemId}";
        }

        public uint GetItemIcon(uint itemId)
        {
            return _testItems.TryGetValue(itemId, out var item) ? item.Icon : 0;
        }

        #endregion

        #region Recipe Data

        public GameRecipe? GetRecipe(uint recipeId)
        {
            return _testRecipes.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }

        public List<GameRecipe> GetRecipesByItem(uint itemId)
        {
            var results = new List<GameRecipe>();
            foreach (var recipe in _testRecipes.Values)
            {
                if (recipe.ResultItemId == itemId)
                    results.Add(recipe);
            }
            return results;
        }

        public List<IngredientInfo> GetRecipeIngredients(uint recipeId)
        {
            // Return test ingredients for Iron Ingot recipe
            if (recipeId == 33)
            {
                return new List<IngredientInfo>
                {
                    new IngredientInfo { ItemId = 5111, ItemName = "Iron Ore", Quantity = 3, IsHQRequired = false },
                    new IngredientInfo { ItemId = 5106, ItemName = "Fire Shard", Quantity = 1, IsHQRequired = false },
                };
            }
            return new List<IngredientInfo>();
        }

        #endregion

        #region Character State

        public uint GetCurrentJob() => 9; // Blacksmith

        public string GetCharacterName() => "Test Character";

        public ulong GetCharacterContentId() => 1234567890123456789;

        public uint GetWorldId() => 73; // Gilgamesh

        public string GetWorldName(uint worldId)
        {
            return worldId switch
            {
                73 => "Gilgamesh",
                74 => "Midgardsormr",
                75 => "Adamantoise",
                _ => $"World {worldId}"
            };
        }

        public string GetDataCenter() => "Aether";

        #endregion

        #region Inventory Queries

        public List<InventoryItemInfo> GetCharacterInventory()
        {
            return new List<InventoryItemInfo>
            {
                new InventoryItemInfo { ItemId = 5111, ItemName = "Iron Ore", Quantity = 50, IsHQ = false, InventoryType = 0, SlotIndex = 0 },
                new InventoryItemInfo { ItemId = 5106, ItemName = "Fire Shard", Quantity = 200, IsHQ = false, InventoryType = 0, SlotIndex = 1 },
                new InventoryItemInfo { ItemId = 5057, ItemName = "Iron Ingot", Quantity = 10, IsHQ = false, InventoryType = 0, SlotIndex = 2 },
                new InventoryItemInfo { ItemId = 5057, ItemName = "Iron Ingot", Quantity = 5, IsHQ = true, InventoryType = 0, SlotIndex = 3 },
            };
        }

        public List<InventoryItemInfo> GetArmoryChest()
        {
            return new List<InventoryItemInfo>();
        }

        public int GetItemCount(uint itemId, bool includeHQ = false)
        {
            var inventory = GetCharacterInventory();
            var count = 0;
            foreach (var item in inventory)
            {
                if (item.ItemId == itemId && (includeHQ || !item.IsHQ))
                    count += item.Quantity;
            }
            return count;
        }

        public bool IsLoggedIn() => true;

        #endregion

        #region Consumables

        public List<GameItem> GetFoodItems()
        {
            return new List<GameItem>
            {
                new GameItem { ItemId = 36060, Name = "Sykon Salad", Icon = 24271, StackSize = 999, ItemLevel = 640 },
            };
        }

        public List<GameItem> GetPotionItems()
        {
            return new List<GameItem>
            {
                new GameItem { ItemId = 36112, Name = "Cunning Craftsman's Syrup", Icon = 20710, StackSize = 999, ItemLevel = 640 },
            };
        }

        public List<GameItem> GetManualItems()
        {
            return new List<GameItem>();
        }

        #endregion

        #region Crafting State

        public bool IsCrafting() => false;

        public CraftingStateInfo? GetCurrentCraftingState() => null;

        #endregion
    }
}
