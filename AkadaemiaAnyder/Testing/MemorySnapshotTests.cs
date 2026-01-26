using System;
using System.Collections.Generic;
using System.IO;
using AkadaemiaAnyder.Core.Models;
using SamplePlugin.MemoryReaders;

namespace SamplePlugin.Testing;

/// <summary>
/// Tests for memory snapshot recording and playback.
/// Verifies that snapshots can be created, saved, and loaded.
/// </summary>
public class MemorySnapshotTests
{
    /// <summary>
    /// Verifies that MemorySnapshotPlayer can load snapshots from disk.
    /// </summary>
    public static bool TestLoadEmptySnapshot()
    {
        var player = new MemorySnapshotPlayer();
        var filePath = "tests/fixtures/empty-character.json";

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"Fixture not found: {filePath}");
            return false;
        }

        var mockReader = player.LoadSnapshot(filePath);
        if (mockReader == null)
        {
            System.Diagnostics.Debug.WriteLine("Failed to load empty-character snapshot");
            return false;
        }

        // Verify empty snapshot returns 0 unlocked recipes
        var unlockedCount = mockReader.GetUnlockedCount();
        if (unlockedCount != 0)
        {
            System.Diagnostics.Debug.WriteLine($"Expected 0 unlocked recipes, got {unlockedCount}");
            return false;
        }

        // Verify total count is 512
        var totalCount = mockReader.GetTotalCount();
        if (totalCount != 512)
        {
            System.Diagnostics.Debug.WriteLine($"Expected 512 total recipes, got {totalCount}");
            return false;
        }

        System.Diagnostics.Debug.WriteLine("TestLoadEmptySnapshot: PASS");
        return true;
    }

    /// <summary>
    /// Verifies that MemorySnapshotPlayer can load partial collection snapshot.
    /// </summary>
    public static bool TestLoadPartialSnapshot()
    {
        var player = new MemorySnapshotPlayer();
        var filePath = "tests/fixtures/partial-collections.json";

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"Fixture not found: {filePath}");
            return false;
        }

        var mockReader = player.LoadSnapshot(filePath);
        if (mockReader == null)
        {
            System.Diagnostics.Debug.WriteLine("Failed to load partial-collections snapshot");
            return false;
        }

        var unlockedCount = mockReader.GetUnlockedCount();
        if (unlockedCount == 0)
        {
            System.Diagnostics.Debug.WriteLine("Partial snapshot should have unlocked recipes");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"TestLoadPartialSnapshot: PASS (unlocked: {unlockedCount})");
        return true;
    }

    /// <summary>
    /// Verifies that MemorySnapshotPlayer can load full collection snapshot.
    /// </summary>
    public static bool TestLoadFullSnapshot()
    {
        var player = new MemorySnapshotPlayer();
        var filePath = "tests/fixtures/full-collections.json";

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"Fixture not found: {filePath}");
            return false;
        }

        var mockReader = player.LoadSnapshot(filePath);
        if (mockReader == null)
        {
            System.Diagnostics.Debug.WriteLine("Failed to load full-collections snapshot");
            return false;
        }

        var unlockedCount = mockReader.GetUnlockedCount();
        var totalCount = mockReader.GetTotalCount();

        if (unlockedCount == 0)
        {
            System.Diagnostics.Debug.WriteLine("Full snapshot should have unlocked recipes");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"TestLoadFullSnapshot: PASS (unlocked: {unlockedCount}/{totalCount})");
        return true;
    }

    /// <summary>
    /// Tests the CreateTestSnapshot helper method.
    /// </summary>
    public static bool TestCreateTestSnapshot()
    {
        var recipesToUnlock = new List<uint> { 0, 1, 2, 64, 128, 192, 256 };
        var mockReader = MemorySnapshotPlayer.CreateTestSnapshot(recipesToUnlock);

        var unlockedCount = mockReader.GetUnlockedCount();
        if (unlockedCount != recipesToUnlock.Count)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Expected {recipesToUnlock.Count} unlocked recipes, got {unlockedCount}");
            return false;
        }

        var data = mockReader.ReadData();
        if (data == null || data.Count != recipesToUnlock.Count)
        {
            System.Diagnostics.Debug.WriteLine("Failed to read test snapshot data");
            return false;
        }

        System.Diagnostics.Debug.WriteLine("TestCreateTestSnapshot: PASS");
        return true;
    }

    /// <summary>
    /// Runs all snapshot tests.
    /// </summary>
    public static void RunAllTests()
    {
        System.Diagnostics.Debug.WriteLine("=== Memory Snapshot Tests ===");

        var tests = new List<(string name, Func<bool> test)>
        {
            ("LoadEmptySnapshot", TestLoadEmptySnapshot),
            ("LoadPartialSnapshot", TestLoadPartialSnapshot),
            ("LoadFullSnapshot", TestLoadFullSnapshot),
            ("CreateTestSnapshot", TestCreateTestSnapshot),
        };

        int passCount = 0;
        int failCount = 0;

        foreach (var (name, test) in tests)
        {
            try
            {
                if (test())
                {
                    passCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{name}: FAIL - {ex.Message}");
                failCount++;
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"=== Results: {passCount} passed, {failCount} failed ===");
    }
}
