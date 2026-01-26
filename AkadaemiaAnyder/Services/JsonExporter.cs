using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Exports collection data to JSON format with metadata.
    /// Supports exporting all collections or filtering by type.
    /// </summary>
    public class JsonExporter
    {
        private readonly CollectionRepository _collectionRepository;
        private readonly RecipeRepository _recipeRepository;
        private readonly GatheringRepository _gatheringRepository;
        private readonly FishingRepository _fishingRepository;
        private readonly DatabaseContext _databaseContext;
        private readonly IPluginLog _log;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public JsonExporter(
            CollectionRepository collectionRepository,
            RecipeRepository recipeRepository,
            GatheringRepository gatheringRepository,
            FishingRepository fishingRepository,
            DatabaseContext databaseContext,
            IPluginLog log)
        {
            _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
            _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
            _gatheringRepository = gatheringRepository ?? throw new ArgumentNullException(nameof(gatheringRepository));
            _fishingRepository = fishingRepository ?? throw new ArgumentNullException(nameof(fishingRepository));
            _databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Exports all collections to a JSON file with metadata.
        /// </summary>
        /// <param name="filePath">Target file path for export</param>
        /// <returns>True if export succeeded, false otherwise</returns>
        public async Task<bool> ExportAllAsync(string filePath)
        {
            try
            {
                _log.Information($"ExportAllAsync: Exporting to {filePath}");

                // Gather all data
                var recipes = await _recipeRepository.GetAllAsync<RecipeEntry>();
                var nodes = await _gatheringRepository.GetAllAsync<GatheringNodeEntry>();
                var holes = await _fishingRepository.GetAllAsync<FishingHoleEntry>();

                // Get character info from first entry (if any)
                var characterName = "Unknown";
                var worldName = "Unknown";
                var firstEntry = recipes.Cast<CollectionEntry>().FirstOrDefault()
                    ?? nodes.Cast<CollectionEntry>().FirstOrDefault()
                    ?? holes.Cast<CollectionEntry>().FirstOrDefault();

                if (firstEntry != null)
                {
                    characterName = firstEntry.CharacterName;
                    worldName = firstEntry.WorldName;
                }

                // Build export data structure
                var exportData = new
                {
                    Metadata = new
                    {
                        ExportTimestamp = DateTime.UtcNow,
                        SchemaVersion = 1,
                        CharacterName = characterName,
                        WorldName = worldName,
                        DatabaseTier = _databaseContext.GetHealthStatus().ToString(),
                        TotalRecipes = recipes.Count,
                        TotalGatheringNodes = nodes.Count,
                        TotalFishingHoles = holes.Count,
                        UnlockedRecipes = recipes.Count(r => r.IsUnlocked),
                        UnlockedGatheringNodes = nodes.Count(n => n.IsUnlocked),
                        UnlockedFishingHoles = holes.Count(h => h.IsUnlocked)
                    },
                    Recipes = recipes,
                    GatheringNodes = nodes,
                    FishingHoles = holes
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(exportData, JsonOptions);

                // Write to file
                await File.WriteAllTextAsync(filePath, json);

                _log.Information($"ExportAllAsync: Successfully exported {recipes.Count + nodes.Count + holes.Count} entries to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"ExportAllAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Exports collections of a specific type to a JSON file.
        /// </summary>
        /// <param name="type">Collection type to export</param>
        /// <param name="filePath">Target file path for export</param>
        /// <returns>True if export succeeded, false otherwise</returns>
        public async Task<bool> ExportByTypeAsync(CollectionType type, string filePath)
        {
            try
            {
                _log.Information($"ExportByTypeAsync: Exporting type={type} to {filePath}");

                List<CollectionEntry> entries;
                string typeName;

                switch (type)
                {
                    case CollectionType.Recipe:
                        var recipes = await _recipeRepository.GetAllAsync<RecipeEntry>();
                        entries = recipes.Cast<CollectionEntry>().ToList();
                        typeName = "Recipes";
                        break;

                    case CollectionType.GatheringNode:
                        var nodes = await _gatheringRepository.GetAllAsync<GatheringNodeEntry>();
                        entries = nodes.Cast<CollectionEntry>().ToList();
                        typeName = "GatheringNodes";
                        break;

                    case CollectionType.FishingHole:
                        var holes = await _fishingRepository.GetAllAsync<FishingHoleEntry>();
                        entries = holes.Cast<CollectionEntry>().ToList();
                        typeName = "FishingHoles";
                        break;

                    default:
                        _log.Warning($"Unknown collection type: {type}");
                        return false;
                }

                // Get character info from first entry (if any)
                var characterName = "Unknown";
                var worldName = "Unknown";
                if (entries.Count > 0)
                {
                    characterName = entries[0].CharacterName;
                    worldName = entries[0].WorldName;
                }

                // Build export data structure
                var exportData = new
                {
                    Metadata = new
                    {
                        ExportTimestamp = DateTime.UtcNow,
                        SchemaVersion = 1,
                        CollectionType = type.ToString(),
                        CharacterName = characterName,
                        WorldName = worldName,
                        DatabaseTier = _databaseContext.GetHealthStatus().ToString(),
                        TotalEntries = entries.Count,
                        UnlockedEntries = entries.Count(e => e.IsUnlocked)
                    },
                    Entries = entries
                };

                // Serialize to JSON
                var json = JsonSerializer.Serialize(exportData, JsonOptions);

                // Write to file
                await File.WriteAllTextAsync(filePath, json);

                _log.Information($"ExportByTypeAsync: Successfully exported {entries.Count} {typeName} to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"ExportByTypeAsync failed: {ex.Message}");
                return false;
            }
        }
    }
}
