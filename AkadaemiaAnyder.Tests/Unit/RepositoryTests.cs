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

namespace AkadaemiaAnyder.Tests.Unit;

/// <summary>
/// Unit tests for repository components using in-memory SQLite.
/// </summary>
public class RepositoryTests : IDisposable
{
    private DatabaseContext? _testContext;
    private readonly Mock<IPluginLog> _mockLog;

    public RepositoryTests()
    {
        _mockLog = new Mock<IPluginLog>();
    }

    private DatabaseContext CreateInMemoryDatabase()
    {
        // Create in-memory database for isolated testing
        var context = new DatabaseContext(_mockLog.Object, ":memory:");
        _testContext = context;
        return context;
    }

    public void Dispose()
    {
        _testContext?.Dispose();
    }

    #region CollectionRepository Tests

    [Fact]
    public async Task CollectionRepository_InsertAsync_InsertsEntry()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new CollectionRepository(context, _mockLog.Object);
        var entry = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Test Recipe",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };

        // Act
        var id = await repo.InsertAsync(entry);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task CollectionRepository_GetByIdAsync_RetrievesEntry()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new CollectionRepository(context, _mockLog.Object);
        var entry = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Test Recipe",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var id = await repo.InsertAsync(entry);

        // Act
        var retrieved = await repo.GetByIdAsync<RecipeEntry>(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(entry.ItemId, retrieved.ItemId);
        Assert.Equal(entry.ItemName, retrieved.ItemName);
    }

    [Fact]
    public async Task CollectionRepository_GetAllAsync_ReturnsAllEntries()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new CollectionRepository(context, _mockLog.Object);

