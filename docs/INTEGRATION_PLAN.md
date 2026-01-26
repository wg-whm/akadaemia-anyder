# Akadaemia Anyder: Technical Integration Plan

**Created:** 2026-01-26
**Status:** Planning Phase

---

## Project Vision

**Akadaemia Anyder = Artisan Fork + Privacy Extensions + Modular Add-ons**

Build a privacy-first FFXIV completionist toolkit by:
1. Forking Artisan (BSD-3-Clause) for proven crafting queue/to-do functionality
2. Adding privacy-focused collection tracking (mounts, minions, fishing, gathering)
3. Creating modular architecture for future extensions that "split out" from core

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Akadaemia Anyder Plugin                    │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────┐  ┌─────────────────────────────────┐  │
│  │ Artisan Fork     │  │ Privacy Extensions (NEW)        │  │
│  │ (Crafting Core)  │  │                                 │  │
│  ├──────────────────┤  ├─────────────────────────────────┤  │
│  │ • Queue UI       │  │ • Collection Tracker            │  │
│  │ • Dynamic Solver │  │ • Fishing Logger                │  │
│  │ • Macro Executor │  │ • Gathering Logger              │  │
│  │ • Job Management │  │ • Unified Inventory Tracker     │  │
│  └──────────────────┘  │ • Session Statistics            │  │
│                        │ • Export/Import (anonymous)     │  │
│                        └─────────────────────────────────┘  │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Shared Infrastructure                                │   │
│  ├──────────────────────────────────────────────────────┤   │
│  │ • Local SQLite Database (no user IDs)               │   │
│  │ • Privacy-First Data Layer                           │   │
│  │ • Configuration Manager                              │   │
│  │ • Unified Settings UI                                │   │
│  │ • Event Bus (inter-module communication)             │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Future Modular Extensions (Plugin Architecture)      │   │
│  ├──────────────────────────────────────────────────────┤   │
│  │ • Market Board Tracker Module                        │   │
│  │ • Housing Decorator Module                           │   │
│  │ • Achievement Helper Module                          │   │
│  │ • Loot History Logger Module                         │   │
│  │ • [User can enable/disable independently]            │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Fork Artisan & Privacy Retrofit

### 1.1 Fork Setup

**Repository Structure:**
```
akadaemia-anyder/
├── SamplePlugin/               # Existing codebase (rename to AkadaemiaAnyder)
│   ├── Modules/
│   │   ├── Artisan/            # Forked Artisan code
│   │   │   ├── CraftingQueue/
│   │   │   ├── Solver/
│   │   │   ├── UI/
│   │   │   └── ARTISAN_LICENSE.txt  # BSD-3-Clause
│   │   ├── Inventory/          # NEW: Unified inventory tracker (saddlebags, retainers)
│   │   ├── Collections/        # NEW: Collection tracking
│   │   ├── Fishing/            # NEW: Fishing logger
│   │   ├── Gathering/          # NEW: Gathering logger
│   │   └── Core/               # Shared infrastructure
│   ├── Database/               # Local SQLite
│   ├── UI/                     # Unified UI
│   └── Plugin.cs               # Main plugin entry point
├── docs/
│   ├── INTEGRATION_PLAN.md     # This file
│   ├── PRIVACY_POLICY.md       # User-facing privacy guarantees
│   └── ATTRIBUTION.md          # BSD-3-Clause compliance
└── LICENSE                     # MIT (for new code) + BSD-3-Clause (Artisan)
```

**Fork Process:**
```bash
# 1. Clone Artisan
git clone https://github.com/PunishXIV/Artisan.git artisan-fork
cd artisan-fork

# 2. Review codebase structure
# Identify key components:
# - CraftingQueue management
# - Solver integration
# - UI components (ImGui windows)
# - Configuration system

# 3. Copy relevant code to akadaemia-anyder/SamplePlugin/Modules/Artisan/
# Preserve directory structure
# Include ARTISAN_LICENSE.txt in the Artisan module directory

# 4. Update namespaces
# Find: Artisan.*
# Replace: AkadaemiaAnyder.Modules.Artisan.*
```

### 1.2 Privacy Modifications to Artisan Fork

**Remove/Modify These Features:**
1. **Teamcraft Integration (if present)**
   ```csharp
   // BEFORE (if exists):
   public async Task ImportFromTeamcraft(string url) { ... }

   // AFTER:
   // Remove method OR convert to manual local import:
   public void ImportFromLocalFile(string jsonPath) { ... }
   ```

2. **Any Cloud/Network Features**
   ```csharp
   // Search for:
   // - System.Net.Http
   // - HttpClient
   // - WebRequest
   // - Any API calls

   // Remove or disable with configuration flag
   ```

3. **Telemetry/Analytics**
   ```csharp
   // Search for:
   // - Analytics
   // - Telemetry
   // - Usage tracking

   // Remove completely
   ```

### 1.3 Add Privacy Infrastructure

**New: Privacy-First Database Layer**
```csharp
// SamplePlugin/Database/PrivacyDatabaseContext.cs
public class PrivacyDatabaseContext : DbContext
{
    // NO character_name column
    // NO user_id column
    // NO server_name column

    public DbSet<CraftingQueueItem> CraftingQueue { get; set; }
    public DbSet<CollectionItem> Collections { get; set; }
    public DbSet<FishingLog> FishingLogs { get; set; }
    public DbSet<GatheringLog> GatheringLogs { get; set; }
    public DbSet<InventorySnapshot> InventorySnapshots { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Store in local plugin config directory
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "pluginConfigs", "AkadaemiaAnyder", "data.db"
        );
        options.UseSqlite($"Data Source={configPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enforce NO PII columns in schema
        modelBuilder.Entity<CraftingQueueItem>()
            .Ignore(c => c.CharacterName)
            .Ignore(c => c.UserId);

        // Same for all entities
    }
}
```

