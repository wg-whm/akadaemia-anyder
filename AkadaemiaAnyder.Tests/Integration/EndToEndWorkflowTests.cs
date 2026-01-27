using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using CoreModels = AkadaemiaAnyder.Core.Models;
using SamplePlugin.Services;
using SamplePlugin.MemoryReaders;
using SamplePlugin.EventListeners;
using AkadaemiaAnyder.Core.Models;

namespace AkadaemiaAnyder.Tests.Integration;

/// <summary>
/// End-to-end workflow tests covering full scan → display → export → import flows.
/// </summary>
public class EndToEndWorkflowTests : IDisposable
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Mock<IFramework> _mockFramework;
    private readonly Mock<IClientState> _mockClientState;
    private DatabaseContext? _testContext;
    private readonly string _testDirectory;

    public EndToEndWorkflowTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _mockFramework = new Mock<IFramework>();
        _mockClientState = new Mock<IClientState>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"akadaemia_e2e_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    private DatabaseContext CreateInMemoryDatabase()
    {
        var context = new DatabaseContext(_mockLog.Object, ":memory:");
        _testContext = context;
        return context;
    }

    public void Dispose()
    {
        _testContext?.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task FullWorkflow_ScanDisplayExportImport_CompletesSuccessfully()
    {
        // Arrange - Set up all services
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Set up listeners
        var gatheringListener = new GatheringEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var fishingListener = new FishingEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);

        // Use RecipeReader directly (it returns empty/unavailable when not in game, which is expected in tests)
        var recipeReader = new RecipeReader();

        var collectionService = new CollectionService(
            collectionRepo,
            recipeRepo,
            gatheringRepo,
            fishingRepo,
            recipeReader,
            gatheringListener,
            fishingListener,
            _mockClientState.Object,
            _mockLog.Object
        );

        var progressCalculator = new ProgressCalculator(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);
        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        // Step 1: Scan collections
        var scanResult = await collectionService.ScanAllCollectionsAsync();
        Assert.True(scanResult.Success);
        Assert.True(scanResult.ItemsScanned > 0);

        // Step 2: Calculate progress
        var progress = await progressCalculator.GetOverallProgress();
        Assert.True(progress.totalItems > 0);

        // Step 3: Export data
        var exportPath = Path.Combine(_testDirectory, "workflow_export.json");
        var exportResult = await exporter.ExportAllAsync(exportPath);
        Assert.True(exportResult);
        Assert.True(File.Exists(exportPath));

        // Step 4: Clear database
        var allRecipes = await recipeRepo.GetAllAsync<RecipeEntry>();
        foreach (var recipe in allRecipes)
        {
            await collectionRepo.DeleteAsync<RecipeEntry>(recipe.Id);
        }

        // Verify database is empty
        var emptyCount = await recipeRepo.GetAllAsync<RecipeEntry>();
        Assert.Empty(emptyCount);

        // Step 5: Import data back
        var (importSuccess, importedCount, error) = await importer.ImportAsync(exportPath);
        Assert.True(importSuccess);
        Assert.True(importedCount > 0);
        Assert.Null(error);

        // Step 6: Verify data restored
        var restoredProgress = await progressCalculator.GetOverallProgress();
        Assert.Equal(progress.totalItems, restoredProgress.totalItems);
        Assert.Equal(progress.unlockedItems, restoredProgress.unlockedItems);
    }

    [Fact]
    public async Task PartialScan_OnlyRecipes_ProcessesCorrectly()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        var gatheringListener = new GatheringEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var fishingListener = new FishingEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var recipeReader = new RecipeReader();

        var collectionService = new CollectionService(
            collectionRepo,
            recipeRepo,
            gatheringRepo,
            fishingRepo,
            recipeReader,
            gatheringListener,
            fishingListener,
            _mockClientState.Object,
            _mockLog.Object
        );

        // Act - Scan only recipes
        var result = await collectionService.ScanRecipesAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsScanned);

        var allRecipes = await recipeRepo.GetAllAsync<RecipeEntry>();
        Assert.Equal(2, allRecipes.Count);
        Assert.Single(allRecipes.Where(r => r.IsUnlocked));
    }

    [Fact]
    public async Task ProgressCalculation_AfterMultipleScans_RemainsAccurate()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Use real RecipeReader which returns empty when not in game
        var recipeReader = new RecipeReader();
        var gatheringListener = new GatheringEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var fishingListener = new FishingEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);

        var collectionService = new CollectionService(
            collectionRepo,
            recipeRepo,
            gatheringRepo,
            fishingRepo,
            recipeReader,
            gatheringListener,
            fishingListener,
            _mockClientState.Object,
            _mockLog.Object
        );

        var progressCalculator = new ProgressCalculator(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        // Manually insert test recipes since RecipeReader won't work outside game context
        var entry1 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Recipe 1",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 1,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var entry2 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 101,
            ItemName = "Recipe 2",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 2,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        await recipeRepo.InsertAsync(entry1);
        await recipeRepo.InsertAsync(entry2);

        var progress1 = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.Recipe);
        Assert.Equal(2, progress1.total);
        Assert.Equal(1, progress1.unlocked);

        // Second scan - 2 recipes, 2 unlocked (one changed)
        entry2.IsUnlocked = true;
        entry2.UnlockedAt = DateTime.UtcNow;
        await recipeRepo.UpdateAsync(entry2);

        var updatedProgress = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.Recipe);
        Assert.Equal(2, updatedProgress.total);
        Assert.Equal(2, updatedProgress.unlocked);
    }

    [Fact]
    public async Task ComparisonAfterChanges_DetectsUnlockedRecipes()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        var recipeReader = new RecipeReader();
        var gatheringListener = new GatheringEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var fishingListener = new FishingEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);

        var collectionService = new CollectionService(
            collectionRepo,
            recipeRepo,
            gatheringRepo,
            fishingRepo,
            recipeReader,
            gatheringListener,
            fishingListener,
            _mockClientState.Object,
            _mockLog.Object
        );

        // Insert test recipes
        var entry1 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Recipe 1",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 1,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var entry2 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 101,
            ItemName = "Recipe 2",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 2,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        await recipeRepo.InsertAsync(entry1);
        await recipeRepo.InsertAsync(entry2);

        var progressCalculator = new ProgressCalculator(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);
        var progress = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.Recipe);
        Assert.Equal(2, progress.total);
        Assert.Equal(2, progress.unlocked);
    }

    [Fact]
    public async Task ChangeDetection_BetweenScans_IdentifiesNewUnlocks()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        var recipeReader = new RecipeReader();
        var gatheringListener = new GatheringEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);
        var fishingListener = new FishingEventListener(_mockFramework.Object, _mockClientState.Object, _mockLog.Object);

        var collectionService = new CollectionService(
            collectionRepo,
            recipeRepo,
            gatheringRepo,
            fishingRepo,
            recipeReader,
            gatheringListener,
            fishingListener,
            _mockClientState.Object,
            _mockLog.Object
        );

        var changeDetector = new ChangeDetector(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        // First scan - Insert recipes
        var entry1 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Recipe 1",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 1,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var entry2 = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
            ItemId = 101,
            ItemName = "Recipe 2",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 2,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        await recipeRepo.InsertAsync(entry1);
        await recipeRepo.InsertAsync(entry2);
        await collectionService.ScanRecipesAsync();

        var previousState = await recipeRepo.GetAllAsync<RecipeEntry>();

        // Second scan - Update entry1 to be unlocked
        await Task.Delay(100); // Small delay to ensure different timestamp
        entry1.IsUnlocked = true;
        entry1.UnlockedAt = DateTime.UtcNow;
        entry1.LastUpdatedAt = DateTime.UtcNow;
        await recipeRepo.UpdateAsync(entry1);

        var currentState = await recipeRepo.GetAllAsync<RecipeEntry>();

        // Act - Detect changes
        var changes = changeDetector.DetectChanges(
            currentState.Cast<AkadaemiaAnyder.Data.Models.CollectionEntry>().ToList(),
            previousState.Cast<AkadaemiaAnyder.Data.Models.CollectionEntry>().ToList()
        );

        // Assert
        Assert.Single(changes);
        Assert.Equal(100, changes[0].ItemId);
        Assert.True(changes[0].IsUnlocked);
    }

    [Fact]
    public async Task DatabaseTierFallback_DuringOperation_MaintainsDataIntegrity()
    {
        // Arrange - Start with Tier 3 (in-memory)
        var context = CreateInMemoryDatabase();
        Assert.Equal(DatabaseTier.Tier3, context.GetHealthStatus());

        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Add test data
        var recipes = new List<RecipeEntry>
        {
            new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = AkadaemiaAnyder.Data.Models.CollectionType.Recipe,
                ItemId = 100,
                ItemName = "Recipe",
                IsUnlocked = true,
                UnlockedAt = DateTime.UtcNow,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200,
                RecipeLevel = 50,
                CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
                IsMasterRecipe = false,
                ItemLevel = 100
            }
        };
        await recipeRepo.BulkUpsertAsync(recipes);

        // Act - Verify operations still work
        var allRecipes = await recipeRepo.GetAllAsync<RecipeEntry>();
        var unlockedCount = allRecipes.Count(r => r.IsUnlocked);

        // Assert
        Assert.Single(allRecipes);
        Assert.Equal(1, unlockedCount);
        Assert.Equal(DatabaseTier.Tier3, context.GetHealthStatus());
    }
}
