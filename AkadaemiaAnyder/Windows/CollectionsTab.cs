using System;
using System.Numerics;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using SamplePlugin.Services;

namespace SamplePlugin.Windows
{
    /// <summary>
    /// Collections tab showing progress for 8 collection types.
    /// Displays collection progress with progress bars for:
    /// - Mounts
    /// - Minions
    /// - Triple Triad Cards
    /// - Orchestrion Rolls
    /// - Emotes
    /// - Hairstyles
    /// - Bardings
    /// - Blue Mage Spells
    /// </summary>
    public class CollectionsTab
    {
        private readonly ProgressCalculator _progressCalculator;
        private readonly LoggingService _logger;

        // Collection type data (8 types total)
        private readonly (string Label, string Description)[] _collections =
        {
            ("Mounts", "Ride in style across Eorzea"),
            ("Minions", "Companion creatures to follow you around"),
            ("Triple Triad Cards", "Collectible trading cards"),
            ("Orchestrion Rolls", "Music recordings for your home"),
            ("Emotes", "Expressive character animations"),
            ("Hairstyles", "Unique haircut styles"),
            ("Bardings", "Chocobo armor and decorations"),
            ("Blue Mage Spells", "Special job ability learn list")
        };

        public CollectionsTab(ProgressCalculator progressCalculator, LoggingService logger)
        {
            _progressCalculator = progressCalculator ?? throw new ArgumentNullException(nameof(progressCalculator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Renders the Collections tab UI
        /// </summary>
        public void Draw()
        {
            // Check if we should draw the tab
            using (var tab = ImRaii.TabItem("Collections"))
            {
                if (!tab.Success)
                    return;

                ImGui.Spacing();

                // Section header
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Collection Progress");
                ImGui.Separator();
                ImGui.Spacing();

                // Draw the 8 collection progress rows
                DrawProgressRow("Mounts", 47, 47);           // Mock: 47/47 = 100%
                DrawProgressRow("Minions", 138, 238);        // Mock: 138/238 = 58%
                DrawProgressRow("Triple Triad Cards", 289, 308);  // Mock: 289/308 = 94%
                DrawProgressRow("Orchestrion Rolls", 256, 401);   // Mock: 256/401 = 64%
                DrawProgressRow("Emotes", 88, 113);          // Mock: 88/113 = 78%
                DrawProgressRow("Hairstyles", 142, 182);     // Mock: 142/182 = 78%
                DrawProgressRow("Bardings", 31, 104);        // Mock: 31/104 = 30%
                DrawProgressRow("Blue Mage Spells", 26, 120);   // Mock: 26/120 = 22%

                ImGui.Spacing();
                ImGui.Spacing();

                // Info section
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "About Collections");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextWrapped("Collection tracking will show your progress across various collection types. " +
                    "Currently showing placeholder data. Use the Scan feature to update with your actual collections.");
            }
        }

        /// <summary>
        /// Draws a single progress row with label, count, and progress bar
        /// </summary>
        private void DrawProgressRow(string label, int unlocked, int total)
        {
            // Calculate percentage
            double percentage = total > 0 ? (unlocked / (double)total) * 100.0 : 0.0;

            // Label
            ImGui.Text($"{label}:");
            ImGui.SameLine(200);

            // Count (unlocked/total)
            ImGui.Text($"{unlocked}/{total}");
            ImGui.SameLine(300);

            // Progress bar with color coding
            Vector4 barColor = GetProgressBarColor(percentage);
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);

            ImGui.ProgressBar((float)(percentage / 100.0), new Vector2(-1, 0), $"{percentage:F1}%");

            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        /// <summary>
        /// Returns color for progress bar based on completion percentage
        /// Green: 100%, Yellow: 50%+, Red: below 50%
        /// </summary>
        private Vector4 GetProgressBarColor(double percentage)
        {
            if (percentage >= 100.0)
                return new Vector4(0, 1, 0, 1);      // Green - Complete
            else if (percentage >= 50.0)
                return new Vector4(1, 1, 0, 1);      // Yellow - Half done
            else
                return new Vector4(1, 0, 0, 1);      // Red - Need progress
        }
    }
}
