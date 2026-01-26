using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Imports collection data from JSON format with validation.
    /// Uses merge strategy to preserve existing unlock dates.
    /// </summary>
    public class JsonImporter
    {
        private readonly CollectionRepository _collectionRepository;
        private readonly RecipeRepository _recipeRepository;
        private readonly GatheringRepository _gatheringRepository;
        private readonly FishingRepository _fishingRepository;
        private readonly IPluginLog _log;

        public JsonImporter(
            CollectionRepository collectionRepository,
            RecipeRepository recipeRepository,
            GatheringRepository gatheringRepository,
            FishingRepository fishingRepository,
            IPluginLog log)
        {
            _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
            _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
            _gatheringRepository = gatheringRepository ?? throw new ArgumentNullException(nameof(gatheringRepository));
            _fishingRepository = fishingRepository ?? throw new ArgumentNullException(nameof(fishingRepository));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Validates a JSON import file without performing the import.
        /// </summary>
        /// <param name="filePath">Path to JSON file to validate</param>
        /// <returns>Tuple of (valid, error message)</returns>
        public (bool valid, string? error) ValidateFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return (false, "File does not exist");
                }

                // Read and parse JSON
                var json = File.ReadAllText(filePath);
                var document = JsonNode.Parse(json);

                if (document == null)
                {
                    return (false, "Invalid JSON format");
                }

                // Check for required Metadata section
                var metadata = document["Metadata"];
                if (metadata == null)
                {
                    return (false, "Missing required 'Metadata' section");
                }

                // Check schema version
                var schemaVersion = metadata["SchemaVersion"]?.GetValue<int>();
                if (!schemaVersion.HasValue)
                {
                    return (false, "Missing required 'Metadata.SchemaVersion' field");
                }

                if (schemaVersion.Value != 1)
                {
                    return (false, $"Unsupported schema version: {schemaVersion.Value} (expected: 1)");
                }

                // Check for at least one data section
                var hasRecipes = document["Recipes"] != null;
                var hasGatheringNodes = document["GatheringNodes"] != null;
                var hasFishingHoles = document["FishingHoles"] != null;
                var hasEntries = document["Entries"] != null;

                if (!hasRecipes && !hasGatheringNodes && !hasFishingHoles && !hasEntries)
                {
                    return (false, "No collection data found (expected 'Recipes', 'GatheringNodes', 'FishingHoles', or 'Entries')");
                }

                _log.Information($"ValidateFile: {filePath} is valid");
                return (true, null);
            }
            catch (JsonException ex)
            {
                return (false, $"JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _log.Error($"ValidateFile failed: {ex.Message}");
                return (false, $"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports collection data from a JSON file.
        /// Merge strategy: Preserve existing unlock dates, don't overwrite with older data.
        /// </summary>
        /// <param name="filePath">Path to JSON file to import</param>
        /// <returns>Tuple of (success, imported count, error message)</returns>
        public async Task<(bool success, int imported, string? error)> ImportAsync(string filePath)
        {
            try
            {
                _log.Information($"ImportAsync: Starting import from {filePath}");

                // Validate file first
                var validation = ValidateFile(filePath);
                if (!validation.valid)
                {
                    _log.Warning($"ImportAsync: Validation failed - {validation.error}");
                    return (false, 0, validation.error);
                }

                // Read and parse JSON
                var json = await File.ReadAllTextAsync(filePath);
                var document = JsonNode.Parse(json);

                int totalImported = 0;

                // Import recipes if present
                var recipesNode = document!["Recipes"];
                if (recipesNode != null)
                {
                    var recipes = JsonSerializer.Deserialize<List<RecipeEntry>>(recipesNode.ToJsonString());
                    if (recipes != null)
                    {
                        var importedRecipes = await ImportRecipes(recipes);
                        totalImported += importedRecipes;
                        _log.Information($"ImportAsync: Imported {importedRecipes} recipes");
                    }
                }

                // Import gathering nodes if present
                var nodesNode = document["GatheringNodes"];
                if (nodesNode != null)
                {
                    var nodes = JsonSerializer.Deserialize<List<GatheringNodeEntry>>(nodesNode.ToJsonString());
                    if (nodes != null)
                    {
                        var importedNodes = await ImportGatheringNodes(nodes);
                        totalImported += importedNodes;
                        _log.Information($"ImportAsync: Imported {importedNodes} gathering nodes");
                    }
                }

                // Import fishing holes if present
                var holesNode = document["FishingHoles"];
                if (holesNode != null)
                {
                    var holes = JsonSerializer.Deserialize<List<FishingHoleEntry>>(holesNode.ToJsonString());
                    if (holes != null)
                    {
                        var importedHoles = await ImportFishingHoles(holes);
                        totalImported += importedHoles;
                        _log.Information($"ImportAsync: Imported {importedHoles} fishing holes");
                    }
                }

                // Handle single-type export format (has "Entries" instead of type-specific arrays)
                var entriesNode = document["Entries"];
                if (entriesNode != null)
                {
                    // Need to determine type from metadata
                    var collectionType = document["Metadata"]?["CollectionType"]?.GetValue<string>();
                    if (collectionType != null)
                    {
                        var imported = await ImportEntriesByType(entriesNode.ToJsonString(), collectionType);
                        totalImported += imported;
                        _log.Information($"ImportAsync: Imported {imported} entries of type {collectionType}");
                    }
                }

                _log.Information($"ImportAsync: Successfully imported {totalImported} total entries");
                return (true, totalImported, null);
            }
            catch (Exception ex)
            {
                _log.Error($"ImportAsync failed: {ex.Message}");
                return (false, 0, $"Import error: {ex.Message}");
            }
        }

        private async Task<int> ImportRecipes(List<RecipeEntry> importedRecipes)
        {
            var existingRecipes = await _recipeRepository.GetAllAsync<RecipeEntry>();
            var existingLookup = existingRecipes.ToDictionary(r => new { r.ItemId, r.CharacterId });

            var toImport = new List<RecipeEntry>();

            foreach (var imported in importedRecipes)
            {
                var key = new { imported.ItemId, imported.CharacterId };

                if (existingLookup.TryGetValue(key, out var existing))
                {
                    // Entry exists - merge strategy: keep newer unlock date
                    if (imported.IsUnlocked && !existing.IsUnlocked)
                    {
                        // Import has unlock, existing doesn't - use import data
                        imported.Id = existing.Id; // Preserve DB ID
                        toImport.Add(imported);
                    }
                    else if (imported.IsUnlocked && existing.IsUnlocked)
                    {
                        // Both unlocked - keep earlier unlock date
                        if (imported.UnlockedAt.HasValue && existing.UnlockedAt.HasValue)
                        {
                            if (imported.UnlockedAt.Value < existing.UnlockedAt.Value)
                            {
                                // Import is older - use import unlock date
                                existing.UnlockedAt = imported.UnlockedAt;
                                toImport.Add(existing);
                            }
                        }
                    }
                }
                else
                {
                    // New entry - add it
                    imported.Id = 0; // Clear ID to force insert
                    toImport.Add(imported);
                }
            }

            if (toImport.Count > 0)
            {
                await _recipeRepository.BulkUpsertAsync(toImport);
            }

            return toImport.Count;
        }

        private async Task<int> ImportGatheringNodes(List<GatheringNodeEntry> importedNodes)
        {
            var existingNodes = await _gatheringRepository.GetAllAsync<GatheringNodeEntry>();
            var existingLookup = existingNodes.ToDictionary(n => new { n.ItemId, n.CharacterId });

            var toImport = new List<GatheringNodeEntry>();

            foreach (var imported in importedNodes)
            {
                var key = new { imported.ItemId, imported.CharacterId };

                if (existingLookup.TryGetValue(key, out var existing))
                {
                    // Entry exists - merge strategy
                    if (imported.IsUnlocked && !existing.IsUnlocked)
                    {
                        imported.Id = existing.Id;
                        toImport.Add(imported);
                    }
                    else if (imported.IsUnlocked && existing.IsUnlocked)
                    {
                        if (imported.UnlockedAt.HasValue && existing.UnlockedAt.HasValue)
                        {
                            if (imported.UnlockedAt.Value < existing.UnlockedAt.Value)
                            {
                                existing.UnlockedAt = imported.UnlockedAt;
                                toImport.Add(existing);
                            }
                        }
                    }
                }
                else
                {
                    imported.Id = 0;
                    toImport.Add(imported);
                }
            }

            if (toImport.Count > 0)
            {
                await _gatheringRepository.BulkUpsertAsync(toImport);
            }

            return toImport.Count;
        }

        private async Task<int> ImportFishingHoles(List<FishingHoleEntry> importedHoles)
        {
            var existingHoles = await _fishingRepository.GetAllAsync<FishingHoleEntry>();
            var existingLookup = existingHoles.ToDictionary(h => new { h.ItemId, h.CharacterId });

            var toImport = new List<FishingHoleEntry>();

            foreach (var imported in importedHoles)
            {
                var key = new { imported.ItemId, imported.CharacterId };

                if (existingLookup.TryGetValue(key, out var existing))
                {
                    // Entry exists - merge strategy
                    if (imported.IsUnlocked && !existing.IsUnlocked)
                    {
                        imported.Id = existing.Id;
                        toImport.Add(imported);
                    }
                    else if (imported.IsUnlocked && existing.IsUnlocked)
                    {
                        if (imported.UnlockedAt.HasValue && existing.UnlockedAt.HasValue)
                        {
                            if (imported.UnlockedAt.Value < existing.UnlockedAt.Value)
                            {
                                existing.UnlockedAt = imported.UnlockedAt;
                                toImport.Add(existing);
                            }
                        }
                    }
                }
                else
                {
                    imported.Id = 0;
                    toImport.Add(imported);
                }
            }

            if (toImport.Count > 0)
            {
                await _fishingRepository.BulkUpsertAsync(toImport);
            }

            return toImport.Count;
        }

        private async Task<int> ImportEntriesByType(string entriesJson, string collectionType)
        {
            switch (collectionType)
            {
                case "Recipe":
                    var recipes = JsonSerializer.Deserialize<List<RecipeEntry>>(entriesJson);
                    return recipes != null ? await ImportRecipes(recipes) : 0;

                case "GatheringNode":
                    var nodes = JsonSerializer.Deserialize<List<GatheringNodeEntry>>(entriesJson);
                    return nodes != null ? await ImportGatheringNodes(nodes) : 0;

                case "FishingHole":
                    var holes = JsonSerializer.Deserialize<List<FishingHoleEntry>>(entriesJson);
                    return holes != null ? await ImportFishingHoles(holes) : 0;

                default:
                    _log.Warning($"Unknown collection type in import: {collectionType}");
                    return 0;
            }
        }
    }
}