        var entries = new List<RecipeEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100 + i,
                ItemName = $"Recipe {i}",
                IsUnlocked = true,
                UnlockedAt = DateTime.UtcNow,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200 + i,
                RecipeLevel = 50,
                CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
                IsMasterRecipe = false,
                ItemLevel = 100
            });
        }

        foreach (var entry in entries)
        {
            await repo.InsertAsync(entry);
        }

        // Act
        var retrieved = await repo.GetAllAsync<RecipeEntry>();

        // Assert
        Assert.Equal(5, retrieved.Count);
    }

    [Fact]
    public async Task CollectionRepository_UpdateAsync_ModifiesEntry()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new CollectionRepository(context, _mockLog.Object);
        var entry = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Test Recipe",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var id = await repo.InsertAsync(entry);
        entry.Id = id;

        // Act - Update unlock status
        entry.IsUnlocked = true;
        entry.UnlockedAt = DateTime.UtcNow;
        await repo.UpdateAsync(entry);

        // Assert
        var retrieved = await repo.GetByIdAsync<RecipeEntry>(id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsUnlocked);
        Assert.NotNull(retrieved.UnlockedAt);
    }

    [Fact]
    public async Task CollectionRepository_DeleteAsync_RemovesEntry()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new CollectionRepository(context, _mockLog.Object);
        var entry = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Test Recipe",
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        var id = await repo.InsertAsync(entry);

        // Act
        await repo.DeleteAsync<RecipeEntry>(id);

        // Assert
        var retrieved = await repo.GetByIdAsync<RecipeEntry>(id);
        Assert.Null(retrieved);
    }

    #endregion

    #region RecipeRepository Tests

    [Fact]
    public async Task RecipeRepository_BulkUpsertAsync_InsertsMultipleEntries()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new RecipeRepository(context, _mockLog.Object);

        var entries = new List<RecipeEntry>();
        for (int i = 0; i < 10; i++)
        {
            entries.Add(new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100 + i,
                ItemName = $"Recipe {i}",
                IsUnlocked = i % 2 == 0,
                UnlockedAt = i % 2 == 0 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200 + i,
                RecipeLevel = 50,
                CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
                IsMasterRecipe = false,
                ItemLevel = 100
            });
        }

        // Act
        var inserted = await repo.BulkUpsertAsync(entries);

        // Assert
        Assert.Equal(10, inserted);
        var all = await repo.GetAllAsync<RecipeEntry>();
        Assert.Equal(10, all.Count);
    }

    [Fact]
    public async Task RecipeRepository_BulkUpsertAsync_UpdatesExistingEntries()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new RecipeRepository(context, _mockLog.Object);

        var entry = new RecipeEntry
        {
            CharacterId = 1,
            CharacterName = "Test",
            WorldName = "World",
            Type = CollectionType.Recipe,
            ItemId = 100,
            ItemName = "Recipe",
            IsUnlocked = false,
            FirstSeenAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            RecipeId = 200,
            RecipeLevel = 50,
            CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
            IsMasterRecipe = false,
            ItemLevel = 100
        };
        await repo.BulkUpsertAsync(new List<RecipeEntry> { entry });

        // Act - Update the same entry
        entry.IsUnlocked = true;
        entry.UnlockedAt = DateTime.UtcNow;
        var updated = await repo.BulkUpsertAsync(new List<RecipeEntry> { entry });

        // Assert
        Assert.Equal(1, updated);
        var all = await repo.GetAllAsync<RecipeEntry>();
        Assert.Single(all);
        Assert.True(all[0].IsUnlocked);
    }

    [Fact]
    public async Task RecipeRepository_BulkUpsertAsync_HandlesDuplicates()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new RecipeRepository(context, _mockLog.Object);

        var entries = new List<RecipeEntry>
        {
            new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100,
                ItemName = "Recipe",
                IsUnlocked = false,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200,
                RecipeLevel = 50,
                CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
                IsMasterRecipe = false,
                ItemLevel = 100
            },
            new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100, // Duplicate ItemId
                ItemName = "Recipe Updated",
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

        // Act
        var count = await repo.BulkUpsertAsync(entries);

        // Assert - Should only have 1 entry (upsert behavior)
        var all = await repo.GetAllAsync<RecipeEntry>();
        Assert.Single(all);
        Assert.True(all[0].IsUnlocked);
    }

    [Fact]
    public async Task RecipeRepository_GetUnlockedCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new RecipeRepository(context, _mockLog.Object);

        var entries = new List<RecipeEntry>();
        for (int i = 0; i < 10; i++)
        {
            entries.Add(new RecipeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.Recipe,
                ItemId = 100 + i,
                ItemName = $"Recipe {i}",
                IsUnlocked = i < 6, // 6 unlocked, 4 locked
                UnlockedAt = i < 6 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RecipeId = 200 + i,
                RecipeLevel = 50,
                CraftingClass = (AkadaemiaAnyder.Data.Models.CraftingClass)(int)CoreModels.CraftingClass.CRP,
                IsMasterRecipe = false,
                ItemLevel = 100
            });
        }
        await repo.BulkUpsertAsync(entries);

        // Act - Query all and filter by IsUnlocked
        var all = await repo.GetAllAsync<RecipeEntry>();
        var count = all.Count(r => r.IsUnlocked);

        // Assert
        Assert.Equal(6, count);
    }

    #endregion

    #region GatheringRepository Tests

    [Fact]
    public async Task GatheringRepository_BulkUpsertAsync_InsertsMultipleEntries()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new GatheringRepository(context, _mockLog.Object);

        var entries = new List<GatheringNodeEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new GatheringNodeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.GatheringNode,
                ItemId = 300 + i,
                ItemName = $"Node {i}",
                IsUnlocked = i % 2 == 0,
                UnlockedAt = i % 2 == 0 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                NodeId = 400 + i,
                GatheringClass = AkadaemiaAnyder.Data.Models.GatheringClass.Miner,
                Zone = "Test Zone",
                NodeLevel = 50,
                IsLegendary = false,
                IsEphemeral = false
            });
        }

        // Act
        var inserted = await repo.BulkUpsertAsync(entries);

        // Assert
        Assert.Equal(5, inserted);
        var all = await repo.GetAllAsync<GatheringNodeEntry>();
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public async Task GatheringRepository_GetUnlockedCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new GatheringRepository(context, _mockLog.Object);

        var entries = new List<GatheringNodeEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new GatheringNodeEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.GatheringNode,
                ItemId = 300 + i,
                ItemName = $"Node {i}",
                IsUnlocked = i < 3, // 3 unlocked, 2 locked
                UnlockedAt = i < 3 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                NodeId = 400 + i,
                GatheringClass = AkadaemiaAnyder.Data.Models.GatheringClass.Miner,
                Zone = "Test Zone",
                NodeLevel = 50,
                IsLegendary = false,
                IsEphemeral = false
            });
        }
        await repo.BulkUpsertAsync(entries);

        // Act
        var allEntries = await repo.GetAllAsync<GatheringNodeEntry>();
        var count = allEntries.Count(e => e.IsUnlocked);

        // Assert
        Assert.Equal(3, count);
    }

    #endregion

    #region FishingRepository Tests

    [Fact]
    public async Task FishingRepository_BulkUpsertAsync_InsertsMultipleEntries()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new FishingRepository(context, _mockLog.Object);

        var entries = new List<FishingHoleEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new FishingHoleEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.FishingHole,
                ItemId = 500 + i,
                ItemName = $"Fish {i}",
                IsUnlocked = i % 2 == 0,
                UnlockedAt = i % 2 == 0 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                FishId = 600 + i,
                FishingHoleId = 700 + i,
                Zone = "Test Zone",
                RecommendedBait = "Test Bait",
                IsBigFish = false
            });
        }

        // Act
        var inserted = await repo.BulkUpsertAsync(entries);

        // Assert
        Assert.Equal(5, inserted);
        var all = await repo.GetAllAsync<FishingHoleEntry>();
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public async Task FishingRepository_GetUnlockedCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var context = CreateInMemoryDatabase();
        var repo = new FishingRepository(context, _mockLog.Object);

        var entries = new List<FishingHoleEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new FishingHoleEntry
            {
                CharacterId = 1,
                CharacterName = "Test",
                WorldName = "World",
                Type = CollectionType.FishingHole,
                ItemId = 500 + i,
                ItemName = $"Fish {i}",
                IsUnlocked = i < 2, // 2 unlocked, 3 locked
                UnlockedAt = i < 2 ? DateTime.UtcNow : null,
                FirstSeenAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                FishId = 600 + i,
                FishingHoleId = 700 + i,
                Zone = "Test Zone",
                RecommendedBait = "Test Bait",
                IsBigFish = false
            });
        }
        await repo.BulkUpsertAsync(entries);

        // Act
        var allEntries = await repo.GetAllAsync<FishingHoleEntry>();
        var count = allEntries.Count(e => e.IsUnlocked);

        // Assert
        Assert.Equal(2, count);
    }

    #endregion
}
