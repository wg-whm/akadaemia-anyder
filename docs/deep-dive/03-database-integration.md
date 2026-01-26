# Deep Dive: Database Integration Approach

**Topic:** Why and how to create abstraction layers for game data and repository access
**Complexity:** Medium-High
**Relevance:** Foundation for all privacy features and local-only functionality

---

## Overview

Artisan currently accesses game data directly via Lumina and FFXIVClientStructs. For Akadaemia Anyder, we need to:
1. Decouple game data access (testability, maintainability)
2. Integrate local SQLite database for inventory/collection tracking
3. Replace external API calls (Universalis) with local queries

**Solution:** Two abstraction layers with dependency injection

---

## Why Abstraction Layers?

### Problem Without Abstraction

```csharp
// Direct coupling to Dalamud services (current Artisan approach)
public class RecipeWindowUI
{
    public void DrawIngredients(uint recipeId)
    {
        // PROBLEM 1: Direct dependency on Service.DataManager
        var recipe = Service.DataManager.GetExcelSheet<Recipe>()?.GetRow(recipeId);

        // PROBLEM 2: Direct HTTP call to external API
        var pricing = await UniversalisClient.GetMarketBoardDataAsync(itemId);

        // PROBLEM 3: Hard to test without game running
        // PROBLEM 4: Hard to swap data sources
        // PROBLEM 5: Privacy code scattered throughout UI
    }
}
```

**Issues:**
- ❌ Cannot unit test without Dalamud running
- ❌ Cannot replace Universalis without modifying UI code
- ❌ Privacy concerns mixed with business logic
- ❌ Tight coupling makes refactoring risky

### Solution With Abstraction

```csharp
// Decoupled via interfaces
public class RecipeWindowUI
{
    private readonly IGameDataProvider _gameData;
    private readonly IRepositoryIntegration _repository;

    public RecipeWindowUI(IGameDataProvider gameData, IRepositoryIntegration repository)
    {
        _gameData = gameData;
        _repository = repository;
    }

    public void DrawIngredients(uint recipeId)
    {
        // SOLUTION 1: Game data via abstraction
        var recipe = _gameData.GetRecipe(recipeId);

        // SOLUTION 2: Local database instead of API
        var availability = _repository.GetMaterialAvailability(itemId);

        // SOLUTION 3: Easy to test (mock the interfaces)
        // SOLUTION 4: Easy to swap implementations
        // SOLUTION 5: Privacy guaranteed at interface level
    }
}
```

**Benefits:**
- ✅ Unit testable (mock interfaces)
- ✅ Privacy enforced by interface contract (no network methods)
- ✅ Swappable implementations (test vs production)
- ✅ Clear separation of concerns

---

## Abstraction Layer 1: IGameDataProvider

### Interface Design

**Purpose:** Abstract access to game data (Lumina sheets, FFXIVClientStructs)

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Provides access to FFXIV game data without direct Lumina/FFXIVClientStructs coupling
    /// </summary>
    public interface IGameDataProvider
    {
        // Item queries
        Item? GetItem(uint itemId);
        string GetItemName(uint itemId);
        uint GetItemIcon(uint itemId);
        ItemUICategory GetItemCategory(uint itemId);

        // Recipe queries
        Recipe? GetRecipe(uint recipeId);
        List<Recipe> GetRecipesByItem(uint itemId);
        List<RecipeIngredient> GetRecipeIngredients(uint recipeId);
        CraftType GetRecipeCraftType(uint recipeId);

        // Character state
        uint GetCurrentJobId();
        string GetCharacterName();
        uint GetHomeWorldId();
        string GetDataCenter();
        CharacterStats GetCharacterStats();

        // Inventory access (real-time)
        unsafe InventoryContainer* GetInventoryContainer(InventoryType type);
        int GetItemCount(uint itemId, bool includeHQ = false, bool includeSaddlebag = false);
        List<InventorySlotInfo> FindItem(uint itemId);

        // Crafting state
        bool IsCrafting();
        CraftingState? GetCurrentCraftingState();

        // Consumables
        List<Item> GetFoodItems();
        List<Item> GetPotionItems();
        List<Item> GetManualItems();

        // Collections (UIState)
        bool IsUnlockLinkUnlocked(uint unlockLink);
        int GetUnlockedMountCount();
        int GetUnlockedMinionCount();
    }

    // Supporting types
    public class RecipeIngredient
    {
        public uint ItemId { get; set; }
        public int Quantity { get; set; }
        public bool RequiresHQ { get; set; }
    }

    public class InventorySlotInfo
    {
        public InventoryType Container { get; set; }
        public int SlotIndex { get; set; }
        public uint ItemId { get; set; }
        public int Quantity { get; set; }
        public bool IsHQ { get; set; }
    }

    public class CharacterStats
    {
        public int Craftsmanship { get; set; }
        public int Control { get; set; }
        public int CP { get; set; }
        public int Level { get; set; }
    }
}
```

### Implementation: DefaultGameDataProvider

```csharp
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;

namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    public class DefaultGameDataProvider : IGameDataProvider
    {
        private readonly IDataManager _dataManager;
        private readonly IClientState _clientState;
        private readonly IPluginLog _log;

        // Cached sheets for performance
        private ExcelSheet<Item>? _itemSheet;
        private ExcelSheet<Recipe>? _recipeSheet;

        public DefaultGameDataProvider(
            IDataManager dataManager,
            IClientState clientState,
            IPluginLog log)
        {
            _dataManager = dataManager;
            _clientState = clientState;
            _log = log;

            // Cache frequently-accessed sheets
            _itemSheet = _dataManager.GetExcelSheet<Item>();
            _recipeSheet = _dataManager.GetExcelSheet<Recipe>();
        }

        public Item? GetItem(uint itemId)
        {
            return _itemSheet?.GetRow(itemId);
        }

        public string GetItemName(uint itemId)
        {
            var item = GetItem(itemId);
            return item?.Name?.ToString() ?? $"Unknown Item {itemId}";
        }

        public List<RecipeIngredient> GetRecipeIngredients(uint recipeId)
        {
            var recipe = _recipeSheet?.GetRow(recipeId);
            if (recipe == null) return new List<RecipeIngredient>();

            var ingredients = new List<RecipeIngredient>();

            for (int i = 0; i < 10; i++)  // Recipes have up to 10 ingredients
            {
                var itemId = recipe.UnkData5[i].ItemIngredient;
                var quantity = recipe.UnkData5[i].AmountIngredient;

                if (itemId == 0 || quantity == 0) continue;

                ingredients.Add(new RecipeIngredient
                {
                    ItemId = itemId,
                    Quantity = quantity,
                    RequiresHQ = false  // TODO: Detect HQ requirement
                });
            }

            return ingredients;
        }

        public unsafe int GetItemCount(uint itemId, bool includeHQ = false, bool includeSaddlebag = false)
        {
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return 0;

            int total = 0;

            // Check character inventory (4 bags)
            for (int bag = 0; bag < 4; bag++)
            {
                var container = inventoryManager->GetInventoryContainer((InventoryType)bag);
                total += CountItemInContainer(container, itemId, includeHQ);
            }

            // Check saddlebags if requested
            if (includeSaddlebag)
            {
                var saddlebag1 = inventoryManager->GetInventoryContainer(InventoryType.SaddleBag1);
                var saddlebag2 = inventoryManager->GetInventoryContainer(InventoryType.SaddleBag2);
                total += CountItemInContainer(saddlebag1, itemId, includeHQ);
                total += CountItemInContainer(saddlebag2, itemId, includeHQ);
            }

            return total;
        }

        private unsafe int CountItemInContainer(InventoryContainer* container, uint itemId, bool includeHQ)
        {
            if (container == null) return 0;

            int count = 0;

            for (int i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null || slot->ItemID == 0) continue;

                if (slot->ItemID == itemId)
                {
                    if (!includeHQ && slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ))
                        continue;

                    count += slot->Quantity;
                }
            }

            return count;
        }

        // ... implement remaining interface methods
    }
}
```

### Mock Implementation for Testing

```csharp
public class MockGameDataProvider : IGameDataProvider
{
    private readonly Dictionary<uint, Item> _mockItems = new();
    private readonly Dictionary<uint, Recipe> _mockRecipes = new();