**New: Privacy Configuration**
```csharp
// SamplePlugin/Configuration/PrivacySettings.cs
public class PrivacySettings
{
    // All defaults are privacy-maximizing
    public bool StoreCharacterNames { get; set; } = false;
    public bool StoreServerNames { get; set; } = false;
    public bool StoreGPSCoordinates { get; set; } = false;
    public bool EnableAnonymousExport { get; set; } = true;

    // Explicit user consent required to enable
    public bool HasReadPrivacyPolicy { get; set; } = false;
}
```

---

## Phase 2: Privacy Extensions (New Code)

### 2.1 Collection Tracker Module

**Purpose:** Track mounts, minions, cards, orchestrion, etc. via UIState bitfields

**Architecture:**
```csharp
// SamplePlugin/Modules/Collections/CollectionTracker.cs
public class CollectionTracker : IDisposable
{
    private readonly IPluginLog _log;
    private readonly IDataManager _dataManager;
    private readonly PrivacyDatabaseContext _db;

    // Tracked collection types
    private readonly Dictionary<CollectionType, CollectionScanner> _scanners;

    public CollectionTracker(IPluginLog log, IDataManager data, PrivacyDatabaseContext db)
    {
        _log = log;
        _dataManager = data;
        _db = db;

        // Initialize scanners for each collection type
        _scanners = new Dictionary<CollectionType, CollectionScanner>
        {
            { CollectionType.Mounts, new MountScanner(data) },
            { CollectionType.Minions, new MinionScanner(data) },
            { CollectionType.TripleTriadCards, new CardScanner(data) },
            { CollectionType.OrchestrionRolls, new OrchestrionScanner(data) },
            { CollectionType.Emotes, new EmoteScanner(data) },
            { CollectionType.Hairstyles, new HairstyleScanner(data) },
            { CollectionType.Bardings, new BardingScanner(data) },
            { CollectionType.BlueMageSpells, new BlueMageScanner(data) }
        };
    }

    public Dictionary<CollectionType, CollectionProgress> ScanAll()
    {
        var results = new Dictionary<CollectionType, CollectionProgress>();

        foreach (var (type, scanner) in _scanners)
        {
            try
            {
                var progress = scanner.Scan();
                results[type] = progress;

                // Store in local database (no character name)
                _db.Collections.Add(new CollectionSnapshot
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CollectionType = type,
                    UnlockedCount = progress.Unlocked,
                    TotalCount = progress.Total
                });
            }
            catch (Exception ex)
            {
                _log.Error($"Collection scan failed for {type}: {ex.Message}");
            }
        }

        _db.SaveChanges();
        return results;
    }
}

public enum CollectionType
{
    Mounts,
    Minions,
    TripleTriadCards,
    OrchestrionRolls,
    Emotes,
    Hairstyles,
    Bardings,
    BlueMageSpells,
    Aetherytes
}

public class CollectionProgress
{
    public int Unlocked { get; set; }
    public int Total { get; set; }
    public double Percentage => Total > 0 ? (double)Unlocked / Total * 100 : 0;
    public List<uint> UnlockedIds { get; set; } = new();
    public List<uint> MissingIds { get; set; } = new();
}
```

**Individual Scanners:**
```csharp
// SamplePlugin/Modules/Collections/Scanners/MountScanner.cs
public class MountScanner : CollectionScanner
{
    public override CollectionProgress Scan()
    {
        var uiState = UIState.Instance();
        if (uiState == null) return new CollectionProgress();

        // Get all mounts from Lumina
        var mountSheet = _dataManager.GetExcelSheet<Mount>();
        var allMounts = mountSheet.Where(m => m.RowId > 0).ToList();

        var unlocked = new List<uint>();
        var missing = new List<uint>();

        foreach (var mount in allMounts)
        {
            // Check unlock status via UIState
            if (IsUnlockLinkUnlocked(mount.UnlockLink))
            {
                unlocked.Add(mount.RowId);
            }
            else
            {
                missing.Add(mount.RowId);
            }
        }

        return new CollectionProgress
        {
            Unlocked = unlocked.Count,
            Total = allMounts.Count,
            UnlockedIds = unlocked,
            MissingIds = missing
        };
    }

    private bool IsUnlockLinkUnlocked(uint unlockLink)
    {
        var uiState = UIState.Instance();
        return uiState->IsUnlockLinkUnlocked(unlockLink);
    }
}

// Similar scanners for:
// - MinionScanner (uses _unlockedCompanions bitfield)
// - CardScanner (uses _unlockedTripleTriadCards bitfield)
// - BardingScanner (uses BuddyEquip sheet + IsUnlockLinkUnlocked)
// - etc.
```

### 2.2 Fishing Logger Module

**Purpose:** Real-time fishing detection with local-only storage

