using System;
using System.Collections.Generic;
using System.Linq;
using AkadaemiaAnyder.Modules.Core.Interfaces;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AkadaemiaAnyder.Modules.Core.Implementations
{
    /// <summary>
    /// Default implementation of IGameDataProvider using Dalamud services.
    /// Each method delegates to a single Dalamud service to maintain single-responsibility.
    /// All game data access is wrapped in try-catch for graceful failure handling.
    /// </summary>
    public class DefaultGameDataProvider : IGameDataProvider
    {
        private readonly IDataManager _dataManager;
        private readonly IClientState _clientState;
        private readonly IPluginLog _log;

        public DefaultGameDataProvider(
            IDataManager dataManager,
            IClientState clientState,
            IPluginLog log)
        {
            _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        #region Item Data - Delegates to IDataManager

        public GameItem? GetItem(uint itemId)
        {
            try
            {
                var sheet = _dataManager.GetExcelSheet<Item>();
                if (sheet == null) return null;

                if (!sheet.TryGetRow(itemId, out var row))
                    return null;

                return new GameItem
                {
                    ItemId = itemId,
                    Name = row.Name.ExtractText(),
                    Icon = row.Icon,
                    StackSize = row.StackSize,
                    ItemLevel = row.LevelItem.RowId,
                    IsUntradable = row.IsUntradable
                };
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetItem({itemId}) failed: {ex.Message}");
                return null;
            }
        }

        public string GetItemName(uint itemId)
        {
            try
            {
                var item = GetItem(itemId);
                return item?.Name ?? $"Unknown Item {itemId}";
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetItemName({itemId}) failed: {ex.Message}");
                return $"Unknown Item {itemId}";
            }
        }

        public uint GetItemIcon(uint itemId)
        {
            try
            {
                var item = GetItem(itemId);
                return item?.Icon ?? 0;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetItemIcon({itemId}) failed: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region Recipe Data - Delegates to IDataManager

        public GameRecipe? GetRecipe(uint recipeId)
        {
            try
            {
                var sheet = _dataManager.GetExcelSheet<Recipe>();
                if (sheet == null) return null;

                if (!sheet.TryGetRow(recipeId, out var row))
                    return null;

                return new GameRecipe
                {
                    RecipeId = recipeId,
                    ResultItemId = row.ItemResult.RowId,
                    ResultItemName = GetItemName(row.ItemResult.RowId),
                    CraftType = (byte)row.CraftType.RowId,
                    RecipeLevelTable = (ushort)row.RecipeLevelTable.RowId,
                    RequiredCraftsmanship = row.RequiredCraftsmanship,
                    RequiredControl = row.RequiredControl,
                    Durability = row.DurabilityFactor,
                    IsExpert = row.IsExpert,
                    IsSpecializationRequired = row.IsSpecializationRequired
                };
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetRecipe({recipeId}) failed: {ex.Message}");
                return null;
            }
        }

        public List<GameRecipe> GetRecipesByItem(uint itemId)
        {
            try
            {
                var sheet = _dataManager.GetExcelSheet<Recipe>();
                if (sheet == null) return new List<GameRecipe>();

                var recipes = new List<GameRecipe>();
                foreach (var row in sheet)
                {
                    if (row.ItemResult.RowId == itemId)
                    {
                        var recipe = GetRecipe(row.RowId);
                        if (recipe != null)
                            recipes.Add(recipe);
                    }
                }
                return recipes;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetRecipesByItem({itemId}) failed: {ex.Message}");
                return new List<GameRecipe>();
            }
        }

        public List<IngredientInfo> GetRecipeIngredients(uint recipeId)
        {
            try
            {
                var sheet = _dataManager.GetExcelSheet<Recipe>();
                if (sheet == null) return new List<IngredientInfo>();

                if (!sheet.TryGetRow(recipeId, out var row))
                    return new List<IngredientInfo>();

                var ingredients = new List<IngredientInfo>();

                // Recipe has Ingredient[0-9] and AmountIngredient[0-9] columns
                for (int i = 0; i < 10; i++)
                {
                    var ingredientId = row.Ingredient[i].RowId;
                    var amount = row.AmountIngredient[i];

                    if (ingredientId > 0 && amount > 0)
                    {
                        ingredients.Add(new IngredientInfo
                        {
                            ItemId = ingredientId,
                            ItemName = GetItemName(ingredientId),
                            Quantity = amount,
                            IsHQRequired = false // FFXIV doesn't have HQ-required ingredients anymore
                        });
                    }
                }

                return ingredients;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetRecipeIngredients({recipeId}) failed: {ex.Message}");
                return new List<IngredientInfo>();
            }
        }

        #endregion

        #region Character State - Delegates to IClientState

        public uint GetCurrentJob()
        {
            try
            {
                var player = _clientState.LocalPlayer;
                return player?.ClassJob.RowId ?? 0;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetCurrentJob failed: {ex.Message}");
                return 0;
            }
        }

        public string GetCharacterName()
        {
            try
            {
                var player = _clientState.LocalPlayer;
                return player?.Name.TextValue ?? "Unknown";
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetCharacterName failed: {ex.Message}");
                return "Unknown";
            }
        }

        public ulong GetCharacterContentId()
        {
            try
            {
                return _clientState.LocalContentId;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetCharacterContentId failed: {ex.Message}");
                return 0;
            }
        }

        public uint GetWorldId()
        {
            try
            {
                var player = _clientState.LocalPlayer;
                return player?.CurrentWorld.RowId ?? 0;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetWorldId failed: {ex.Message}");
                return 0;
            }
        }

        public string GetWorldName(uint worldId)
        {
            try
            {
                var sheet = _dataManager.GetExcelSheet<World>();
                if (sheet == null) return "Unknown";

                if (!sheet.TryGetRow(worldId, out var row))
                    return "Unknown";

                return row.Name.ExtractText();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetWorldName({worldId}) failed: {ex.Message}");
                return "Unknown";
            }
        }

        public string GetDataCenter()
        {
            try
            {
                var worldId = GetWorldId();
                var worldSheet = _dataManager.GetExcelSheet<World>();
                if (worldSheet == null) return "Unknown";

                if (!worldSheet.TryGetRow(worldId, out var worldRow))
                    return "Unknown";

                var dcSheet = _dataManager.GetExcelSheet<WorldDCGroupType>();
                if (dcSheet == null) return "Unknown";

                if (!dcSheet.TryGetRow(worldRow.DataCenter.RowId, out var dcRow))
                    return "Unknown";

                return dcRow.Name.ExtractText();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetDataCenter failed: {ex.Message}");
                return "Unknown";
            }
        }

        #endregion

        #region Inventory Queries - Delegates to FFXIVClientStructs (via safe accessors)

        public List<InventoryItemInfo> GetCharacterInventory()
        {
            // Note: Full inventory scanning requires unsafe FFXIVClientStructs access.
            // This is a placeholder that should be expanded when Phase 4 integrates
            // with the existing SafeMemoryReader pattern.
            try
            {
                _log.Debug("[DefaultGameDataProvider] GetCharacterInventory called - placeholder implementation");
                return new List<InventoryItemInfo>();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetCharacterInventory failed: {ex.Message}");
                return new List<InventoryItemInfo>();
            }
        }

        public List<InventoryItemInfo> GetArmoryChest()
        {
            try
            {
                _log.Debug("[DefaultGameDataProvider] GetArmoryChest called - placeholder implementation");
                return new List<InventoryItemInfo>();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetArmoryChest failed: {ex.Message}");
                return new List<InventoryItemInfo>();
            }
        }

        public int GetItemCount(uint itemId, bool includeHQ = false)
        {
            try
            {
                // Placeholder - will integrate with actual inventory scanning in Phase 4
                var inventory = GetCharacterInventory();
                return inventory
                    .Where(i => i.ItemId == itemId && (includeHQ || !i.IsHQ))
                    .Sum(i => i.Quantity);
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetItemCount({itemId}) failed: {ex.Message}");
                return 0;
            }
        }

        public bool IsLoggedIn()
        {
            try
            {
                return _clientState.IsLoggedIn;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] IsLoggedIn failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Consumables - Delegates to IDataManager + Inventory

        public List<GameItem> GetFoodItems()
        {
            try
            {
                // Placeholder - requires inventory scanning + food category filtering
                _log.Debug("[DefaultGameDataProvider] GetFoodItems called - placeholder implementation");
                return new List<GameItem>();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetFoodItems failed: {ex.Message}");
                return new List<GameItem>();
            }
        }

        public List<GameItem> GetPotionItems()
        {
            try
            {
                _log.Debug("[DefaultGameDataProvider] GetPotionItems called - placeholder implementation");
                return new List<GameItem>();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetPotionItems failed: {ex.Message}");
                return new List<GameItem>();
            }
        }

        public List<GameItem> GetManualItems()
        {
            try
            {
                _log.Debug("[DefaultGameDataProvider] GetManualItems called - placeholder implementation");
                return new List<GameItem>();
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetManualItems failed: {ex.Message}");
                return new List<GameItem>();
            }
        }

        #endregion

        #region Crafting State - Delegates to FFXIVClientStructs

        public bool IsCrafting()
        {
            try
            {
                // Placeholder - requires FFXIVClientStructs CraftingState access
                _log.Debug("[DefaultGameDataProvider] IsCrafting called - placeholder implementation");
                return false;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] IsCrafting failed: {ex.Message}");
                return false;
            }
        }

        public CraftingStateInfo? GetCurrentCraftingState()
        {
            try
            {
                if (!IsCrafting())
                    return null;

                // Placeholder - requires FFXIVClientStructs CraftingState access
                _log.Debug("[DefaultGameDataProvider] GetCurrentCraftingState called - placeholder implementation");
                return null;
            }
            catch (Exception ex)
            {
                _log.Warning($"[DefaultGameDataProvider] GetCurrentCraftingState failed: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
