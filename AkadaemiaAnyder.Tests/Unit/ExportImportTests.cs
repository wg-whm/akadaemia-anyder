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

namespace AkadaemiaAnyder.Tests.Unit;

/// <summary>
/// Unit tests for JSON export and import functionality.
/// </summary>
public class ExportImportTests : IDisposable
{
    private readonly Mock<IPluginLog> _mockLog;
    private DatabaseContext? _testContext;
    private readonly string _testDirectory;

    public ExportImportTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"akadaemia_export_test_{Guid.NewGuid()}");
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

    #region JsonExporter Tests

    [Fact]
    public async Task JsonExporter_ExportAllAsync_WithEmptyCollections_Succeeds()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);

        var exportPath = Path.Combine(_testDirectory, "empty_export.json");

        // Act
        var result = await exporter.ExportAllAsync(exportPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(exportPath));

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Metadata", json);
        Assert.Contains("Recipes", json);
        Assert.Contains("GatheringNodes", json);
        Assert.Contains("FishingHoles", json);
    }

    [Fact]
    public async Task JsonExporter_ExportAllAsync_WithLargeDataset_Succeeds()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Seed 1000+ recipes
        var recipes = new List<RecipeEntry>();
        for (int i = 0; i < 1000; i++)
        {
            recipes.Add(new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 1000 + i,
                ItemName = $"Recipe {i}",
                IsUnlocked = i % 2 == 0,
                UnlockedAt = i % 2 == 0 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 2000 + i,
                RecipeLevel = 50,
                CraftingClass = CraftingClass.Carpenter,
                IsMasterRecipe = false,
                ItemLevel = 100
            });
        }
        await recipeRepo.BulkUpsertAsync(recipes);

        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var exportPath = Path.Combine(_testDirectory, "large_export.json");

        // Act
        var result = await exporter.ExportAllAsync(exportPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(exportPath));

        var fileInfo = new FileInfo(exportPath);
        Assert.True(fileInfo.Length > 10000); // Should be substantial
    }

    [Fact]
    public async Task JsonExporter_ExportByTypeAsync_ExportsOnlySpecifiedType()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Seed recipes and gathering nodes
        var recipes = new List<RecipeEntry>
        {
            new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100,
                ItemName = "Recipe 1",
                IsUnlocked = true,
                UnlockedAt = DateTime.UtcNow,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200,
                RecipeLevel = 50,
                CraftingClass = CraftingClass.Carpenter,
                IsMasterRecipe = false,
                ItemLevel = 100
            }
        };
        await recipeRepo.BulkUpsertAsync(recipes);

        var nodes = new List<GatheringNodeEntry>
        {
            new GatheringNodeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.GatheringNode,
                ItemId = 300,
                ItemName = "Node 1",
                IsUnlocked = true,
                UnlockedAt = DateTime.UtcNow,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                NodeId = 400,
                GatheringClass = GatheringClass.Miner,
                Zone = "Test Zone",
                NodeLevel = 50,
                IsLegendary = false,
                IsEphemeral = false
            }
        };
        await gatheringRepo.BulkUpsertAsync(nodes);

        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var exportPath = Path.Combine(_testDirectory, "recipes_only.json");

        // Act
        var result = await exporter.ExportByTypeAsync(CollectionType.Recipe, exportPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(exportPath));

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Recipe", json);
        Assert.DoesNotContain("GatheringNode", json);
    }

    #endregion

    #region JsonImporter Tests

    [Fact]
    public void JsonImporter_ValidateFile_AcceptsValidSchema()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var validJson = @"{
            ""Metadata"": {
                ""SchemaVersion"": 1,
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z"",
                ""CharacterName"": ""Test"",
                ""WorldName"": ""World"",
                ""TotalEntries"": 0,
                ""DatabaseTier"": ""Tier1""
            },
            ""Recipes"": [],
            ""GatheringNodes"": [],
            ""FishingHoles"": []
        }";

        var filePath = Path.Combine(_testDirectory, "valid.json");
        File.WriteAllText(filePath, validJson);

        // Act
        var (valid, error) = importer.ValidateFile(filePath);

        // Assert
        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void JsonImporter_ValidateFile_RejectsMissingSchemaVersion()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var invalidJson = @"{
            ""Metadata"": {
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z""
            },
            ""Recipes"": []
        }";

        var filePath = Path.Combine(_testDirectory, "invalid_schema.json");
        File.WriteAllText(filePath, invalidJson);

        // Act
        var (valid, error) = importer.ValidateFile(filePath);

        // Assert
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("SchemaVersion", error);
    }

    [Fact]
    public void JsonImporter_ValidateFile_RejectsMalformedJson()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var malformedJson = @"{
            ""Metadata"": {
                ""SchemaVersion"": 1,
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z""
            },
            ""Recipes"": [
                { missing closing brace
            ]
        }";

        var filePath = Path.Combine(_testDirectory, "malformed.json");
        File.WriteAllText(filePath, malformedJson);

        // Act
        var (valid, error) = importer.ValidateFile(filePath);

        // Assert
        Assert.False(valid);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task JsonImporter_ImportAsync_HandlesEmptyCollections()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var emptyJson = @"{
            ""Metadata"": {
                ""SchemaVersion"": 1,
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z"",
                ""CharacterName"": ""Test"",
                ""WorldName"": ""World"",
                ""TotalEntries"": 0,
                ""DatabaseTier"": ""Tier1""
            },
            ""Recipes"": [],
            ""GatheringNodes"": [],
            ""FishingHoles"": []
        }";

        var filePath = Path.Combine(_testDirectory, "empty_import.json");
        File.WriteAllText(filePath, emptyJson);

        // Act
        var (success, imported, error) = await importer.ImportAsync(filePath);

        // Assert
        Assert.True(success);
        Assert.Equal(0, imported);
        Assert.Null(error);
    }

    [Fact]
    public async Task JsonImporter_ImportAsync_HandlesDuplicateItemIds()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Pre-populate with existing entry
        var existingRecipe = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Original Recipe",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow.AddDays(-1),
            LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = CraftingClass.Carpenter,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        await recipeRepo.BulkUpsertAsync(new List<RecipeEntry> { existingRecipe });

        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var importJson = @"{
            ""Metadata"": {
                ""SchemaVersion"": 1,
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z"",
                ""CharacterName"": ""Test"",
                ""WorldName"": ""World"",
                ""TotalEntries"": 1,
                ""DatabaseTier"": ""Tier1""
            },
            ""Recipes"": [
                {
                    ""CharacterId"": 1,
                    ""CharacterName"": ""Test"",
                    ""WorldName"": ""World"",
                    ""Type"": 1,
                    ""ItemId"": 100,
                    ""ItemName"": ""Updated Recipe"",
                    ""IsUnlocked"": true,
                    ""UnlockedAt"": ""2026-01-25T00:00:00Z"",
                    ""FirstSeenAt"": ""2026-01-24T00:00:00Z"",
                    ""LastUpdatedAt"": ""2026-01-25T00:00:00Z"",
                    ""RecipeId"": 200,
                    ""RecipeLevel"": 50,
                    ""CraftingClass"": 8,
                    ""IsMasterRecipe"": false,
                    ""ItemLevel"": 100
                }
            ]
        }";

        var filePath = Path.Combine(_testDirectory, "duplicate_import.json");
        File.WriteAllText(filePath, importJson);

        // Act
        var (success, imported, error) = await importer.ImportAsync(filePath);

        // Assert
        Assert.True(success);
        Assert.Equal(1, imported);

        // Verify the entry was updated (upsert behavior)
        var allRecipes = await recipeRepo.GetAllAsync<RecipeEntry>();
        Assert.Single(allRecipes);
        Assert.True(allRecipes[0].IsUnlocked);
        Assert.Equal("Updated Recipe", allRecipes[0].ItemName);
    }

    [Fact]
    public async Task JsonImporter_ImportAsync_PreservesUnlockDates()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);
        var importer = new JsonImporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, _mockLog.Object);

        var unlockDate = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var importJson = $@"{{
            ""Metadata"": {{
                ""SchemaVersion"": 1,
                ""ExportTimestamp"": ""2026-01-25T00:00:00Z"",
                ""CharacterName"": ""Test"",
                ""WorldName"": ""World"",
                ""TotalEntries"": 1,
                ""DatabaseTier"": ""Tier1""
            }},
            ""Recipes"": [
                {{
                    ""CharacterId"": 1,
                    ""CharacterName"": ""Test"",
                    ""WorldName"": ""World"",
                    ""Type"": 1,
                    ""ItemId"": 100,
                    ""ItemName"": ""Recipe"",
                    ""IsUnlocked"": true,
                    ""UnlockedAt"": ""{unlockDate:yyyy-MM-ddTHH:mm:ssZ}"",
                    ""FirstSeenAt"": ""2026-01-24T00:00:00Z"",
                    ""LastUpdatedAt"": ""2026-01-25T00:00:00Z"",
                    ""RecipeId"": 200,
                    ""RecipeLevel"": 50,
                    ""CraftingClass"": 8,
                    ""IsMasterRecipe"": false,
                    ""ItemLevel"": 100
                }}
            ]
        }}";

        var filePath = Path.Combine(_testDirectory, "unlock_date_import.json");
        File.WriteAllText(filePath, importJson);

        // Act
        var (success, imported, error) = await importer.ImportAsync(filePath);

        // Assert
        Assert.True(success);
        Assert.Equal(1, imported);

        var allRecipes = await recipeRepo.GetAllAsync<RecipeEntry>();
        Assert.Single(allRecipes);
        Assert.NotNull(allRecipes[0].UnlockedAt);
        Assert.Equal(unlockDate, allRecipes[0].UnlockedAt!.Value.ToUniversalTime());
    }

    #endregion
}
