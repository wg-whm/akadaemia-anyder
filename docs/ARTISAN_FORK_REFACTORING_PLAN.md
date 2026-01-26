# Artisan Fork: Detailed Refactoring Plan

**Created:** 2026-01-26
**Target:** Akadaemia Anyder integration
**Approach:** Shallow integration (copy + modify)
**Estimated Total Effort:** 18-22 hours

---

## Table of Contents

1. [Phase 0: Pre-Fork Preparation](#phase-0-pre-fork-preparation)
2. [Phase 1: Privacy Code Removal](#phase-1-privacy-code-removal)
3. [Phase 2: Namespace Refactoring](#phase-2-namespace-refactoring)
4. [Phase 3: Abstraction Layer Design](#phase-3-abstraction-layer-design)
5. [Phase 4: Database Integration](#phase-4-database-integration)
6. [Phase 5: UI Extensions](#phase-5-ui-extensions)
7. [Phase 6: Testing & Validation](#phase-6-testing--validation)
8. [Migration Checklist](#migration-checklist)

---

## Phase 0: Pre-Fork Preparation

**Duration:** 1-2 hours
**Goal:** Set up fork infrastructure and backup original code

### Step 0.1: Fork Repository Structure

```bash
cd C:\Code
mkdir artisan-fork-backup
cd artisan-fork-backup

# Clone Artisan repository
git clone https://github.com/PunishXIV/Artisan.git
cd Artisan

# Create backup branch
git checkout -b akadaemia-anyder-fork
git tag artisan-original-20260126

# Create analysis branch for testing
git checkout -b analysis-phase
```

### Step 0.2: Document Current State

```bash
# Generate file inventory
Get-ChildItem -Recurse -File | Where-Object { $_.Extension -eq ".cs" } |
  Select-Object FullName, Length |
  Export-Csv C:\Code\akadaemia-anyder\docs\artisan-original-files.csv

# Count lines of code
(Get-ChildItem -Recurse -File -Filter "*.cs" | Get-Content | Measure-Object -Line).Lines
# Expected: ~25,000-30,000 lines

# Generate dependency tree
dotnet list package --include-transitive > C:\Code\akadaemia-anyder\docs\artisan-dependencies.txt
```

### Step 0.3: Verify Build

```bash
# Build original Artisan to verify environment
dotnet build Artisan.csproj

# Expected output: Build succeeded (with warnings OK)
# Should generate: bin/Debug/Artisan.dll
```

**✅ Validation Criteria:**
- [ ] Repository cloned successfully
- [ ] Backup branch created
- [ ] Original code builds without errors
- [ ] File inventory generated

---

## Phase 1: Privacy Code Removal

**Duration:** 4 hours
**Goal:** Remove all network-dependent and privacy-sensitive code

### Step 1.1: Remove Universalis Module

**Files to DELETE:**
```
Universalis/
├── UniversalisClient.cs           # DELETE
├── DataCenters.cs                 # DELETE
└── MarketboardData.cs             # DELETE
```

**Files to MODIFY:**

#### `UI/RecipeWindowUI.cs` (Line ~500-600)
```csharp
// BEFORE:
if (Configuration.UseUniversalis) {
    var pricing = await UniversalisClient.GetMarketBoardDataAsync(itemId);
    ImGui.Text($"Market Price: {pricing.CurrentMinimumPrice:N0} gil");
}

// AFTER:
// Removed Universalis pricing
ImGui.TextDisabled("Market pricing disabled (privacy mode)");
// TODO: Add local inventory check here in Phase 4
```

#### `UI/ListEditor.cs` (Line ~800-900)
```csharp
// BEFORE:
if (Configuration.UseUniversalis) {
    totalCost += await UniversalisClient.GetMarketBoardDataAsync(ingredient.ItemId);
}

// AFTER:
// Removed market cost estimation
ImGui.TextDisabled("Cost estimation disabled (privacy mode)");
// TODO: Add local material availability in Phase 4
```

#### `Configuration.cs` (Lines to DELETE)
```csharp
// DELETE these properties:
public bool UseUniversalis { get; set; }
public bool UseSolverEstimates { get; set; }

// DELETE from Save() method:
// Any serialization of Universalis settings
```

**Commands:**
```bash
# Delete Universalis directory
rm -rf Universalis/

# Remove Universalis NuGet package references
dotnet remove package Universalis  # If exists as separate package
```

### Step 1.2: Remove Discord Webhook Integration

**Files to MODIFY:**

#### `Configuration.cs`
```csharp
// DELETE these properties (lines ~50-60):
public bool UsingDiscordHooks { get; set; }
public string? DiscordWebhookUrl { get; set; }

// SEARCH for usage of these properties and remove:
# Likely in notification systems or endurance mode completion events
```

**Search for webhook usage:**
```bash
# Find all references to Discord
grep -r "Discord" --include="*.cs" .

# Expected files:
# - Configuration.cs (remove config properties)
# - Possibly Autocraft/Endurance.cs (remove completion webhooks)
```

#### `Artisan.csproj`
```xml
<!-- REMOVE this package reference -->
<PackageReference Include="Discord.Net.Webhook" Version="3.15.3" />
```

**Commands:**
```bash
# Remove Discord package
dotnet remove package Discord.Net.Webhook
```

### Step 1.3: Remove Teamcraft Integration

**Files to DELETE:**
```
CraftingList/Teamcraft.cs          # DELETE entirely
```

**Files to MODIFY:**

#### `CraftingList/CraftingListUI.cs`
```csharp
// BEFORE:
if (ImGui.Button("Import from Teamcraft")) {
    Teamcraft.ImportList(clipboardText);
}

if (ImGui.Button("Export to Teamcraft")) {
    Teamcraft.ExportList(currentList);
}

// AFTER:
// Removed Teamcraft import/export buttons
// TODO: Add local JSON import/export in Phase 4
```

**Commands:**
```bash
# Delete Teamcraft file
rm CraftingList/Teamcraft.cs
```

### Step 1.4: Verify No External Network Calls Remain

**Search for HTTP usage:**
```bash
# Find all HttpClient usages
grep -r "HttpClient" --include="*.cs" .
grep -r "WebRequest" --include="*.cs" .
grep -r "WebSocket" --include="*.cs" .
grep -r "api\." --include="*.cs" .

# Expected: Zero results after removals
```

**Manual code review checklist:**
- [ ] No `using System.Net.Http;` statements
- [ ] No `HttpClient` instantiations
- [ ] No `WebRequest` or `WebClient` usage
- [ ] No external API endpoints in code
- [ ] No Discord, Universalis, or Teamcraft references

**✅ Validation Criteria:**
- [ ] Universalis module deleted
- [ ] Discord webhook code removed
- [ ] Teamcraft integration removed
- [ ] Zero network calls detected (grep verification)
- [ ] Project still builds (warnings OK, no errors)

---

## Phase 2: Namespace Refactoring

**Duration:** 3-4 hours
**Goal:** Convert all `Artisan.*` namespaces to `AkadaemiaAnyder.Modules.Artisan.*`

### Step 2.1: Generate File List for Refactoring

```powershell
# Generate list of all .cs files needing namespace changes
Get-ChildItem -Recurse -File -Filter "*.cs" |
  Select-String -Pattern "^namespace Artisan" |
  Select-Object -ExpandProperty Path -Unique |
  Out-File C:\Code\akadaemia-anyder\docs\artisan-files-to-refactor.txt

# Expected: ~100+ files
```

### Step 2.2: Automated Namespace Replacement

**Using PowerShell:**
```powershell
# Define replacement patterns
$files = Get-ChildItem -Recurse -File -Filter "*.cs"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw

    # Replace namespace declarations
    $content = $content -replace "^namespace Artisan", "namespace AkadaemiaAnyder.Modules.Artisan"
    $content = $content -replace "namespace Artisan\.", "namespace AkadaemiaAnyder.Modules.Artisan."

    # Replace using statements
    $content = $content -replace "using Artisan\.", "using AkadaemiaAnyder.Modules.Artisan."
    $content = $content -replace "using static Artisan\.", "using static AkadaemiaAnyder.Modules.Artisan."

    # Save changes
    Set-Content -Path $file.FullName -Value $content
}

Write-Host "Namespace refactoring complete. Verify with git diff."
```

**Using Visual Studio:**
```
1. Open solution in Visual Studio
2. Edit > Find and Replace > Replace in Files
3. Find what: namespace Artisan\.
   Replace with: namespace AkadaemiaAnyder.Modules.Artisan.
   Look in: Entire Solution
   Match case: Yes
   Use regular expressions: Yes
4. Click "Replace All"
5. Repeat for "using Artisan\." and "using static Artisan\."
```

### Step 2.3: Update Project File

**`Artisan.csproj` → `AkadaemiaAnyder.Modules.Artisan.csproj`**

```xml
<!-- BEFORE -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Artisan</AssemblyName>
    <RootNamespace>Artisan</RootNamespace>
  </PropertyGroup>
</Project>

<!-- AFTER -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>AkadaemiaAnyder.Modules.Artisan</AssemblyName>
    <RootNamespace>AkadaemiaAnyder.Modules.Artisan</RootNamespace>
  </PropertyGroup>
</Project>
```

### Step 2.4: Rename Main Plugin File

```bash
# Rename Artisan.cs → ArtisanPlugin.cs
mv Artisan.cs ArtisanPlugin.cs
```

**Update class name in `ArtisanPlugin.cs`:**
```csharp
// BEFORE:
public sealed class Artisan : IDalamudPlugin

// AFTER:
public sealed class ArtisanPlugin : IDalamudPlugin
```

### Step 2.5: Update All References to Main Plugin Class

**Files likely to reference `Artisan` class:**
- `Configuration.cs` - Remove circular references if any
- `UI/PluginUI.cs` - May have `Artisan.Instance` references
- `IPC/IPC.cs` - May register plugin name

**Search and replace:**
```powershell
# Find all references to "Artisan.Artisan"
Get-ChildItem -Recurse -File -Filter "*.cs" |
  Select-String -Pattern "Artisan\.Artisan" |
  Select-Object -ExpandProperty Path -Unique

# Replace with ArtisanPlugin
# Manual review recommended for these cases
```

### Step 2.6: Verify Build After Refactoring

```bash
# Rebuild project
dotnet clean
dotnet build AkadaemiaAnyder.Modules.Artisan.csproj

# Expected: Build succeeded
# Common errors:
# - CS0246: Type or namespace name not found (missed namespace update)
# - CS0234: Namespace does not exist (using statement not updated)
```

**✅ Validation Criteria:**
- [ ] All namespace declarations updated to `AkadaemiaAnyder.Modules.Artisan.*`
- [ ] All using statements updated
- [ ] Project file updated (AssemblyName, RootNamespace)
- [ ] Main plugin class renamed to `ArtisanPlugin`
- [ ] Project builds without errors
- [ ] Git diff shows consistent namespace changes

---

## Phase 3: Abstraction Layer Design

**Duration:** 6 hours
**Goal:** Create interfaces to decouple game data and database access

### Step 3.1: Create Core Infrastructure Directory

```bash
# Create new module directory structure
mkdir -p Modules/Core
cd Modules/Core
```

### Step 3.2: Design IGameDataProvider Interface

**File:** `Modules/Core/IGameDataProvider.cs`

```csharp
using Lumina.Excel.GeneratedSheets;

namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Abstraction layer for game data access (Lumina, FFXIVClientStructs)
    /// </summary>
    public interface IGameDataProvider
    {
        // Item data
        Item? GetItem(uint itemId);
        string GetItemName(uint itemId);
        uint GetItemIcon(uint itemId);

        // Recipe data
        Recipe? GetRecipe(uint recipeId);
        List<Recipe> GetRecipesByItem(uint itemId);
        List<IngredientInfo> GetRecipeIngredients(uint recipeId);

        // Character state
        uint GetCurrentJob();
        string GetCharacterName();
        uint GetWorldId();
        string GetDataCenter();

        // Inventory queries
        List<InventoryItem> GetCharacterInventory();
        List<InventoryItem> GetArmoryChest();
        int GetItemCount(uint itemId, bool includeHQ = false);

        // Consumables
        List<Item> GetFoodItems();
        List<Item> GetPotionItems();
        List<Item> GetManualItems();

        // Crafting state
        bool IsCrafting();
        CraftingState? GetCurrentCraftingState();
    }

    public class IngredientInfo
    {
        public uint ItemId { get; set; }
        public int Quantity { get; set; }
        public bool IsHQ { get; set; }
    }

    public class InventoryItem
    {
        public uint ItemId { get; set; }
        public int Quantity { get; set; }
        public bool IsHQ { get; set; }
        public int Spiritbond { get; set; }
        public int Condition { get; set; }
    }
}
```

### Step 3.3: Implement DefaultGameDataProvider

**File:** `Modules/Core/DefaultGameDataProvider.cs`

```csharp
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;

namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Default implementation using Dalamud services
    /// </summary>
    public class DefaultGameDataProvider : IGameDataProvider
    {
        private readonly IDataManager _dataManager;
        private readonly IClientState _clientState;

        public DefaultGameDataProvider(IDataManager dataManager, IClientState clientState)
        {
            _dataManager = dataManager;
            _clientState = clientState;
        }

        public Item? GetItem(uint itemId)
        {
            return _dataManager.GetExcelSheet<Item>()?.GetRow(itemId);
        }

        public string GetItemName(uint itemId)
        {
            return GetItem(itemId)?.Name?.ToString() ?? $"Unknown Item {itemId}";
        }

        public uint GetItemIcon(uint itemId)
        {
            return GetItem(itemId)?.Icon ?? 0;
        }

        public Recipe? GetRecipe(uint recipeId)
        {
            return _dataManager.GetExcelSheet<Recipe>()?.GetRow(recipeId);
        }

        public List<Recipe> GetRecipesByItem(uint itemId)
        {
            return _dataManager.GetExcelSheet<Recipe>()
                ?.Where(r => r.ItemResult.Row == itemId)
                .ToList() ?? new List<Recipe>();
        }

        public unsafe List<InventoryItem> GetCharacterInventory()
        {
            var result = new List<InventoryItem>();
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return result;

            // Scan all inventory bags
            for (int bag = 0; bag < 4; bag++)
            {
                var container = inventoryManager->GetInventoryContainer((InventoryType)(bag));
                if (container == null) continue;

                for (int i = 0; i < container->Size; i++)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemID == 0) continue;

                    result.Add(new InventoryItem
                    {
                        ItemId = slot->ItemID,
                        Quantity = slot->Quantity,
                        IsHQ = slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ),
                        Spiritbond = slot->Spiritbond,
                        Condition = slot->Condition
                    });
                }
            }

            return result;
        }

        // ... implement remaining interface methods
    }
}
```

### Step 3.4: Design IRepositoryIntegration Interface

**File:** `Modules/Core/IRepositoryIntegration.cs`

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Integration with Akadaemia Anyder database for local-only data
    /// </summary>
    public interface IRepositoryIntegration
    {
        // Material availability (replaces Universalis)
        MaterialAvailability GetMaterialAvailability(uint itemId);
        List<MaterialLocation> FindMaterialLocations(uint itemId);

        // Collection bindings
        bool IsItemCollected(uint itemId, CollectionType type);
        CollectionProgress GetCollectionProgress(CollectionType type);

        // Crafting list persistence (local database)
        void SaveCraftingList(CraftingListData list);
        CraftingListData LoadCraftingList(string listId);
        List<CraftingListData> LoadAllCraftingLists();
        void DeleteCraftingList(string listId);

        // Recipe tracking
        List<uint> GetCraftedRecipes();
        void RecordCraftedRecipe(uint recipeId, bool wasHQ);
        CraftingHistory GetCraftingHistory(uint recipeId);

        // Session tracking
        void RecordCraftingSession(CraftingSessionData session);
        List<CraftingSessionData> GetRecentSessions(int days);
    }

    public class MaterialAvailability
    {
        public uint ItemId { get; set; }
        public int InInventory { get; set; }
        public int InSaddlebag { get; set; }
        public int InRetainers { get; set; }
        public int Total => InInventory + InSaddlebag + InRetainers;
        public Dictionary<string, int> ByLocation { get; set; } = new();
    }

    public class MaterialLocation
    {
        public string Location { get; set; } = ""; // "inventory", "saddlebag", "retainer_1"
        public int SlotId { get; set; }
        public int Quantity { get; set; }
    }

    public enum CollectionType
    {
        Mount, Minion, TripleTriadCard, OrchestrionRoll,
        Emote, Hairstyle, Barding, BlueMageSpell
    }

    public class CraftingListData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public List<CraftingListItemData> Items { get; set; } = new();
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class CraftingListItemData
    {
        public uint RecipeId { get; set; }
        public int Quantity { get; set; }
        public int QuantityCrafted { get; set; }
    }

    public class CraftingHistory
    {
        public uint RecipeId { get; set; }
        public int TotalCrafted { get; set; }
        public int HQCount { get; set; }
        public DateTime FirstCrafted { get; set; }
        public DateTime LastCrafted { get; set; }
    }

    public class CraftingSessionData
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int ItemsCrafted { get; set; }
        public int HQCount { get; set; }
        public List<uint> RecipeIds { get; set; } = new();
    }
}
```

### Step 3.5: Create Mock Implementations for Testing

**File:** `Modules/Core/MockRepositoryIntegration.cs`

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Mock implementation for testing without database
    /// Returns empty/placeholder data
    /// </summary>
    public class MockRepositoryIntegration : IRepositoryIntegration
    {
        public MaterialAvailability GetMaterialAvailability(uint itemId)
        {
            // Return placeholder data
            return new MaterialAvailability
            {
                ItemId = itemId,
                InInventory = 0,
                InSaddlebag = 0,
                InRetainers = 0
            };
        }

        public List<MaterialLocation> FindMaterialLocations(uint itemId)
        {
            return new List<MaterialLocation>();
        }

        // ... implement all interface methods with placeholder returns
    }
}
```

**✅ Validation Criteria:**
- [ ] IGameDataProvider interface defined (14+ methods)
- [ ] DefaultGameDataProvider implemented
- [ ] IRepositoryIntegration interface defined (10+ methods)
- [ ] MockRepositoryIntegration implemented
- [ ] All interfaces compile without errors
- [ ] Data models (MaterialAvailability, etc.) defined

---

## Phase 4: Database Integration

**Duration:** 4-5 hours
**Goal:** Connect Artisan components to Akadaemia database

### Step 4.1: Inject IGameDataProvider into Existing Components

**Files to modify:**

#### `RawInformation/MemoryHelper.cs`
```csharp
// BEFORE:
public static Item? GetItem(uint itemId)
{
    return Service.DataManager.GetExcelSheet<Item>()?.GetRow(itemId);
}

// AFTER:
private readonly IGameDataProvider _gameData;

public MemoryHelper(IGameDataProvider gameData)
{
    _gameData = gameData;
}

public Item? GetItem(uint itemId)
{
    return _gameData.GetItem(itemId);
}
```

#### `UI/RecipeWindowUI.cs`
```csharp
// BEFORE:
var recipe = Service.DataManager.GetExcelSheet<Recipe>()?.GetRow(recipeId);

// AFTER:
private readonly IGameDataProvider _gameData;

public RecipeWindowUI(IGameDataProvider gameData)
{
    _gameData = gameData;
}

public void Draw()
{
    var recipe = _gameData.GetRecipe(recipeId);
    // ...
}
```

### Step 4.2: Inject IRepositoryIntegration for Material Lookups

#### `UI/RecipeWindowUI.cs` - Material Availability Display
```csharp
private readonly IRepositoryIntegration _repository;

public RecipeWindowUI(IGameDataProvider gameData, IRepositoryIntegration repository)
{
    _gameData = gameData;
    _repository = repository;
}

private void DrawMaterialRequirements(Recipe recipe)
{
    foreach (var ingredient in _gameData.GetRecipeIngredients(recipe.RowId))
    {
        // Get material availability from local database
        var availability = _repository.GetMaterialAvailability(ingredient.ItemId);

        ImGui.Text($"{_gameData.GetItemName(ingredient.ItemId)} ×{ingredient.Quantity}");

        if (availability.Total > 0)
        {
            ImGui.SameLine();
            var color = availability.Total >= ingredient.Quantity
                ? new Vector4(0, 1, 0, 1)  // Green (have enough)
                : new Vector4(1, 1, 0, 1); // Yellow (need more)
            ImGui.TextColored(color, $"(have {availability.Total})");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    $"Inventory: {availability.InInventory}\n" +
                    $"Saddlebag: {availability.InSaddlebag}\n" +
                    $"Retainers: {availability.InRetainers}"
                );
            }
        }
        else
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "(need to obtain)");
        }
    }
}
```

#### `UI/ListEditor.cs` - Total Material Cost
```csharp
private void DrawMaterialSummary(CraftingList list)
{
    ImGui.Text("Total Materials Needed:");
    ImGui.Separator();

    // Aggregate all ingredients across all list items
    var materialTotals = new Dictionary<uint, int>();
    foreach (var item in list.Items)
    {
        var recipe = _gameData.GetRecipe(item.RecipeId);
        if (recipe == null) continue;

        foreach (var ingredient in _gameData.GetRecipeIngredients(recipe.RowId))
        {
            var needed = ingredient.Quantity * item.Quantity;
            materialTotals[ingredient.ItemId] =
                materialTotals.GetValueOrDefault(ingredient.ItemId) + needed;
        }
    }

    // Display materials with availability
    foreach (var (itemId, needed) in materialTotals)
    {
        var availability = _repository.GetMaterialAvailability(itemId);
        var have = availability.Total;
        var shortfall = Math.Max(0, needed - have);

        ImGui.Text($"{_gameData.GetItemName(itemId)}:");
        ImGui.SameLine(300);

        if (shortfall == 0)
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), $"{have}/{needed} ✓");
        }
        else
        {
            ImGui.Text($"{have}/{needed}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"(need {shortfall} more)");
        }
    }
}
```

### Step 4.3: Replace Configuration Persistence with Repository

#### `CraftingList/CraftingList.cs`
```csharp
// BEFORE: Configuration-based persistence
public void Save()
{
    Configuration.CraftingLists.Add(this);
    Configuration.Save();
}

// AFTER: Repository-based persistence
private readonly IRepositoryIntegration _repository;

public void Save()
{
    var data = new CraftingListData
    {
        Id = this.Id,
        Name = this.Name,
        Items = this.Items.Select(i => new CraftingListItemData
        {
            RecipeId = i.RecipeID,
            Quantity = i.Quantity,
            QuantityCrafted = i.QuantityCrafted
        }).ToList(),
        LastModified = DateTime.UtcNow
    };

    _repository.SaveCraftingList(data);
}

public static List<CraftingList> LoadAll(IRepositoryIntegration repository)
{
    return repository.LoadAllCraftingLists()
        .Select(data => new CraftingList(repository)
        {
            Id = data.Id,
            Name = data.Name,
            Items = data.Items.Select(i => new CraftingListItem
            {
                RecipeID = i.RecipeId,
                Quantity = i.Quantity,
                QuantityCrafted = i.QuantityCrafted
            }).ToList()
        })
        .ToList();
}
```

### Step 4.4: Update Main Plugin for Dependency Injection

#### `ArtisanPlugin.cs`
```csharp
public sealed class ArtisanPlugin : IDalamudPlugin
{
    private readonly IGameDataProvider _gameData;
    private readonly IRepositoryIntegration _repository;

    public ArtisanPlugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] IDataManager dataManager,
        [RequiredVersion("1.0")] IClientState clientState)
    {
        // Initialize abstraction layers
        _gameData = new DefaultGameDataProvider(dataManager, clientState);

        // TODO: Replace with actual repository implementation
        _repository = new MockRepositoryIntegration();

        // Initialize components with injected dependencies
        PluginUI = new PluginUI(_gameData, _repository);
        CraftingProcessor = new CraftingProcessor(_gameData);
        // ... etc
    }
}
```

**✅ Validation Criteria:**
- [ ] IGameDataProvider injected into 5+ components
- [ ] IRepositoryIntegration injected into UI components
- [ ] Material availability displayed in RecipeWindowUI
- [ ] Material summary calculated in ListEditor
- [ ] CraftingList persistence uses repository
- [ ] Main plugin initializes dependencies
- [ ] MockRepositoryIntegration used temporarily
- [ ] Project builds and runs with mock data

---

## Phase 5: UI Extensions

**Duration:** 4 hours
**Goal:** Add new tabs for Inventory, Collections, Privacy Settings

### Step 5.1: Create New UI Tab Enum Values

#### `UI/PluginUI.cs`
```csharp
public enum OpenWindow
{
    Overview,
    Settings,
    Endurance,
    Macros,
    RaphaelCache,
    RecipeAssigner,
    CraftingLists,
    ListBuilder,
    FCWorkshops,
    Simulator,
    About,
    DEBUG,

    // NEW TABS for Akadaemia Anyder
    Inventory,          // Universal inventory search
    Collections,        // Mount/minion/fishing logs
    PrivacySettings    // Privacy controls
}
```

### Step 5.2: Create Inventory Tab UI

**File:** `UI/InventoryTab.cs`

```csharp
using ImGuiNET;

namespace AkadaemiaAnyder.Modules.Artisan.UI
{
    public class InventoryTab
    {
        private readonly IRepositoryIntegration _repository;
        private readonly IGameDataProvider _gameData;

        private string _searchQuery = "";
        private List<MaterialLocation> _searchResults = new();

        public InventoryTab(IRepositoryIntegration repository, IGameDataProvider gameData)
        {
            _repository = repository;
            _gameData = gameData;
        }

        public void Draw()
        {
            ImGui.Text("Universal Inventory Search");
            ImGui.Separator();

            // Search bar
            ImGui.SetNextItemWidth(400);
            if (ImGui.InputText("##search", ref _searchQuery, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                PerformSearch();
            }

            ImGui.SameLine();
            if (ImGui.Button("Search"))
            {
                PerformSearch();
            }

            ImGui.Spacing();
            ImGui.Separator();

            // Results display
            if (_searchResults.Any())
            {
                DrawSearchResults();
            }
            else if (!string.IsNullOrEmpty(_searchQuery))
            {
                ImGui.TextDisabled("No results found");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // Storage summary dashboard
            DrawStorageSummary();
        }

        private void PerformSearch()
        {
            if (string.IsNullOrEmpty(_searchQuery)) return;

            // Find items matching search query
            var matchingItems = FindItemsByName(_searchQuery);

            _searchResults.Clear();
            foreach (var itemId in matchingItems)
            {
                var locations = _repository.FindMaterialLocations(itemId);
                _searchResults.AddRange(locations);
            }
        }

        private void DrawSearchResults()
        {
            ImGui.Text($"Found in {_searchResults.Count} location(s):");

            // Group by item
            var grouped = _searchResults.GroupBy(r => r.ItemId);

            foreach (var group in grouped)
            {
                var itemName = _gameData.GetItemName(group.Key);
                var totalQuantity = group.Sum(r => r.Quantity);

                if (ImGui.TreeNode($"{itemName} (Total: {totalQuantity})"))
                {
                    foreach (var location in group)
                    {
                        var icon = GetLocationIcon(location.Location);
                        ImGui.BulletText($"{icon} {location.Location}: {location.Quantity} (Slot {location.SlotId})");
                    }
                    ImGui.TreePop();
                }
            }
        }

        private void DrawStorageSummary()
        {
            ImGui.Text("Storage Overview:");

            // TODO: Get actual storage stats from repository
            DrawStorageBar("Inventory", 89, 140);
            DrawStorageBar("Saddlebag", 45, 70);
            DrawStorageBar("Glamour Dresser", 387, 400);

            // Retainers
            for (int i = 1; i <= 3; i++)
            {
                DrawStorageBar($"Retainer {i}", 0, 175);
            }
        }

        private void DrawStorageBar(string name, int used, int capacity)
        {
            float percentage = capacity > 0 ? (float)used / capacity : 0;
            var color = percentage > 0.9f ? new Vector4(1, 0, 0, 1) :
                       percentage > 0.75f ? new Vector4(1, 1, 0, 1) :
                       new Vector4(0, 1, 0, 1);

            ImGui.Text($"{name}:");
            ImGui.SameLine(150);
            ImGui.Text($"{used}/{capacity}");
            ImGui.SameLine(250);
            ImGui.ProgressBar(percentage, new Vector2(200, 20));

            if (percentage > 0.9f)
            {
                ImGui.SameLine();
                ImGui.TextColored(color, "⚠ FULL");
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

        private List<uint> FindItemsByName(string query)
        {
            // TODO: Implement item name search via IGameDataProvider
            return new List<uint>();
        }
    }
}
```

### Step 5.3: Create Collections Tab UI

**File:** `UI/CollectionsTab.cs`

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.UI
{
    public class CollectionsTab
    {
        private readonly IRepositoryIntegration _repository;

        public CollectionsTab(IRepositoryIntegration repository)
        {
            _repository = repository;
        }

        public void Draw()
        {
            ImGui.Text("Collection Progress");
            ImGui.Separator();

            DrawCollectionProgress(CollectionType.Mount, "Mounts");
            DrawCollectionProgress(CollectionType.Minion, "Minions");
            DrawCollectionProgress(CollectionType.TripleTriadCard, "Triple Triad Cards");
            DrawCollectionProgress(CollectionType.OrchestrionRoll, "Orchestrion Rolls");
            DrawCollectionProgress(CollectionType.Emote, "Emotes");
            DrawCollectionProgress(CollectionType.Hairstyle, "Hairstyles");
            DrawCollectionProgress(CollectionType.Barding, "Bardings");
            DrawCollectionProgress(CollectionType.BlueMageSpell, "Blue Mage Spells");
        }

        private void DrawCollectionProgress(CollectionType type, string label)
        {
            var progress = _repository.GetCollectionProgress(type);

            ImGui.Text($"{label}:");
            ImGui.SameLine(200);
            ImGui.Text($"{progress.Unlocked}/{progress.Total} ({progress.Percentage:F1}%)");

            ImGui.ProgressBar((float)progress.Percentage / 100, new Vector2(300, 20));
        }
    }
}
```

### Step 5.4: Create Privacy Settings Tab UI

**File:** `UI/PrivacySettingsTab.cs`

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.UI
{
    public class PrivacySettingsTab
    {
        private readonly Configuration _config;

        public PrivacySettingsTab(Configuration config)
        {
            _config = config;
        }

        public void Draw()
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Privacy-First Design");
            ImGui.Separator();

            ImGui.TextWrapped(
                "Akadaemia Anyder stores all data locally on your computer. " +
                "No character names, no user IDs, no network requests."
            );

            ImGui.Spacing();
            ImGui.Separator();

            ImGui.Text("Data Storage Settings:");
            ImGui.Spacing();

            // Character name storage toggle
            bool storeCharacterNames = _config.PrivacySettings.StoreCharacterNames;
            if (ImGui.Checkbox("Store character names", ref storeCharacterNames))
            {
                if (storeCharacterNames)
                {
                    ImGui.OpenPopup("CharacterNameWarning");
                }
                else
                {
                    _config.PrivacySettings.StoreCharacterNames = false;
                    _config.Save();
                }
            }
            ImGui.TextDisabled("(Disabled by default for privacy)");

            // Warning popup
            if (ImGui.BeginPopupModal("CharacterNameWarning"))
            {
                ImGui.Text("Storing character names reduces privacy.");
                ImGui.Text("Are you sure?");
                ImGui.Spacing();

                if (ImGui.Button("Yes, enable"))
                {
                    _config.PrivacySettings.StoreCharacterNames = true;
                    _config.Save();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("No, cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.Spacing();
            ImGui.Separator();

            ImGui.Text("Export Options:");
            ImGui.Spacing();

            bool anonymousExport = _config.PrivacySettings.EnableAnonymousExport;
            ImGui.Checkbox("Anonymize exports by default", ref anonymousExport);
            _config.PrivacySettings.EnableAnonymousExport = anonymousExport;
            ImGui.TextDisabled("(Strips character names and server info from exports)");

            ImGui.Spacing();

            if (ImGui.Button("Export All Data (JSON)"))
            {
                // TODO: Implement export
            }

            ImGui.SameLine();

            if (ImGui.Button("Export All Data (CSV)"))
            {
                // TODO: Implement export
            }
        }
    }
}
```

### Step 5.5: Integrate New Tabs into Main Window

#### `UI/PluginUI.cs`
```csharp
private InventoryTab? _inventoryTab;
private CollectionsTab? _collectionsTab;
private PrivacySettingsTab? _privacyTab;

public PluginUI(IGameDataProvider gameData, IRepositoryIntegration repository, Configuration config)
{
    _inventoryTab = new InventoryTab(repository, gameData);
    _collectionsTab = new CollectionsTab(repository);
    _privacyTab = new PrivacySettingsTab(config);
}

public override void Draw()
{
    // ... existing tab rendering

    // Add new tabs to tab bar
    if (ImGui.BeginTabItem("Inventory"))
    {
        _inventoryTab?.Draw();
        ImGui.EndTabItem();
    }

    if (ImGui.BeginTabItem("Collections"))
    {
        _collectionsTab?.Draw();
        ImGui.EndTabItem();
    }

    if (ImGui.BeginTabItem("Privacy"))
    {
        _privacyTab?.Draw();
        ImGui.EndTabItem();
    }
}
```

**✅ Validation Criteria:**
- [ ] Inventory tab UI created
- [ ] Collections tab UI created
- [ ] Privacy Settings tab UI created
- [ ] All new tabs integrated into main window
- [ ] Tabs render without errors
- [ ] Search functionality implemented
- [ ] Storage summary displays
- [ ] Privacy controls functional

---

## Phase 6: Testing & Validation

**Duration:** 4 hours
**Goal:** Verify fork works correctly and privacy is maintained

### Step 6.1: Unit Test Creation

**File:** `Tests/Core/DefaultGameDataProviderTests.cs`

```csharp
using Xunit;

namespace AkadaemiaAnyder.Modules.Artisan.Tests.Core
{
    public class DefaultGameDataProviderTests
    {
        [Fact]
        public void GetItem_ValidItemId_ReturnsItem()
        {
            // Arrange
            var provider = new DefaultGameDataProvider(mockDataManager, mockClientState);

            // Act
            var item = provider.GetItem(5333); // Potion of Strength

            // Assert
            Assert.NotNull(item);
            Assert.Equal("Potion of Strength", item.Name.ToString());
        }

        [Fact]
        public void GetItemName_InvalidItemId_ReturnsUnknownItemString()
        {
            // Arrange
            var provider = new DefaultGameDataProvider(mockDataManager, mockClientState);

            // Act
            var name = provider.GetItemName(999999999);

            // Assert
            Assert.Contains("Unknown Item", name);
        }
    }
}
```

### Step 6.2: Network Call Verification

**Script:** `tests/verify-no-network.ps1`

```powershell
# Verify no external network calls in codebase
param(
    [string]$ProjectRoot = "C:\Code\akadaemia-anyder\SamplePlugin\Modules\Artisan"
)

Write-Host "Verifying no external network calls..." -ForegroundColor Cyan

$networkPatterns = @(
    "HttpClient",
    "WebRequest",
    "WebClient",
    "WebSocket",
    "api\.",
    "universalis",
    "teamcraft",
    "discord",
    "webhook"
)

$violations = @()

foreach ($pattern in $networkPatterns) {
    $results = Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.cs" |
        Select-String -Pattern $pattern -CaseSensitive:$false

    if ($results) {
        $violations += $results
    }
}

if ($violations.Count -eq 0) {
    Write-Host "✓ No network calls detected" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Found $($violations.Count) potential network calls:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $($_.Filename):$($_.LineNumber) - $($_.Line.Trim())" }
    exit 1
}
```

### Step 6.3: Build Verification

```bash
# Clean rebuild
dotnet clean
dotnet build -c Release

# Expected output:
# Build succeeded
# 0 Error(s)
# X Warning(s) (warnings are OK)

# Verify DLL produced
ls bin/Release/net8.0-windows/AkadaemiaAnyder.Modules.Artisan.dll

# Expected: File exists, size ~200-300 KB
```

### Step 6.4: In-Game Testing Checklist

**Manual testing required:**

```
□ Plugin loads without errors
□ Main UI window opens (/artisan command)
□ All existing tabs render correctly
  □ Overview tab
  □ Settings tab
  □ Crafting Lists tab
  □ Simulator tab
□ New tabs render correctly
  □ Inventory tab
  □ Collections tab
  □ Privacy Settings tab
□ Crafting queue functionality
  □ Add recipe to queue
  □ Start crafting
  □ Crafting completes successfully
□ Material availability display
  □ Shows local inventory counts
  □ No market pricing displayed (Universalis removed)
□ Privacy settings
  □ Character name storage toggle works
  □ Export buttons present (even if not functional yet)
□ No network errors in Dalamud log
□ No crashes or exceptions
```

**✅ Validation Criteria:**
- [ ] Unit tests created and passing
- [ ] Network verification script passes
- [ ] Clean release build succeeds
- [ ] Plugin loads in-game without errors
- [ ] All existing functionality preserved
- [ ] New tabs visible and functional
- [ ] No external network calls detected
- [ ] No crashes or exceptions

---

## Migration Checklist

### Pre-Migration

- [ ] Artisan repository cloned
- [ ] Backup branch created
- [ ] Original code builds successfully
- [ ] File inventory generated
- [ ] Dependency tree documented

### Phase 1: Privacy Removal

- [ ] Universalis directory deleted
- [ ] Teamcraft.cs deleted
- [ ] Discord webhook config removed
- [ ] Discord.Net.Webhook package removed
- [ ] Network call verification passed
- [ ] Project builds after removals

### Phase 2: Namespace Refactoring

- [ ] All namespace declarations updated
- [ ] All using statements updated
- [ ] Project file updated (AssemblyName, RootNamespace)
- [ ] Main plugin class renamed to ArtisanPlugin
- [ ] Project builds after refactoring
- [ ] Git diff reviewed for consistency

### Phase 3: Abstraction Layer

- [ ] IGameDataProvider interface defined
- [ ] DefaultGameDataProvider implemented
- [ ] IRepositoryIntegration interface defined
- [ ] MockRepositoryIntegration implemented
- [ ] All interfaces compile

### Phase 4: Database Integration

- [ ] IGameDataProvider injected into components
- [ ] IRepositoryIntegration injected into UI
- [ ] Material availability display updated
- [ ] CraftingList persistence uses repository
- [ ] Main plugin initializes dependencies
- [ ] Project builds with mock data

### Phase 5: UI Extensions

- [ ] Inventory tab created
- [ ] Collections tab created
- [ ] Privacy Settings tab created
- [ ] New tabs integrated into main window
- [ ] All tabs render without errors

### Phase 6: Testing & Validation

- [ ] Unit tests created
- [ ] Unit tests passing
- [ ] Network verification script passed
- [ ] Clean release build succeeded
- [ ] In-game testing completed
- [ ] All existing functionality preserved
- [ ] No crashes or exceptions

### Final Sign-off

- [ ] All checklists above completed
- [ ] Documentation updated
- [ ] License compliance verified
- [ ] Code reviewed by peer (if available)
- [ ] Ready for Phase 7: Real Repository Integration

---

## Next Steps After Completion

### Phase 7: Real Repository Integration

Replace `MockRepositoryIntegration` with actual Akadaemia Anyder database implementation:

```csharp
// In ArtisanPlugin.cs
// BEFORE:
_repository = new MockRepositoryIntegration();

// AFTER:
_repository = new AkadaemiaAnyderRepository(databaseContext);
```

### Phase 8: Advanced Features

- [ ] Implement fishing/gathering log integration
- [ ] Add collection scanning automation
- [ ] Create session statistics tracking
- [ ] Build export functionality (JSON/CSV)

### Phase 9: Polish & Release

- [ ] Performance optimization
- [ ] UI/UX refinement based on user feedback
- [ ] Comprehensive testing with multiple characters
- [ ] Create user documentation
- [ ] Prepare for Dalamud plugin repository submission

---

**END OF REFACTORING PLAN**

This document provides a complete, step-by-step guide to forking Artisan and integrating it into Akadaemia Anyder while maintaining privacy-first principles. Each phase includes validation criteria to ensure successful completion before proceeding to the next phase.