    public void AddMockItem(uint id, string name, uint icon = 0)
    {
        _mockItems[id] = new Item { RowId = id, Name = name, Icon = icon };
    }

    public void AddMockRecipe(uint id, uint resultItemId, List<RecipeIngredient> ingredients)
    {
        _mockRecipes[id] = new Recipe
        {
            RowId = id,
            ItemResult = new LazyRow<Item> { Row = resultItemId },
            // ... mock ingredient data
        };
    }

    public Item? GetItem(uint itemId)
    {
        return _mockItems.TryGetValue(itemId, out var item) ? item : null;
    }

    public string GetItemName(uint itemId)
    {
        return _mockItems.TryGetValue(itemId, out var item)
            ? item.Name
            : $"Mock Item {itemId}";
    }

    // ... implement all interface methods with mock data
}
```

---

## Abstraction Layer 2: IRepositoryIntegration

### Interface Design

**Purpose:** Abstract access to Akadaemia Anyder's local SQLite database

```csharp
namespace AkadaemiaAnyder.Modules.Artisan.Core
{
    /// <summary>
    /// Integration with Akadaemia Anyder database for privacy-first local storage
    /// </summary>
    public interface IRepositoryIntegration
    {
        // Material availability (replaces Universalis)
        MaterialAvailability GetMaterialAvailability(uint itemId);
        List<MaterialLocation> FindMaterialLocations(uint itemId);
        void RefreshInventorySnapshot();  // Trigger rescan

        // Collection queries
        bool IsItemCollected(uint itemId, CollectionType type);
        CollectionProgress GetCollectionProgress(CollectionType type);
        List<uint> GetMissingCollectionItems(CollectionType type);

        // Crafting list persistence (local database, not configuration)
        void SaveCraftingList(CraftingListData list);
        CraftingListData? LoadCraftingList(string listId);
        List<CraftingListData> LoadAllCraftingLists();
        void DeleteCraftingList(string listId);

        // Recipe tracking
        List<uint> GetCraftedRecipes();
        void RecordCraftedRecipe(uint recipeId, int quantity, bool wasHQ);
        CraftingHistory GetCraftingHistory(uint recipeId);

        // Session tracking
        void StartCraftingSession();
        void EndCraftingSession();
        List<CraftingSessionSummary> GetRecentSessions(int days);

        // Fishing/gathering logs
        List<FishingLogEntry> GetFishingLog(uint? spotId = null, int? lastDays = null);
        List<GatheringLogEntry> GetGatheringLog(uint? nodeId = null, int? lastDays = null);

        // Statistics
        int GetTotalItemsCrafted();
        int GetTotalItemsGathered();
        int GetTotalFishCaught();
        Dictionary<uint, int> GetMostCraftedRecipes(int topN = 10);
    }
}
```

### Real Implementation: AkadaemiaAnyderRepository

```csharp
using Microsoft.EntityFrameworkCore;

namespace AkadaemiaAnyder.Core.Database
{
    public class AkadaemiaAnyderRepository : IRepositoryIntegration
    {
        private readonly PrivacyDatabaseContext _db;
        private readonly IPluginLog _log;

        public AkadaemiaAnyderRepository(PrivacyDatabaseContext db, IPluginLog log)
        {
            _db = db;
            _log = log;
        }

