using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using SamplePlugin.Services;

namespace SamplePlugin.Windows;

/// <summary>
/// Privacy settings tab for controlling PII storage and export anonymization.
/// Implements security-critical privacy controls with privacy-first defaults.
/// </summary>
public class PrivacySettingsTab
{
    private readonly Configuration configuration;
    private readonly JsonExporter jsonExporter;
    private readonly LoggingService logger;

    // Export operation state
    private bool isExportingJson = false;
    private bool isExportingCsv = false;
    private string? lastExportStatus = null;
    private DateTime? lastExportTime = null;

    // Modal popup state
    private bool shouldOpenWarningPopup = false;
    private bool pendingCharacterNameState = false;

    public PrivacySettingsTab(Configuration config, JsonExporter jsonExporter, LoggingService logger)
    {
        this.configuration = config;
        this.jsonExporter = jsonExporter;
        this.logger = logger;
    }

    public void Draw()
    {
        DrawPrivacyHeader();
        ImGui.Spacing();
        ImGui.Spacing();

        DrawCharacterNameSetting();
        ImGui.Spacing();
        ImGui.Spacing();

        DrawAnonymousExportSetting();
        ImGui.Spacing();
        ImGui.Spacing();

        DrawExportSection();
        ImGui.Spacing();
        ImGui.Spacing();

        DrawExportStatus();

        // Handle modal popup outside of main drawing logic
        HandleWarningPopup();
    }

