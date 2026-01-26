using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AkadaemiaAnyder.Core.Models;
using SamplePlugin.MemoryReaders;

namespace SamplePlugin.Testing;

/// <summary>
/// Plays back memory snapshots into mock memory readers for testing.
/// Allows testing RecipeReader logic without requiring the game to run.
/// </summary>
public class MemorySnapshotPlayer
{
    /// <summary>
    /// JSON schema for memory snapshots (matches MemorySnapshotRecorder).
    /// </summary>
    public class MemorySnapshot
    {
        [JsonPropertyName("RecipeNote")]
        public string? RecipeNoteBase64 { get; set; }

        [JsonPropertyName("metadata")]
        public SnapshotMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Metadata about the snapshot.
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
    /// Loads a snapshot from a JSON file and returns a mock memory reader.
    /// </summary>
    /// <param name="filePath">Path to the snapshot JSON file</param>
    /// <returns>A MockMemoryReader configured with snapshot data, or null if file not found</returns>
    public MockMemoryReader<List<CraftingRecipe>>? LoadSnapshot(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var snapshot = JsonSerializer.Deserialize<MemorySnapshot>(json);

            if (snapshot?.RecipeNoteBase64 == null)
            {
                return null;
            }

            // Decode Base64 to get recipe data
            var recipes = DeserializeRecipeNoteFromBase64(snapshot.RecipeNoteBase64);

            // Create mock reader with the snapshot data
            var totalCount = snapshot.Metadata?.RecipeCount ?? 512; // Default: 8 classes × 64 recipes
            var unlockedCount = recipes?.Count ?? 0;

            return new MockMemoryReader<List<CraftingRecipe>>(
                mockData: recipes,
                totalCount: totalCount,
                unlockedCount: unlockedCount
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deserializes recipe data from a Base64-encoded string.
    /// </summary>
    private List<CraftingRecipe>? DeserializeRecipeNoteFromBase64(string base64Data)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Data);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<List<CraftingRecipe>>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to deserialize recipe data: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a mock memory reader with test data representing different progress states.
    /// </summary>
    /// <param name="recipesToUnlock">List of recipe IDs to mark as unlocked</param>
    /// <param name="baseCount">Total number of recipes (default: 512)</param>
    /// <returns>A MockMemoryReader configured with the specified recipes</returns>
    public static MockMemoryReader<List<CraftingRecipe>> CreateTestSnapshot(
        List<uint> recipesToUnlock,
        int baseCount = 512)
    {
        var recipes = new List<CraftingRecipe>();

        foreach (var recipeId in recipesToUnlock)
        {
            var craftingClass = (CraftingClass)(recipeId / 64); // 64 recipes per class
            recipes.Add(new CraftingRecipe
            {
                RecipeId = recipeId,
                CraftingClass = craftingClass,
                IsUnlocked = true,
                UnlockedAt = DateTime.UtcNow,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeLevel = 0,
                IsMasterRecipe = false,
                ItemLevel = 0
            });
        }

        return new MockMemoryReader<List<CraftingRecipe>>(
            mockData: recipes,
            totalCount: baseCount,
            unlockedCount: recipes.Count
        );
    }
}
