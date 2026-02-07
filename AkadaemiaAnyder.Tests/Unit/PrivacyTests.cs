using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;
using SamplePlugin;
using SamplePlugin.Services;

namespace AkadaemiaAnyder.Tests.Unit;

/// <summary>
/// Unit tests for privacy settings and export anonymization.
/// Verifies privacy-first defaults and anonymization behavior.
/// </summary>
public class PrivacyTests : IDisposable
{
    private readonly Mock<IPluginLog> _mockLog;
    private DatabaseContext? _testContext;
    private readonly string _testDirectory;

    public PrivacyTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"akadaemia_privacy_test_{Guid.NewGuid()}");
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

    #region PrivacySettingsConfig Default Tests

    [Fact]
    public void PrivacySettingsConfig_DefaultConstructor_StoreCharacterNamesIsFalse()
    {
        // Arrange & Act
        var settings = new PrivacySettingsConfig();

        // Assert - Privacy first: character names should NOT be stored by default
        Assert.False(settings.StoreCharacterNames);
    }

    [Fact]
    public void PrivacySettingsConfig_DefaultConstructor_EnableAnonymousExportIsTrue()
    {
        // Arrange & Act
        var settings = new PrivacySettingsConfig();

        // Assert - Privacy first: anonymous exports should be ON by default
        Assert.True(settings.EnableAnonymousExport);
    }

    [Fact]
    public void PrivacySettingsConfig_DefaultConstructor_ExcludeServerFromExportIsTrue()
    {
        // Arrange & Act
        var settings = new PrivacySettingsConfig();

        // Assert - Privacy first: server info should be excluded by default
        Assert.True(settings.ExcludeServerFromExport);
    }

    [Fact]
    public void Configuration_DefaultConstructor_PrivacySettingsIsPrivacyFirst()
    {
        // Arrange & Act
        var config = new Configuration();

        // Assert - All privacy defaults should favor user privacy
        Assert.NotNull(config.PrivacySettings);
        Assert.False(config.PrivacySettings.StoreCharacterNames);
        Assert.True(config.PrivacySettings.EnableAnonymousExport);
        Assert.True(config.PrivacySettings.ExcludeServerFromExport);
    }

    #endregion

    #region UIStateConfig Default Tests

    [Fact]
    public void UIStateConfig_DefaultConstructor_HasExpectedDefaults()
    {
        // Arrange & Act
        var uiState = new UIStateConfig();

        // Assert
        Assert.Equal(0, uiState.LastSelectedTab);
        Assert.True(uiState.InventorySearchExpanded);
        Assert.Equal(string.Empty, uiState.LastInventorySearchQuery);
    }

    [Fact]
    public void Configuration_DefaultConstructor_UIStateIsInitialized()
    {
        // Arrange & Act
        var config = new Configuration();

        // Assert
        Assert.NotNull(config.UIState);
    }

    #endregion

    #region Export Anonymization Tests

    [Fact]
    public async Task JsonExporter_ExportAllAsync_IncludesCharacterNameWhenStorageEnabled()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Seed data with character name
        var recipes = new List<RecipeEntry>
        {
            new RecipeEntry
            {
                CharacterId = 123456,
                CharacterName = "Test Character",
                WorldName = "Gilgamesh",
                Type = CollectionType.Recipe,
                ItemId = 100,
                ItemName = "Test Recipe",
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

        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var exportPath = Path.Combine(_testDirectory, "with_character_name.json");

        // Act
        var result = await exporter.ExportAllAsync(exportPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(exportPath));

        var json = await File.ReadAllTextAsync(exportPath);
        // When storage is enabled, character name should be present
        Assert.Contains("Test Character", json);
        Assert.Contains("Gilgamesh", json);
    }

    [Fact]
    public async Task JsonExporter_ExportAllAsync_MetadataIncludesSchemaVersion()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var exportPath = Path.Combine(_testDirectory, "metadata_test.json");

        // Act
        var result = await exporter.ExportAllAsync(exportPath);

        // Assert
        Assert.True(result);

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("schemaVersion", json);
        Assert.Contains("exportTimestamp", json);
        Assert.Contains("databaseTier", json);
    }

    [Fact]
    public async Task JsonExporter_ExportByTypeAsync_IncludesOnlySpecifiedType()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        // Seed recipes
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

        // Seed gathering nodes
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
        var exportPath = Path.Combine(_testDirectory, "type_filter_test.json");

        // Act - Export only recipes
        var result = await exporter.ExportByTypeAsync(CollectionType.Recipe, exportPath);

        // Assert
        Assert.True(result);

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Recipe 1", json);
        Assert.DoesNotContain("Node 1", json);
        Assert.DoesNotContain("GatheringNode", json);
    }

    #endregion

    #region Privacy Settings Serialization Tests

    [Fact]
    public void PrivacySettingsConfig_CanBeSerializedAndDeserialized()
    {
        // Arrange
        var original = new PrivacySettingsConfig
        {
            StoreCharacterNames = true,
            EnableAnonymousExport = false,
            ExcludeServerFromExport = false
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PrivacySettingsConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.StoreCharacterNames, deserialized.StoreCharacterNames);
        Assert.Equal(original.EnableAnonymousExport, deserialized.EnableAnonymousExport);
        Assert.Equal(original.ExcludeServerFromExport, deserialized.ExcludeServerFromExport);
    }

    [Fact]
    public void UIStateConfig_CanBeSerializedAndDeserialized()
    {
        // Arrange
        var original = new UIStateConfig
        {
            LastSelectedTab = 3,
            InventorySearchExpanded = false,
            LastInventorySearchQuery = "test query"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<UIStateConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.LastSelectedTab, deserialized.LastSelectedTab);
        Assert.Equal(original.InventorySearchExpanded, deserialized.InventorySearchExpanded);
        Assert.Equal(original.LastInventorySearchQuery, deserialized.LastInventorySearchQuery);
    }

    #endregion

    #region Privacy Edge Cases

    [Fact]
    public void PrivacySettingsConfig_ChangingStoreCharacterNames_DoesNotAffectOtherSettings()
    {
        // Arrange
        var settings = new PrivacySettingsConfig();

        // Act
        settings.StoreCharacterNames = true;

        // Assert - Other settings should remain at their defaults
        Assert.True(settings.EnableAnonymousExport);
        Assert.True(settings.ExcludeServerFromExport);
    }

    [Fact]
    public void PrivacySettingsConfig_ChangingEnableAnonymousExport_DoesNotAffectOtherSettings()
    {
        // Arrange
        var settings = new PrivacySettingsConfig();

        // Act
        settings.EnableAnonymousExport = false;

        // Assert - Other settings should remain at their defaults
        Assert.False(settings.StoreCharacterNames);
        Assert.True(settings.ExcludeServerFromExport);
    }

    [Fact]
    public async Task JsonExporter_ExportAllAsync_HandlesEmptyDatabase()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var collectionRepo = new CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new FishingRepository(context, _mockLog.Object);

        var exporter = new JsonExporter(collectionRepo, recipeRepo, gatheringRepo, fishingRepo, context, _mockLog.Object);
        var exportPath = Path.Combine(_testDirectory, "empty_database.json");

        // Act
        var result = await exporter.ExportAllAsync(exportPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(exportPath));

        var json = await File.ReadAllTextAsync(exportPath);
        // Should have metadata even with empty collections
        Assert.Contains("metadata", json);
        Assert.Contains("Unknown", json); // Default character name when no data
    }

    #endregion
}
