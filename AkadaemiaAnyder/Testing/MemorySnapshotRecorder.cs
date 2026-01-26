using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AkadaemiaAnyder.Core.Models;
using SamplePlugin.MemoryReaders;

namespace SamplePlugin.Testing;

/// <summary>
/// Records RecipeNote structure data from live game memory into JSON snapshots.
/// Serializes RecipeNote byte arrays to Base64 for portability across machines.
/// </summary>
public class MemorySnapshotRecorder
{
    private readonly IMemoryReader<List<CraftingRecipe>> _memoryReader;

    /// <summary>
    /// JSON schema for memory snapshots.
    /// </summary>
    public class MemorySnapshot
    {
        [JsonPropertyName("RecipeNote")]
        public string? RecipeNoteBase64 { get; set; }

        [JsonPropertyName("metadata")]
        public SnapshotMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Metadata about when and how the snapshot was captured.
    /// </summary>
    public class SnapshotMetadata
    {
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("gameVersion")]
        public string? GameVersion { get; set; }

        [JsonPropertyName("characterId")]
        public uint CharacterId { get; set; }

        [JsonPropertyName("characterName")]
        public string? CharacterName { get; set; }

        [JsonPropertyName("worldName")]
        public string? WorldName { get; set; }

        [JsonPropertyName("recipeCount")]
        public int RecipeCount { get; set; }

        [JsonPropertyName("unlockedCount")]
        public int UnlockedCount { get; set; }
    }

    /// <summary>
    /// Creates a snapshot recorder that uses the provided memory reader.
    /// </summary>
    /// <param name="memoryReader">The memory reader to capture data from</param>
    public MemorySnapshotRecorder(IMemoryReader<List<CraftingRecipe>> memoryReader)
    {
        _memoryReader = memoryReader ?? throw new ArgumentNullException(nameof(memoryReader));
    }

    /// <summary>
    /// Records current game state to a JSON file.
    /// Requires the memory reader to be available (game running).
    /// </summary>
    /// <param name="filePath">Path to save the snapshot JSON file</param>
    /// <param name="metadata">Optional metadata about the capture</param>
    /// <returns>True if recording succeeded, false if memory unavailable</returns>
    public bool RecordSnapshot(string filePath, SnapshotMetadata? metadata = null)
    {
        if (!_memoryReader.IsAvailable())
        {
            return false;
        }

        // Read the recipe data from memory
        var recipes = _memoryReader.ReadData();
        if (recipes == null)
        {
            return false;
        }

        // Create snapshot with metadata
        var snapshot = new MemorySnapshot
        {
            // For now, store a placeholder - the actual RecipeNote bytes would require
            // serializing the FFXIVClientStructs.RecipeNote structure directly.
            // This requires unsafe code to access the raw memory and copy bytes.
            RecipeNoteBase64 = SerializeRecipeNoteToBase64(recipes),
            Metadata = metadata ?? CreateDefaultMetadata(recipes)
        };

        // Serialize to JSON
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(snapshot, options);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to file
        File.WriteAllText(filePath, json);
        return true;
    }

    /// <summary>
    /// Serializes the recipe data to a Base64 string for storage.
    /// In a real implementation, this would capture the raw RecipeNote bytes from memory.
    /// </summary>
    private string SerializeRecipeNoteToBase64(List<CraftingRecipe> recipes)
    {
        // Serialize the recipe list as JSON, then convert to Base64
        // This is a portable format that can be decoded on any machine
        var json = JsonSerializer.Serialize(recipes);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Creates default metadata based on current game state.
    /// </summary>
    private SnapshotMetadata CreateDefaultMetadata(List<CraftingRecipe> recipes)
    {
        return new SnapshotMetadata
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            GameVersion = "7.15", // Default version
            CharacterId = 0, // Would need to read from game state
            CharacterName = "Unknown",
            WorldName = "Unknown",
            RecipeCount = _memoryReader.GetTotalCount(),
            UnlockedCount = recipes.Count
        };
    }
}
