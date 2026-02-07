using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkadaemiaAnyder.Modules.Core.Interfaces;

namespace AkadaemiaAnyder.Modules.Core.Implementations
{
    /// <summary>
    /// Mock implementation of IRepositoryIntegration for testing without database.
    /// Returns realistic test data for unit testing and offline development.
    /// All async methods return completed tasks for synchronous testing.
    /// </summary>
    public class MockRepositoryIntegration : IRepositoryIntegration
    {
        private readonly Dictionary<string, CraftingListData> _craftingLists = new();
        private readonly Dictionary<uint, CraftingHistory> _craftingHistory = new();
        private readonly List<CraftingSessionData> _sessions = new();

        #region Material Availability

        public Task<MaterialAvailability> GetMaterialAvailabilityAsync(uint itemId)
        {
            // Return mock availability data
            var availability = new MaterialAvailability
            {
                ItemId = itemId,
                ItemName = $"Item {itemId}",
                InInventory = 25,
                InSaddlebag = 10,
                InRetainers = 50,
                InGlamourDresser = 0,
                InArmoryChest = 0,
                ByLocation = new Dictionary<string, int>
                {
                    { "inventory", 25 },
                    { "saddlebag", 10 },
                    { "retainer_1", 30 },
                    { "retainer_2", 20 },
                }
            };

            return Task.FromResult(availability);
        }

        public Task<List<MaterialLocation>> FindMaterialLocationsAsync(uint itemId)
        {
            var locations = new List<MaterialLocation>
            {
                new MaterialLocation { Location = "inventory", SlotId = 5, Quantity = 25, IsHQ = false },
                new MaterialLocation { Location = "saddlebag", SlotId = 0, Quantity = 10, IsHQ = false },
                new MaterialLocation { Location = "retainer_1", SlotId = 12, Quantity = 30, IsHQ = false },
                new MaterialLocation { Location = "retainer_2", SlotId = 8, Quantity = 20, IsHQ = true },
            };

            return Task.FromResult(locations);
        }

        public Task<Dictionary<uint, MaterialAvailability>> GetBulkMaterialAvailabilityAsync(IEnumerable<uint> itemIds)
        {
            var result = new Dictionary<uint, MaterialAvailability>();
            foreach (var itemId in itemIds)
            {
                result[itemId] = GetMaterialAvailabilityAsync(itemId).Result;
            }
            return Task.FromResult(result);
        }

        #endregion

        #region Collection Bindings

        public Task<bool> IsItemCollectedAsync(uint itemId, CollectionCategory category)
        {
            // Mock: items with even IDs are collected
            return Task.FromResult(itemId % 2 == 0);
        }

        public Task<CollectionProgress> GetCollectionProgressAsync(CollectionCategory category)
        {
            // Return mock progress based on category
            var progress = category switch
            {
                CollectionCategory.Recipe => new CollectionProgress { Category = category, Unlocked = 450, Total = 512 },
                CollectionCategory.Gathering => new CollectionProgress { Category = category, Unlocked = 100, Total = 256 },
                CollectionCategory.Fishing => new CollectionProgress { Category = category, Unlocked = 75, Total = 300 },
                CollectionCategory.Mount => new CollectionProgress { Category = category, Unlocked = 120, Total = 280 },
                CollectionCategory.Minion => new CollectionProgress { Category = category, Unlocked = 200, Total = 500 },
                _ => new CollectionProgress { Category = category, Unlocked = 50, Total = 100 },
            };

            return Task.FromResult(progress);
        }

        public Task<Dictionary<CollectionCategory, CollectionProgress>> GetAllCollectionProgressAsync()
        {
            var result = new Dictionary<CollectionCategory, CollectionProgress>();
            foreach (CollectionCategory category in Enum.GetValues(typeof(CollectionCategory)))
            {
                result[category] = GetCollectionProgressAsync(category).Result;
            }
            return Task.FromResult(result);
        }

        #endregion

        #region Crafting List Persistence

        public Task SaveCraftingListAsync(CraftingListData list)
        {
            list.LastModified = DateTime.UtcNow;
            _craftingLists[list.Id] = list;
            return Task.CompletedTask;
        }

        public Task<CraftingListData?> LoadCraftingListAsync(string listId)
        {
            _craftingLists.TryGetValue(listId, out var list);
            return Task.FromResult(list);
        }

        public Task<List<CraftingListData>> LoadAllCraftingListsAsync()
        {
            return Task.FromResult(_craftingLists.Values.ToList());
        }

        public Task DeleteCraftingListAsync(string listId)
        {
            _craftingLists.Remove(listId);
            return Task.CompletedTask;
        }

        #endregion

        #region Recipe Tracking

        public Task<List<uint>> GetCraftedRecipesAsync()
        {
            return Task.FromResult(_craftingHistory.Keys.ToList());
        }

        public Task RecordCraftedRecipeAsync(uint recipeId, bool wasHQ)
        {
            if (!_craftingHistory.TryGetValue(recipeId, out var history))
            {
                history = new CraftingHistory
                {
                    RecipeId = recipeId,
                    RecipeName = $"Recipe {recipeId}",
                    FirstCrafted = DateTime.UtcNow,
                };
                _craftingHistory[recipeId] = history;
            }

            history.TotalCrafted++;
            if (wasHQ) history.HQCount++;
            history.LastCrafted = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        public Task<CraftingHistory> GetCraftingHistoryAsync(uint recipeId)
        {
            if (_craftingHistory.TryGetValue(recipeId, out var history))
            {
                return Task.FromResult(history);
            }

            // Return empty history for recipes not yet crafted
            return Task.FromResult(new CraftingHistory
            {
                RecipeId = recipeId,
                RecipeName = $"Recipe {recipeId}",
                TotalCrafted = 0,
                HQCount = 0,
            });
        }

        #endregion

        #region Session Tracking

        public Task RecordCraftingSessionAsync(CraftingSessionData session)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<List<CraftingSessionData>> GetRecentSessionsAsync(int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var recent = _sessions
                .Where(s => s.Start >= cutoff)
                .OrderByDescending(s => s.Start)
                .ToList();

            return Task.FromResult(recent);
        }

        #endregion
    }
}
