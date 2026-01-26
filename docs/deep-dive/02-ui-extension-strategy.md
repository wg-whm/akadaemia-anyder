# Deep Dive: UI Extension Strategy

**Topic:** How to extend Artisan's ImGui UI with new tabs and windows
**Complexity:** Medium
**Relevance:** Critical for adding Inventory, Collections, and Privacy Settings tabs

---

## Overview

Artisan uses **ImGui** (Immediate Mode GUI) via Dalamud's rendering system. UI is structured as tabbed windows with consistent styling via OtterGui library.

**Key Components:**
- `PluginUI.cs` - Main window with tab navigation
- OtterGui - Custom ImGui controls (tables, color pickers, etc.)
- Theme system - Consistent styling across UI
- Window manager - State persistence and positioning

---

## Tab System Architecture

### Main Window Structure

**File:** `UI/PluginUI.cs` (50 KB)

```csharp
public class PluginUI : Window
{
    private OpenWindow _selectedTab = OpenWindow.Overview;

    public enum OpenWindow
    {
        Overview,
        Settings,
        Endurance,
        Macros,
        CraftingLists,
        Simulator,
        About,
        // NEW tabs go here
    }

    public override void Draw()
    {
        // Left sidebar navigation
        if (ImGui.BeginChild("##LeftNav", new Vector2(150, 0), true))
        {
            if (ImGui.Selectable("Overview", _selectedTab == OpenWindow.Overview))
                _selectedTab = OpenWindow.Overview;

            if (ImGui.Selectable("Settings", _selectedTab == OpenWindow.Settings))
                _selectedTab = OpenWindow.Settings;

            // ... more tabs

            ImGui.EndChild();
        }

        ImGui.SameLine();

        // Right content area
        if (ImGui.BeginChild("##Content", new Vector2(0, 0), false))
        {
            DrawSelectedTab();
            ImGui.EndChild();
        }
    }

    private void DrawSelectedTab()
    {
        switch (_selectedTab)
        {
            case OpenWindow.Overview:
                DrawOverviewTab();
                break;
            case OpenWindow.Settings:
                DrawSettingsTab();
                break;
            // ... more cases
        }
    }
}
```

### Adding New Tabs

**Step 1: Add to enum**
```csharp
public enum OpenWindow
{
    // ... existing tabs
    Inventory,          // NEW
    Collections,        // NEW
    PrivacySettings    // NEW
}
```

**Step 2: Add navigation button**
```csharp
if (ImGui.Selectable("Inventory", _selectedTab == OpenWindow.Inventory))
    _selectedTab = OpenWindow.Inventory;

if (ImGui.Selectable("Collections", _selectedTab == OpenWindow.Collections))
    _selectedTab = OpenWindow.Collections;

if (ImGui.Selectable("Privacy", _selectedTab == OpenWindow.PrivacySettings))
    _selectedTab = OpenWindow.PrivacySettings;
```

**Step 3: Add draw method**
```csharp
private void DrawSelectedTab()
{
    switch (_selectedTab)
    {
        // ... existing cases
        case OpenWindow.Inventory:
            DrawInventoryTab();
            break;
        case OpenWindow.Collections:
            DrawCollectionsTab();
            break;
        case OpenWindow.PrivacySettings:
            DrawPrivacySettingsTab();
            break;
    }
}
```

**Effort:** 30 minutes per new tab

---

## ImGui Layout Patterns

### Artisan's Common UI Patterns

**1. Section Headers with Separators**
```csharp
ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Section Title");
ImGui.Separator();
ImGui.Spacing();
```

**2. Labeled Values with Alignment**
```csharp
ImGui.Text("Progress:");
ImGui.SameLine(150);  // Align value at column 150
ImGui.Text($"{progress}/{maxProgress}");
```

**3. Progress Bars**
```csharp
float percentage = (float)progress / maxProgress;
ImGui.ProgressBar(percentage, new Vector2(300, 20));
```

**4. Tables with OtterGui**
```csharp
using var table = ImRaii.Table("##table", 3, ImGuiTableFlags.Borders);
if (!table) return;

ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed, 200);
ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 100);
ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);
ImGui.TableHeadersRow();

foreach (var item in items)
{
    ImGui.TableNextRow();
    ImGui.TableNextColumn();
    ImGui.Text(item.Name);
    ImGui.TableNextColumn();
    ImGui.Text(item.Quantity.ToString());
    ImGui.TableNextColumn();
    ImGui.Text(item.Status);
}
```

