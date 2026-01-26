using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SamplePlugin.Services;
using AkadaemiaAnyder.Core.Models;
using AkadaemiaAnyder.Data;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly ICollectionService collectionService;
    private readonly ProgressCalculator progressCalculator;
    private readonly JsonExporter jsonExporter;
    private readonly JsonImporter jsonImporter;
    private readonly DatabaseContext databaseContext;
    private readonly LoggingService logger;

    // CRITICAL: Field-based state (NOT properties) for thread-safe UI updates
    private bool isScanning = false;
    private ScanResult? lastScanResult = null;
    private (int total, int unlocked, double percentage) overallStats = (0, 0, 0.0);
    private (int total, int unlocked, double percentage) recipeStats = (0, 0, 0.0);
    private (int total, int unlocked, double percentage) gatheringStats = (0, 0, 0.0);
    private (int total, int unlocked, double percentage) fishingStats = (0, 0, 0.0);
    private string statusMessage = string.Empty;
    private bool isExporting = false;
    private bool isImporting = false;
    private string exportMessage = string.Empty;
    private string importMessage = string.Empty;

    public MainWindow(
        Plugin plugin,
        ICollectionService collectionService,
        ProgressCalculator progressCalculator,
        JsonExporter jsonExporter,
        JsonImporter jsonImporter,
        DatabaseContext databaseContext,
        LoggingService logger)
        : base("Akadaemia Anyder##MainWindow", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        this.collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        this.progressCalculator = progressCalculator ?? throw new ArgumentNullException(nameof(progressCalculator));
        this.jsonExporter = jsonExporter ?? throw new ArgumentNullException(nameof(jsonExporter));
        this.jsonImporter = jsonImporter ?? throw new ArgumentNullException(nameof(jsonImporter));
        this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load initial stats on startup
        RefreshStatsInBackground();
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    public override void Draw()
    {
        using (var tabBar = ImRaii.TabBar("MainTabBar"))
        {
            if (!tabBar.Success)
                return;

            DrawOverviewTab();
            DrawRecipesTab();
            DrawGatheringTab();
            DrawFishingTab();
            DrawSettingsTab();
        }
    }

    private void DrawOverviewTab()
    {
        using (var tab = ImRaii.TabItem("Overview"))
        {
            if (!tab.Success)
                return;

            ImGui.Spacing();

            // Overall progress section
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Overall Progress");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Total Collections: {overallStats.unlocked}/{overallStats.total}");
            ImGui.ProgressBar((float)(overallStats.percentage / 100.0), new Vector2(-1, 0), $"{overallStats.percentage:F1}%");

            ImGui.Spacing();
            ImGui.Spacing();

            // Category breakdown
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Category Breakdown");
            ImGui.Separator();
            ImGui.Spacing();

            DrawProgressRow("Recipes", recipeStats);
            DrawProgressRow("Gathering Nodes", gatheringStats);
            DrawProgressRow("Fishing Holes", fishingStats);

            ImGui.Spacing();
            ImGui.Spacing();

            // Scan section
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Collection Scanner");
            ImGui.Separator();
            ImGui.Spacing();

            // Scan button
            var buttonDisabled = isScanning;
            if (buttonDisabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
            }

            if (ImGui.Button("Scan All Collections", new Vector2(200, 30)))
            {
                if (!isScanning)
                {
                    OnScanButtonClick();
                }
            }

            if (buttonDisabled)
            {
                ImGui.PopStyleVar();
            }

            if (isScanning)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Scanning...");
            }

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                ImGui.Spacing();
                var messageColor = statusMessage.StartsWith("Error") || statusMessage.Contains("failed")
                    ? new Vector4(1, 0, 0, 1)  // Red for errors
                    : statusMessage.Contains("complete")
                        ? new Vector4(0, 1, 0, 1)  // Green for success
                        : new Vector4(1, 1, 1, 1);  // White for info

                ImGui.TextColored(messageColor, statusMessage);
            }

            // Last scan results
            if (lastScanResult != null)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Last Scan Results");
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text($"Status: {(lastScanResult.Success ? "Success" : "Failed")}");
                ImGui.Text($"Items Scanned: {lastScanResult.ItemsScanned}");
                ImGui.Text($"Items Updated: {lastScanResult.ItemsUpdated}");
                ImGui.Text($"New Items: {lastScanResult.NewItems}");
                ImGui.Text($"Duration: {lastScanResult.Duration.TotalSeconds:F2}s");

                if (!lastScanResult.Success && !string.IsNullOrEmpty(lastScanResult.ErrorMessage))
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {lastScanResult.ErrorMessage}");
                }
            }
        }
    }

    private void DrawRecipesTab()
    {
        using (var tab = ImRaii.TabItem("Recipes"))
        {
            if (!tab.Success)
                return;

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Recipe Collection Progress");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Recipes: {recipeStats.unlocked}/{recipeStats.total}");
            ImGui.ProgressBar((float)(recipeStats.percentage / 100.0), new Vector2(-1, 0), $"{recipeStats.percentage:F1}%");

            ImGui.Spacing();
            ImGui.Text("Recipe tracking shows which crafting recipes you have unlocked.");
            ImGui.Text("Use the scanner to update your recipe progress.");
        }
    }

    private void DrawGatheringTab()
    {
        using (var tab = ImRaii.TabItem("Gathering"))
        {
            if (!tab.Success)
                return;

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Gathering Node Progress");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Gathering Nodes: {gatheringStats.unlocked}/{gatheringStats.total}");
            ImGui.ProgressBar((float)(gatheringStats.percentage / 100.0), new Vector2(-1, 0), $"{gatheringStats.percentage:F1}%");

            ImGui.Spacing();
            ImGui.Text("Gathering tracking shows which gathering nodes you have discovered.");
            ImGui.Text("Nodes are tracked automatically as you discover them in-game.");
        }
    }

    private void DrawFishingTab()
    {
        using (var tab = ImRaii.TabItem("Fishing"))
        {
            if (!tab.Success)
                return;

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Fishing Hole Progress");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Fishing Holes: {fishingStats.unlocked}/{fishingStats.total}");
            ImGui.ProgressBar((float)(fishingStats.percentage / 100.0), new Vector2(-1, 0), $"{fishingStats.percentage:F1}%");

            ImGui.Spacing();
            ImGui.Text("Fishing tracking shows which fishing holes you have discovered.");
            ImGui.Text("Holes are tracked automatically as you fish at new locations.");
        }
    }

    private void DrawSettingsTab()
    {
        using (var tab = ImRaii.TabItem("Settings"))
        {
            if (!tab.Success)
                return;

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Data Management");
            ImGui.Separator();
            ImGui.Spacing();

            // Export section
            var exportDisabled = isExporting || isImporting;
            if (exportDisabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
            }

            if (ImGui.Button("Export to JSON", new Vector2(200, 30)))
            {
                if (!isExporting && !isImporting)
                {
                    OnExportButtonClick();
                }
            }

            if (exportDisabled)
            {
                ImGui.PopStyleVar();
            }

            if (isExporting)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Exporting...");
            }

            if (!string.IsNullOrEmpty(exportMessage))
            {
                var exportColor = exportMessage.StartsWith("Error")
                    ? new Vector4(1, 0, 0, 1)
                    : new Vector4(0, 1, 0, 1);
                ImGui.TextColored(exportColor, exportMessage);
            }

            ImGui.Spacing();

            // Import section
            var importDisabled = isImporting || isExporting;
            if (importDisabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
            }

            if (ImGui.Button("Import from JSON", new Vector2(200, 30)))
            {
                if (!isImporting && !isExporting)
                {
                    OnImportButtonClick();
                }
            }

            if (importDisabled)
            {
                ImGui.PopStyleVar();
            }

            if (isImporting)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Importing...");
            }

            if (!string.IsNullOrEmpty(importMessage))
            {
                var importColor = importMessage.StartsWith("Error")
                    ? new Vector4(1, 0, 0, 1)
                    : new Vector4(0, 1, 0, 1);
                ImGui.TextColored(importColor, importMessage);
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // Database health section
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Database Status");
            ImGui.Separator();
            ImGui.Spacing();

            var tier = databaseContext.GetHealthStatus();
            var (tierColor, tierDescription) = GetTierInfo(tier);

            ImGui.TextColored(tierColor, $"Database Tier: {tier}");
            ImGui.Text(tierDescription);
        }
    }

    private void DrawProgressRow(string label, (int total, int unlocked, double percentage) stats)
    {
        ImGui.Text($"{label}:");
        ImGui.SameLine(200);
        ImGui.Text($"{stats.unlocked}/{stats.total}");
        ImGui.ProgressBar((float)(stats.percentage / 100.0), new Vector2(-1, 0), $"{stats.percentage:F1}%");
        ImGui.Spacing();
    }

    private (Vector4 color, string description) GetTierInfo(DatabaseTier tier)
    {
        return tier switch
        {
            DatabaseTier.Tier1 => (
                new Vector4(0, 1, 0, 1),
                "Normal operation - file-based database"
            ),
            DatabaseTier.Tier2 => (
                new Vector4(1, 1, 0, 1),
                "Recovered from corruption - database was reset"
            ),
            DatabaseTier.Tier3 => (
                new Vector4(1, 0.5f, 0, 1),
                "In-memory only - persistent storage unavailable"
            ),
            DatabaseTier.Degraded => (
                new Vector4(1, 0, 0, 1),
                "All initialization tiers failed - no database available"
            ),
            _ => (
                new Vector4(1, 1, 1, 1),
                "Unknown status"
            )
        };
    }

    // CRITICAL: Framework.RunOnFrameworkThread for async operations
    private void OnScanButtonClick()
    {
        if (isScanning)
            return;

        isScanning = true;
        statusMessage = "Starting collection scan...";

        // Run async work on framework thread
        Plugin.Framework.RunOnTick(async () =>
        {
            try
            {
                logger.LogInfo("Starting collection scan");
                lastScanResult = await collectionService.ScanAllCollectionsAsync();

                if (lastScanResult.Success)
                {
                    logger.LogInfo($"Scan completed: {lastScanResult.ItemsScanned} items scanned, {lastScanResult.NewItems} new items");
                    statusMessage = "Scan complete! Refreshing statistics...";
                    await RefreshStatsAsync();
                    statusMessage = $"Scan complete! Found {lastScanResult.NewItems} new items.";
                }
                else
                {
                    logger.LogError($"Scan failed: {lastScanResult.ErrorMessage}");
                    statusMessage = $"Scan failed: {lastScanResult.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Scan failed with exception", ex);
                statusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                isScanning = false;
            }
        });
    }

    private void OnExportButtonClick()
    {
        if (isExporting)
            return;

        isExporting = true;
        exportMessage = "Preparing export...";

        Plugin.Framework.RunOnTick(async () =>
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var exportPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"AkadaemiaAnyder_Export_{timestamp}.json"
                );

                logger.LogInfo($"Exporting to {exportPath}");
                var success = await jsonExporter.ExportAllAsync(exportPath);

                if (success)
                {
                    logger.LogInfo($"Export successful: {exportPath}");
                    exportMessage = $"Export successful! Saved to:\n{exportPath}";
                }
                else
                {
                    logger.LogError("Export failed");
                    exportMessage = "Export failed. Check the logs for details.";
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Export failed with exception", ex);
                exportMessage = $"Error: {ex.Message}";
            }
            finally
            {
                isExporting = false;
            }
        });
    }

    private void OnImportButtonClick()
    {
        if (isImporting)
            return;

        // For now, use a simple file path prompt approach
        // In a full implementation, this would use a file dialog
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var importPath = System.IO.Path.Combine(documentsPath, "AkadaemiaAnyder_Import.json");

        if (!System.IO.File.Exists(importPath))
        {
            importMessage = $"Error: Import file not found.\nPlace your JSON file at:\n{importPath}";
            logger.LogWarning($"Import file not found: {importPath}");
            return;
        }

        isImporting = true;
        importMessage = "Validating import file...";

        Plugin.Framework.RunOnTick(async () =>
        {
            try
            {
                logger.LogInfo($"Importing from {importPath}");

                // Validate first
                var validation = jsonImporter.ValidateFile(importPath);
                if (!validation.valid)
                {
                    logger.LogError($"Import validation failed: {validation.error}");
                    importMessage = $"Validation failed: {validation.error}";
                    isImporting = false;
                    return;
                }

                importMessage = "Importing data...";
                var (success, imported, error) = await jsonImporter.ImportAsync(importPath);

                if (success)
                {
                    logger.LogInfo($"Import successful: {imported} entries imported");
                    importMessage = $"Import successful! Imported {imported} entries.";
                    await RefreshStatsAsync();
                }
                else
                {
                    logger.LogError($"Import failed: {error}");
                    importMessage = $"Import failed: {error}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Import failed with exception", ex);
                importMessage = $"Error: {ex.Message}";
            }
            finally
            {
                isImporting = false;
            }
        });
    }

    private async System.Threading.Tasks.Task RefreshStatsAsync()
    {
        try
        {
            logger.LogDebug("Refreshing statistics");

            // Refresh all stats
            overallStats = await progressCalculator.GetOverallProgress();
            recipeStats = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.Recipe);
            gatheringStats = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.GatheringNode);
            fishingStats = await progressCalculator.GetCollectionProgress(AkadaemiaAnyder.Data.Models.CollectionType.FishingHole);

            logger.LogDebug($"Stats refreshed - Overall: {overallStats.unlocked}/{overallStats.total}");
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to refresh statistics", ex);
        }
    }

    private void RefreshStatsInBackground()
    {
        Plugin.Framework.RunOnTick(async () =>
        {
            await RefreshStatsAsync();
        });
    }
}
