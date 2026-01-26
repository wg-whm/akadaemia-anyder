using System;
using System.Threading;
using AkadaemiaAnyder.Core.Models;
using SamplePlugin;

namespace SamplePlugin.EventListeners;

/// <summary>
/// Tests for GatheringEventListener and FishingEventListener.
/// </summary>
public static class EventListenerTests
{
    /// <summary>
    /// Test GatheringEventListener lifecycle and basic functionality.
    /// </summary>
    public static void TestGatheringListener()
    {
        Plugin.Log.Information("=== Testing GatheringEventListener ===");

        var listener = new GatheringEventListener(
            Plugin.Framework,
            Plugin.ClientState,
            Plugin.Log
        );

        // Test lifecycle
        Plugin.Log.Information($"Initial state - IsActive: {listener.IsActive}");

        listener.Start();
        Plugin.Log.Information($"After Start() - IsActive: {listener.IsActive}");

        // Wait briefly to allow events to fire
        Thread.Sleep(100);

        var collectedNodes = listener.GetCollectedItems();
        Plugin.Log.Information($"Collected {collectedNodes.Count} gathering nodes");

        listener.Stop();
        Plugin.Log.Information($"After Stop() - IsActive: {listener.IsActive}");

        // Test clear
        listener.ClearCollectedItems();
        var clearedNodes = listener.GetCollectedItems();
        Plugin.Log.Information($"After Clear() - Items: {clearedNodes.Count} (expected: 0)");

        Plugin.Log.Information("=== GatheringEventListener Test Complete ===");
    }

    /// <summary>
    /// Test FishingEventListener lifecycle and basic functionality.
    /// </summary>
    public static void TestFishingListener()
    {
        Plugin.Log.Information("=== Testing FishingEventListener ===");

        var listener = new FishingEventListener(
            Plugin.Framework,
            Plugin.ClientState,
            Plugin.Log
        );

        // Test lifecycle
        Plugin.Log.Information($"Initial state - IsActive: {listener.IsActive}");

        listener.Start();
        Plugin.Log.Information($"After Start() - IsActive: {listener.IsActive}");

        // Wait briefly to allow events to fire
        Thread.Sleep(100);

        var collectedHoles = listener.GetCollectedItems();
        Plugin.Log.Information($"Collected {collectedHoles.Count} fishing holes");

        listener.Stop();
        Plugin.Log.Information($"After Stop() - IsActive: {listener.IsActive}");

        // Test clear
        listener.ClearCollectedItems();
        var clearedHoles = listener.GetCollectedItems();
        Plugin.Log.Information($"After Clear() - Items: {clearedHoles.Count} (expected: 0)");

        Plugin.Log.Information("=== FishingEventListener Test Complete ===");
    }

    /// <summary>
    /// Verify ICollectionListener interface implementation.
    /// </summary>
    public static void VerifyInterfaceImplementation()
    {
        Plugin.Log.Information("=== Verifying ICollectionListener Implementation ===");

        // Verify GatheringEventListener implements interface
        ICollectionListener<GatheringNode> gatheringListener = new GatheringEventListener(
            Plugin.Framework,
            Plugin.ClientState,
            Plugin.Log
        );
        Plugin.Log.Information("[✓] GatheringEventListener implements ICollectionListener<GatheringNode>");

        // Verify FishingEventListener implements interface
        ICollectionListener<FishingHole> fishingListener = new FishingEventListener(
            Plugin.Framework,
            Plugin.ClientState,
            Plugin.Log
        );
        Plugin.Log.Information("[✓] FishingEventListener implements ICollectionListener<FishingHole>");

        // Verify interface methods are available
        var hasStart = gatheringListener.GetType().GetMethod("Start") != null;
        var hasStop = gatheringListener.GetType().GetMethod("Stop") != null;
        var hasGetCollected = gatheringListener.GetType().GetMethod("GetCollectedItems") != null;
        var hasClear = gatheringListener.GetType().GetMethod("ClearCollectedItems") != null;
        var hasIsActive = gatheringListener.GetType().GetProperty("IsActive") != null;

        Plugin.Log.Information($"[{(hasStart ? "✓" : "✗")}] Start() method available");
        Plugin.Log.Information($"[{(hasStop ? "✓" : "✗")}] Stop() method available");
        Plugin.Log.Information($"[{(hasGetCollected ? "✓" : "✗")}] GetCollectedItems() method available");
        Plugin.Log.Information($"[{(hasClear ? "✓" : "✗")}] ClearCollectedItems() method available");
        Plugin.Log.Information($"[{(hasIsActive ? "✓" : "✗")}] IsActive property available");

        Plugin.Log.Information("=== Verification Complete ===");
    }
}