```csharp
// SamplePlugin/Modules/Fishing/FishingLogger.cs
public class FishingLogger : IDisposable
{
    private readonly IPluginLog _log;
    private readonly PrivacyDatabaseContext _db;
    private readonly IFramework _framework;

    // Subscribe to FishingNote events
    private unsafe FishingNote* _fishingNote;

    public void Enable()
    {
        _framework.Update += OnFrameworkUpdate;
        _log.Info("[FishingLogger] Enabled - monitoring fishing events");
    }

    private unsafe void OnFrameworkUpdate(object framework)
    {
        _fishingNote = FishingNote.Instance();
        if (_fishingNote == null) return;

        // Detect fishing events (bite, catch, etc.)
        // Similar to existing packet capture logic but via memory reading

        // When fish caught:
        OnFishCaught(new FishCaughtEvent
        {
            ItemId = detectedFishId,
            SpotId = detectedSpotId,
            WeatherId = GetCurrentWeather(),
            BaitId = GetCurrentBait(),
            BiteTimeMs = CalculateBiteTime(),
            Size = detectedSize,
            IsHQ = detectedHQ
        });
    }

    private void OnFishCaught(FishCaughtEvent evt)
    {
        // Store in local database (NO character name, NO GPS coordinates)
        _db.FishingLogs.Add(new FishingLog
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ItemId = evt.ItemId,
            SpotId = evt.SpotId,        // Spot ID, not GPS coordinates
            WeatherId = evt.WeatherId,
            BaitId = evt.BaitId,
            BiteTimeMs = evt.BiteTimeMs,
            Size = evt.Size,
            IsHQ = evt.IsHQ
            // Notably absent: character name, exact coordinates, player stats
        });

        _db.SaveChanges();

        _log.Info($"[FishingLogger] Logged fish catch: Item {evt.ItemId} at Spot {evt.SpotId}");
    }
}

// Database model
public class FishingLog
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public uint ItemId { get; set; }
    public uint SpotId { get; set; }
    public uint WeatherId { get; set; }
    public uint BaitId { get; set; }
    public int BiteTimeMs { get; set; }
    public int Size { get; set; }
    public bool IsHQ { get; set; }

    // Privacy: NO character_name, NO gps_x/gps_y, NO player_stats
}
```

### 2.3 Gathering Logger Module

**Purpose:** Real-time gathering detection (similar to fishing)

```csharp
// SamplePlugin/Modules/Gathering/GatheringLogger.cs
public class GatheringLogger : IDisposable
{
    // Similar structure to FishingLogger
    // Subscribe to GatheringNote events
    // Log: {timestamp, itemId, nodeId, zoneId, isHQ}
    // NO character name, NO exact GPS coordinates
}
```

### 2.4 Unified Inventory Tracker Module

**Purpose:** Track ALL storage locations with universal search (inventory, saddlebags, retainers, glamour dresser)

**Why Tier 1:** Saddlebags are core inventory, not optional. No existing tool provides universal search across all storage. Massive QoL improvement.

**Tracked Storage Locations:**
- Character Inventory (140 slots)
- **Chocobo Saddlebags** (70 slots) - CRITICAL inclusion
- Retainers (up to 10 retainers × 175 slots)
- Armory Chest (all equipment slots)
- Glamour Dresser (400 slots)
- Free Company Chest (optional)
- Housing Storage (optional)

**Architecture:**
```csharp
// SamplePlugin/Modules/Inventory/UnifiedInventoryTracker.cs
public class UnifiedInventoryTracker : IDisposable
{
    private readonly IPluginLog _log;
    private readonly PrivacyDatabaseContext _db;
    private readonly IFramework _framework;

    // Subscribe to inventory updates
    public void Enable()
    {
        _framework.Update += OnFrameworkUpdate;

        // Scan on login
        if (IsLoggedIn())
        {
            ScanAllInventory();
        }
    }

    private unsafe void ScanAllInventory()
    {
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return;

        // Scan character inventory (4 bags)
        ScanInventoryContainer(InventoryType.Inventory1, "inventory");
        ScanInventoryContainer(InventoryType.Inventory2, "inventory");
        ScanInventoryContainer(InventoryType.Inventory3, "inventory");
        ScanInventoryContainer(InventoryType.Inventory4, "inventory");

        // Scan chocobo saddlebags (2 bags)
        ScanInventoryContainer(InventoryType.SaddleBag1, "saddlebag");
        ScanInventoryContainer(InventoryType.SaddleBag2, "saddlebag");

        // Armory chest (equipment storage)
        ScanInventoryContainer(InventoryType.ArmoryMainHand, "armory");
        ScanInventoryContainer(InventoryType.ArmoryHead, "armory");
        // ... all armory slots

        _log.Info("[UnifiedInventoryTracker] Full inventory scan complete");
    }

    private unsafe void ScanInventoryContainer(InventoryType type, string location)
    {
        var inventoryManager = InventoryManager.Instance();
        var container = inventoryManager->GetInventoryContainer(type);

        if (container == null) return;

        for (int i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot == null || slot->ItemID == 0) continue;

            // Update or insert snapshot
            var existing = _db.InventorySnapshots.FirstOrDefault(s =>
                s.Location == location && s.SlotId == i);

            if (existing != null)
            {
                existing.ItemId = slot->ItemID;
                existing.Quantity = slot->Quantity;
                existing.IsHQ = slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ);
                existing.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                _db.InventorySnapshots.Add(new InventorySnapshot
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Location = location,
                    SlotId = i,
                    ItemId = slot->ItemID,
                    Quantity = slot->Quantity,
                    IsHQ = slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ),
                    Spiritbond = slot->Spiritbond,
                    Condition = slot->Condition
                });
            }
        }

        _db.SaveChanges();
    }

    // Retainer scanning (requires summoning bell menu open)
    public void OnRetainerMenuOpen(int retainerId)
    {
        _log.Info($"[UnifiedInventoryTracker] Scanning retainer {retainerId}");

        // Scan all retainer pages
        for (int page = 1; page <= 5; page++)
        {
            ScanInventoryContainer(
                (InventoryType)((int)InventoryType.RetainerPage1 + page - 1),
                $"retainer_{retainerId}"
            );
        }
    }

    // Universal search
    public List<InventorySearchResult> Search(string query)
    {
        var results = new List<InventorySearchResult>();

        // Get item sheet for name lookup
        var itemSheet = _dataManager.GetExcelSheet<Item>();

        // Search by item name
        var matchingItems = itemSheet
            .Where(i => i.Name.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.RowId)
            .ToHashSet();

        // Find all inventory entries for matching items
        var snapshots = _db.InventorySnapshots
            .Where(s => matchingItems.Contains(s.ItemId))
            .ToList();

        // Group by location
        foreach (var group in snapshots.GroupBy(s => s.Location))
        {
            var totalQuantity = group.Sum(s => s.Quantity);
            results.Add(new InventorySearchResult
            {
                Location = group.Key,
                ItemId = group.First().ItemId,
                TotalQuantity = totalQuantity,
                Slots = group.Select(s => s.SlotId).ToList()
            });
        }

        return results;
    }
}

// Database model
public class InventorySnapshot
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public string Location { get; set; }  // "inventory", "saddlebag", "retainer_1", etc.
    public int SlotId { get; set; }
    public uint ItemId { get; set; }
    public int Quantity { get; set; }
    public bool IsHQ { get; set; }
    public int Spiritbond { get; set; }
    public int Condition { get; set; }

    // Privacy: NO character_name, NO retainer_name
}

public class InventorySearchResult
{
    public string Location { get; set; }
    public uint ItemId { get; set; }
    public int TotalQuantity { get; set; }
    public List<int> Slots { get; set; }
}
```

