using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using Dalamud.Plugin.Services;
using SamplePlugin.Services;

namespace SamplePlugin.Testing
{
    /// <summary>
    /// Test suite for T8 Supporting Services.
    /// Tests ProgressCalculator, ChangeDetector, JsonExporter, JsonImporter, LoggingService, and TelemetryService.
    /// </summary>
    public static class T8ServiceTests
    {
        public static async Task RunAllTests(IPluginLog log, DatabaseContext dbContext)
        {
            Console.WriteLine("=== T8 Supporting Services Test Suite ===\n");

            var results = new List<(string testName, bool passed)>();

            // Setup test data
            var (collectionRepo, recipeRepo, gatheringRepo, fishingRepo) = SetupTestRepositories(log, dbContext);
            await SeedTestData(recipeRepo, gatheringRepo, fishingRepo);

            // Test 1: ProgressCalculator - GetCollectionProgress
            Console.WriteLine("Test 1: ProgressCalculator.GetCollectionProgress");
            results.Add(("ProgressCalculator.GetCollectionProgress", await TestProgressCalculatorByType(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 2: ProgressCalculator - GetOverallProgress
            Console.WriteLine("\nTest 2: ProgressCalculator.GetOverallProgress");
            results.Add(("ProgressCalculator.GetOverallProgress", await TestProgressCalculatorOverall(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 3: ChangeDetector - DetectChanges
            Console.WriteLine("\nTest 3: ChangeDetector.DetectChanges");
            results.Add(("ChangeDetector.DetectChanges", TestChangeDetector(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 4: ChangeDetector - GetRecentUnlocks
            Console.WriteLine("\nTest 4: ChangeDetector.GetRecentUnlocks");
            results.Add(("ChangeDetector.GetRecentUnlocks", await TestRecentUnlocks(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 5: JsonExporter - ExportAllAsync
            Console.WriteLine("\nTest 5: JsonExporter.ExportAllAsync");
            results.Add(("JsonExporter.ExportAllAsync", await TestJsonExportAll(log, dbContext, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 6: JsonExporter - ExportByTypeAsync
            Console.WriteLine("\nTest 6: JsonExporter.ExportByTypeAsync");
            results.Add(("JsonExporter.ExportByTypeAsync", await TestJsonExportByType(log, dbContext, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 7: JsonImporter - ValidateFile
            Console.WriteLine("\nTest 7: JsonImporter.ValidateFile");
            results.Add(("JsonImporter.ValidateFile", TestJsonImporterValidation(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 8: JsonImporter - ImportAsync
            Console.WriteLine("\nTest 8: JsonImporter.ImportAsync");
            results.Add(("JsonImporter.ImportAsync", await TestJsonImport(log, collectionRepo, recipeRepo, gatheringRepo, fishingRepo)));

            // Test 9: LoggingService - All log levels
            Console.WriteLine("\nTest 9: LoggingService - All log levels");
            results.Add(("LoggingService", TestLoggingService(log)));

            // Test 10: TelemetryService - Record and retrieve metrics
            Console.WriteLine("\nTest 10: TelemetryService - Metrics tracking");
            results.Add(("TelemetryService", TestTelemetryService(log)));

            // Summary
            Console.WriteLine("\n=== Test Summary ===");
            int passed = 0;
            int failed = 0;

            foreach (var (testName, result) in results)
            {
                var status = result ? "PASS" : "FAIL";
                Console.WriteLine($"{status}: {testName}");
                if (result) passed++; else failed++;
            }

            Console.WriteLine($"\nTotal: {results.Count} tests, {passed} passed, {failed} failed");
        }

        private static (CollectionRepository, RecipeRepository, GatheringRepository, FishingRepository) SetupTestRepositories(IPluginLog log, DatabaseContext dbContext)
        {
            var collectionRepo = new CollectionRepository(dbContext, log);
            var recipeRepo = new RecipeRepository(dbContext, log);
            var gatheringRepo = new GatheringRepository(dbContext, log);
            var fishingRepo = new FishingRepository(dbContext, log);

            return (collectionRepo, recipeRepo, gatheringRepo, fishingRepo);
        }

        private static async Task SeedTestData(RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            // Seed 10 recipes (5 unlocked, 5 locked)
            var recipes = new List<RecipeEntry>();
            for (int i = 1; i <= 10; i++)
            {
                recipes.Add(new RecipeEntry
                {
                    CharacterId = 1,
                    CharacterName = "Test Character",
                    WorldName = "Test World",
                    Type = CollectionType.Recipe,
                    ItemId = 1000 + i,
                    ItemName = $"Recipe {i}",
                    IsUnlocked = i <= 5,
                    UnlockedAt = i <= 5 ? DateTime.UtcNow.AddMinutes(-i * 10) : null,
                    FirstSeenAt = DateTime.UtcNow.AddHours(-1),
                    LastUpdatedAt = DateTime.UtcNow,
                    RecipeId = 2000 + i,
                    RecipeLevel = 50,
                    CraftingClass = CraftingClass.Carpenter,
                    IsMasterRecipe = false,
                    ItemLevel = 100
                });
            }
            await recipeRepo.BulkUpsertAsync(recipes);

            // Seed 5 gathering nodes (3 unlocked, 2 locked)
            var nodes = new List<GatheringNodeEntry>();
            for (int i = 1; i <= 5; i++)
            {
                nodes.Add(new GatheringNodeEntry
                {
                    CharacterId = 1,
                    CharacterName = "Test Character",
                    WorldName = "Test World",
                    Type = CollectionType.GatheringNode,
                    ItemId = 3000 + i,
                    ItemName = $"Node {i}",
                    IsUnlocked = i <= 3,
                    UnlockedAt = i <= 3 ? DateTime.UtcNow.AddMinutes(-i * 15) : null,
                    FirstSeenAt = DateTime.UtcNow.AddHours(-1),
                    LastUpdatedAt = DateTime.UtcNow,
                    NodeId = 4000 + i,
                    GatheringClass = GatheringClass.Miner,
                    Zone = "Test Zone",
                    NodeLevel = 50,
                    IsLegendary = false,
                    IsEphemeral = false
                });
            }
            await gatheringRepo.BulkUpsertAsync(nodes);

            // Seed 5 fishing holes (2 unlocked, 3 locked)
            var holes = new List<FishingHoleEntry>();
            for (int i = 1; i <= 5; i++)
            {
                holes.Add(new FishingHoleEntry
                {
                    CharacterId = 1,
                    CharacterName = "Test Character",
                    WorldName = "Test World",
                    Type = CollectionType.FishingHole,
                    ItemId = 5000 + i,
                    ItemName = $"Fish {i}",
                    IsUnlocked = i <= 2,
                    UnlockedAt = i <= 2 ? DateTime.UtcNow.AddMinutes(-i * 20) : null,
                    FirstSeenAt = DateTime.UtcNow.AddHours(-1),
                    LastUpdatedAt = DateTime.UtcNow,
                    FishId = 6000 + i,
                    FishingHoleId = 7000 + i,
                    Zone = "Test Zone",
                    RecommendedBait = "Test Bait",
                    IsBigFish = false
                });
            }
            await fishingRepo.BulkUpsertAsync(holes);
        }

        private static async Task<bool> TestProgressCalculatorByType(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var calculator = new ProgressCalculator(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Test Recipe progress (5/10 = 50%)
                var recipeProgress = await calculator.GetCollectionProgress(CollectionType.Recipe);
                if (recipeProgress.total != 10 || recipeProgress.unlocked != 5 || Math.Abs(recipeProgress.percentage - 50.0) > 0.1)
                {
                    Console.WriteLine($"  FAIL: Recipe progress incorrect - Expected 5/10 (50%), got {recipeProgress.unlocked}/{recipeProgress.total} ({recipeProgress.percentage:F2}%)");
                    return false;
                }

                // Test Gathering progress (3/5 = 60%)
                var gatheringProgress = await calculator.GetCollectionProgress(CollectionType.GatheringNode);
                if (gatheringProgress.total != 5 || gatheringProgress.unlocked != 3 || Math.Abs(gatheringProgress.percentage - 60.0) > 0.1)
                {
                    Console.WriteLine($"  FAIL: Gathering progress incorrect - Expected 3/5 (60%), got {gatheringProgress.unlocked}/{gatheringProgress.total} ({gatheringProgress.percentage:F2}%)");
                    return false;
                }

                // Test Fishing progress (2/5 = 40%)
                var fishingProgress = await calculator.GetCollectionProgress(CollectionType.FishingHole);
                if (fishingProgress.total != 5 || fishingProgress.unlocked != 2 || Math.Abs(fishingProgress.percentage - 40.0) > 0.1)
                {
                    Console.WriteLine($"  FAIL: Fishing progress incorrect - Expected 2/5 (40%), got {fishingProgress.unlocked}/{fishingProgress.total} ({fishingProgress.percentage:F2}%)");
                    return false;
                }

                Console.WriteLine($"  PASS: All collection type progress calculations correct");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestProgressCalculatorOverall(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var calculator = new ProgressCalculator(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Overall: 10/20 = 50%
                var overallProgress = await calculator.GetOverallProgress();
                if (overallProgress.totalItems != 20 || overallProgress.unlockedItems != 10 || Math.Abs(overallProgress.percentage - 50.0) > 0.1)
                {
                    Console.WriteLine($"  FAIL: Overall progress incorrect - Expected 10/20 (50%), got {overallProgress.unlockedItems}/{overallProgress.totalItems} ({overallProgress.percentage:F2}%)");
                    return false;
                }

                Console.WriteLine($"  PASS: Overall progress calculation correct");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static bool TestChangeDetector(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var detector = new ChangeDetector(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Create "previous" state with 3 unlocked items
                var previous = new List<CollectionEntry>
                {
                    new RecipeEntry { ItemId = 1001, CharacterId = 1, IsUnlocked = true },
                    new RecipeEntry { ItemId = 1002, CharacterId = 1, IsUnlocked = false },
                    new RecipeEntry { ItemId = 1003, CharacterId = 1, IsUnlocked = false }
                };

                // Create "current" state with 1 additional unlock
                var current = new List<CollectionEntry>
                {
                    new RecipeEntry { ItemId = 1001, CharacterId = 1, IsUnlocked = true },
                    new RecipeEntry { ItemId = 1002, CharacterId = 1, IsUnlocked = true }, // Newly unlocked
                    new RecipeEntry { ItemId = 1003, CharacterId = 1, IsUnlocked = false }
                };

                var changes = detector.DetectChanges(current, previous);

                if (changes.Count != 1 || changes[0].ItemId != 1002)
                {
                    Console.WriteLine($"  FAIL: Expected 1 change (ItemId 1002), got {changes.Count} changes");
                    return false;
                }

                Console.WriteLine($"  PASS: Change detection correctly identified new unlock");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestRecentUnlocks(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var detector = new ChangeDetector(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Get unlocks from last hour (should get all 10 unlocked items from seed data)
                var recentUnlocks = await detector.GetRecentUnlocks(TimeSpan.FromHours(1));

                if (recentUnlocks.Count != 10) // 5 recipes + 3 nodes + 2 fish
                {
                    Console.WriteLine($"  FAIL: Expected 10 recent unlocks, got {recentUnlocks.Count}");
                    return false;
                }

                // Verify they're sorted by unlock time (most recent first)
                for (int i = 0; i < recentUnlocks.Count - 1; i++)
                {
                    if (recentUnlocks[i].UnlockedAt < recentUnlocks[i + 1].UnlockedAt)
                    {
                        Console.WriteLine($"  FAIL: Recent unlocks not sorted correctly");
                        return false;
                    }
                }

                Console.WriteLine($"  PASS: Recent unlocks retrieved and sorted correctly");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestJsonExportAll(IPluginLog log, DatabaseContext dbContext, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, dbContext, log);
                var tempFile = Path.Combine(Path.GetTempPath(), $"test_export_all_{Guid.NewGuid()}.json");

                var success = await exporter.ExportAllAsync(tempFile);

                if (!success)
                {
                    Console.WriteLine($"  FAIL: Export failed");
                    return false;
                }

                if (!File.Exists(tempFile))
                {
                    Console.WriteLine($"  FAIL: Export file not created");
                    return false;
                }

                var json = await File.ReadAllTextAsync(tempFile);
                File.Delete(tempFile);

                if (!json.Contains("Metadata") || !json.Contains("Recipes") || !json.Contains("GatheringNodes") || !json.Contains("FishingHoles"))
                {
                    Console.WriteLine($"  FAIL: Export JSON missing required sections");
                    return false;
                }

                Console.WriteLine($"  PASS: Export all succeeded with valid JSON");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestJsonExportByType(IPluginLog log, DatabaseContext dbContext, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, dbContext, log);
                var tempFile = Path.Combine(Path.GetTempPath(), $"test_export_type_{Guid.NewGuid()}.json");

                var success = await exporter.ExportByTypeAsync(CollectionType.Recipe, tempFile);

                if (!success)
                {
                    Console.WriteLine($"  FAIL: Export by type failed");
                    return false;
                }

                if (!File.Exists(tempFile))
                {
                    Console.WriteLine($"  FAIL: Export file not created");
                    return false;
                }

                var json = await File.ReadAllTextAsync(tempFile);
                File.Delete(tempFile);

                if (!json.Contains("Metadata") || !json.Contains("Entries") || !json.Contains("Recipe"))
                {
                    Console.WriteLine($"  FAIL: Export JSON missing required sections");
                    return false;
                }

                Console.WriteLine($"  PASS: Export by type succeeded with valid JSON");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static bool TestJsonImporterValidation(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Test valid JSON
                var validJson = @"{
                    ""Metadata"": {
                        ""SchemaVersion"": 1,
                        ""ExportTimestamp"": ""2026-01-25T00:00:00Z""
                    },
                    ""Recipes"": []
                }";
                var tempFile = Path.Combine(Path.GetTempPath(), $"test_valid_{Guid.NewGuid()}.json");
                File.WriteAllText(tempFile, validJson);

                var (valid, error) = importer.ValidateFile(tempFile);
                File.Delete(tempFile);

                if (!valid)
                {
                    Console.WriteLine($"  FAIL: Valid JSON rejected - {error}");
                    return false;
                }

                // Test invalid JSON (missing schema version)
                var invalidJson = @"{
                    ""Metadata"": {},
                    ""Recipes"": []
                }";
                tempFile = Path.Combine(Path.GetTempPath(), $"test_invalid_{Guid.NewGuid()}.json");
                File.WriteAllText(tempFile, invalidJson);

                (valid, error) = importer.ValidateFile(tempFile);
                File.Delete(tempFile);

                if (valid)
                {
                    Console.WriteLine($"  FAIL: Invalid JSON accepted");
                    return false;
                }

                Console.WriteLine($"  PASS: Validation correctly accepts/rejects JSON");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestJsonImport(IPluginLog log, CollectionRepository collectionRepo, RecipeRepository recipeRepo, GatheringRepository gatheringRepo, FishingRepository fishingRepo)
        {
            try
            {
                var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, log);

                // Create test JSON with 2 new recipes
                var testJson = @"{
                    ""Metadata"": {
                        ""SchemaVersion"": 1,
                        ""ExportTimestamp"": ""2026-01-25T00:00:00Z""
                    },
                    ""Recipes"": [
                        {
                            ""CharacterId"": 2,
                            ""CharacterName"": ""Import Test"",
                            ""WorldName"": ""Import World"",
                            ""Type"": 1,
                            ""ItemId"": 9001,
                            ""ItemName"": ""Imported Recipe 1"",
                            ""IsUnlocked"": true,
                            ""UnlockedAt"": ""2026-01-25T00:00:00Z"",
                            ""FirstSeenAt"": ""2026-01-25T00:00:00Z"",
                            ""LastUpdatedAt"": ""2026-01-25T00:00:00Z"",
                            ""RecipeId"": 9001,
                            ""RecipeLevel"": 50,
                            ""CraftingClass"": 8,
                            ""IsMasterRecipe"": false,
                            ""ItemLevel"": 100
                        }
                    ]
                }";

                var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.json");
                await File.WriteAllTextAsync(tempFile, testJson);

                var (success, imported, error) = await importer.ImportAsync(tempFile);
                File.Delete(tempFile);

                if (!success)
                {
                    Console.WriteLine($"  FAIL: Import failed - {error}");
                    return false;
                }

                if (imported != 1)
                {
                    Console.WriteLine($"  FAIL: Expected 1 import, got {imported}");
                    return false;
                }

                Console.WriteLine($"  PASS: Import succeeded with correct count");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static bool TestLoggingService(IPluginLog log)
        {
            try
            {
                var loggingService = new LoggingService(log);

                // Test all log levels (should not throw)
                loggingService.LogInfo("Test info message");
                loggingService.LogWarning("Test warning message");
                loggingService.LogError("Test error message");
                loggingService.LogError("Test error with exception", new Exception("Test exception"));
                loggingService.LogDebug("Test debug message");

                Console.WriteLine($"  PASS: All log levels executed without exception");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }

        private static bool TestTelemetryService(IPluginLog log)
        {
            try
            {
                var telemetry = new TelemetryService(log);

                // Record some metrics
                telemetry.RecordScan(CollectionType.Recipe, true);
                telemetry.RecordScan(CollectionType.Recipe, true);
                telemetry.RecordScan(CollectionType.Recipe, false);
                telemetry.RecordScan(CollectionType.GatheringNode, true);
                telemetry.RecordDatabaseTierChange(DatabaseTier.Tier1);
                telemetry.RecordMemoryReadFailure("TestReader");
                telemetry.RecordMemoryReadFailure("TestReader");

                // Get metrics
                var snapshot = telemetry.GetMetrics();

                // Verify metrics
                if (snapshot.RecipeScanSuccessCount != 2)
                {
                    Console.WriteLine($"  FAIL: Expected 2 recipe successes, got {snapshot.RecipeScanSuccessCount}");
                    return false;
                }

                if (snapshot.RecipeScanFailureCount != 1)
                {
                    Console.WriteLine($"  FAIL: Expected 1 recipe failure, got {snapshot.RecipeScanFailureCount}");
                    return false;
                }

                if (snapshot.CurrentDatabaseTier != DatabaseTier.Tier1)
                {
                    Console.WriteLine($"  FAIL: Expected Tier1, got {snapshot.CurrentDatabaseTier}");
                    return false;
                }

                if (snapshot.MemoryReadFailures["TestReader"] != 2)
                {
                    Console.WriteLine($"  FAIL: Expected 2 memory failures for TestReader, got {snapshot.MemoryReadFailures["TestReader"]}");
                    return false;
                }

                if (snapshot.TotalScans != 3)
                {
                    Console.WriteLine($"  FAIL: Expected 3 total scans, got {snapshot.TotalScans}");
                    return false;
                }

                Console.WriteLine($"  PASS: Telemetry metrics tracked correctly");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Exception - {ex.Message}");
                return false;
            }
        }
    }
}