    private void DrawPrivacyHeader()
    {
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Privacy Settings");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(
            "Akadaemia Anyder is designed with privacy-first principles. " +
            "By default, character names are NOT stored, and all exports are anonymized. " +
            "Only enable character name storage if you understand the privacy implications."
        );
    }

    private void DrawCharacterNameSetting()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.9f, 0.4f, 1.0f), "Character Name Storage");
        ImGui.Spacing();

        ImGui.TextWrapped(
            "When DISABLED (default): Character names are never stored in the database. " +
            "All records use only character IDs for privacy protection."
        );
        ImGui.Spacing();

        ImGui.TextWrapped(
            "When ENABLED: Character names will be stored alongside records. " +
            "This allows easier identification but reduces privacy. " +
            "Use only if you understand the implications."
        );
        ImGui.Spacing();

        var storeNames = configuration.PrivacySettings.StoreCharacterNames;
        if (ImGui.Checkbox("Store Character Names", ref storeNames))
        {
            // If turning ON, show warning popup
            if (storeNames && !configuration.PrivacySettings.StoreCharacterNames)
            {
                pendingCharacterNameState = true;
                shouldOpenWarningPopup = true;
            }
            // If turning OFF, apply immediately
            else if (!storeNames)
            {
                configuration.PrivacySettings.StoreCharacterNames = false;
                configuration.Save();
                logger.LogInfo("Character name storage disabled");
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Controls whether character names are stored in the database.\n" +
                "Default: OFF (privacy-first)"
            );
        }
    }

    private void DrawAnonymousExportSetting()
    {
        ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.8f, 1.0f), "Anonymous Export");
        ImGui.Spacing();

        ImGui.TextWrapped(
            "When ENABLED (default): Exports replace character names with anonymized IDs " +
            "like 'Character_001'. This protects privacy when sharing data."
        );
        ImGui.Spacing();

        ImGui.TextWrapped(
            "When DISABLED: Exports include actual character names if stored. " +
            "Only disable if you need real names and understand the privacy implications."
        );
        ImGui.Spacing();

        var enableAnonymous = configuration.PrivacySettings.EnableAnonymousExport;
        if (ImGui.Checkbox("Enable Anonymous Export", ref enableAnonymous))
        {
            configuration.PrivacySettings.EnableAnonymousExport = enableAnonymous;
            configuration.Save();
            logger.LogInfo($"Anonymous export {(enableAnonymous ? "enabled" : "disabled")}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Controls whether exports are anonymized.\n" +
                "Default: ON (privacy-first)"
            );
        }
    }

    private void DrawExportSection()
    {
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Export Data");
        ImGui.Separator();
        ImGui.Spacing();

        var anonymizationNote = configuration.PrivacySettings.EnableAnonymousExport
            ? "Exports will be anonymized (character names replaced with IDs)"
            : "Exports will include character names if stored";

        ImGui.TextWrapped(anonymizationNote);
        ImGui.Spacing();

        // JSON Export Button
        var exportDisabled = isExportingJson || isExportingCsv;
        if (exportDisabled)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        }

        if (ImGui.Button("Export to JSON", new Vector2(200, 30)))
        {
            if (!isExportingJson && !isExportingCsv)
            {
                OnExportJsonButtonClick();
            }
        }

        if (exportDisabled)
        {
            ImGui.PopStyleVar();
        }

        if (ImGui.IsItemHovered())
        {
            var tooltip = isExportingJson ? "Export in progress..."
                : isExportingCsv ? "CSV export in progress..."
                : "Export all data to JSON format";
            ImGui.SetTooltip(tooltip);
        }

        ImGui.SameLine();

        // CSV Export Button (placeholder for future implementation)
        if (exportDisabled)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        }

        if (ImGui.Button("Export to CSV", new Vector2(200, 30)))
        {
            if (!isExportingJson && !isExportingCsv)
            {
                OnExportCsvButtonClick();
            }
        }

        if (exportDisabled)
        {
            ImGui.PopStyleVar();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("CSV export (coming soon)");
        }
    }

    private void DrawExportStatus()
    {
        if (lastExportStatus == null)
            return;

        ImGui.Separator();
        ImGui.Spacing();

        var isError = lastExportStatus.Contains("failed", StringComparison.OrdinalIgnoreCase)
                      || lastExportStatus.Contains("error", StringComparison.OrdinalIgnoreCase);

        var statusColor = isError
            ? new Vector4(1.0f, 0.4f, 0.4f, 1.0f) // Red for errors
            : new Vector4(0.4f, 1.0f, 0.4f, 1.0f); // Green for success

        ImGui.TextColored(statusColor, "Last Export:");
        ImGui.SameLine();
        ImGui.Text(lastExportStatus);

        if (lastExportTime.HasValue)
        {
            ImGui.Text($"Time: {lastExportTime.Value:yyyy-MM-dd HH:mm:ss}");
        }
    }

    private void HandleWarningPopup()
    {
        // Open popup if flagged
        if (shouldOpenWarningPopup)
        {
            ImGui.OpenPopup("Character Name Warning");
            shouldOpenWarningPopup = false;
        }

        // Draw popup modal
        if (ImGui.BeginPopupModal("Character Name Warning", ref shouldOpenWarningPopup,
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.2f, 1.0f), "Privacy Warning");
            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextWrapped(
                "You are about to enable character name storage.\n\n" +
                "This means:\n" +
                "- Character names will be stored in the database\n" +
                "- Names may be included in exports\n" +
                "- This reduces privacy protection\n\n" +
                "Only enable this if you understand the privacy implications."
            );

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Confirm button
            if (ImGui.Button("I Understand - Enable", new Vector2(200, 30)))
            {
                configuration.PrivacySettings.StoreCharacterNames = true;
                configuration.Save();
                logger.LogWarning("Character name storage ENABLED by user");
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            // Cancel button
            if (ImGui.Button("Cancel", new Vector2(100, 30)))
            {
                // Don't change setting
                logger.LogInfo("Character name storage enable cancelled");
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void OnExportJsonButtonClick()
    {
        isExportingJson = true;
        lastExportStatus = "Exporting...";

        Task.Run(async () =>
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"akadaemia_export_{timestamp}.json";
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    filename
                );

                // Note: Anonymization is handled internally by JsonExporter based on Configuration
                await jsonExporter.ExportAllAsync(path);

                lastExportStatus = $"Export successful: {filename}";
                lastExportTime = DateTime.Now;
                logger.LogInfo($"JSON export completed: {path}");
            }
            catch (Exception ex)
            {
                lastExportStatus = $"Export failed: {ex.Message}";
                lastExportTime = DateTime.Now;
                logger.LogError($"JSON export failed: {ex}");
            }
            finally
            {
                isExportingJson = false;
            }
        });
    }

    private void OnExportCsvButtonClick()
    {
        // Placeholder for future CSV export implementation
        lastExportStatus = "CSV export not yet implemented";
        lastExportTime = DateTime.Now;
        logger.LogInfo("CSV export requested (not implemented)");
    }
}