**UI Integration:**
```csharp
private void DrawInventoryTab()
{
    ImGui.Text("Universal Inventory Search");
    ImGui.Separator();

    // Search bar
    ImGui.InputText("Search", ref _searchQuery, 256);

    if (ImGui.Button("Search") || ImGui.IsKeyPressed(ImGuiKey.Enter))
    {
        _searchResults = _inventoryTracker.Search(_searchQuery);
    }

    ImGui.SameLine();
    if (ImGui.Button("Refresh All"))
    {
        _inventoryTracker.ScanAllInventory();
    }

    // Display results
    if (_searchResults != null && _searchResults.Any())
    {
        ImGui.Separator();
        ImGui.Text($"Found in {_searchResults.Count} location(s):");

        foreach (var result in _searchResults)
        {
            var itemName = GetItemName(result.ItemId);
            var locationIcon = GetLocationIcon(result.Location);

            ImGui.Text($"{locationIcon} {result.Location}:");
            ImGui.SameLine(200);
            ImGui.Text($"{result.TotalQuantity}× {itemName}");
            ImGui.TextDisabled($"  Slots: {string.Join(", ", result.Slots)}");
        }

        // Total across all locations
        var grandTotal = _searchResults.Sum(r => r.TotalQuantity);
        ImGui.Separator();
        ImGui.Text($"Total: {grandTotal}×");
    }

    // Storage summary dashboard
    ImGui.Separator();
    ImGui.Text("Storage Overview:");

    DrawStorageSummary("Inventory", 140, GetUsedSlots("inventory"));
    DrawStorageSummary("Saddlebag", 70, GetUsedSlots("saddlebag"));
    DrawStorageSummary("Glamour Dresser", 400, GetUsedSlots("glamour"));

    // Retainers
    for (int i = 1; i <= 10; i++)
    {
        var used = GetUsedSlots($"retainer_{i}");
        if (used > 0)
        {
            DrawStorageSummary($"Retainer {i}", 175, used);
        }
    }
}

private void DrawStorageSummary(string name, int capacity, int used)
{
    float percentage = (float)used / capacity;
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
        ImGui.TextColored(color, "⚠️ FULL");
    }
}

private string GetLocationIcon(string location)
{
    return location switch
    {
        "inventory" => "🎒",
        "saddlebag" => "🐤",
        "armory" => "⚔️",
        "glamour" => "👗",
        _ when location.StartsWith("retainer") => "👤",
        _ => "📦"
    };
}
```

**Smart Saddlebag Suggestions:**
```csharp
public List<SaddlebagSuggestion> GetSaddlebagSuggestions()
{
    var suggestions = new List<SaddlebagSuggestion>();

    var saddlebagItems = _db.InventorySnapshots
        .Where(s => s.Location == "saddlebag")
        .ToList();

    foreach (var item in saddlebagItems)
    {
        var itemData = GetItemData(item.ItemId);

        // Suggest moving non-field items to retainer
        if (itemData.ItemUICategory == ItemUICategory.Housing)
        {
            suggestions.Add(new SaddlebagSuggestion
            {
                ItemId = item.ItemId,
                Suggestion = "Move to retainer (housing item not needed in field)",
                Priority = SuggestionPriority.Medium
            });
        }

        if (itemData.ItemUICategory == ItemUICategory.Minion && item.Quantity == 1)
        {
            suggestions.Add(new SaddlebagSuggestion
            {
                ItemId = item.ItemId,
                Suggestion = "Use minion or move to retainer",
                Priority = SuggestionPriority.Low
            });
        }

        // Flag repair items as good
        if (itemData.Name.Contains("Dark Matter"))
        {
            suggestions.Add(new SaddlebagSuggestion
            {
                ItemId = item.ItemId,
                Suggestion = "✅ Good for field repairs",
                Priority = SuggestionPriority.Info
            });
        }
    }

    return suggestions;
}
```

