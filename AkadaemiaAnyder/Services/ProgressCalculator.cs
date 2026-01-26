using System;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Calculates completion percentages for collection types.
    /// Provides per-type and overall progress statistics.
    /// </summary>
    public class ProgressCalculator
    {
        private readonly CollectionRepository _collectionRepository;
        private readonly RecipeRepository _recipeRepository;
        private readonly GatheringRepository _gatheringRepository;
        private readonly FishingRepository _fishingRepository;
        private readonly IPluginLog _log;

        public ProgressCalculator(
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
        /// Gets completion progress for a specific collection type.
        /// </summary>
        /// <returns>Tuple of (total items, unlocked items, completion percentage)</returns>
        public async Task<(int total, int unlocked, double percentage)> GetCollectionProgress(CollectionType type)
        {
            try
            {
                _log.Debug($"GetCollectionProgress(type={type})");

                int total = 0;
                int unlocked = 0;

                switch (type)
                {
                    case CollectionType.Recipe:
                        var recipes = await _recipeRepository.GetAllAsync<RecipeEntry>();
                        total = recipes.Count;
                        unlocked = recipes.FindAll(r => r.IsUnlocked).Count;
                        break;

                    case CollectionType.GatheringNode:
                        var nodes = await _gatheringRepository.GetAllAsync<GatheringNodeEntry>();
                        total = nodes.Count;
                        unlocked = nodes.FindAll(n => n.IsUnlocked).Count;
                        break;

                    case CollectionType.FishingHole:
                        var holes = await _fishingRepository.GetAllAsync<FishingHoleEntry>();
                        total = holes.Count;
                        unlocked = holes.FindAll(f => f.IsUnlocked).Count;
                        break;

                    default:
                        _log.Warning($"Unknown collection type: {type}");
                        return (0, 0, 0.0);
                }

                var percentage = total > 0 ? (unlocked / (double)total) * 100.0 : 0.0;
                _log.Debug($"GetCollectionProgress(type={type}): {unlocked}/{total} ({percentage:F2}%)");

                return (total, unlocked, percentage);
            }
            catch (Exception ex)
            {
                _log.Error($"GetCollectionProgress failed: {ex.Message}");
                return (0, 0, 0.0);
            }
        }

        /// <summary>
        /// Gets overall completion progress across all collection types.
        /// </summary>
        /// <returns>Tuple of (total items, unlocked items, completion percentage)</returns>
        public async Task<(int totalItems, int unlockedItems, double percentage)> GetOverallProgress()
        {
            try
            {
                _log.Debug("GetOverallProgress");

                var recipeProgress = await GetCollectionProgress(CollectionType.Recipe);
                var gatheringProgress = await GetCollectionProgress(CollectionType.GatheringNode);
                var fishingProgress = await GetCollectionProgress(CollectionType.FishingHole);

                int totalItems = recipeProgress.total + gatheringProgress.total + fishingProgress.total;
                int unlockedItems = recipeProgress.unlocked + gatheringProgress.unlocked + fishingProgress.unlocked;

                var percentage = totalItems > 0 ? (unlockedItems / (double)totalItems) * 100.0 : 0.0;

                _log.Debug($"GetOverallProgress: {unlockedItems}/{totalItems} ({percentage:F2}%)");
                return (totalItems, unlockedItems, percentage);
            }
            catch (Exception ex)
            {
                _log.Error($"GetOverallProgress failed: {ex.Message}");
                return (0, 0, 0.0);
            }
        }
    }
}