**5. Tooltips on Hover**
```csharp
ImGui.Text("Material Name");
if (ImGui.IsItemHovered())
{
    ImGui.SetTooltip("Additional details here\nSecond line of tooltip");
}
```

**6. Collapsible Trees**
```csharp
if (ImGui.TreeNode($"Details##{itemId}"))
{
    ImGui.Indent();
    ImGui.Text("Nested content here");
    ImGui.Unindent();
    ImGui.TreePop();
}
```

---

## Example: Complete Inventory Tab Implementation

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.UI
{
    public partial class PluginUI
    {
        private string _inventorySearchQuery = "";
        private List<MaterialSearchResult> _inventorySearchResults = new();

        private void DrawInventoryTab()
        {
            // Header
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Universal Inventory Search");
            ImGui.Separator();
            ImGui.Spacing();

            // Search bar
            ImGui.Text("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(400);
            bool enterPressed = ImGui.InputText(
                "##search",
                ref _inventorySearchQuery,
                256,
                ImGuiInputTextFlags.EnterReturnsTrue
            );

            ImGui.SameLine();
            bool searchClicked = ImGui.Button("Search");

            if (enterPressed || searchClicked)
            {
                PerformInventorySearch();
            }

            ImGui.SameLine();
            if (ImGui.Button("Refresh Snapshot"))
            {
                _repository.RefreshInventorySnapshot();
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "Refreshed!");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Results section
            if (_inventorySearchResults.Any())
            {
                DrawSearchResults();
            }
            else if (!string.IsNullOrEmpty(_inventorySearchQuery))
            {
                ImGui.TextDisabled("No results found");
            }
            else
            {
                ImGui.TextDisabled("Enter an item name to search across all storage");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Storage summary dashboard
            DrawStorageSummary();
        }

        private void PerformInventorySearch()
        {
            _inventorySearchResults.Clear();

            // Find matching items via game data provider
            var matchingItems = FindItemsByNameFuzzy(_inventorySearchQuery);

            foreach (var itemId in matchingItems)
            {
                var locations = _repository.FindMaterialLocations(itemId);
                if (locations.Any())
                {
                    _inventorySearchResults.Add(new MaterialSearchResult
                    {
                        ItemId = itemId,
                        ItemName = _gameData.GetItemName(itemId),
                        Locations = locations
                    });
                }
            }
        }

        private void DrawSearchResults()
        {
            ImGui.Text($"Found {_inventorySearchResults.Count} item(s):");
            ImGui.Spacing();

            foreach (var result in _inventorySearchResults)
            {
                // Item header
                if (ImGui.TreeNodeEx($"{result.ItemName}##{result.ItemId}", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var totalQuantity = result.Locations.Sum(l => l.Quantity);
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), $"  Total: {totalQuantity}");

                    // Location breakdown
                    using (var table = ImRaii.Table($"##locs_{result.ItemId}", 3))
                    {
                        if (table)
                        {
                            ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed, 150);
                            ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 80);
                            ImGui.TableHeadersRow();

                            foreach (var loc in result.Locations)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{GetLocationIcon(loc.Location)} {loc.Location}");
                                ImGui.TableNextColumn();
                                ImGui.Text(loc.Quantity.ToString());
                                ImGui.TableNextColumn();
                                ImGui.Text($"Slot {loc.SlotId}");
                            }
                        }
                    }

                    ImGui.TreePop();
                }
            }
        }

        private void DrawStorageSummary()
        {
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Storage Overview");
            ImGui.Separator();
            ImGui.Spacing();

            // Get storage stats from repository
            var storageStats = _repository.GetStorageStatistics();

            DrawStorageBar("Inventory", storageStats.InventoryUsed, 140);
            DrawStorageBar("Saddlebag", storageStats.SaddlebagUsed, 70);
            DrawStorageBar("Glamour Dresser", storageStats.GlamourDresserUsed, 400);

            for (int i = 1; i <= storageStats.RetainerCount; i++)
            {
                var used = storageStats.GetRetainerUsed(i);
                if (used > 0)
                {
                    DrawStorageBar($"Retainer {i}", used, 175);
                }
            }
        }

        private void DrawStorageBar(string name, int used, int capacity)
        {
            float percentage = capacity > 0 ? (float)used / capacity : 0;

            var color = percentage > 0.9f ? new Vector4(1, 0, 0, 1) :      // Red
                       percentage > 0.75f ? new Vector4(1, 1, 0, 1) :      // Yellow
                       new Vector4(0.5f, 1f, 0.5f, 1f);                     // Green

            ImGui.Text($"{name}:");
            ImGui.SameLine(150);
            ImGui.Text($"{used}/{capacity}");
            ImGui.SameLine(250);
            ImGui.ProgressBar(percentage, new Vector2(300, 20));

            if (percentage > 0.9f)
            {
                ImGui.SameLine();
                ImGui.TextColored(color, "⚠ NEARLY FULL");
            }
        }

        private string GetLocationIcon(string location)
        {
            return location switch
            {
                "inventory" => "🎒",
                "saddlebag" => "🐤",
                "armory" => "⚔",
                "glamour" => "👗",
                _ when location.StartsWith("retainer") => "👤",
                _ => "📦"
            };
        }
    }

    public class MaterialSearchResult
    {
        public uint ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public List<MaterialLocation> Locations { get; set; } = new();
    }
}
```

---

## OtterGui Custom Controls

### Table Helper

```csharp
using OtterGui.Raii;

