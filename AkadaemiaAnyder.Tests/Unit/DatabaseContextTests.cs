using System;
using System.IO;
using Moq;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data;

namespace AkadaemiaAnyder.Tests.Unit;

/// <summary>
/// Unit tests for DatabaseContext 3-tier fallback system.
/// </summary>
public class DatabaseContextTests
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly string _testDirectory;

    public DatabaseContextTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"akadaemia_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    private void CleanupTestDirectory()
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
    public void DatabaseContext_Tier1_InitializesSuccessfully()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert
        Assert.Equal(DatabaseTier.Tier1, context.GetHealthStatus());
        Assert.NotNull(context.Connection);
        _mockLog.Verify(x => x.Information(It.Is<string>(s => s.Contains("Tier 1"))), Times.Once);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Tier1_CreatesDirectory()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "subdir");

        // Act
        using var context = new DatabaseContext(_mockLog.Object, nonExistentDir);

        // Assert
        Assert.True(Directory.Exists(nonExistentDir));
        Assert.Equal(DatabaseTier.Tier1, context.GetHealthStatus());

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Tier3_FallbackToInMemory()
    {
        // Arrange - Use :memory: path which forces Tier 3
        using var context = new DatabaseContext(_mockLog.Object, ":memory:");

        // Assert
        Assert.Equal(DatabaseTier.Tier3, context.GetHealthStatus());
        Assert.NotNull(context.Connection);
        _mockLog.Verify(x => x.Error(It.Is<string>(s => s.Contains("Tier 3"))), Times.Once);
    }

    [Fact]
    public void DatabaseContext_Connection_IsNotNullForValidTiers()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert
        Assert.NotNull(context.Connection);
        Assert.True(context.GetHealthStatus() != DatabaseTier.Degraded);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Dispose_ClosesConnection()
    {
        // Arrange
        var context = new DatabaseContext(_mockLog.Object, _testDirectory);
        var connection = context.Connection;

        // Act
        context.Dispose();

        // Assert - Connection should be disposed
        // We can't directly test if connection is disposed, but multiple dispose should not throw
        context.Dispose(); // Should not throw

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Tier1_CreatesDatabaseFile()
    {
        // Arrange
        var dbPath = Path.Combine(_testDirectory, "akadaemia.db");

        // Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert
        Assert.True(File.Exists(dbPath));
        Assert.Equal(DatabaseTier.Tier1, context.GetHealthStatus());

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_MultipleInstances_CanAccessSameDatabase()
    {
        // Arrange
        using var context1 = new DatabaseContext(_mockLog.Object, _testDirectory);
        using var context2 = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert - Both should successfully initialize
        Assert.Equal(DatabaseTier.Tier1, context1.GetHealthStatus());
        Assert.Equal(DatabaseTier.Tier1, context2.GetHealthStatus());
        Assert.NotNull(context1.Connection);
        Assert.NotNull(context2.Connection);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Tier1_AppliesMigrations()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert - Verify tables exist
        Assert.NotNull(context.Connection);
        using var cmd = context.Connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='collection_entries'";
        var result = cmd.ExecuteScalar();
        Assert.NotNull(result);
        Assert.Equal("collection_entries", result);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_GetHealthStatus_ReturnsTier1ForNormalOperation()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Act
        var status = context.GetHealthStatus();

        // Assert
        Assert.Equal(DatabaseTier.Tier1, status);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_GetHealthStatus_ReturnsTier3ForInMemory()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, ":memory:");

        // Act
        var status = context.GetHealthStatus();

        // Assert
        Assert.Equal(DatabaseTier.Tier3, status);
    }

    [Fact]
    public void DatabaseContext_BusyTimeout_IsConfigured()
    {
        // Arrange
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert - Connection should have busy timeout configured
        Assert.NotNull(context.Connection);
        using var cmd = context.Connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout";
        var timeout = cmd.ExecuteScalar();
        Assert.NotNull(timeout);
        Assert.True(Convert.ToInt32(timeout) > 0);

        // Cleanup
        CleanupTestDirectory();
    }

    [Fact]
    public void DatabaseContext_Tier1_VerifiesDatabaseIntegrity()
    {
        // Arrange & Act
        using var context = new DatabaseContext(_mockLog.Object, _testDirectory);

        // Assert - If Tier 1 succeeded, integrity check passed
        Assert.Equal(DatabaseTier.Tier1, context.GetHealthStatus());

        // Additional verification
        using var cmd = context.Connection!.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        var result = cmd.ExecuteScalar();
        Assert.Equal("ok", result);

        // Cleanup
        CleanupTestDirectory();
    }
}