        public MaterialAvailability GetMaterialAvailability(uint itemId)
        {
            // Query latest inventory snapshots
            var snapshots = _db.InventorySnapshots
                .Where(s => s.ItemId == itemId)
                .ToList();

            var availability = new MaterialAvailability
            {
                ItemId = itemId,
                InInventory = snapshots.Where(s => s.Location == "inventory").Sum(s => s.Quantity),
                InSaddlebag = snapshots.Where(s => s.Location == "saddlebag").Sum(s => s.Quantity),
                InRetainers = snapshots.Where(s => s.Location.StartsWith("retainer_")).Sum(s => s.Quantity)
            };

            // Build location breakdown
            foreach (var group in snapshots.GroupBy(s => s.Location))
            {
                availability.ByLocation[group.Key] = group.Sum(s => s.Quantity);
            }

            return availability;
        }

        public List<MaterialLocation> FindMaterialLocations(uint itemId)
        {
            return _db.InventorySnapshots
                .Where(s => s.ItemId == itemId && s.Quantity > 0)
                .Select(s => new MaterialLocation
                {
                    Location = s.Location,
                    SlotId = s.SlotId,
                    Quantity = s.Quantity,
                    IsHQ = s.IsHQ
                })
                .ToList();
        }

        public void SaveCraftingList(CraftingListData list)
        {
            var existing = _db.CraftingLists.Find(list.Id);

            if (existing != null)
            {
                // Update existing
                existing.Name = list.Name;
                existing.Items = list.Items;
                existing.LastModified = DateTime.UtcNow;
            }
            else
            {
                // Insert new
                list.Created = DateTime.UtcNow;
                list.LastModified = DateTime.UtcNow;
                _db.CraftingLists.Add(list);
            }

            _db.SaveChanges();
            _log.Info($"[Repository] Saved crafting list: {list.Name} ({list.Items.Count} items)");
        }

        public void RecordCraftedRecipe(uint recipeId, int quantity, bool wasHQ)
        {
            var history = _db.CraftingHistory.FirstOrDefault(h => h.RecipeId == recipeId);

            if (history == null)
            {
                history = new CraftingHistory
                {
                    RecipeId = recipeId,
                    TotalCrafted = 0,
                    HQCount = 0,
                    FirstCrafted = DateTime.UtcNow
                };
                _db.CraftingHistory.Add(history);
            }

            history.TotalCrafted += quantity;
            if (wasHQ) history.HQCount += quantity;
            history.LastCrafted = DateTime.UtcNow;

            _db.SaveChanges();
        }

        public CollectionProgress GetCollectionProgress(CollectionType type)
        {
            var latest = _db.CollectionSnapshots
                .Where(s => s.CollectionType == type)
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault();

            if (latest == null)
            {
                return new CollectionProgress
                {
                    CollectionType = type,
                    Unlocked = 0,
                    Total = 0
                };
            }

            return new CollectionProgress
            {
                CollectionType = type,
                Unlocked = latest.UnlockedCount,
                Total = latest.TotalCount,
                Percentage = latest.TotalCount > 0
                    ? (double)latest.UnlockedCount / latest.TotalCount * 100
                    : 0
            };
        }

        // ... implement remaining interface methods
    }
}
```

---

## Dependency Injection Strategy

### Plugin Initialization

```csharp
// ArtisanPlugin.cs
public sealed class ArtisanPlugin : IDalamudPlugin
{
    private readonly IGameDataProvider _gameData;
    private readonly IRepositoryIntegration _repository;
    private readonly PrivacyDatabaseContext _db;

    public ArtisanPlugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] IDataManager dataManager,
        [RequiredVersion("1.0")] IClientState clientState,
        [RequiredVersion("1.0")] IPluginLog log)
    {
        // Initialize database
        _db = new PrivacyDatabaseContext();
        _db.Database.Migrate();  // Apply schema migrations

        // Initialize abstraction layers
        _gameData = new DefaultGameDataProvider(dataManager, clientState, log);
        _repository = new AkadaemiaAnyderRepository(_db, log);

        // Initialize components with injected dependencies
        PluginUI = new PluginUI(_gameData, _repository, Configuration, log);
        CraftingProcessor = new CraftingProcessor(_gameData, _repository, log);
        ConsumableChecker = new ConsumableChecker(_gameData, log);

        // ... initialize other components
    }

    public void Dispose()
    {
        _db?.Dispose();
        // ... dispose other resources
    }
}
```

### Constructor Injection Pattern

**Before (Direct Dependencies):**
```csharp
public class RecipeWindowUI
{
    public RecipeWindowUI()
    {
        // Tightly coupled to global Service class
    }

