using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AkadaemiaAnyder.Core.Models;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;
using SamplePlugin.EventListeners;
using SamplePlugin.MemoryReaders;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Orchestrates collection scanning across all three collection types.
    /// Implements partial success contract: if 1+ readers succeed, overall Success=true.
    /// </summary>
    public class CollectionService : ICollectionService
    {
        private readonly CollectionRepository _collectionRepository;
        private readonly RecipeRepository _recipeRepository;
        private readonly GatheringRepository _gatheringRepository;
        private readonly FishingRepository _fishingRepository;
        private readonly SafeMemoryReader<List<CraftingRecipe>> _safeRecipeReader;
        private readonly GatheringEventListener _gatheringListener;
        private readonly FishingEventListener _fishingListener;
        private readonly IClientState _clientState;
        private readonly IPluginLog _log;

        public CollectionService(
            CollectionRepository collectionRepository,
            RecipeRepository recipeRepository,
            GatheringRepository gatheringRepository,
            FishingRepository fishingRepository,
            RecipeReader recipeReader,
            GatheringEventListener gatheringListener,
            FishingEventListener fishingListener,
            IClientState clientState,
            IPluginLog log)
        {
            _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
            _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
            _gatheringRepository = gatheringRepository ?? throw new ArgumentNullException(nameof(gatheringRepository));
            _fishingRepository = fishingRepository ?? throw new ArgumentNullException(nameof(fishingRepository));
            _gatheringListener = gatheringListener ?? throw new ArgumentNullException(nameof(gatheringListener));
            _fishingListener = fishingListener ?? throw new ArgumentNullException(nameof(fishingListener));
            _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            // Wrap RecipeReader in SafeMemoryReader for exception handling
            if (recipeReader == null) throw new ArgumentNullException(nameof(recipeReader));
            _safeRecipeReader = new SafeMemoryReader<List<CraftingRecipe>>(
                recipeReader,
                msg => _log.Error(msg),
                msg => _log.Warning(msg)
            );
        }

        public async Task<ScanResult> ScanAllCollectionsAsync()
        {
            _log.Information("Starting ScanAllCollectionsAsync");
            var stopwatch = Stopwatch.StartNew();

            var errors = new List<string>();
            int totalScanned = 0;
            int totalUpdated = 0;
            int totalNew = 0;
            int successCount = 0;

            // Scan recipes
            try
            {
                var recipeResult = await ScanRecipesAsync();
                if (recipeResult.Success)
                {
                    successCount++;
                    totalScanned += recipeResult.ItemsScanned;
                    totalUpdated += recipeResult.ItemsUpdated;
                    totalNew += recipeResult.NewItems;
                    _log.Information($"Recipe scan succeeded: {recipeResult.ItemsScanned} scanned, {recipeResult.ItemsUpdated} updated");
                }
                else
                {
                    errors.Add($"Recipe scan failed: {recipeResult.ErrorMessage}");
                    _log.Warning($"Recipe scan failed: {recipeResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Recipe scan exception: {ex.Message}");
                _log.Error($"Recipe scan exception: {ex.Message}");
            }

            // Scan gathering
            try
            {
                var gatheringResult = await ScanGatheringAsync();
                if (gatheringResult.Success)
                {
                    successCount++;
                    totalScanned += gatheringResult.ItemsScanned;
                    totalUpdated += gatheringResult.ItemsUpdated;
                    totalNew += gatheringResult.NewItems;
                    _log.Information($"Gathering scan succeeded: {gatheringResult.ItemsScanned} scanned, {gatheringResult.ItemsUpdated} updated");
                }
                else
                {
                    errors.Add($"Gathering scan failed: {gatheringResult.ErrorMessage}");
                    _log.Warning($"Gathering scan failed: {gatheringResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Gathering scan exception: {ex.Message}");
                _log.Error($"Gathering scan exception: {ex.Message}");
            }

            // Scan fishing
            try
            {
                var fishingResult = await ScanFishingAsync();
                if (fishingResult.Success)
                {
                    successCount++;
                    totalScanned += fishingResult.ItemsScanned;
                    totalUpdated += fishingResult.ItemsUpdated;
                    totalNew += fishingResult.NewItems;
                    _log.Information($"Fishing scan succeeded: {fishingResult.ItemsScanned} scanned, {fishingResult.ItemsUpdated} updated");
                }
                else
                {
                    errors.Add($"Fishing scan failed: {fishingResult.ErrorMessage}");
                    _log.Warning($"Fishing scan failed: {fishingResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Fishing scan exception: {ex.Message}");
                _log.Error($"Fishing scan exception: {ex.Message}");
            }

            stopwatch.Stop();

            // Partial success contract: if 1+ scanners succeeded, overall Success=true
            if (successCount > 0)
            {
                var errorSummary = errors.Any() ? $" (with {errors.Count} errors)" : "";
                _log.Information($"ScanAllCollectionsAsync completed: {successCount}/3 scanners succeeded, {totalScanned} total scanned{errorSummary}");

                return new ScanResult
                {
                    Success = true,
                    ItemsScanned = totalScanned,
                    ItemsUpdated = totalUpdated,
                    NewItems = totalNew,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = errors.Any() ? string.Join("; ", errors) : null
                };
            }
            else
            {
                // All scanners failed
                _log.Error($"ScanAllCollectionsAsync failed: all scanners failed");
                return new ScanResult
                {
                    Success = false,
                    ItemsScanned = 0,
                    ItemsUpdated = 0,
                    NewItems = 0,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = string.Join("; ", errors),
                    ErrorType = ScanErrorType.MemoryUnavailable
                };
            }
        }

        public async Task<ScanResult> ScanRecipesAsync()
        {
            _log.Information("Starting ScanRecipesAsync");
            var stopwatch = Stopwatch.StartNew();

            if (!_clientState.IsLoggedIn)
            {
                _log.Warning("ScanRecipesAsync: Player not logged in");
                return ScanResult.FailureResult(ScanErrorType.GameNotRunning, "Player not logged in");
            }

            var errors = new List<string>();

            try
            {
                // Read recipe data from memory via SafeMemoryReader
                var recipeData = await Task.Run(() => _safeRecipeReader.ReadData());

                if (recipeData != null)
                {
                    // Filter out null items (critical null handling pattern from symposium Round 9)
                    var validRecipes = recipeData.Where(r => r != null).ToList();

                    if (validRecipes.Any())
                    {
                        _log.Information($"ScanRecipesAsync: Retrieved {validRecipes.Count} valid recipes from memory");

                        // Convert Core.Models.CraftingRecipe to Data.Models.RecipeEntry
                        var recipeEntries = ConvertCraftingRecipesToEntries(validRecipes);

                        // Bulk upsert to database
                        var updatedCount = await _recipeRepository.BulkUpsertAsync(recipeEntries);

                        stopwatch.Stop();
                        _log.Information($"ScanRecipesAsync: Updated {updatedCount} recipe entries in {stopwatch.ElapsedMilliseconds}ms");

                        return ScanResult.SuccessResult(
                            scanned: validRecipes.Count,
                            updated: updatedCount,
                            newItems: updatedCount, // Simplified - could track actual new vs updated
                            duration: stopwatch.Elapsed
                        );
                    }
                    else
                    {
                        errors.Add("Recipe reader returned empty list after null filtering");
                        _log.Warning("ScanRecipesAsync: No valid recipes after null filtering");
                    }
                }
                else
                {
                    errors.Add("Recipe reader returned null");
                    _log.Warning("ScanRecipesAsync: Recipe reader returned null");
                }

                // If we get here, read failed but didn't throw
                stopwatch.Stop();
                return new ScanResult
                {
                    Success = false,
                    ItemsScanned = 0,
                    ItemsUpdated = 0,
                    NewItems = 0,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = string.Join("; ", errors),
                    ErrorType = ScanErrorType.MemoryUnavailable
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log.Error($"ScanRecipesAsync exception: {ex.Message}");
                return ScanResult.FailureResult(
                    ScanErrorType.MemoryAccessViolation,
                    $"Exception during recipe scan: {ex.Message}"
                );
            }
        }

        public async Task<ScanResult> ScanGatheringAsync()
        {
            _log.Information("Starting ScanGatheringAsync");
            var stopwatch = Stopwatch.StartNew();

            if (!_clientState.IsLoggedIn)
            {
                _log.Warning("ScanGatheringAsync: Player not logged in");
                return ScanResult.FailureResult(ScanErrorType.GameNotRunning, "Player not logged in");
            }

            try
            {
                // Get collected items from event listener
                var gatheringData = _gatheringListener.GetCollectedItems();

                if (gatheringData != null)
                {
                    // Filter out null items
                    var validNodes = gatheringData.Where(n => n != null).ToList();

                    if (validNodes.Any())
                    {
                        _log.Information($"ScanGatheringAsync: Retrieved {validNodes.Count} valid gathering nodes from listener");

                        // Convert Core.Models.GatheringNode to Data.Models.GatheringNodeEntry
                        var nodeEntries = ConvertGatheringNodesToEntries(validNodes);

                        // Bulk upsert to database
                        var updatedCount = await _gatheringRepository.BulkUpsertAsync(nodeEntries);

                        stopwatch.Stop();
                        _log.Information($"ScanGatheringAsync: Updated {updatedCount} node entries in {stopwatch.ElapsedMilliseconds}ms");

                        return ScanResult.SuccessResult(
                            scanned: validNodes.Count,
                            updated: updatedCount,
                            newItems: updatedCount,
                            duration: stopwatch.Elapsed
                        );
                    }
                    else
                    {
                        // No nodes collected yet - this is not an error, just nothing to process
                        stopwatch.Stop();
                        _log.Debug("ScanGatheringAsync: No gathering nodes collected yet");
                        return ScanResult.SuccessResult(0, 0, 0, stopwatch.Elapsed);
                    }
                }
                else
                {
                    stopwatch.Stop();
                    _log.Warning("ScanGatheringAsync: Listener returned null");
                    return ScanResult.FailureResult(
                        ScanErrorType.MemoryUnavailable,
                        "Gathering listener returned null"
                    );
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log.Error($"ScanGatheringAsync exception: {ex.Message}");
                return ScanResult.FailureResult(
                    ScanErrorType.DatabaseError,
                    $"Exception during gathering scan: {ex.Message}"
                );
            }
        }

        public async Task<ScanResult> ScanFishingAsync()
        {
            _log.Information("Starting ScanFishingAsync");
            var stopwatch = Stopwatch.StartNew();

            if (!_clientState.IsLoggedIn)
            {
                _log.Warning("ScanFishingAsync: Player not logged in");
                return ScanResult.FailureResult(ScanErrorType.GameNotRunning, "Player not logged in");
            }

            try
            {
                // Get collected items from event listener
                var fishingData = _fishingListener.GetCollectedItems();

                if (fishingData != null)
                {
                    // Filter out null items
                    var validHoles = fishingData.Where(h => h != null).ToList();

                    if (validHoles.Any())
                    {
                        _log.Information($"ScanFishingAsync: Retrieved {validHoles.Count} valid fishing holes from listener");

                        // Convert Core.Models.FishingHole to Data.Models.FishingHoleEntry
                        var holeEntries = ConvertFishingHolesToEntries(validHoles);

                        // Bulk upsert to database
                        var updatedCount = await _fishingRepository.BulkUpsertAsync(holeEntries);

                        stopwatch.Stop();
                        _log.Information($"ScanFishingAsync: Updated {updatedCount} fishing entries in {stopwatch.ElapsedMilliseconds}ms");

                        return ScanResult.SuccessResult(
                            scanned: validHoles.Count,
                            updated: updatedCount,
                            newItems: updatedCount,
                            duration: stopwatch.Elapsed
                        );
                    }
                    else
                    {
                        // No fish collected yet - this is not an error, just nothing to process
                        stopwatch.Stop();
                        _log.Debug("ScanFishingAsync: No fishing holes collected yet");
                        return ScanResult.SuccessResult(0, 0, 0, stopwatch.Elapsed);
                    }
                }
                else
                {
                    stopwatch.Stop();
                    _log.Warning("ScanFishingAsync: Listener returned null");
                    return ScanResult.FailureResult(
                        ScanErrorType.MemoryUnavailable,
                        "Fishing listener returned null"
                    );
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log.Error($"ScanFishingAsync exception: {ex.Message}");
                return ScanResult.FailureResult(
                    ScanErrorType.DatabaseError,
                    $"Exception during fishing scan: {ex.Message}"
                );
            }
        }

        public async Task<(int total, int unlocked, double percentage)> GetStatsAsync(AkadaemiaAnyder.Data.Models.CollectionType type)
        {
            _log.Debug($"GetStatsAsync(type={type})");

            try
            {
                List<AkadaemiaAnyder.Data.Models.CollectionEntry> allEntries;

                switch (type)
                {
                    case AkadaemiaAnyder.Data.Models.CollectionType.Recipe:
                        allEntries = (await _recipeRepository.GetAllAsync<RecipeEntry>()).Cast<AkadaemiaAnyder.Data.Models.CollectionEntry>().ToList();
                        break;
                    case AkadaemiaAnyder.Data.Models.CollectionType.GatheringNode:
                        allEntries = (await _gatheringRepository.GetAllAsync<GatheringNodeEntry>()).Cast<AkadaemiaAnyder.Data.Models.CollectionEntry>().ToList();
                        break;
                    case AkadaemiaAnyder.Data.Models.CollectionType.FishingHole:
                        allEntries = (await _fishingRepository.GetAllAsync<FishingHoleEntry>()).Cast<AkadaemiaAnyder.Data.Models.CollectionEntry>().ToList();
                        break;
                    default:
                        _log.Warning($"Unknown collection type: {type}");
                        return (0, 0, 0.0);
                }

                var total = allEntries.Count;
                var unlocked = allEntries.Count(e => e.IsUnlocked);
                var percentage = total > 0 ? (unlocked / (double)total) * 100.0 : 0.0;

                _log.Debug($"GetStatsAsync(type={type}): {unlocked}/{total} ({percentage:F2}%)");
                return (total, unlocked, percentage);
            }
            catch (Exception ex)
            {
                _log.Error($"GetStatsAsync exception: {ex.Message}");
                return (0, 0, 0.0);
            }
        }

        /// <summary>
        /// Converts Core.Models.CraftingRecipe to Data.Models.RecipeEntry.
        /// </summary>
        private List<RecipeEntry> ConvertCraftingRecipesToEntries(List<CraftingRecipe> recipes)
        {
            // Use simple character tracking - ID 0 for now (could be enhanced with proper character service)
            var characterId = 0;
            // Note: LocalPlayer is deprecated and requires main thread
            // For now use placeholder values - character tracking can be enhanced later
            var characterName = _clientState.LocalContentId.ToString() ?? "Unknown";
            var worldName = "Unknown";

            return recipes.Select(r => new RecipeEntry
            {
                CharacterId = (int)characterId,
                CharacterName = characterName,
                WorldName = worldName,
                Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
                ItemId = (int)r.ItemId,
                ItemName = r.ItemName,
                IsUnlocked = r.IsUnlocked,
                UnlockedAt = r.UnlockedAt,
                FirstSeenAt = r.FirstSeenAt,
                LastUpdatedAt = r.LastUpdatedAt,
                RecipeId = (int)r.RecipeId,
                RecipeLevel = r.RecipeLevel,
                CraftingClass = MapCraftingClass(r.CraftingClass),
                IsMasterRecipe = r.IsMasterRecipe,
                MasterBookId = r.MasterBookId.HasValue ? (int)r.MasterBookId.Value : null,
                ItemLevel = r.ItemLevel
            }).ToList();
        }

        /// <summary>
        /// Converts Core.Models.GatheringNode to Data.Models.GatheringNodeEntry.
        /// </summary>
        private List<GatheringNodeEntry> ConvertGatheringNodesToEntries(List<GatheringNode> nodes)
        {
            // Use simple character tracking - ID 0 for now (could be enhanced with proper character service)
            var characterId = 0;
            // Note: LocalPlayer deprecated and requires main thread - use ContentId instead
            var characterName = _clientState.LocalContentId.ToString();
            var worldName = "Unknown";

            return nodes.Select(n => new GatheringNodeEntry
            {
                CharacterId = (int)characterId,
                CharacterName = characterName,
                WorldName = worldName,
                Type = AkadaemiaAnyder.Data.Models.CollectionType.GatheringNode,
                ItemId = (int)n.ItemId,
                ItemName = n.ItemName,
                IsUnlocked = n.IsUnlocked,
                UnlockedAt = n.UnlockedAt,
                FirstSeenAt = n.FirstSeenAt,
                LastUpdatedAt = n.LastUpdatedAt,
                NodeId = (int)n.NodeId,
                GatheringClass = MapGatheringClass(n.GatheringClass),
                Zone = n.Zone,
                FolkloreBookId = n.FolkloreBookId.HasValue ? (int)n.FolkloreBookId.Value : null,
                NodeLevel = n.NodeLevel,
                IsLegendary = n.IsLegendary,
                IsEphemeral = n.IsEphemeral
            }).ToList();
        }

        /// <summary>
        /// Converts Core.Models.FishingHole to Data.Models.FishingHoleEntry.
        /// </summary>
        private List<FishingHoleEntry> ConvertFishingHolesToEntries(List<FishingHole> holes)
        {
            // Use simple character tracking - ID 0 for now (could be enhanced with proper character service)
            var characterId = 0;
            // Note: LocalPlayer deprecated and requires main thread - use ContentId instead
            var characterName = _clientState.LocalContentId.ToString();
            var worldName = "Unknown";

            return holes.Select(h => new FishingHoleEntry
            {
                CharacterId = (int)characterId,
                CharacterName = characterName,
                WorldName = worldName,
                Type = AkadaemiaAnyder.Data.Models.CollectionType.FishingHole,
                ItemId = (int)h.ItemId,
                ItemName = h.ItemName,
                IsUnlocked = h.IsUnlocked,
                UnlockedAt = h.UnlockedAt,
                FirstSeenAt = h.FirstSeenAt,
                LastUpdatedAt = h.LastUpdatedAt,
                FishId = (int)h.FishId,
                FishingHoleId = (int)h.FishingHoleId,
                Zone = h.Zone,
                RecommendedBait = h.RecommendedBait,
                IsBigFish = h.IsBigFish,
                WeatherRequirement = h.WeatherRequirement,
                TimeRequirement = h.TimeRequirement
            }).ToList();
        }

        /// <summary>
        /// Maps Core.Models.CraftingClass to Data.Models.CraftingClass.
        /// Core uses 0-7, Data uses 8-15 (FFXIV class IDs).
        /// </summary>
        private AkadaemiaAnyder.Data.Models.CraftingClass MapCraftingClass(AkadaemiaAnyder.Core.Models.CraftingClass coreClass)
        {
            return coreClass switch
            {
                AkadaemiaAnyder.Core.Models.CraftingClass.CRP => AkadaemiaAnyder.Data.Models.CraftingClass.Carpenter,
                AkadaemiaAnyder.Core.Models.CraftingClass.BSM => AkadaemiaAnyder.Data.Models.CraftingClass.Blacksmith,
                AkadaemiaAnyder.Core.Models.CraftingClass.ARM => AkadaemiaAnyder.Data.Models.CraftingClass.Armorer,
                AkadaemiaAnyder.Core.Models.CraftingClass.GSM => AkadaemiaAnyder.Data.Models.CraftingClass.Goldsmith,
                AkadaemiaAnyder.Core.Models.CraftingClass.LTW => AkadaemiaAnyder.Data.Models.CraftingClass.Leatherworker,
                AkadaemiaAnyder.Core.Models.CraftingClass.WVR => AkadaemiaAnyder.Data.Models.CraftingClass.Weaver,
                AkadaemiaAnyder.Core.Models.CraftingClass.ALC => AkadaemiaAnyder.Data.Models.CraftingClass.Alchemist,
                AkadaemiaAnyder.Core.Models.CraftingClass.CUL => AkadaemiaAnyder.Data.Models.CraftingClass.Culinarian,
                _ => AkadaemiaAnyder.Data.Models.CraftingClass.Carpenter
            };
        }

        /// <summary>
        /// Maps Core.Models.GatheringClass to Data.Models.GatheringClass.
        /// Core uses 0-1, Data uses 16-18 (FFXIV class IDs).
        /// </summary>
        private AkadaemiaAnyder.Data.Models.GatheringClass MapGatheringClass(AkadaemiaAnyder.Core.Models.GatheringClass coreClass)
        {
            return coreClass switch
            {
                AkadaemiaAnyder.Core.Models.GatheringClass.Miner => AkadaemiaAnyder.Data.Models.GatheringClass.Miner,
                AkadaemiaAnyder.Core.Models.GatheringClass.Botanist => AkadaemiaAnyder.Data.Models.GatheringClass.Botanist,
                _ => AkadaemiaAnyder.Data.Models.GatheringClass.Miner
            };
        }
    }
}