**Integration with Crafting Module:**
```csharp
// When Artisan crafting queue needs materials
public MaterialAvailability CheckMaterialAvailability(uint itemId, int requiredQuantity)
{
    var search = _inventoryTracker.Search(GetItemName(itemId));

    var totalAvailable = search.Sum(r => r.TotalQuantity);

    return new MaterialAvailability
    {
        ItemId = itemId,
        Required = requiredQuantity,
        Available = totalAvailable,
        HasEnough = totalAvailable >= requiredQuantity,
        Locations = search.Select(r => new MaterialLocation
        {
            Location = r.Location,
            Quantity = r.TotalQuantity
        }).ToList()
    };
}

// Display in crafting queue UI
private void DrawMaterialRequirements()
{
    ImGui.Text("Required Materials:");

    foreach (var material in GetRequiredMaterials())
    {
        var availability = _inventoryTracker.CheckMaterialAvailability(
            material.ItemId,
            material.Quantity
        );

        var icon = availability.HasEnough ? "✅" : "❌";
        ImGui.Text($"{icon} {GetItemName(material.ItemId)} ×{material.Quantity}");

        if (availability.Available > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"(have {availability.Available})");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(string.Join("\n", availability.Locations
                    .Select(l => $"{l.Location}: {l.Quantity}")));
            }
        }
    }
}
```

---

## Phase 3: Unified UI & Settings

### 3.1 Main Plugin Window

**Tabbed Interface:**
```csharp
// SamplePlugin/UI/MainWindow.cs
public class MainWindow : Window
{
    private enum TabView
    {
        CraftingQueue,      // Artisan fork UI
        Inventory,          // NEW: Universal inventory search (includes saddlebags)
        Collections,        // NEW: Collection progress
        FishingLog,         // NEW: Fishing history
        GatheringLog,       // NEW: Gathering history
        Statistics,         // NEW: Session stats
        Settings            // Unified settings
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("AkadaemiaAnyderTabs"))
        {
            if (ImGui.BeginTabItem("Crafting Queue"))
            {
                DrawCraftingQueueTab();  // Artisan fork UI
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Inventory"))
            {
                DrawInventoryTab();  // Universal search (saddlebags, retainers, etc.)
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Collections"))
            {
                DrawCollectionsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Fishing Log"))
            {
                DrawFishingLogTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Gathering Log"))
            {
                DrawGatheringLogTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Statistics"))
            {
                DrawStatisticsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}
```

### 3.2 Collections Tab UI

```csharp
private void DrawCollectionsTab()
{
    ImGui.Text("Collection Progress");
    ImGui.Separator();

    if (ImGui.Button("Scan All Collections"))
    {
        var results = _collectionTracker.ScanAll();
        _lastScanResults = results;
    }

    ImGui.SameLine();
    ImGui.TextDisabled("Last scan: " + _lastScanTime?.ToString("HH:mm:ss"));

    if (_lastScanResults != null)
    {
        ImGui.BeginChild("CollectionsList", new Vector2(0, 0), true);

        foreach (var (type, progress) in _lastScanResults)
        {
            ImGui.Text($"{type}:");
            ImGui.SameLine(200);
            ImGui.Text($"{progress.Unlocked}/{progress.Total} ({progress.Percentage:F1}%)");

            // Progress bar
            ImGui.ProgressBar((float)progress.Percentage / 100, new Vector2(300, 20));

            // Expandable details
            if (ImGui.TreeNode($"##details_{type}"))
            {
                ImGui.Text($"Missing: {progress.MissingIds.Count}");
                // Show missing items with names from Lumina
                ImGui.TreePop();
            }
        }

        ImGui.EndChild();
    }
}
```

### 3.3 Privacy Settings Tab

```csharp
private void DrawSettingsTab()
{
    ImGui.Text("Privacy Settings");
    ImGui.Separator();

    // Privacy-first defaults
    var settings = _configuration.PrivacySettings;

    ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ All data stored locally on your computer");
    ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ No user IDs or tracking");
    ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ No network requests");

    ImGui.Spacing();
    ImGui.Text("Optional Data Storage:");

    if (ImGui.Checkbox("Store character names", ref settings.StoreCharacterNames))
    {
        ImGui.OpenPopup("CharacterNameWarning");
    }
    ImGui.TextDisabled("(Disabled by default for privacy)");

    if (ImGui.BeginPopupModal("CharacterNameWarning"))
    {
        ImGui.Text("Storing character names reduces privacy.");
        ImGui.Text("Are you sure?");

        if (ImGui.Button("Yes, enable"))
        {
            _configuration.Save();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("No, cancel"))
        {
            settings.StoreCharacterNames = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    ImGui.Spacing();
    ImGui.Separator();
    ImGui.Text("Export Options:");

    ImGui.Checkbox("Anonymize exports by default", ref settings.EnableAnonymousExport);
    ImGui.TextDisabled("(Strips character names and server info from exports)");

    if (ImGui.Button("Export All Data (JSON)"))
    {
        ExportToJson(settings.EnableAnonymousExport);
    }

    ImGui.SameLine();

    if (ImGui.Button("Export All Data (CSV)"))
    {
        ExportToCsv(settings.EnableAnonymousExport);
    }
}
```

---

## Phase 4: Modular Extension Architecture

### 4.1 Plugin System Design

**Goal:** Allow future features to be enabled/disabled independently

