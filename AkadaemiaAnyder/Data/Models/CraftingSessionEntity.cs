using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Crafting session tracking entity.
    /// Maps to the 'crafting_sessions' table.
    /// Records a crafting session with start/end times and item counts.
    /// </summary>
    public class CraftingSessionEntity
    {
        /// <summary>
        /// Unique identifier for this session.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Character content ID (character-specific session).
        /// </summary>
        public ulong CharacterContentId { get; set; }

        /// <summary>
        /// Session start timestamp.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Session end timestamp.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Calculated property: duration of session.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Total items crafted during this session.
        /// </summary>
        public int ItemsCrafted { get; set; }

        /// <summary>
        /// Number of high-quality items in this session.
        /// </summary>
        public int HQCount { get; set; }

        /// <summary>
        /// Calculated property: number of normal-quality items.
        /// </summary>
        public int NQCount => ItemsCrafted - HQCount;

        /// <summary>
        /// JSON array of recipe IDs crafted in this session.
        /// Format: [recipeid1, recipeid2, ...]
        /// </summary>
        public string RecipeIdsJson { get; set; } = "[]";

        /// <summary>
        /// Deserialize recipe IDs from JSON storage.
        /// </summary>
        /// <returns>List of recipe IDs, empty list if JSON is invalid.</returns>
        public List<uint> GetRecipeIds()
        {
            try
            {
                return JsonSerializer.Deserialize<List<uint>>(RecipeIdsJson) ?? new();
            }
            catch
            {
                return new List<uint>();
            }
        }

        /// <summary>
        /// Serialize recipe IDs to JSON storage.
        /// </summary>
        /// <param name="recipeIds">List of recipe IDs to store.</param>
        public void SetRecipeIds(List<uint> recipeIds)
        {
            RecipeIdsJson = JsonSerializer.Serialize(recipeIds);
        }
    }
}
