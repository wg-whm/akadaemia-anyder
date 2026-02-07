using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using SamplePlugin.Services;
using AkadaemiaAnyder.Data.Services;

namespace SamplePlugin.Windows;

/// <summary>
/// Provides universal inventory search across all storage locations.
/// </summary>
public class InventoryTab
{
    private readonly MaterialAvailabilityCacheService _cacheService;
    private readonly LoggingService _logger;

    // UI state
    private string _searchQuery = string.Empty;
    private bool _isSearching = false;
    private List<SearchResult> _searchResults = new();
    private string? _errorMessage = null;

    // Storage summary state
    private StorageSummary? _storageSummary = null;
    private bool _loadingSummary = false;

    // Colors matching MainWindow.cs style
    private readonly Vector4 _successColor = new(0.0f, 1.0f, 0.0f, 1.0f);
    private readonly Vector4 _errorColor = new(1.0f, 0.0f, 0.0f, 1.0f);
    private readonly Vector4 _warningColor = new(1.0f, 0.65f, 0.0f, 1.0f);
    private readonly Vector4 _infoColor = new(0.5f, 0.8f, 1.0f, 1.0f);
    private readonly Vector4 _headerColor = new(0.8f, 0.8f, 0.8f, 1.0f);

    public InventoryTab(MaterialAvailabilityCacheService cacheService, LoggingService logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the inventory tab UI.
    /// </summary>
    public void Draw()
    {
        DrawStorageSummary();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawSearchSection();
        ImGui.Spacing();
        DrawSearchResults();
    }

    private void DrawStorageSummary()
    {
        ImGui.TextColored(_headerColor, "Storage Overview");
        ImGui.Spacing();

        if (_storageSummary == null && !_loadingSummary)
        {
            if (ImGui.Button("Load Storage Summary"))
            {
                LoadStorageSummaryAsync();
            }
            return;
        }

        if (_loadingSummary)
        {
            ImGui.TextColored(_infoColor, "Loading storage data...");
            return;
        }

        if (_storageSummary == null)
            return;

        // Inventory capacity bar
        DrawCapacityBar("Inventory", _storageSummary.InventoryUsed, _storageSummary.InventoryCapacity);

        // Saddlebag capacity bar
        DrawCapacityBar("Saddlebag", _storageSummary.SaddlebagUsed, _storageSummary.SaddlebagCapacity);

        // Retainer summary
        if (_storageSummary.RetainerCount > 0)
        {
            ImGui.Text($"Retainers: {_storageSummary.RetainerCount}");
            DrawCapacityBar("Retainers (Total)", _storageSummary.RetainerUsed, _storageSummary.RetainerCapacity);
        }

        // Refresh button
        ImGui.Spacing();
        if (ImGui.Button("Refresh##StorageSummary"))
        {
            LoadStorageSummaryAsync();
        }
    }

    private void DrawCapacityBar(string label, int used, int capacity)
    {
        if (capacity <= 0)
            return;

        float percentage = capacity > 0 ? (float)used / capacity : 0f;

        // Color based on capacity usage
        Vector4 barColor = percentage switch
        {
            >= 0.9f => _errorColor,
            >= 0.75f => _warningColor,
            _ => _successColor
        };

        ImGui.Text($"{label}:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
        ImGui.ProgressBar(percentage, new Vector2(200, 0), $"{used}/{capacity}");
        ImGui.PopStyleColor();
    }

    private void DrawSearchSection()
    {
        ImGui.TextColored(_headerColor, "Universal Search");
        ImGui.Spacing();

        // Search input
        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##SearchQuery", "Enter item name...", ref _searchQuery, 256);

        ImGui.SameLine();

        // Search button
        bool canSearch = !string.IsNullOrWhiteSpace(_searchQuery) && !_isSearching;
        if (!canSearch)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Search"))
        {
            PerformSearchAsync();
        }

        if (!canSearch)
        {
            ImGui.EndDisabled();
        }

        // Clear button
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _searchQuery = string.Empty;
            _searchResults.Clear();
            _errorMessage = null;
        }

        // Search status
        if (_isSearching)
        {
            ImGui.Spacing();
            ImGui.TextColored(_infoColor, "Searching...");
        }

        // Error message
        if (_errorMessage != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(_errorColor, _errorMessage);
        }
    }

    private void DrawSearchResults()
    {
        if (_searchResults.Count == 0)
            return;

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(_headerColor, $"Results ({_searchResults.Count})");
        ImGui.Spacing();

        // Group results by item
        var groupedResults = _searchResults
            .GroupBy(r => r.ItemId)
            .OrderBy(g => g.Key);

        foreach (var itemGroup in groupedResults)
        {
            var firstResult = itemGroup.First();

            // Item header with total count
            int totalCount = itemGroup.Sum(r => r.Quantity);
            ImGui.TextColored(_infoColor, $"{firstResult.ItemName} (Total: {totalCount})");

            // Indent for locations
            ImGui.Indent();

            // Group by location type
            var locationGroups = itemGroup
                .GroupBy(r => r.LocationType)
                .OrderBy(g => GetLocationPriority(g.Key));

            foreach (var locationGroup in locationGroups)
            {
                string locationLabel = GetLocationLabel(locationGroup.Key);
                int locationTotal = locationGroup.Sum(r => r.Quantity);

                using (var treeNode = ImRaii.TreeNode($"{locationLabel}: {locationTotal}###{firstResult.ItemId}_{locationGroup.Key}"))
                {
                    if (treeNode.Success)
                    {
                        foreach (var result in locationGroup.OrderBy(r => r.SpecificLocation))
                        {
                            ImGui.BulletText($"{result.SpecificLocation}: {result.Quantity}");
                        }
                    }
                }
            }

            ImGui.Unindent();
            ImGui.Spacing();
        }
    }

    private void PerformSearchAsync()
    {
        if (_isSearching || string.IsNullOrWhiteSpace(_searchQuery))
            return;

        _isSearching = true;
        _errorMessage = null;
        _searchResults.Clear();

        var query = _searchQuery.Trim();

        // Note: Full inventory search requires integration with game data and item search.
        // This is a placeholder implementation - actual search would require IDataManager
        // for item name lookup and the cache service for location data.

        // For now, show a message that search is pending implementation
        _errorMessage = $"Search for '{query}' - functionality pending full inventory scanning implementation";
        _isSearching = false;
        _searchResults = new List<SearchResult>();

        _logger.LogInfo($"Inventory search initiated for: {query}");
    }

    private void LoadStorageSummaryAsync()
    {
        if (_loadingSummary)
            return;

        _loadingSummary = true;

        Plugin.Framework.RunOnTick(async () =>
        {
            try
            {
                // Get summary data from cache service
                // This is a simplified version - you may need to adjust based on actual service capabilities
                var summary = new StorageSummary
                {
                    InventoryUsed = 0,
                    InventoryCapacity = 140, // Standard inventory size
                    SaddlebagUsed = 0,
                    SaddlebagCapacity = 140,
                    RetainerUsed = 0,
                    RetainerCapacity = 0,
                    RetainerCount = 0
                };

                // Note: Actual implementation would query the cache service or repository
                // for real storage data. This is a placeholder structure.

                _storageSummary = summary;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load storage summary: {ex.Message}");
            }
            finally
            {
                _loadingSummary = false;
            }
        });
    }

    private static string GetLocationLabel(string locationType)
    {
        return locationType switch
        {
            "Inventory" => "Inventory",
            "Saddlebag" => "Saddlebag",
            "Retainer" => "Retainers",
            "Armory" => "Armory Chest",
            "CrystalBag" => "Crystal Bag",
            _ => locationType
        };
    }

    private static int GetLocationPriority(string locationType)
    {
        return locationType switch
        {
            "Inventory" => 0,
            "Saddlebag" => 1,
            "Armory" => 2,
            "CrystalBag" => 3,
            "Retainer" => 4,
            _ => 99
        };
    }

    /// <summary>
    /// Represents a single search result for an item in a specific location.
    /// </summary>
    private class SearchResult
    {
        public uint ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string LocationType { get; init; } = string.Empty;
        public string SpecificLocation { get; init; } = string.Empty;
        public int Quantity { get; init; }
    }

    /// <summary>
    /// Summary of storage capacity across all locations.
    /// </summary>
    private class StorageSummary
    {
        public int InventoryUsed { get; init; }
        public int InventoryCapacity { get; init; }
        public int SaddlebagUsed { get; init; }
        public int SaddlebagCapacity { get; init; }
        public int RetainerUsed { get; init; }
        public int RetainerCapacity { get; init; }
        public int RetainerCount { get; init; }
    }
}