    public void DrawCost(uint recipeId)
    {
        var recipe = Service.DataManager.GetExcelSheet<Recipe>()?.GetRow(recipeId);
        var pricing = await UniversalisClient.GetMarketBoardDataAsync(itemId);
        // ...
    }
}
```

**After (Injected Dependencies):**
```csharp
public class RecipeWindowUI
{
    private readonly IGameDataProvider _gameData;
    private readonly IRepositoryIntegration _repository;
    private readonly IPluginLog _log;

    public RecipeWindowUI(
        IGameDataProvider gameData,
        IRepositoryIntegration repository,
        IPluginLog log)
    {
        _gameData = gameData;
        _repository = repository;
        _log = log;
    }

    public void DrawCost(uint recipeId)
    {
        var recipe = _gameData.GetRecipe(recipeId);
        var ingredients = _gameData.GetRecipeIngredients(recipeId);

        foreach (var ingredient in ingredients)
        {
            var availability = _repository.GetMaterialAvailability(ingredient.ItemId);

            ImGui.Text($"{_gameData.GetItemName(ingredient.ItemId)} ×{ingredient.Quantity}");
            ImGui.SameLine();

            if (availability.Total >= ingredient.Quantity)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"✓ Have {availability.Total}");
            }
            else
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Need {ingredient.Quantity - availability.Total} more");
            }
        }
    }
}
```

---

## Performance Comparison: Local DB vs Universalis API

### Universalis API Call

**Latency Breakdown:**
```
DNS lookup:     10-50ms
TCP handshake:  20-100ms
TLS handshake:  50-150ms
HTTP request:   10-30ms
API processing: 50-200ms
HTTP response:  10-30ms
JSON parsing:   5-15ms
━━━━━━━━━━━━━━━━━━━━━━━━
Total:          155-575ms
```

**For 10 ingredients:** 1.5-5.75 seconds
**Cache TTL:** 5 minutes (Universalis recommendation)

### Local SQLite Query

**Latency Breakdown:**
```
Index lookup:   0.1-0.5ms
Row scan:       0.1-0.3ms
Data return:    0.05-0.1ms
━━━━━━━━━━━━━━━━━━━━━━━━
Total:          0.25-0.9ms
```

**For 10 ingredients:** 2.5-9ms
**Cache:** Unnecessary (instant queries)

**Performance Gain: 170-640× faster**

### Memory Usage

**Universalis Approach:**
- HTTP client pool: ~50 KB per client
- JSON deserialization: ~5 KB per response
- Cache storage: ~20 KB per item (5-minute TTL)
- **Total for 100 items:** ~2.5 MB

**Local Database Approach:**
- SQLite connection: ~500 KB
- Query result cache: ~1 KB per item
- Index memory: ~50 KB
- **Total for 100 items:** ~650 KB

**Memory Savings: ~75% reduction**

---

## Caching Strategies

### Inventory Snapshot Caching

**Problem:** Real-time inventory queries are expensive (memory access)

**Solution:** Periodic snapshots + change detection

```csharp
public class InventorySnapshotManager
{
    private DateTime _lastSnapshot = DateTime.MinValue;
    private readonly TimeSpan _snapshotInterval = TimeSpan.FromSeconds(30);

    public void OnFrameworkUpdate()
    {
        if (DateTime.UtcNow - _lastSnapshot > _snapshotInterval)
        {
            RefreshSnapshot();
            _lastSnapshot = DateTime.UtcNow;
        }
    }

    private unsafe void RefreshSnapshot()
    {
        // Scan all inventory containers
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return;

        // Update database with current inventory state
        foreach (var container in GetAllContainers())
        {
            UpdateContainerSnapshot(container);
        }
    }