// Creating tables with automatic cleanup
using var table = ImRaii.Table("##myTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
if (!table) return;  // Failed to create table

ImGui.TableSetupColumn("Column 1");
ImGui.TableSetupColumn("Column 2");
ImGui.TableSetupColumn("Column 3");
ImGui.TableHeadersRow();

// Rows auto-cleaned up when 'using' scope exits
```

### Color Picker

```csharp
using OtterGui.Widgets;

var color = new Vector4(1, 0, 0, 1);
if (ColorPickerButton.Draw("##colorPicker", ref color))
{
    // Color changed
    Configuration.ThemeColor = color;
}
```

---

## Theme System

### Applying Consistent Styling

```csharp
private void ApplyTheme()
{
    // Push Artisan's theme colors
    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.4f, 0.6f, 1f));
    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.5f, 0.7f, 1f));
    ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.4f, 0.6f, 0.8f, 1f));

    // Push spacing
    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 3));
}

private void RemoveTheme()
{
    ImGui.PopStyleColor(3);  // Pop 3 colors
    ImGui.PopStyleVar(2);    // Pop 2 style vars
}

public override void Draw()
{
    ApplyTheme();
    try
    {
        // Draw UI content
    }
    finally
    {
        RemoveTheme();  // Always clean up
    }
}
```

---

## State Persistence

### Window Position/Size

```csharp
public class PluginUI : Window
{
    private Vector2 _lastWindowSize;
    private Vector2 _lastWindowPos;

    public PluginUI() : base("Akadaemia Anyder##MainWindow")
    {
        // Load saved position from configuration
        if (Configuration.WindowSize != Vector2.Zero)
        {
            this.Size = Configuration.WindowSize;
            this.Position = Configuration.WindowPosition;
        }
        else
        {
            // Default size
            this.Size = new Vector2(900, 600);
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 400),
                MaximumSize = new Vector2(9999, 9999)
            };
        }
    }

    public override void OnClose()
    {
        // Save window state
        Configuration.WindowSize = ImGui.GetWindowSize();
        Configuration.WindowPosition = ImGui.GetWindowPos();
        Configuration.Save();
    }
}
```

### Tab Selection Persistence

```csharp
public class Configuration
{
    public OpenWindow LastSelectedTab { get; set; } = OpenWindow.Overview;
}

// In PluginUI:
public PluginUI()
{
    _selectedTab = Configuration.LastSelectedTab;
}

