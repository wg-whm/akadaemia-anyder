using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Detects changes in collection unlocks between scans.
    /// Tracks new unlocks and provides recent unlock history.
    /// </summary>
    public class ChangeDetector
    {
        private readonly CollectionRepository _collectionRepository;
        private readonly RecipeRepository _recipeRepository;
        private readonly GatheringRepository _gatheringRepository;
        private readonly FishingRepository _fishingRepository;
        private readonly IPluginLog _log;

        public ChangeDetector(
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
        /// Detects new unlocks by comparing current state with previous state.
        /// Compares entries by ItemId, CharacterId, and IsUnlocked status.
        /// </summary>
        /// <param name="current">Current collection state</param>
        /// <param name="previous">Previous collection state</param>
        /// <returns>List of newly unlocked entries</returns>
        public List<CollectionEntry> DetectChanges(List<CollectionEntry> current, List<CollectionEntry> previous)
        {
            try
            {
                if (current == null || previous == null)
                {
                    _log.Warning("DetectChanges called with null list");
                    return new List<CollectionEntry>();
                }

                _log.Debug($"DetectChanges: Comparing {current.Count} current vs {previous.Count} previous");

                // Create lookup dictionary for previous state
                var previousLookup = previous
                    .GroupBy(e => new { e.ItemId, e.CharacterId })
                    .ToDictionary(g => g.Key, g => g.First());

                // Find entries that are now unlocked but weren't before
                var newUnlocks = new List<CollectionEntry>();

                foreach (var currentEntry in current)
                {
                    var key = new { currentEntry.ItemId, currentEntry.CharacterId };

                    // Check if entry exists in previous state
                    if (previousLookup.TryGetValue(key, out var previousEntry))
                    {
                        // Entry existed before - check if it's newly unlocked
                        if (currentEntry.IsUnlocked && !previousEntry.IsUnlocked)
                        {
                            newUnlocks.Add(currentEntry);
                        }
                    }
                    else
                    {
                        // Entry is completely new and unlocked
                        if (currentEntry.IsUnlocked)
                        {
                            newUnlocks.Add(currentEntry);
                        }
                    }
                }

                _log.Debug($"DetectChanges: Found {newUnlocks.Count} new unlocks");
                return newUnlocks;
            }
            catch (Exception ex)
            {
                _log.Error($"DetectChanges failed: {ex.Message}");
                return new List<CollectionEntry>();
            }
        }

        /// <summary>
        /// Gets all unlocks that occurred within the specified time window.
        /// </summary>
        /// <param name="window">Time window to look back</param>
        /// <returns>List of recently unlocked entries</returns>
        public async Task<List<CollectionEntry>> GetRecentUnlocks(TimeSpan window)
        {
            try
            {
                _log.Debug($"GetRecentUnlocks: Looking back {window.TotalMinutes:F0} minutes");

                var cutoffTime = DateTime.UtcNow - window;
                var recentUnlocks = new List<CollectionEntry>();

                // Get all entries from all types
                var recipes = await _recipeRepository.GetAllAsync<RecipeEntry>();
                var nodes = await _gatheringRepository.GetAllAsync<GatheringNodeEntry>();
                var holes = await _fishingRepository.GetAllAsync<FishingHoleEntry>();

                // Combine all entries
                var allEntries = new List<CollectionEntry>();
                allEntries.AddRange(recipes);
                allEntries.AddRange(nodes);
                allEntries.AddRange(holes);

                // Filter for recent unlocks
                foreach (var entry in allEntries)
                {
                    if (entry.IsUnlocked && entry.UnlockedAt.HasValue && entry.UnlockedAt.Value >= cutoffTime)
                    {
                        recentUnlocks.Add(entry);
                    }
                }

                // Sort by unlock time (most recent first)
                recentUnlocks = recentUnlocks
                    .OrderByDescending(e => e.UnlockedAt ?? DateTime.MinValue)
                    .ToList();

                _log.Debug($"GetRecentUnlocks: Found {recentUnlocks.Count} unlocks within window");
                return recentUnlocks;
            }
            catch (Exception ex)
            {
                _log.Error($"GetRecentUnlocks failed: {ex.Message}");
                return new List<CollectionEntry>();
            }
        }
    }
}