```csharp
// SamplePlugin/Modules/Core/IModule.cs
public interface IModule : IDisposable
{
    string ModuleName { get; }
    string ModuleDescription { get; }
    bool IsEnabled { get; set; }

    void Initialize();
    void Enable();
    void Disable();

    // Optional UI contribution
    void DrawUI();
    void DrawSettings();
}

// SamplePlugin/Modules/Core/ModuleManager.cs
public class ModuleManager
{
    private readonly Dictionary<string, IModule> _modules = new();

    public void RegisterModule(IModule module)
    {
        _modules[module.ModuleName] = module;

        if (module.IsEnabled)
        {
            module.Initialize();
            module.Enable();
        }
    }

    public void EnableModule(string moduleName)
    {
        if (_modules.TryGetValue(moduleName, out var module))
        {
            module.Enable();
            module.IsEnabled = true;
        }
    }

    public void DisableModule(string moduleName)
    {
        if (_modules.TryGetValue(moduleName, out var module))
        {
            module.Disable();
            module.IsEnabled = false;
        }
    }

    public IEnumerable<IModule> GetAllModules() => _modules.Values;
}
```

### 4.2 Example Future Modules

**Note:** Retainer inventory tracking is now part of core UnifiedInventoryTracker (Tier 1). Future retainer module could add venture timer tracking.

**Retainer Ventures Module:**
```csharp
// SamplePlugin/Modules/Retainers/RetainerVenturesModule.cs
public class RetainerVenturesModule : IModule
{
    public string ModuleName => "Retainer Ventures";
    public string ModuleDescription => "Track venture completion times and history";
    public bool IsEnabled { get; set; }

    public void Initialize()
    {
        // Subscribe to retainer info packets
        // Track venture completion times
        // Log venture results history
        // Show notifications when ventures complete
    }

    public void DrawUI()
    {
        // Display venture timers
        // Show venture history (what items retainers brought back)
        // Suggest next ventures based on item needs
    }
}
```

**Market Board Tracker Module:**
```csharp
// SamplePlugin/Modules/MarketBoard/MarketBoardModule.cs
public class MarketBoardModule : IModule
{
    public string ModuleName => "Market Board Tracker";
    public string ModuleDescription => "Local-only market board history";
    public bool IsEnabled { get; set; }

    // Privacy-focused: NO upload to Universalis
    // Only track items YOU search for
    // Show YOUR search history and price trends

    public void Initialize()
    {
        // Subscribe to market board packets
        // Store in local database (privacy-first)
    }
}
```

**Housing Decorator Module:**
```csharp
// SamplePlugin/Modules/Housing/HousingModule.cs
public class HousingModule : IModule
{
    public string ModuleName => "Housing Decorator";
    public string ModuleDescription => "Furniture catalog and wishlist";
    public bool IsEnabled { get; set; }

    // Track which furniture you own
    // Create wishlists for housing projects
    // Show where to obtain furniture

    public void Initialize()
    {
        // Scan HousingItem sheet
        // Cross-reference with inventory/storage
    }
}
```

### 4.3 Module Settings UI

```csharp
private void DrawModuleSettingsTab()
{
    ImGui.Text("Enabled Modules:");
    ImGui.Separator();

    foreach (var module in _moduleManager.GetAllModules())
    {
        bool enabled = module.IsEnabled;
        if (ImGui.Checkbox($"##module_{module.ModuleName}", ref enabled))
        {
            if (enabled)
                _moduleManager.EnableModule(module.ModuleName);
            else
                _moduleManager.DisableModule(module.ModuleName);
        }

        ImGui.SameLine();
        ImGui.Text(module.ModuleName);
        ImGui.TextDisabled(module.ModuleDescription);

        ImGui.Spacing();
    }
}
```

---

## Phase 5: Attribution & Licensing

### 5.1 License File Structure

```
LICENSE
├── MIT License (applies to NEW code)
│   Copyright (c) 2026 [Your Name]
│
│   Permission is hereby granted, free of charge...
│
└── BSD-3-Clause License (applies to Artisan fork)
    Copyright (c) 2023 Puni.sh

    Redistribution and use in source and binary forms...
```

### 5.2 Attribution Documentation

**Create:** `docs/ATTRIBUTION.md`
```markdown
# Attribution & Licensing

## Artisan Crafting Module

The crafting queue functionality in Akadaemia Anyder is based on **Artisan** by Puni.sh.

- **Original Project:** https://github.com/PunishXIV/Artisan
- **License:** BSD-3-Clause
- **Copyright:** 2023 Puni.sh
- **Modifications:** Privacy enhancements, local-only mode, UI integration

See `SamplePlugin/Modules/Artisan/ARTISAN_LICENSE.txt` for full license text.

## Akadaemia Anyder (New Code)

All new code (collection tracking, fishing logger, gathering logger, privacy infrastructure) is licensed under MIT.

- **License:** MIT
- **Copyright:** 2026 [Your Name]
```

### 5.3 In-App Attribution

```csharp
// In About dialog or main window footer
ImGui.Separator();
ImGui.TextDisabled("Crafting module based on Artisan by Puni.sh (BSD-3-Clause)");
if (ImGui.IsItemHovered())
{
    ImGui.SetTooltip("Original: https://github.com/PunishXIV/Artisan");
}
```

---

## Development Roadmap

**AI-Accelerated Timeline:** 6-8 weeks to MVP (vs 11-13 weeks manual)

**Approach:** Use AI agents for code generation, parallel module development, automated test creation, and documentation. Manual work required for game testing, UI/UX refinement, and integration verification.

### **Milestone 1: Foundation (1-1.5 weeks)** ⚡ AI-accelerated
**Original estimate:** 2-3 weeks | **With AI:** 1-1.5 weeks (~50% reduction)