    // Force immediate refresh
    public void ForceRefresh()
    {
        RefreshSnapshot();
        _lastSnapshot = DateTime.UtcNow;
    }
}
```

**Trade-offs:**
- ✅ Reduces memory access overhead (1 scan per 30s vs per query)
- ✅ Database queries are instant (indexed)
- ⚠️ Snapshot may be up to 30s stale
- ⚠️ Must force refresh before crafting (ensure accurate material counts)

### Recipe Data Caching

**Problem:** Recipe sheets are large (~3,000 recipes)

**Solution:** Lazy loading + in-memory cache

```csharp
public class CachedGameDataProvider : IGameDataProvider
{
    private readonly IGameDataProvider _inner;
    private readonly Dictionary<uint, Recipe> _recipeCache = new();
    private readonly Dictionary<uint, List<RecipeIngredient>> _ingredientCache = new();

    public CachedGameDataProvider(IGameDataProvider inner)
    {
        _inner = inner;
    }

    public Recipe? GetRecipe(uint recipeId)
    {
        if (_recipeCache.TryGetValue(recipeId, out var cached))
            return cached;

        var recipe = _inner.GetRecipe(recipeId);
        if (recipe != null)
            _recipeCache[recipeId] = recipe;

        return recipe;
    }

    public List<RecipeIngredient> GetRecipeIngredients(uint recipeId)
    {
        if (_ingredientCache.TryGetValue(recipeId, out var cached))
            return cached;

        var ingredients = _inner.GetRecipeIngredients(recipeId);
        _ingredientCache[recipeId] = ingredients;

        return ingredients;
    }
}
```

**Benefits:**
- ✅ First access: Normal speed
- ✅ Subsequent accesses: Instant (in-memory lookup)
- ✅ Memory footprint: ~200 KB for 100 cached recipes

---

## Database Schema Design

### Core Tables

**1. Inventory Snapshots**
```sql
CREATE TABLE inventory_snapshot (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,
    location TEXT NOT NULL,      -- "inventory", "saddlebag", "retainer_1"
    slot_id INTEGER NOT NULL,
    item_id INTEGER NOT NULL,
    quantity INTEGER NOT NULL,
    is_hq INTEGER NOT NULL DEFAULT 0,
    spiritbond INTEGER DEFAULT 0,
    condition INTEGER DEFAULT 30000,

    -- Privacy: NO character_name, NO retainer_name

    UNIQUE(location, slot_id)  -- One entry per slot (upsert on refresh)
);

CREATE INDEX idx_inventory_item ON inventory_snapshot(item_id);
CREATE INDEX idx_inventory_location ON inventory_snapshot(location);
```

**2. Crafting Lists**
```sql
CREATE TABLE crafting_list (
    id TEXT PRIMARY KEY,          -- GUID
    name TEXT NOT NULL,
    created INTEGER NOT NULL,
    last_modified INTEGER NOT NULL,

    -- Privacy: NO character_name, NO user_id
);

CREATE TABLE crafting_list_item (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    list_id TEXT NOT NULL,
    recipe_id INTEGER NOT NULL,
    quantity INTEGER NOT NULL,
    quantity_crafted INTEGER NOT NULL DEFAULT 0,

    FOREIGN KEY (list_id) REFERENCES crafting_list(id) ON DELETE CASCADE
);

CREATE INDEX idx_list_items ON crafting_list_item(list_id);
```

**3. Crafting History**
```sql
CREATE TABLE crafting_history (
    recipe_id INTEGER PRIMARY KEY,
    total_crafted INTEGER NOT NULL DEFAULT 0,
    hq_count INTEGER NOT NULL DEFAULT 0,
    first_crafted INTEGER NOT NULL,
    last_crafted INTEGER NOT NULL

    -- Privacy: NO character_name
);
```

**4. Collection Snapshots**
```sql
CREATE TABLE collection_snapshot (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,
    collection_type TEXT NOT NULL,  -- "Mount", "Minion", etc.
    unlocked_count INTEGER NOT NULL,
    total_count INTEGER NOT NULL

    -- Privacy: NO character_name
);

