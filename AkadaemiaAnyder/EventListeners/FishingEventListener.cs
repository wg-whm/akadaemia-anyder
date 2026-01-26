using System;
using System.Collections.Generic;
using AkadaemiaAnyder.Core.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SamplePlugin.EventListeners;

/// <summary>
/// Event-based listener for fishing catches.
/// Subscribes to Dalamud framework events to detect fishing activity.
/// </summary>
public class FishingEventListener : ICollectionListener<FishingHole>
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;

    private readonly List<FishingHole> _collectedHoles = new();
    private readonly HashSet<uint> _seenFishIds = new(); // Prevent duplicates
    private bool _isActive;

    public bool IsActive => _isActive;

    /// <summary>
    /// Creates a new fishing event listener.
    /// </summary>
    /// <param name="framework">Dalamud framework service</param>
    /// <param name="clientState">Client state service</param>
    /// <param name="log">Logging service</param>
    public FishingEventListener(
        IFramework framework,
        IClientState clientState,
        IPluginLog log)
    {
        _framework = framework ?? throw new ArgumentNullException(nameof(framework));
        _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Start listening to fishing events.
    /// </summary>
    public void Start()
    {
        if (_isActive)
        {
            _log.Warning("FishingEventListener already started");
            return;
        }

        _framework.Update += OnFrameworkUpdate;
        _clientState.TerritoryChanged += OnTerritoryChanged;
        _isActive = true;

        _log.Information("FishingEventListener started");
    }

    /// <summary>
    /// Stop listening to fishing events.
    /// </summary>
    public void Stop()
    {
        if (!_isActive)
        {
            _log.Warning("FishingEventListener not active");
            return;
        }

        _framework.Update -= OnFrameworkUpdate;
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _isActive = false;

        _log.Information("FishingEventListener stopped");
    }

    /// <summary>
    /// Get all fishing holes collected since Start() or last Clear().
    /// </summary>
    /// <returns>List of fishing holes</returns>
    public List<FishingHole> GetCollectedItems()
    {
        return new List<FishingHole>(_collectedHoles);
    }

    /// <summary>
    /// Clear the collected fishing holes list.
    /// </summary>
    public void ClearCollectedItems()
    {
        _collectedHoles.Clear();
        _seenFishIds.Clear();
        _log.Debug("FishingEventListener: Cleared collected items");
    }

    /// <summary>
    /// Framework update event handler.
    /// Checks for fishing activity each frame.
    /// </summary>
    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            // Check if player is logged in
            if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            {
                return;
            }

            // Access UIState for fishing note data
            var uiState = UIState.Instance();
            if (uiState == null)
            {
                return;
            }

            // Check if FishingNote is available
            // Note: FishingNote in FFXIVClientStructs is currently a stub with no exposed methods
            // This is documented in STATUS.md as the blocking issue
            //
            // When FFXIVClientStructs adds FishingNote support similar to RecipeNote,
            // we can implement: uiState->FishingNote->IsFishCaught(fishId)
            //
            // For now, we detect the player is in fishing class but cannot
            // query which fish are caught

            var player = _clientState.LocalPlayer;
            var classJobId = player.ClassJob.RowId;
            bool isFisherClass = classJobId == 18; // 18 = FSH

            if (!isFisherClass)
            {
                return; // Not fishing
            }

            // Future implementation when FishingNote becomes available:
            // for (ushort fishId = 0; fishId < maxFish; fishId++)
            // {
            //     if (uiState->FishingNote->IsFishCaught(fishId) && !_seenFishIds.Contains(fishId))
            //     {
            //         var fish = CreateFishingHoleFromId(fishId);
            //         AddFishingHole(fish);
            //     }
            // }

            _log.Debug("FishingEventListener: Player in fishing class, but FishingNote API not available");

        }
        catch (Exception ex)
        {
            _log.Error($"FishingEventListener.OnFrameworkUpdate error: {ex.Message}");
        }
    }

    /// <summary>
    /// Territory changed event handler.
    /// Could be used to detect zone-specific fishing holes.
    /// </summary>
    private void OnTerritoryChanged(ushort territoryId)
    {
        _log.Debug($"FishingEventListener: Territory changed to {territoryId}");
        // TODO: Implement territory-based fishing tracking
    }

    /// <summary>
    /// Add a fishing hole to the collection (internal use).
    /// </summary>
    /// <param name="hole">The fishing hole to add</param>
    private void AddFishingHole(FishingHole hole)
    {
        if (_seenFishIds.Contains(hole.FishId))
        {
            return; // Already collected
        }

        _collectedHoles.Add(hole);
        _seenFishIds.Add(hole.FishId);
        _log.Information($"FishingEventListener: Collected fish {hole.FishId}");
    }
}