private void OnTabChange(OpenWindow newTab)
{
    _selectedTab = newTab;
    Configuration.LastSelectedTab = newTab;
    Configuration.Save();
}
```

---

## Performance Considerations

### Rendering Optimization

**Problem:** Drawing 1000+ items in a table causes lag

**Solution 1: Clipping (Built-in)**
```csharp
// ImGui automatically clips off-screen items
// Just wrap in scrollable region
if (ImGui.BeginChild("##ScrollableList", new Vector2(0, 400), true))
{
    foreach (var item in items)  // ImGui only renders visible items
    {
        ImGui.Text(item.Name);
    }
    ImGui.EndChild();
}
```

**Solution 2: Virtual Scrolling (Manual)**
```csharp
private void DrawVirtualList(List<Item> items)
{
    const int itemHeight = 20;
    float scrollY = ImGui.GetScrollY();
    int firstVisible = (int)(scrollY / itemHeight);
    int visibleCount = (int)(ImGui.GetWindowHeight() / itemHeight) + 2;

    // Set dummy height for full scroll range
    ImGui.Dummy(new Vector2(0, items.Count * itemHeight));

    // Only draw visible items
    for (int i = firstVisible; i < Math.Min(firstVisible + visibleCount, items.Count); i++)
    {
        ImGui.SetCursorPosY(i * itemHeight);
        ImGui.Text(items[i].Name);
    }
}
```

**Solution 3: Pagination**
```csharp
private int _currentPage = 0;
private const int ItemsPerPage = 50;

private void DrawPaginatedList(List<Item> items)
{
    var page = items.Skip(_currentPage * ItemsPerPage).Take(ItemsPerPage);

    foreach (var item in page)
    {
        ImGui.Text(item.Name);
    }

    ImGui.Separator();

    if (ImGui.Button("< Previous") && _currentPage > 0)
        _currentPage--;

    ImGui.SameLine();
    ImGui.Text($"Page {_currentPage + 1} / {(items.Count - 1) / ItemsPerPage + 1}");

    ImGui.SameLine();
    if (ImGui.Button("Next >") && (_currentPage + 1) * ItemsPerPage < items.Count)
        _currentPage++;
}
```

---

## Recommendations for New Tabs

### Inventory Tab Design

**Layout:**
```
┌──────────────────────────────────────────────────────────┐
│ Universal Inventory Search                               │
├──────────────────────────────────────────────────────────┤
│ Search: [________________]  [Search]  [Refresh]          │
│                                                           │
│ Results (2 items):                                        │
│   ▼ Titanium Ore (Total: 47)                            │
│     🐤 Saddlebag: 8 (Slot 12)                           │
│     👤 Retainer 1: 18 (Slot 42)                          │
│     👤 Retainer 2: 21 (Slot 67)                          │
│                                                           │
├──────────────────────────────────────────────────────────┤
│ Storage Overview                                          │
│   Inventory:        ████████░░ 89/140                    │
│   Saddlebag:        ████████░░ 45/70                     │
│   Glamour Dresser:  █████████░ 387/400  ⚠ NEARLY FULL   │
│   Retainer 1:       ██████████ 175/175  ⚠ FULL          │
└──────────────────────────────────────────────────────────┘
```

### Collections Tab Design

**Layout:**
```
┌──────────────────────────────────────────────────────────┐
│ Collection Progress                                       │
├──────────────────────────────────────────────────────────┤
│ Mounts:            278/487 (57.1%)  ████████░░░░░░       │
│ Minions:           412/584 (70.5%)  ██████████░░░        │
│ Triple Triad:      215/465 (46.2%)  ██████░░░░░░░░       │
│ Orchestrion:       124/512 (24.2%)  ███░░░░░░░░░░░       │
│ Emotes:            88/180 (48.9%)   ██████░░░░░░░░       │
│ Hairstyles:        42/95 (44.2%)    █████░░░░░░░░░       │
│ Bardings:          18/62 (29.0%)    ███░░░░░░░░░░░       │
│ Blue Mage:         58/104 (55.8%)   ███████░░░░░░        │
└──────────────────────────────────────────────────────────┘
```

### Privacy Settings Tab Design

**Layout:**
```
┌──────────────────────────────────────────────────────────┐
│ Privacy-First Design                                      │
├──────────────────────────────────────────────────────────┤
│ ✓ All data stored locally on your computer               │
│ ✓ No user IDs or tracking                                │
│ ✓ No network requests                                     │
│                                                           │
│ Optional Data Storage:                                    │
│ [ ] Store character names (disabled for privacy)         │
│ [ ] Store server names (disabled for privacy)            │
│                                                           │
│ Export Options:                                           │
│ [✓] Anonymize exports by default                         │
│                                                           │
│ [Export to JSON]  [Export to CSV]  [Delete All Data]     │
└──────────────────────────────────────────────────────────┘
```

---

**End of UI Extension Strategy Deep Dive**

Key takeaway: ImGui tab system is straightforward to extend. Add enum value, navigation button, draw method. Use OtterGui for tables, follow Artisan's styling patterns.