CREATE INDEX idx_collection_type ON collection_snapshot(collection_type, timestamp);
```

**5. Fishing Logs**
```sql
CREATE TABLE fishing_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,
    item_id INTEGER NOT NULL,
    spot_id INTEGER NOT NULL,     -- Spot ID, NOT GPS coordinates
    weather_id INTEGER NOT NULL,
    bait_id INTEGER NOT NULL,
    bite_time_ms INTEGER NOT NULL,
    size INTEGER NOT NULL,
    is_hq INTEGER NOT NULL DEFAULT 0

    -- Privacy: NO character_name, NO gps_x/gps_y, NO player_stats
);

CREATE INDEX idx_fishing_item ON fishing_log(item_id);
CREATE INDEX idx_fishing_spot ON fishing_log(spot_id);
CREATE INDEX idx_fishing_timestamp ON fishing_log(timestamp);
```

### Migration Strategy

**Using Entity Framework Core Migrations:**

```bash
# Create initial migration
dotnet ef migrations add InitialSchema

# Apply migration
dotnet ef database update

# Future migrations
dotnet ef migrations add AddFishingLog
dotnet ef database update
```

**Migration File Example:**
```csharp
public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_snapshot",
            columns: table => new
            {
                id = table.Column<long>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                timestamp = table.Column<long>(nullable: false),
                location = table.Column<string>(nullable: false),
                slot_id = table.Column<int>(nullable: false),
                item_id = table.Column<uint>(nullable: false),
                quantity = table.Column<int>(nullable: false),
                is_hq = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inventory_snapshot", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_inventory_item",
            table: "inventory_snapshot",
            column: "item_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inventory_snapshot");
    }
}
```

---

## Integration Points in Artisan Code

### Where to Inject IGameDataProvider

**File-by-file injection plan:**

| File | Current Access | New Access | Effort |
|------|----------------|------------|--------|
| `UI/RecipeWindowUI.cs` | Service.DataManager | _gameData.GetRecipe() | 1-2 hours |
| `UI/ListEditor.cs` | Service.DataManager | _gameData.GetItem() | 1-2 hours |
| `CraftingLogic/CraftingProcessor.cs` | Direct Lumina | _gameData.GetRecipe() | 2-3 hours |
| `Autocraft/ConsumableChecker.cs` | Service.DataManager | _gameData.GetFoodItems() | 1 hour |
| `RawInformation/MemoryHelper.cs` | Static methods | Instance methods | 2-3 hours |

**Total Injection Effort:** 7-11 hours

### Where to Inject IRepositoryIntegration

| File | Purpose | Usage | Effort |
|------|---------|-------|--------|
| `UI/RecipeWindowUI.cs` | Material availability display | _repository.GetMaterialAvailability() | 1 hour |
| `UI/ListEditor.cs` | Total material summary | _repository.FindMaterialLocations() | 1 hour |
| `CraftingList/CraftingList.cs` | Persist lists to DB | _repository.SaveCraftingList() | 2 hours |
| `CraftingLogic/CraftingProcessor.cs` | Record crafted recipes | _repository.RecordCraftedRecipe() | 1 hour |

**Total Injection Effort:** 5 hours

---

## Testing Database Integration

### Unit Test Example

```csharp
[Fact]
public void GetMaterialAvailability_WithInventoryAndSaddlebag_ReturnsCombinedTotal()
{
    // Arrange
    using var db = CreateTestDatabase();
    db.InventorySnapshots.AddRange(
        new InventorySnapshot
        {
            Location = "inventory",
            SlotId = 10,
            ItemId = 5333,  // Titanium Ore
            Quantity = 15
        },
        new InventorySnapshot
        {
            Location = "saddlebag",
            SlotId = 5,
            ItemId = 5333,
            Quantity = 8
        }
    );
    db.SaveChanges();

    var repository = new AkadaemiaAnyderRepository(db, mockLog);

    // Act
    var availability = repository.GetMaterialAvailability(5333);

    // Assert
    Assert.Equal(15, availability.InInventory);
    Assert.Equal(8, availability.InSaddlebag);
    Assert.Equal(23, availability.Total);
}
```

### Integration Test

```csharp
[Fact]
public void RecipeWindowUI_WithLocalMaterials_DisplaysAvailability()
{
    // Arrange
    var mockGameData = new MockGameDataProvider();
    mockGameData.AddMockRecipe(1, 5334, new List<RecipeIngredient>
    {
        new RecipeIngredient { ItemId = 5333, Quantity = 10 }
    });

    var mockRepo = new MockRepositoryIntegration();
    mockRepo.SetMaterialAvailability(5333, new MaterialAvailability
    {
        ItemId = 5333,
        InInventory = 15,
        InSaddlebag = 8,
        InRetainers = 0
    });

    var ui = new RecipeWindowUI(mockGameData, mockRepo, mockLog);

    // Act
    ui.DrawCost(recipeId: 1);

    // Assert
    // Verify ImGui rendered "Have 23" (not Universalis pricing)
    // This requires ImGui testing framework or manual verification
}
```

---

## Performance Optimization

### Query Optimization

**Problem:** N+1 queries when loading crafting list with 50 items

**Bad:**
```csharp
foreach (var item in list.Items)
{
    var availability = _repository.GetMaterialAvailability(item.RecipeId);  // 1 query per item
}
// Total: 50 queries
```

**Good:**
```csharp
var itemIds = list.Items.SelectMany(i => GetRecipeIngredients(i.RecipeId))
                        .Select(ing => ing.ItemId)
                        .Distinct()
                        .ToList();