**AI-automated tasks:**
- [ ] Fork Artisan codebase (manual git ops: 2 hours)
- [ ] Review codebase structure (AI summarizes: 4 hours)
- [ ] Update namespaces and references (AI bulk refactor: 1 day)
- [ ] Remove/modify cloud features for privacy (AI identifies and strips: 1-2 days)
- [ ] Set up local SQLite database (AI generates EF Core models: 1 day)
- [ ] Create privacy configuration system (AI generates classes: 4 hours)
- [ ] Write attribution documentation (AI drafts: 2 hours)

**Manual verification required:**
- [ ] Test crafting queue functionality (in-game testing: 2 days)
- [ ] Verify no network calls remain (code review: 4 hours)

**Parallelization opportunities:**
- Database setup + Privacy config (parallel)
- Attribution docs + Code review (parallel)

### **Milestone 2: Privacy Extensions (2-3 weeks)** ⚡ AI-accelerated + parallel
**Original estimate:** 4-5 weeks | **With AI:** 2-3 weeks (~40% reduction)

**Week 1: Core Module Scaffolding (AI parallel agents)**
- [ ] **Agent 1:** Implement UnifiedInventoryTracker module
  - Generate inventory scanner for all container types
  - Implement universal search functionality
  - Add smart saddlebag suggestions
  - **AI generates:** 80% of code, manual: memory access debugging

- [ ] **Agent 2:** Implement CollectionTracker module
  - Build individual collection scanners (mounts, minions, cards, etc.)
  - Generate scan logic for UIState bitfields
  - **AI generates:** 90% of code, manual: testing with real data

- [ ] **Agent 3:** Implement FishingLogger + GatheringLogger modules
  - Adapt existing packet capture patterns to memory reading
  - Implement event detection logic
  - **AI generates:** 85% of code, manual: event timing calibration

**Week 2: UI Implementation (AI-assisted)**
- [ ] Create unified UI with tabs (AI generates ImGui code: 2 days)
- [ ] Build inventory tab UI with search and storage summary (AI: 1.5 days)
- [ ] Build collections tab UI (AI: 1 day)
- [ ] Build fishing/gathering log UI (AI: 1 day)
- [ ] Add privacy settings tab (AI: 0.5 days)
- [ ] Manual UI/UX refinement and layout tweaking (2 days)

**Week 3: Integration & Testing**
- [ ] Integrate inventory tracker with Artisan crafting queue (AI: 1 day)
- [ ] In-game testing of all modules (manual: 3-4 days)
- [ ] Bug fixes and adjustments (AI-assisted: 2-3 days)

**Parallelization strategy:**
- 3 AI agents develop modules simultaneously (Week 1)
- UI development overlaps with module bug fixing (Week 2)

### **Milestone 3: Polish & Testing (1-1.5 weeks)** ⚡ AI-accelerated
**Original estimate:** 2 weeks | **With AI:** 1-1.5 weeks (~30% reduction)

**AI-automated tasks:**
- [ ] Write comprehensive tests for new modules (AI generates xUnit tests: 2 days)
- [ ] Generate privacy policy documentation (AI drafts: 4 hours)
- [ ] Write user guide / README (AI drafts: 1 day)
- [ ] Generate code documentation (AI: 4 hours)

**Manual verification required:**
- [ ] Performance optimization (AI suggests, manual measures: 2 days)
- [ ] UI/UX polish (subjective refinement: 2 days)
- [ ] Test with multiple characters (in-game: 2 days)
- [ ] Test database migrations (manual: 1 day)

**Parallelization:**
- Test generation + Documentation (parallel)
- Performance testing + UI polish (parallel)

### **Milestone 4: Modular Architecture (4-5 days)** ⚡ AI-accelerated
**Original estimate:** 2 weeks | **With AI:** 4-5 days (~65% reduction)

**AI-automated tasks:**
- [ ] Design and implement IModule interface (AI: 4 hours)
- [ ] Create ModuleManager (AI: 1 day)
- [ ] Refactor existing modules to IModule pattern (AI bulk refactor: 1 day)
- [ ] Build module settings UI (AI generates ImGui: 4 hours)
- [ ] Document module development guide (AI: 4 hours)

**Manual verification:**
- [ ] Test enable/disable functionality (manual: 1 day)
- [ ] Integration testing (manual: 1 day)

### **Milestone 5: Initial Release (3-4 days)** ⚡ AI-accelerated
**Original estimate:** 1 week | **With AI:** 3-4 days (~50% reduction)

**AI-automated tasks:**
- [ ] Write release notes (AI: 2 hours)
- [ ] Create marketing materials (AI drafts README, feature list: 1 day)
- [ ] Generate screenshots with annotations (AI captions: 2 hours)

**Manual work:**
- [ ] Final in-game testing (manual: 1 day)
- [ ] Create installer (manual: 4 hours)
- [ ] Publish to GitHub (manual: 2 hours)
- [ ] Submit to Dalamud plugin repository (manual: 2 hours)

### **Milestone 6: Future Modules (1-2 weeks per module)** ⚡ AI-accelerated
**Approach:** AI agents develop modules autonomously, human verifies and tests

**Per-module timeline with AI:**
- AI agent develops module (2-3 days)
- Manual game testing (2-3 days)
- Bug fixes and refinement (1-2 days)

**Modules in priority order:**
- [ ] Retainer Ventures Module (timer tracking, completion notifications)
- [ ] Housing Decorator Module (furniture catalog, wishlists)
- [ ] Achievement Helper Module (progress tracking)
- [ ] Loot History Logger Module (acquisition tracking)
- [ ] Market Board Tracker Module (local price history)

---

