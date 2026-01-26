using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Integration tests for repository layer.
    /// Tests CRUD operations, transactions, retry logic, and concurrency handling.
    /// </summary>
    public class RepositoryIntegrationTest
    {
        private readonly IPluginLog log;

        public RepositoryIntegrationTest(IPluginLog pluginLog)
        {
            log = pluginLog;
        }

        public async Task<bool> RunAllTests()
        {
            log.Information("=== Repository Integration Tests ===");

            var results = new List<(string TestName, bool Passed)>();

            results.Add(("CRUD Operations - Recipe", await TestCrudOperations()));
            results.Add(("CRUD Operations - Gathering", await TestGatheringCrud()));
            results.Add(("CRUD Operations - Fishing", await TestFishingCrud()));
            results.Add(("Bulk Upsert (1000 records)", await TestBulkUpsert()));
            results.Add(("Transaction Rollback", await TestTransactionRollback()));
            results.Add(("Specialized Repository Filters", await TestSpecializedFilters()));
            results.Add(("Concurrency Test", await TestConcurrency()));

            // Print results
            log.Information("--- Test Results ---");
            foreach (var (testName, passed) in results)
            {
                var status = passed ? "PASS" : "FAIL";
                log.Information($"{status}: {testName}");
            }

            var allPassed = results.All(r => r.Passed);
            log.Information($"Overall: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")}");

            return allPassed;
        }

        private async Task<bool> TestCrudOperations()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                // Create test recipe
                var recipe = new RecipeEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test Character",
                    WorldName = "Tonberry",
                    Type = CollectionType.Recipe,
                    ItemId = 1000,
                    ItemName = "Iron Ingot",
                    IsUnlocked = false,
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    RecipeId = 5000,
                    RecipeLevel = 15,
                    CraftingClass = CraftingClass.Blacksmith,
                    IsMasterRecipe = false,
                    ItemLevel = 20
                };

                // Test INSERT
                var insertedId = await repository.InsertAsync(recipe);
                if (insertedId <= 0)
                {
                    log.Error("Insert failed: returned ID <= 0");
                    return false;
                }

                log.Debug($"Inserted recipe with ID: {insertedId}");

                // Test GET BY ID
                var retrieved = await repository.GetByIdAsync<RecipeEntry>(insertedId);
                if (retrieved == null)
                {
                    log.Error("Get by ID failed: returned null");
                    return false;
                }

                if (retrieved.ItemName != "Iron Ingot" || retrieved.RecipeLevel != 15)
                {
                    log.Error("Get by ID failed: data mismatch");
                    return false;
                }

                log.Debug("Retrieved recipe matches inserted data");

                // Test UPDATE
                retrieved.IsUnlocked = true;
                retrieved.UnlockedAt = DateTime.UtcNow;
                retrieved.LastUpdatedAt = DateTime.UtcNow;

                var updateResult = await repository.UpdateAsync(retrieved);
                if (updateResult != 1)
                {
                    log.Error($"Update failed: expected 1 row, got {updateResult}");
                    return false;
                }

                var updated = await repository.GetByIdAsync<RecipeEntry>(insertedId);
                if (updated?.IsUnlocked != true)
                {
                    log.Error("Update failed: IsUnlocked not set");
                    return false;
                }

                log.Debug("Updated recipe successfully");

                // Test GET ALL
                var allRecipes = await repository.GetAllAsync<RecipeEntry>();
                if (allRecipes.Count == 0)
                {
                    log.Error("GetAll failed: returned empty list");
                    return false;
                }

                log.Debug($"Retrieved {allRecipes.Count} recipes");

                // Test DELETE
                var deleteResult = await repository.DeleteAsync<RecipeEntry>(insertedId);
                if (deleteResult != 1)
                {
                    log.Error($"Delete failed: expected 1 row, got {deleteResult}");
                    return false;
                }

                var deleted = await repository.GetByIdAsync<RecipeEntry>(insertedId);
                if (deleted != null)
                {
                    log.Error("Delete failed: entry still exists");
                    return false;
                }

                log.Debug("Deleted recipe successfully");

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestCrudOperations failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestGatheringCrud()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                var node = new GatheringNodeEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test Character",
                    WorldName = "Tonberry",
                    Type = CollectionType.GatheringNode,
                    ItemId = 2000,
                    ItemName = "Mythril Ore",
                    IsUnlocked = true,
                    UnlockedAt = DateTime.UtcNow,
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    NodeId = 8000,
                    GatheringClass = GatheringClass.Miner,
                    Zone = "Coerthas Central Highlands",
                    NodeLevel = 50,
                    IsLegendary = true,
                    IsEphemeral = false
                };

                var id = await repository.InsertAsync(node);
                var retrieved = await repository.GetByIdAsync<GatheringNodeEntry>(id);

                if (retrieved?.Zone != "Coerthas Central Highlands" || !retrieved.IsLegendary)
                {
                    log.Error("Gathering node data mismatch");
                    return false;
                }

                log.Debug("Gathering node CRUD successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestGatheringCrud failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestFishingCrud()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                var fish = new FishingHoleEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test Character",
                    WorldName = "Tonberry",
                    Type = CollectionType.FishingHole,
                    ItemId = 3000,
                    ItemName = "Titanic Sawfish",
                    IsUnlocked = false,
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    FishId = 9000,
                    FishingHoleId = 7500,
                    Zone = "The Ruby Sea",
                    RecommendedBait = "Shrimp Cage Feeder",
                    IsBigFish = true,
                    WeatherRequirement = "Clear",
                    TimeRequirement = "10:00-18:00"
                };

                var id = await repository.InsertAsync(fish);
                var retrieved = await repository.GetByIdAsync<FishingHoleEntry>(id);

                if (retrieved?.IsBigFish != true || retrieved.WeatherRequirement != "Clear")
                {
                    log.Error("Fishing hole data mismatch");
                    return false;
                }

                log.Debug("Fishing hole CRUD successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestFishingCrud failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestBulkUpsert()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                // Create 1000 test recipes
                var recipes = new List<RecipeEntry>();
                for (int i = 0; i < 1000; i++)
                {
                    recipes.Add(new RecipeEntry
                    {
                        CharacterId = 12345,
                        CharacterName = "Test Character",
                        WorldName = "Tonberry",
                        Type = CollectionType.Recipe,
                        ItemId = 10000 + i,
                        ItemName = $"Recipe {i}",
                        IsUnlocked = i % 2 == 0,
                        FirstSeenAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow,
                        RecipeId = 50000 + i,
                        RecipeLevel = (i % 90) + 1,
                        CraftingClass = (CraftingClass)((i % 8) + 8), // Rotate through crafting classes
                        IsMasterRecipe = i % 10 == 0,
                        ItemLevel = (i % 100) + 1
                    });
                }

                log.Information("Starting bulk upsert of 1000 recipes...");
                var stopwatch = Stopwatch.StartNew();
                var result = await repository.BulkUpsertAsync(recipes);
                stopwatch.Stop();

                if (result != 1000)
                {
                    log.Error($"Bulk upsert failed: expected 1000, got {result}");
                    return false;
                }

                log.Information($"Bulk upsert completed in {stopwatch.ElapsedMilliseconds}ms");

                // Verify data
                var allRecipes = await repository.GetAllAsync<RecipeEntry>();
                if (allRecipes.Count < 1000)
                {
                    log.Error($"Bulk upsert verification failed: expected at least 1000, got {allRecipes.Count}");
                    return false;
                }

                log.Debug("Bulk upsert successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestBulkUpsert failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestTransactionRollback()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                // Get initial count
                var initialRecipes = await repository.GetAllAsync<RecipeEntry>();
                var initialCount = initialRecipes.Count;

                try
                {
                    // Attempt to insert invalid data (this should fail)
                    var invalidRecipe = new RecipeEntry
                    {
                        CharacterId = 0, // Invalid
                        CharacterName = "", // Invalid
                        WorldName = "",
                        Type = CollectionType.Recipe,
                        ItemId = -1, // Invalid
                        ItemName = "",
                        FirstSeenAt = DateTime.MinValue,
                        LastUpdatedAt = DateTime.MinValue,
                        RecipeId = 0,
                        RecipeLevel = 0,
                        CraftingClass = CraftingClass.Blacksmith,
                        ItemLevel = 0
                    };

                    await repository.InsertAsync(invalidRecipe);
                }
                catch
                {
                    // Expected to fail
                }

                // Verify count hasn't changed
                var finalRecipes = await repository.GetAllAsync<RecipeEntry>();
                if (finalRecipes.Count != initialCount)
                {
                    log.Error($"Transaction rollback failed: count changed from {initialCount} to {finalRecipes.Count}");
                    return false;
                }

                log.Debug("Transaction rollback successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestTransactionRollback failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestSpecializedFilters()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var recipeRepo = new RecipeRepository(context, log);
                var gatheringRepo = new GatheringRepository(context, log);
                var fishingRepo = new FishingRepository(context, log);

                // Insert test data
                var recipe1 = new RecipeEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test",
                    WorldName = "Tonberry",
                    Type = CollectionType.Recipe,
                    ItemId = 1001,
                    ItemName = "BSM Recipe",
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    RecipeId = 5001,
                    RecipeLevel = 50,
                    CraftingClass = CraftingClass.Blacksmith,
                    IsMasterRecipe = true,
                    ItemLevel = 90
                };

                var recipe2 = new RecipeEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test",
                    WorldName = "Tonberry",
                    Type = CollectionType.Recipe,
                    ItemId = 1002,
                    ItemName = "CRP Recipe",
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    RecipeId = 5002,
                    RecipeLevel = 30,
                    CraftingClass = CraftingClass.Carpenter,
                    IsMasterRecipe = false,
                    ItemLevel = 50
                };

                await recipeRepo.InsertAsync(recipe1);
                await recipeRepo.InsertAsync(recipe2);

                // Test RecipeRepository filters
                var bsmRecipes = await recipeRepo.GetByCraftingClassAsync(CraftingClass.Blacksmith);
                if (bsmRecipes.Count != 1 || bsmRecipes[0].ItemName != "BSM Recipe")
                {
                    log.Error("RecipeRepository.GetByCraftingClassAsync failed");
                    return false;
                }

                var masterRecipes = await recipeRepo.GetMasterRecipesAsync();
                if (masterRecipes.Count != 1 || masterRecipes[0].ItemName != "BSM Recipe")
                {
                    log.Error("RecipeRepository.GetMasterRecipesAsync failed");
                    return false;
                }

                // Insert gathering test data
                var node = new GatheringNodeEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test",
                    WorldName = "Tonberry",
                    Type = CollectionType.GatheringNode,
                    ItemId = 2001,
                    ItemName = "Legendary Node",
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    NodeId = 8001,
                    GatheringClass = GatheringClass.Miner,
                    Zone = "Test Zone",
                    NodeLevel = 80,
                    IsLegendary = true,
                    IsEphemeral = false
                };

                await gatheringRepo.InsertAsync(node);

                var legendaryNodes = await gatheringRepo.GetLegendaryNodesAsync();
                if (legendaryNodes.Count != 1 || !legendaryNodes[0].IsLegendary)
                {
                    log.Error("GatheringRepository.GetLegendaryNodesAsync failed");
                    return false;
                }

                // Insert fishing test data
                var bigFish = new FishingHoleEntry
                {
                    CharacterId = 12345,
                    CharacterName = "Test",
                    WorldName = "Tonberry",
                    Type = CollectionType.FishingHole,
                    ItemId = 3001,
                    ItemName = "Big Fish",
                    FirstSeenAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    FishId = 9001,
                    FishingHoleId = 7501,
                    Zone = "Test Sea",
                    RecommendedBait = "Test Bait",
                    IsBigFish = true,
                    WeatherRequirement = "Rain"
                };

                await fishingRepo.InsertAsync(bigFish);

                var bigFishList = await fishingRepo.GetBigFishAsync();
                if (bigFishList.Count != 1 || !bigFishList[0].IsBigFish)
                {
                    log.Error("FishingRepository.GetBigFishAsync failed");
                    return false;
                }

                var weatherRestricted = await fishingRepo.GetWeatherRestrictedAsync();
                if (weatherRestricted.Count != 1 || weatherRestricted[0].WeatherRequirement != "Rain")
                {
                    log.Error("FishingRepository.GetWeatherRestrictedAsync failed");
                    return false;
                }

                log.Debug("Specialized repository filters successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestSpecializedFilters failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestConcurrency()
        {
            try
            {
                using var context = DatabaseTestUtility.CreateTestContext(log);
                var repository = new CollectionRepository(context, log);

                log.Information("Starting concurrency test with 10 parallel writes...");

                // Launch 10 concurrent insert operations
                var tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    var taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var recipe = new RecipeEntry
                        {
                            CharacterId = 12345,
                            CharacterName = $"Concurrent {taskId}",
                            WorldName = "Tonberry",
                            Type = CollectionType.Recipe,
                            ItemId = 20000 + taskId,
                            ItemName = $"Concurrent Recipe {taskId}",
                            FirstSeenAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow,
                            RecipeId = 60000 + taskId,
                            RecipeLevel = 50,
                            CraftingClass = CraftingClass.Blacksmith,
                            ItemLevel = 90
                        };

                        await repository.InsertAsync(recipe);
                        log.Debug($"Concurrent task {taskId} completed");
                    }));
                }

                await Task.WhenAll(tasks);

                // Verify all entries were inserted
                var allRecipes = await repository.GetAllAsync<RecipeEntry>();
                var concurrentRecipes = allRecipes.Where(r => r.CharacterName.StartsWith("Concurrent")).ToList();

                if (concurrentRecipes.Count != 10)
                {
                    log.Error($"Concurrency test failed: expected 10 entries, got {concurrentRecipes.Count}");
                    return false;
                }

                log.Debug("Concurrency test successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"TestConcurrency failed: {ex.Message}");
                return false;
            }
        }
    }
}
