using System;
using Dalamud.Plugin.Services;

namespace SamplePlugin;

/// <summary>
/// Validates availability of FFXIVClientStructs memory structures required for Akadaemia Anyder.
/// This validator checks whether UIState and its embedded note structures are accessible.
/// </summary>
public unsafe class MemoryStructureValidator
{
    private readonly IPluginLog _log;

    public MemoryStructureValidator(IPluginLog log)
    {
        _log = log;
    }

    /// <summary>
    /// Validation result for memory structure availability check.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Overall GO/NO-GO decision for plugin implementation.
        /// </summary>
        public bool CanProceed { get; set; }

        /// <summary>
        /// Is UIState.Instance() accessible?
        /// </summary>
        public bool UIStateAvailable { get; set; }

        /// <summary>
        /// Is RecipeNote structure accessible and usable?
        /// </summary>
        public bool RecipeNoteAvailable { get; set; }

        /// <summary>
        /// Is GatheringNote structure accessible?
        /// </summary>
        public bool GatheringNoteAvailable { get; set; }

        /// <summary>
        /// Is FishingNote structure accessible?
        /// </summary>
        public bool FishingNoteAvailable { get; set; }

        /// <summary>
        /// Detailed messages about what was found or what failed.
        /// </summary>
        public string[] Messages { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Overall recommendation based on validation results.
        /// </summary>
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates that all required memory structures are available.
    /// </summary>
    /// <returns>Validation result with availability status for each structure.</returns>
    public ValidationResult Validate()
    {
        var result = new ValidationResult();
        var messages = new System.Collections.Generic.List<string>();

        _log.Information("[MemoryValidator] Starting memory structure validation...");

        // Step 1: Check if UIState.Instance() is available
        try
        {
            var uiState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();

            if (uiState == null)
            {
                result.UIStateAvailable = false;
                messages.Add("CRITICAL: UIState.Instance() returned null");
                messages.Add("Game may not be fully loaded or Dalamud hook failed");
                _log.Error("[MemoryValidator] UIState.Instance() returned null");

                result.CanProceed = false;
                result.Recommendation = "NO-GO: UIState not available. Wait for game to fully load.";
                result.Messages = messages.ToArray();
                return result;
            }

            result.UIStateAvailable = true;
            messages.Add("✓ UIState.Instance() is available");
            _log.Information("[MemoryValidator] UIState.Instance() is available");

            // Step 2: Check RecipeNote (embedded struct, cannot be null)
            try
            {
                var recipeNote = &uiState->RecipeNote;

                // RecipeNote is an embedded struct, so the pointer itself won't be null
                // But we can check if it has valid data by checking RecipeList pointer
                if (recipeNote->RecipeList == null)
                {
                    messages.Add("⚠ RecipeNote structure accessible but RecipeList is null");
                    messages.Add("  RecipeList may not be initialized until crafting UI is opened");
                    result.RecipeNoteAvailable = false;
                    _log.Warning("[MemoryValidator] RecipeNote.RecipeList is null (may need initialization)");
                }
                else
                {
                    messages.Add($"✓ RecipeNote is available (RecipeCount: {recipeNote->RecipeList->RecipeCount})");
                    result.RecipeNoteAvailable = true;
                    _log.Information($"[MemoryValidator] RecipeNote available with {recipeNote->RecipeList->RecipeCount} recipes");
                }
            }
            catch (Exception ex)
            {
                result.RecipeNoteAvailable = false;
                messages.Add($"✗ RecipeNote access failed: {ex.Message}");
                _log.Error(ex, "[MemoryValidator] Failed to access RecipeNote");
            }

            // Step 3: Check GatheringNote (embedded struct)
            try
            {
                var gatheringNote = &uiState->GatheringNote;

                // GatheringNote is a stub in FFXIVClientStructs - we can access it but fields are undocumented
                // The structure exists, so we can mark it as "accessible" but not necessarily "usable"
                result.GatheringNoteAvailable = true;
                messages.Add("⚠ GatheringNote structure accessible (WARNING: Fields undocumented in FFXIVClientStructs)");
                messages.Add("  Reverse engineering required to use this structure");
                _log.Warning("[MemoryValidator] GatheringNote accessible but undocumented (stub definition)");
            }
            catch (Exception ex)
            {
                result.GatheringNoteAvailable = false;
                messages.Add($"✗ GatheringNote access failed: {ex.Message}");
                _log.Error(ex, "[MemoryValidator] Failed to access GatheringNote");
            }

            // Step 4: Check FishingNote (embedded struct)
            try
            {
                var fishingNote = &uiState->FishingNote;

                // FishingNote is a stub in FFXIVClientStructs - we can access it but fields are undocumented
                result.FishingNoteAvailable = true;
                messages.Add("⚠ FishingNote structure accessible (WARNING: Fields undocumented in FFXIVClientStructs)");
                messages.Add("  Reverse engineering required to use this structure");
                _log.Warning("[MemoryValidator] FishingNote accessible but undocumented (stub definition)");
            }
            catch (Exception ex)
            {
                result.FishingNoteAvailable = false;
                messages.Add($"✗ FishingNote access failed: {ex.Message}");
                _log.Error(ex, "[MemoryValidator] Failed to access FishingNote");
            }

            // Step 5: Make GO/NO-GO decision
            if (result.UIStateAvailable && result.RecipeNoteAvailable)
            {
                result.CanProceed = true;
                result.Recommendation = "CONDITIONAL GO: RecipeNote is usable. GatheringNote and FishingNote require reverse engineering or alternative approaches.";
                messages.Add("");
                messages.Add("GO/NO-GO DECISION: CONDITIONAL GO");
                messages.Add("  ✓ Recipe tracking can be implemented using RecipeNote.IsRecipeUnlocked()");
                messages.Add("  ✗ Gathering tracking needs alternative approach (stub definition in ClientStructs)");
                messages.Add("  ✗ Fishing tracking needs alternative approach (stub definition in ClientStructs)");
                messages.Add("");
                messages.Add("RECOMMENDATION: Implement recipe tracking now, investigate alternatives for gathering/fishing");

                _log.Information("[MemoryValidator] Validation result: CONDITIONAL GO");
            }
            else if (result.UIStateAvailable)
            {
                result.CanProceed = false;
                result.Recommendation = "NO-GO: RecipeNote not available or not initialized.";
                messages.Add("");
                messages.Add("GO/NO-GO DECISION: NO-GO");
                messages.Add("  RecipeNote is required but not available");
                messages.Add("  Try opening the crafting log in-game to initialize RecipeNote data");

                _log.Warning("[MemoryValidator] Validation result: NO-GO (RecipeNote unavailable)");
            }
            else
            {
                result.CanProceed = false;
                result.Recommendation = "NO-GO: UIState not available.";
                _log.Error("[MemoryValidator] Validation result: NO-GO (UIState unavailable)");
            }
        }
        catch (Exception ex)
        {
            result.CanProceed = false;
            result.UIStateAvailable = false;
            messages.Add($"CRITICAL ERROR: {ex.Message}");
            result.Recommendation = "NO-GO: Unexpected error during validation.";
            _log.Error(ex, "[MemoryValidator] Critical error during validation");
        }

        result.Messages = messages.ToArray();
        return result;
    }

    /// <summary>
    /// Prints validation results to the plugin log in a human-readable format.
    /// </summary>
    public void ValidateAndLog()
    {
        var result = Validate();

        _log.Information("========================================");
        _log.Information("Memory Structure Validation Results");
        _log.Information("========================================");

        foreach (var message in result.Messages)
        {
            _log.Information(message);
        }

        _log.Information("========================================");
        _log.Information($"Final Decision: {(result.CanProceed ? "GO (with conditions)" : "NO-GO")}");
        _log.Information("========================================");
    }
}
