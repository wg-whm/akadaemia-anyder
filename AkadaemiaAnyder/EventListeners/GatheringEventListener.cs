using System;
using System.Collections.Generic;
using AkadaemiaAnyder.Core.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SamplePlugin.EventListeners;

/// <summary>
/// Event-based listener for gathering node unlocks.
/// Subscribes to Dalamud framework events to detect gathering activity.
/// </summary>
public class GatheringEventListener : ICollectionListener<GatheringNode>
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;

    private readonly List<GatheringNode> _collectedNodes = new();
    private readonly HashSet<uint> _seenNodeIds = new(); // Prevent duplicates
    private bool _isActive;

    public bool IsActive => _isActive;

    /// <summary>
    /// Creates a new gathering event listener.
    /// </summary>
    /// <param name="framework">Dalamud framework service</param>
    /// <param name="clientState">Client state service</param>
    /// <param name="log">Logging service</param>
    public GatheringEventListener(
        IFramework framework,
        IClientState clientState,
        IPluginLog log)
    {
        _framework = framework ?? throw new ArgumentNullException(nameof(framework));
        _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Start listening to gathering events.
    /// </summary>
    public void Start()
    {
        if (_isActive)
        {
            _log.Warning("GatheringEventListener already started");
            return;
        }

        _framework.Update += OnFrameworkUpdate;
        _clientState.TerritoryChanged += OnTerritoryChanged;
        _isActive = true;

        _log.Information("GatheringEventListener started");
    }

    /// <summary>
    /// Stop listening to gathering events.
    /// </summary>
    public void Stop()
    {
        if (!_isActive)
        {
            _log.Warning("GatheringEventListener not active");
            return;
        }

        _framework.Update -= OnFrameworkUpdate;
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _isActive = false;

        _log.Information("GatheringEventListener stopped");
    }

    /// <summary>
    /// Get all nodes collected since Start() or last Clear().
    /// </summary>
    /// <returns>List of gathered nodes</returns>
    public List<GatheringNode> GetCollectedItems()
    {
        return new List<GatheringNode>(_collectedNodes);
    }

    /// <summary>
    /// Clear the collected nodes list.
    /// </summary>
    public void ClearCollectedItems()
    {
        _collectedNodes.Clear();
        _seenNodeIds.Clear();
        _log.Debug("GatheringEventListener: Cleared collected items");
    }

    /// <summary>
    /// Framework update event handler.
    /// Checks for gathering activity each frame.
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

            // Access UIState for gathering note data
            var uiState = UIState.Instance();
            if (uiState == null)
            {
                return;
            }

            // Check if GatheringNote is available
            // Note: GatheringNote in FFXIVClientStructs is currently a stub with no exposed methods
            // This is documented in STATUS.md as the blocking issue
            //
            // When FFXIVClientStructs adds GatheringNote support similar to RecipeNote,
            // we can implement: uiState->GatheringNote->IsNodeUnlocked(nodeId)
            //
            // For now, we detect the player is in a gathering class but cannot
            // query which nodes are unlocked

            var player = _clientState.LocalPlayer;
            var classJobId = player.ClassJob.RowId;
            bool isGatheringClass = classJobId == 16 || classJobId == 17; // 16 = MIN, 17 = BTN

            if (!isGatheringClass)
            {
                return; // Not gathering
            }

            // Future implementation when GatheringNote becomes available:
            // for (ushort nodeId = 0; nodeId < maxNodes; nodeId++)
            // {
            //     if (uiState->GatheringNote->IsNodeUnlocked(nodeId) && !_seenNodeIds.Contains(nodeId))
            //     {
            //         var node = CreateGatheringNodeFromId(nodeId);
            //         AddNode(node);
            //     }
            // }

            _log.Debug("GatheringEventListener: Player in gathering class, but GatheringNote API not available");

        }
        catch (Exception ex)
        {
            _log.Error($"GatheringEventListener.OnFrameworkUpdate error: {ex.Message}");
        }
    }

    /// <summary>
    /// Territory changed event handler.
    /// Could be used to detect zone-specific gathering opportunities.
    /// </summary>
    private void OnTerritoryChanged(ushort territoryId)
    {
        _log.Debug($"GatheringEventListener: Territory changed to {territoryId}");
        // TODO: Implement territory-based gathering tracking
    }

    /// <summary>
    /// Add a gathering node to the collection (internal use).
    /// </summary>
    /// <param name="node">The node to add</param>
    private void AddNode(GatheringNode node)
    {
        if (_seenNodeIds.Contains(node.NodeId))
        {
            return; // Already collected
        }

        _collectedNodes.Add(node);
        _seenNodeIds.Add(node.NodeId);
        _log.Information($"GatheringEventListener: Collected node {node.NodeId}");
    }
}