var availabilities = _repository.GetMaterialAvailabilityBatch(itemIds);  // 1 query

foreach (var item in list.Items)
{
    var ingredients = GetRecipeIngredients(item.RecipeId);
    foreach (var ing in ingredients)
    {
        var availability = availabilities[ing.ItemId];  // In-memory lookup
    }
}
// Total: 1 query
```

**Batch Query Implementation:**
```csharp
public Dictionary<uint, MaterialAvailability> GetMaterialAvailabilityBatch(List<uint> itemIds)
{
    var snapshots = _db.InventorySnapshots
        .Where(s => itemIds.Contains(s.ItemId))
        .ToList();  // Single query

    return itemIds.ToDictionary(
        itemId => itemId,
        itemId => new MaterialAvailability
        {
            ItemId = itemId,
            InInventory = snapshots.Where(s => s.ItemId == itemId && s.Location == "inventory").Sum(s => s.Quantity),
            InSaddlebag = snapshots.Where(s => s.ItemId == itemId && s.Location == "saddlebag").Sum(s => s.Quantity),
            InRetainers = snapshots.Where(s => s.ItemId == itemId && s.Location.StartsWith("retainer_")).Sum(s => s.Quantity)
        }
    );
}
```

---

## Recommendations

### For Akadaemia Anyder Fork:

1. **Use abstraction layers from Day 1**
   - Don't copy Artisan's direct Service.DataManager usage
   - Inject IGameDataProvider and IRepositoryIntegration everywhere

2. **Keep database queries simple**
   - Use EF Core LINQ (don't hand-write SQL unless performance-critical)
   - Index aggressively (item_id, timestamp, location)
   - Profile with realistic data (10k+ fishing logs)

3. **Cache strategically**
   - Cache recipe data (static game data)
   - Snapshot inventory periodically (30s interval)
   - Don't cache crafting state (changes every action)

4. **Test with mocks**
   - Create MockGameDataProvider and MockRepositoryIntegration
   - Unit test all UI components with mocks
   - Integration test with real database (in-memory SQLite)

5. **Measure performance**
   - Benchmark GetMaterialAvailability() with 1k items
   - Profile UI rendering with large crafting lists (100+ items)
   - Monitor database file size growth

---

**End of Database Integration Deep Dive**

Key takeaway: Abstraction layers provide testability, privacy enforcement, and performance. Inject dependencies everywhere, use batch queries, cache strategically.
