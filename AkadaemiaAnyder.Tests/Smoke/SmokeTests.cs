using System;
using System.IO;
using Moq;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data;

namespace AkadaemiaAnyder.Tests.Smoke;

/// <summary>
/// Smoke tests validating basic plugin functionality without full Dalamud environment.
/// These tests verify that core components can be instantiated and don't throw on creation.
/// </summary>
public class SmokeTests : IDisposable
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Mock<IFramework> _mockFramework;
    private readonly Mock<IClientState> _mockClientState;
    private readonly string _testDirectory;

    public SmokeTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _mockFramework = new Mock<IFramework>();
        _mockClientState = new Mock<IClientState>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"akadaemia_smoke_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
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
    public void Plugin_DatabaseContext_InitializesWithoutThrowing()
    {
        // Arrange & Act
        DatabaseContext? context = null;
        Exception? caughtException = null;

        try
        {
            context = new DatabaseContext(_mockLog.Object, _testDirectory);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(context);
        Assert.NotNull(context.Connection);

        // Cleanup
        context?.Dispose();
    }

    [Fact]
    public void Plugin_DatabaseContext_AnyTierAcceptable()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert - Any tier except Degraded is acceptable
        var tier = context.GetHealthStatus();
        Assert.NotEqual(DatabaseTier.Degraded, tier);
    }

    [Fact]
    public void Plugin_InMemoryDatabase_InitializesSuccessfully()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, ":memory:");

        // Assert
        Assert.Equal(DatabaseTier.Tier3, context.GetHealthStatus());
        Assert.NotNull(context.Connection);
    }

    [Fact]
    public void Plugin_DatabaseContext_CanExecuteBasicQuery()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Act
        Exception? caughtException = null;
        object? result = null;

        try
        {
            using var cmd = context.Connection!.CreateCommand();
            cmd.CommandText = "SELECT 1";
            result = cmd.ExecuteScalar();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(result);
        Assert.Equal(1L, result);
    }

    [Fact]
    public void Plugin_DatabaseContext_TablesExist()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Act
        using var cmd = context.Connection!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
        var tableCount = Convert.ToInt32(cmd.ExecuteScalar());

        // Assert
        Assert.True(tableCount > 0, "Database should have tables after initialization");
    }

    [Fact]
    public void Plugin_DatabaseContext_CollectionEntriesTableExists()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Act
        using var cmd = context.Connection!.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='collection_entries'";
        var result = cmd.ExecuteScalar();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("collection_entries", result);
    }

    [Fact]
    public void Plugin_DatabaseContext_DisposesCleanly()
    {
        // Arrange
        var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Act
        Exception? caughtException = null;
        try
        {
            context.Dispose();
            context.Dispose(); // Multiple dispose should not throw
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
    }

    [Fact]
    public void Plugin_DatabaseContext_HandlesMissingDirectory()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "subdir", "another", "deep");

        // Act
        Exception? caughtException = null;
        DatabaseContext? context = null;

        try
        {
            context = new DatabaseContext(_mockLog.Object, nonExistentPath);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert - Should create directory and succeed
        Assert.Null(caughtException);
        Assert.NotNull(context);
        Assert.True(Directory.Exists(nonExistentPath));

        // Cleanup
        context?.Dispose();
    }

    [Fact]
    public void Plugin_EventListeners_CanBeCreated()
    {
        // Arrange & Act
        Exception? caughtException = null;
        object? gatheringListener = null;
        object? fishingListener = null;

        try
        {
            gatheringListener = new SamplePlugin.EventListeners.GatheringEventListener(
                _mockFramework.Object,
                _mockClientState.Object,
                _mockLog.Object
            );

            fishingListener = new SamplePlugin.EventListeners.FishingEventListener(
                _mockFramework.Object,
                _mockClientState.Object,
                _mockLog.Object
            );
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(gatheringListener);
        Assert.NotNull(fishingListener);
    }

    [Fact]
    public void Plugin_MemoryReaders_CanBeCreated()
    {
        // Arrange & Act
        Exception? caughtException = null;
        object? recipeReader = null;

        try
        {
            recipeReader = new SamplePlugin.MemoryReaders.RecipeReader();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(recipeReader);
    }

    [Fact]
    public void Plugin_Services_CanBeCreatedWithDependencies()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);
        var collectionRepo = new AkadaemiaAnyder.Data.Repositories.CollectionRepository(context, _mockLog.Object);
        var recipeRepo = new AkadaemiaAnyder.Data.Repositories.RecipeRepository(context, _mockLog.Object);
        var gatheringRepo = new AkadaemiaAnyder.Data.Repositories.GatheringRepository(context, _mockLog.Object);
        var fishingRepo = new AkadaemiaAnyder.Data.Repositories.FishingRepository(context, _mockLog.Object);

        // Act
        Exception? caughtException = null;
        object? progressCalculator = null;
        object? changeDetector = null;
        object? exporter = null;
        object? importer = null;

        try
        {
            progressCalculator = new SamplePlugin.Services.ProgressCalculator(
                collectionRepo,
                recipeRepo,
                gatheringRepo,
                fishingRepo,
                _mockLog.Object
            );

            changeDetector = new SamplePlugin.Services.ChangeDetector(
                collectionRepo,
                recipeRepo,
                gatheringRepo,
                fishingRepo,
                _mockLog.Object
            );

            exporter = new SamplePlugin.Services.JsonExporter(
                collectionRepo,
                recipeRepo,
                gatheringRepo,
                fishingRepo,
                context,
                _mockLog.Object
            );

            importer = new SamplePlugin.Services.JsonImporter(
                collectionRepo,
                recipeRepo,
                gatheringRepo,
                fishingRepo,
                _mockLog.Object
            );
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(progressCalculator);
        Assert.NotNull(changeDetector);
        Assert.NotNull(exporter);
        Assert.NotNull(importer);
    }

    [Fact]
    public void Plugin_LoggingService_CanBeCreated()
    {
        // Arrange & Act
        Exception? caughtException = null;
        object? loggingService = null;

        try
        {
            loggingService = new SamplePlugin.Services.LoggingService(_mockLog.Object);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(loggingService);
    }

    [Fact]
    public void Plugin_TelemetryService_CanBeCreated()
    {
        // Arrange & Act
        Exception? caughtException = null;
        object? telemetryService = null;

        try
        {
            telemetryService = new SamplePlugin.Services.TelemetryService(_mockLog.Object);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.Null(caughtException);
        Assert.NotNull(telemetryService);
    }
}