## AI Development Strategy

### **What AI Handles Well (80-90% automation)**
✅ Code scaffolding and boilerplate
✅ Database models and migrations
✅ Test generation (unit, integration)
✅ Documentation writing
✅ Namespace refactoring
✅ ImGui UI code generation
✅ Pattern replication (e.g., collection scanners)
✅ Privacy code review (identifying network calls)

### **What Requires Manual Work (human-critical)**
⚠️ In-game testing with real FFXIV client
⚠️ Memory access debugging (game-specific)
⚠️ UI/UX refinement (subjective design decisions)
⚠️ Performance profiling with real data
⚠️ Event timing calibration (fishing/gathering detection)
⚠️ Integration verification (Artisan + our code)

### **Parallel Development Approach**
```
Week 1-2: Foundation
  ├─ Agent A: Fork + Integration
  └─ Agent B: Database + Privacy setup (parallel)

Week 3-4: Core Modules (3 agents in parallel)
  ├─ Agent A: UnifiedInventoryTracker
  ├─ Agent B: CollectionTracker
  └─ Agent C: Fishing/GatheringLogger

Week 5: UI + Integration
  ├─ Agent A: UI generation
  └─ Human: Layout refinement + game testing

Week 6: Polish + Tests
  ├─ Agent A: Test generation + docs
  └─ Human: Performance + multi-char testing

Week 7: Modular Architecture
  ├─ Agent A: IModule pattern refactor
  └─ Human: Integration testing

Week 8: Release Prep
  ├─ Agent A: Marketing materials
  └─ Human: Final testing + publish
```

---

## Risk Factors & Mitigation

### **Risk: AI-generated code requires extensive debugging**
**Likelihood:** Medium
**Mitigation:**
- Review all AI-generated memory access code manually
- Test incrementally (don't wait until full implementation)
- Use AI to generate comprehensive tests alongside implementation

### **Risk: Game updates break memory structures**
**Likelihood:** High (happens with every patch)
**Mitigation:**
- Design abstraction layer for memory access
- Document memory structure dependencies
- Test with game updates promptly

### **Risk: Integration with Artisan fork more complex than expected**
**Likelihood:** Medium
**Mitigation:**
- Start with shallow integration (minimal changes to Artisan)
- Gradually refactor for modularity
- Budget 1 extra week for integration issues

### **Risk: Performance issues with large datasets**
**Likelihood:** Low-Medium
**Mitigation:**
- Profile early with realistic data (10k+ items)
- Implement pagination from the start
- Use database indexing aggressively

---

## Summary: AI-Accelerated Timeline

| Milestone | Manual Estimate | AI-Accelerated | Savings |
|-----------|----------------|----------------|---------|
| 1. Foundation | 2-3 weeks | 1-1.5 weeks | ~50% |
| 2. Privacy Extensions | 4-5 weeks | 2-3 weeks | ~40% |
| 3. Polish & Testing | 2 weeks | 1-1.5 weeks | ~30% |
| 4. Modular Architecture | 2 weeks | 4-5 days | ~65% |
| 5. Initial Release | 1 week | 3-4 days | ~50% |
| **TOTAL** | **11-13 weeks** | **6-8 weeks** | **~45%** |

**Recommended approach:** Target **7 weeks** (middle of range) with buffer for unexpected issues.

**When to start:** Ready to begin immediately with Milestone 1.

---

## Technical Challenges & Solutions

### Challenge 1: Recipe Detection Still Blocked
**Status:** Unresolved in current codebase
**Solution Options:**
1. Defer to future (wait for game update or new memory structures discovered)
2. Event-based tracking (only track when you craft, miss historical data)
3. Manual import from external tools (Teamcraft export → Akadaemia Anyder import)

**Recommendation:** Option 2 for MVP (event-based), Option 3 for user convenience

### Challenge 2: Artisan Code Complexity
**Risk:** Artisan may have complex dependencies or tightly coupled code
**Mitigation:**
1. Start with shallow integration (use as library, minimal modifications)
2. Gradually refactor for modularity
3. Document any breaking changes or workarounds

### Challenge 3: Database Migrations
**Risk:** Schema changes as we add features
**Solution:** Use Entity Framework migrations
```csharp
// Add migration:
dotnet ef migrations add AddFishingLogTable

// Apply migration:
dotnet ef database update
```

### Challenge 4: Performance with Large Datasets
**Risk:** 10k+ fishing logs, 100k+ gathering logs could slow UI
**Mitigation:**
1. Pagination in UI
2. Indexed database queries
3. Lazy loading for log displays
4. Optional: Archive old data to separate database file

---

## Success Metrics

### Privacy Compliance
- ✅ Zero network requests in production build
- ✅ No user IDs or character names stored (unless explicitly enabled)
- ✅ All data in local SQLite database
- ✅ Attribution properly displayed

### Functionality
- ✅ Crafting queue works (Artisan fork functional)
- ✅ Collection scanning works (8+ collection types)
- ✅ Fishing logging works (real-time detection)
- ✅ Gathering logging works (real-time detection)
- ✅ UI responsive and intuitive

### Modularity
- ✅ Can enable/disable modules independently
- ✅ Can add new modules without modifying core
- ✅ Module API documented for future development

---

## Next Steps

1. **Immediate:** Fork Artisan repository and review codebase structure
2. **Week 1:** Integrate Artisan into Akadaemia Anyder project
3. **Week 2:** Implement privacy database layer
4. **Week 3:** Build first privacy extension (CollectionTracker)
5. **Week 4:** Unified UI prototype

**Ready to start with Step 1?**
