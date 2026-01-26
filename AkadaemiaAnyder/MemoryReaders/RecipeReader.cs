using System;
using System.Collections.Generic;
using AkadaemiaAnyder.Core.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using RecipeNotePtr = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;

namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Reads crafting recipe unlock state from game memory using UIState.RecipeNote.
/// Uses Lumina to get actual recipe IDs, then checks unlock status via FFXIVClientStructs.
/// MUST be wrapped with SafeMemoryReader for production use.
/// </summary>
public unsafe class RecipeReader : IMemoryReader<List<CraftingRecipe>>
{
    private readonly IPluginLog _log;
    private readonly IDataManager _dataManager;

    public RecipeReader(IPluginLog log, IDataManager dataManager)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
    }

    /// <summary>
    /// Checks if UIState and RecipeNote are accessible.
    /// </summary>
    /// <returns>True if recipe memory is available</returns>
    public bool IsAvailable()
    {
        var uiState = UIState.Instance();
        return uiState != null;
    }

    /// <summary>
    /// Reads all unlocked recipes from game memory.
    /// Uses RecipeNote.RecipeList directly instead of IsRecipeUnlocked() which was returning false for all recipes.
    /// Architecture:
    /// 1. RecipeNote.Instance() -> Get singleton
    /// 2. instance->InitializeRecipeList() -> Populate recipe data (REQUIRED!)
    /// 3. instance->RecipeList -> Get RecipeData* (offset 0xB8)
    /// 4. recipeList->Recipes -> Get RecipeEntry* array (offset 0x00)
    /// 5. recipeList->RecipeCount -> Get count (offset 0x08)
    /// 6. Each RecipeEntry has RecipeId at offset 0x3B2 (ushort)
    /// </summary>
    /// <returns>List of unlocked recipes, empty list if unavailable</returns>
    public List<CraftingRecipe>? ReadData()
    {
        _log.Information("[RecipeReader] ReadData() called - using RecipeList direct access");

        // Get RecipeNote singleton instance
        var recipeNote = RecipeNotePtr.Instance();
        if (recipeNote == null)
        {
            _log.Warning("[RecipeReader] RecipeNote.Instance() returned null - game not ready");
            return new List<CraftingRecipe>();
        }

        _log.Information("[RecipeReader] RecipeNote instance acquired, initializing RecipeList...");

        // Initialize RecipeList (populates recipe data from game memory)
        recipeNote->InitializeRecipeList();

        // Access RecipeList (RecipeData* at offset 0xB8)
        var recipeList = recipeNote->RecipeList;
        if (recipeList == null)
        {
            _log.Warning("[RecipeReader] RecipeList is still null after InitializeRecipeList() - character may not be logged in");
            return new List<CraftingRecipe>();
        }

        // Get recipe count and array pointer
        int recipeCount = recipeList->RecipeCount;
        var recipes = recipeList->Recipes;

        _log.Information($"[RecipeReader] RecipeList accessed: {recipeCount} recipes available");

        if (recipes == null || recipeCount == 0)
        {
            _log.Warning("[RecipeReader] Recipes array is null or empty");
            return new List<CraftingRecipe>();
        }

        // Get Recipe sheet from Lumina for cross-referencing
        var recipeSheet = _dataManager.GetExcelSheet<Recipe>();
        if (recipeSheet == null)
        {
            _log.Error("[RecipeReader] Failed to get Recipe sheet from Lumina");
            return new List<CraftingRecipe>();
        }

        var unlockedRecipes = new List<CraftingRecipe>();

        // Iterate through unlocked recipes in memory
        for (int i = 0; i < recipeCount; i++)
        {
            var recipeEntry = &recipes[i];
            var recipeId = recipeEntry->RecipeId;

            // Log first 10 for debugging
            if (i < 10)
            {
                _log.Information($"[RecipeReader] Entry {i}: RecipeId={recipeId}, ItemId={recipeEntry->ItemId}");
            }

            // Cross-reference with Lumina to get full recipe data
            var recipe = recipeSheet.GetRow(recipeId);
            if (recipe.RowId > 0)
            {
                var craftingClass = MapCraftingType(recipe.CraftType.RowId);

                unlockedRecipes.Add(new CraftingRecipe
                {
                    RecipeId = recipeId,
                    ItemId = recipe.ItemResult.RowId,
                    CraftingClass = craftingClass,
                    IsUnlocked = true,
                    UnlockedAt = DateTime.UtcNow,
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    RecipeLevel = (int)recipe.RecipeLevelTable.RowId,
                    IsMasterRecipe = recipe.IsSpecializationRequired,
                    ItemLevel = (int)recipe.ItemResult.Value.LevelItem.RowId
                });

                if (i < 10)
                {
                    _log.Information($"[RecipeReader]   -> Matched to: {recipe.ItemResult.Value.Name}");
                }
            }
            else
            {
                _log.Warning($"[RecipeReader] RecipeId {recipeId} not found in Lumina data");
            }
        }

        _log.Information($"[RecipeReader] Scan complete: found {unlockedRecipes.Count} unlocked recipes from RecipeList");
        return unlockedRecipes;
    }

    /// <summary>
    /// Maps Lumina CraftType row ID to CraftingClass enum.
    /// CraftType 0-7 = CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL
    /// </summary>
    private CraftingClass MapCraftingType(uint craftTypeId)
    {
        return craftTypeId switch
        {
            0 => CraftingClass.CRP,
            1 => CraftingClass.BSM,
            2 => CraftingClass.ARM,
            3 => CraftingClass.GSM,
            4 => CraftingClass.LTW,
            5 => CraftingClass.WVR,
            6 => CraftingClass.ALC,
            7 => CraftingClass.CUL,
            _ => CraftingClass.CRP // Default fallback
        };
    }

    /// <summary>
    /// Gets total number of recipes from Lumina.
    /// </summary>
    /// <returns>Total number of recipes in game data</returns>
    public int GetTotalCount()
    {
        var recipeSheet = _dataManager.GetExcelSheet<Recipe>();
        if (recipeSheet == null) return 0;

        // Count rows by iterating
        int count = 0;
        foreach (var _ in recipeSheet)
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Gets count of currently unlocked recipes.
    /// Uses RecipeList direct access instead of IsRecipeUnlocked().
    /// </summary>
    /// <returns>Number of unlocked recipes, 0 if unavailable</returns>
    public int GetUnlockedCount()
    {
        var recipeNote = RecipeNotePtr.Instance();
        if (recipeNote == null)
        {
            return 0;
        }

        // Initialize RecipeList first
        recipeNote->InitializeRecipeList();

        var recipeList = recipeNote->RecipeList;
        if (recipeList == null)
        {
            return 0;
        }

        return recipeList->RecipeCount;
    }
}
