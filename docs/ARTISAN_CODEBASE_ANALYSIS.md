# ARTISAN CODEBASE ANALYSIS FOR AKADAEMIA ANYDER FORK

**Analysis Date:** 2026-01-26
**Repository:** https://github.com/PunishXIV/Artisan
**License:** BSD-3-Clause (Puni.sh, 2023)
**Stars:** 214 | **Forks:** 202

---

## EXECUTIVE SUMMARY

**Artisan** is a well-structured, actively-maintained FFXIV crafting automation plugin with **moderately complex** architecture but **low integration risk** for forking into Akadaemia Anyder.

### Key Findings:

1. **Privacy Concerns: CRITICAL** - Three modules must be removed:
   - **Universalis API client** (market board pricing)
   - **Discord webhook integration** (notification system)
   - **Teamcraft import/export** (list sharing)

2. **Code Quality: GOOD** - Clean separation of concerns with loosely-coupled modules

3. **Integration Complexity: MODERATE** - UI is hierarchical and extensible; crafting logic is isolated

4. **Estimated Privacy Retrofit Effort: 6-8 hours** - Straightforward code removal with no deep dependencies

5. **Recommended Approach: SHALLOW INTEGRATION** - Copy crafting logic modules; replace privacy-sensitive components with local-only equivalents

---

## 1. PROJECT STRUCTURE

### Directory Layout

```
Artisan/
├── Artisan.cs                      # Main plugin entry point
├── Configuration.cs                # Settings management
├── Artisan.csproj                 # Project file + dependencies
│
├── Autocraft/                      # Automated crafting features
│   ├── ConsumableChecker.cs       # Food/potion/manual management
│   ├── Endurance.cs               # Endurance mode (repeat crafting)
│   ├── RepairManager.cs           # Gear repair automation
│   └── Throttler.cs
│
├── CraftingLogic/                 # Core crafting simulation & solvers
│   ├── CraftingProcessor.cs       # Main solver orchestration
│   ├── Simulator.cs               # Crafting state machine (26KB)
│   ├── Solvers/                   # 6 solver implementations
│   │   ├── ExpertSolver.cs        # Dynamic heuristic solver (60KB)
│   │   ├── MacroSolver.cs
│   │   ├── RaphaelSolver.cs       # External solver integration
│   │   ├── StandardSolver.cs
│   │   ├── ScriptSolver.cs        # User-defined rotation scripts
│   │   └── ProgressOnlySolver.cs
│
├── CraftingList/                  # List management & import/export
│   ├── CraftingList.cs            # List data structure (27KB)
│   ├── CraftingListUI.cs          # UI for list management
│   └── Teamcraft.cs               # ⚠️ PRIVACY: Teamcraft integration
│
├── GameInterop/                   # Game state & action execution
│   ├── Crafting.cs                # State machine (43KB)
│   ├── PreCrafting.cs             # Pre-craft setup (gear/consumables)
│
├── IPC/                           # Inter-plugin communication
│   ├── IPC.cs                     # Main IPC provider (11 endpoints)
│   ├── AutoRetainerIPC.cs
│   ├── RetainerInfo.cs            # Allagan Tools IPC
│
├── UI/                            # ImGui user interface
│   ├── PluginUI.cs                # Main window with tabs (50KB)
│   ├── ListEditor.cs              # List editing UI (64KB)
│   ├── RecipeWindowUI.cs          # Recipe configuration UI (42KB)
│   ├── SimulatorUI.cs             # Crafting simulator UI (50KB)
│
├── Universalis/                   # ⚠️ PRIVACY: Market data integration
│   ├── UniversalisClient.cs       # HTTP client for pricing API
│   └── MarketboardData.cs         # Price data structures
│
└── RawInformation/                # Game data extraction
    ├── CharacterStats.cs
    ├── LuminaSheets.cs
    └── MemoryHelper.cs
```

### Key Namespace Structure

```
Artisan
├── .Artisan                       # Main plugin class
├── .Autocraft                     # Endurance, repairs, consumables
├── .CraftingLogic                 # Solvers and simulation
│   ├── .Solvers
│   └── .CraftData
├── .CraftingList                  # List management
├── .GameInterop                   # Game state machine
├── .IPC                           # Inter-plugin communication
├── .RawInformation                # Game data extraction
├── .Universalis                   # ⚠️ Market pricing (REMOVE)
└── .UI                            # ImGui interface
```

---

## 2. CORE COMPONENTS ANALYSIS

### 2.1 Crafting Queue Management

**Files**: `CraftingList/CraftingList.cs`, `CraftingListUI.cs`

**How It Works**:
- User creates a `CraftingList` object containing a list of items with quantities
- Each list item references a recipe ID and desired quantity
- Lists are stored in configuration and persisted to `Artisan.json`

**Data Structure**:
```csharp
List<CraftingListItem>  // Each item has:
  - uint RecipeID
  - int Quantity
  - int QuantityCrafted
  - JobID Class (for job-specific items)
  - SolverType (can override per-recipe)
```

**Coupling**: **LOW** - List management is independent of solver/UI

---

### 2.2 Dynamic Solver Integration

**Files**: `CraftingLogic/Solvers/*.cs`, `CraftingProcessor.cs`

**Architecture**:
- `ISolver` interface defines `SolveNextStep(SimulatorState state)` → returns `CraftAction`
- Six solvers implemented with fallback chain:
  1. Script solver (user-defined rotations)
  2. Raphael solver (external AI)
  3. Expert solver (heuristic)
  4. Standard solver (simpler heuristic)
  5. Macro solver (fixed rotation)
  6. ProgressOnly solver (minimal logic)

**ExpertSolver Details** (60 KB - largest):
- Heuristic-based decision engine
- Phases: Opener → Mid-craft → Finisher
- Evaluates: remaining progress, available CP, quality targets, condition exploitation

**Coupling**: **LOW-MODERATE** - Solver interface is clean, simulator is isolated

---

### 2.3 Macro Executor

**Files**: `GameInterop/Crafting.cs` (43 KB state machine)

**State Machine**:
```
IdleNormal → WaitStart → InProgress → WaitAction → WaitFinish
```

**Action Execution Flow**:
1. Solver returns `CraftAction` (enum: Groundwork, StandardTouch, etc.)
2. `Crafting.Execute()` applies action to game state
3. State machine transitions based on game addon data
4. Events: `CraftStarted`, `CraftAdvanced`, `CraftFinished`

**Coupling**: **MODERATE** - Tightly coupled to Dalamud's game addon structures

---

### 2.4 UI Components

**Files**: `UI/*.cs` (350+ KB total)

**Main Window Structure** (`PluginUI.cs` - 50 KB):
- Tabbed interface with left navigation
- Tabs: Overview, Settings, Endurance, Macros, Crafting Lists, Simulator, About

**Key UI Windows**:
1. **ListEditor.cs** (64 KB) - Create/edit crafting lists
2. **SimulatorUI.cs** (50 KB) - Interactive crafting simulator
3. **RecipeWindowUI.cs** (42 KB) - Per-recipe configuration
4. **MacroEditor.cs** (23 KB) - Script editor for custom rotations

**Coupling**: **LOW** - Easy to add new tabs/windows

---

## 3. PRIVACY-SENSITIVE CODE (CRITICAL)

### 3.1 Universalis Market Data Integration ⚠️ HIGH PRIORITY

**Files**: `Universalis/UniversalisClient.cs`, `MarketboardData.cs`

**What It Does**:
- Fetches market pricing data from **Universalis API** (api.universalis.app)
- Used for estimated material costs in list UI
- HTTP GET requests with 10-second timeout

**Network Call Details**:
```csharp
HttpClient GET: https://universalis.app/api/v2/marketboard/{itemID}?datacenter={dcName}

Response: CurrentMinimumPrice, AveragePriceNQ/HQ, AllListings
```

**Data Sent**: Item IDs, player world/DC (inferred from config)

**Risk Level**: **MEDIUM** - Anonymized queries but reveals crafting patterns

**Removal Effort**: **2 hours** - Clean single-file removal

**Action**: Remove `Universalis/` directory entirely; stub out UI components that display pricing

---

### 3.2 Discord Webhook Integration ⚠️ MEDIUM PRIORITY

**Files**: `Configuration.cs` (webhook URL storage)

**Configuration Properties**:
```csharp
public bool UsingDiscordHooks { get; set; }
public string? DiscordWebhookUrl { get; set; }
```

**Risk Level**: **LOW** - User-provided webhook, not automatic telemetry

**Removal Effort**: **1 hour** - Remove config properties and any hook invocations

**Action**: Remove webhook-related code; keep config structure but disable functionality

---

### 3.3 Teamcraft List Import/Export ⚠️ LOW PRIORITY

**Files**: `CraftingList/Teamcraft.cs`

**What It Does**:
- Imports crafting lists from Teamcraft website (teamcraft.fr)
- Users copy/paste list data from website
- Exports lists as Base64-encoded strings

**Privacy Implications**:
- No network calls (user-initiated copy/paste)
- But enables sharing of private crafting lists to external website

**Removal Effort**: **1 hour** - Remove `Teamcraft.cs` file entirely

**Action**: Remove import/export from UI; keep internal list format

---

### 3.4 Character Name/World Tracking ✅ OK TO KEEP

**Data Collected**:
- Player character name (for recipe filtering by class)
- World/DC (for Universalis lookups - will be removed)
- Class/job ID

**Risk Level**: **LOW** - Used for local features, not transmitted

**Action**: OK to retain for local-only use

---

## 4. DEPENDENCIES

### NuGet Packages

| Package | Version | Purpose | Action |
|---------|---------|---------|--------|
| DalamudPackager | 13.1.0 | Plugin build system | Keep |
| **Discord.Net.Webhook** | 3.15.3 | Discord integration | **REMOVE** |
| ECommons | 3.1.0.10 | Common utilities | Keep |
| FuzzySharp | 2.0.2 | Fuzzy string matching | Keep |
| NAudio | 2.2.1 | Sound effects | Keep |
| Microsoft.CodeAnalysis | 4.10.0 | Script compilation | Keep |

### Project References

- **OtterGui** - UI controls library (Keep)
- **PunishLib** - Common utilities (Keep)

### Dalamud Framework References

- Dalamud.dll, FFXIVClientStructs, ImGui bindings, Lumina (All required)

---

## 5. CONFIGURATION SYSTEM

**Files**: `Configuration.cs`, `Artisan.json`

**Configuration Categories**:

1. **Crafting Behavior** - Solver selection, quality targets, failure prediction
2. **Automation Features** - Repair, endurance mode, delays
3. **UI & Display** - Toast notifications, theme, column visibility
4. **Advanced Options** - Retainer management, sounds, solver settings
5. **Data Storage** - Crafting lists, recipe configs, macro cache

**Privacy-Sensitive Config** (REMOVE):
```csharp
public bool UsingDiscordHooks { get; set; }          // ⚠️ REMOVE
public string? DiscordWebhookUrl { get; set; }       // ⚠️ REMOVE
public bool UseUniversalis { get; set; }             // ⚠️ REMOVE
```

---

## 6. INTEGRATION POINTS FOR AKADAEMIA ANYDER

### 6.1 Material Availability Checks

**Current**: Universalis API lookups for pricing

**New Approach**:
```csharp
// Replace Universalis calls with local DB
AkadaemiaAnyder.Database.GetMaterialAvailability(itemID)
  → { InInventory: 10, InSaddlebag: 5, InRetainer: 42 }
```

**Files to Modify**:
- `UI/RecipeWindowUI.cs` - Remove Universalis calls, add local DB calls
- `UI/ListEditor.cs` - Show local material counts instead of market pricing

---

### 6.2 UI Tab Extensions

**Current**: 11 tabs in main window

**New Tabs for Akadaemia**:
1. **Inventory** - Universal search (saddlebags, retainers, all storage)
2. **Collections** - Mounts, minions, fishing/gathering logs
3. **Privacy Settings** - Control what data is stored

**Implementation**:
```csharp
// In PluginUI.cs
enum OpenWindow {
    Overview,
    Settings,
    ...
    Inventory,           // NEW
    Collections,         // NEW
    PrivacySettings,     // NEW
}
```

**Effort**: **2-3 hours** - UI is templated and easy to extend

---

### 6.3 Database Integration Points

**Where to Hook**:

1. **Material lookup** (RecipeConfig, UI display)
   ```csharp
   AkadaemiaAnyder.Repository.GetMaterial(itemID)
   ```

2. **Character inventory** (ConsumableChecker)
   ```csharp
   AkadaemiaAnyder.Database.GetCharacterInventory()
   ```

3. **List persistence** (CraftingList)
   ```csharp
   AkadaemiaAnyder.Repository.SaveCraftingList(list)
   ```

---

## 7. CODE COUPLING ANALYSIS

### Tight Coupling (Refactoring Difficult)

| Component | Reason | Effort |
|-----------|--------|--------|
| `GameInterop/Crafting.cs` | Direct memory reads via FFXIVClientStructs | 8-12 hours |
| `GameInterop/PreCrafting.cs` | Gear switching via task chain | 4-6 hours |

**Why**: These components interact directly with game memory. Refactoring requires deep FFXIV addon knowledge.

### Loose Coupling (Easy to Modify)

| Component | Reason | Effort |
|-----------|--------|--------|
| `CraftingList/*` | Pure data structures + UI | 2-3 hours |
| `CraftingLogic/Simulator.cs` | Isolated state machine | 1-2 hours |
| `UI/*` | ImGui presentation layer | 2-4 hours |
| `Universalis/*` | Single-purpose, no dependencies | 1 hour |

---

## 8. NAMESPACE REFACTORING PLAN

### Current Structure
```
Artisan.*
```

### New Structure (Akadaemia Anyder)
```
AkadaemiaAnyder.Modules.Artisan.*
```

### Files Requiring Updates

**All files** that reference Artisan types (~100+ C# files)

**Tooling**: Use Find & Replace in IDE:
```
Find:    namespace Artisan\.
Replace: namespace AkadaemiaAnyder.Modules.Artisan.

Find:    using static Artisan\.
Replace: using static AkadaemiaAnyder.Modules.Artisan.
```

**Effort**: **2-3 hours** automated, 1-2 hours manual verification

---

## 9. LICENSE COMPLIANCE CHECK

### BSD-3-Clause License (Puni.sh, 2023)

**Requirements**:
1. ✅ Include original copyright notice in source redistributions
2. ✅ Include copyright in binary documentation
3. ⚠️ **Cannot use "Puni.sh" to endorse derived products without permission**

**Compliance Actions**:
1. Keep `LICENCE.md` in fork (rename to `ARTISAN_LICENSE.md`)
2. Add attribution in Akadaemia Anyder README:
   ```markdown
   ## Attribution

   This project includes code from [Artisan](https://github.com/PunishXIV/Artisan),
   licensed under BSD-3-Clause by Puni.sh (2023).
   ```
3. Do NOT mention "Puni.sh" in marketing statements

**Effort**: **1 hour** for compliance documentation

---

## 10. INTEGRATION COMPLEXITY ASSESSMENT

### Complexity Summary

| Factor | Rating | Impact |
|--------|--------|--------|
| Codebase Size | Moderate (350 KB) | Standard refactoring |
| Module Independence | Good | Can copy/adapt selectively |
| Game API Dependencies | High | Must keep FFXIVClientStructs usage |
| **Privacy Cleanup** | **Simple** | **3 files to remove/refactor** |
| UI Integration | Easy | Tabs are templated |
| Database Integration | Moderate | Need abstraction layer |

### Risk Factors

1. **Game Patch Compatibility** - Medium
   - Direct memory offsets break with patches
   - Must maintain against FFXIV patch cycle

2. **Dependency Version Conflicts** - Low
   - All NuGet packages are standard

3. **Performance** - Low
   - Simulator is efficient
   - UI rendering is standard ImGui

---

## PRIVACY RETROFIT ROADMAP

### Phase 1: Identify & Remove (4 hours)

1. **Delete Universalis module**
   ```bash
   rm -rf Universalis/
   ```

2. **Remove Teamcraft integration**
   ```bash
   rm CraftingList/Teamcraft.cs
   ```

3. **Remove Discord webhook config**
   ```csharp
   // In Configuration.cs, delete:
   public bool UsingDiscordHooks { get; set; }
   public string? DiscordWebhookUrl { get; set; }
   ```

4. **Remove Discord.Net.Webhook NuGet package**
   ```bash
   dotnet remove package Discord.Net.Webhook
   ```

### Phase 2: Add Abstraction Layer (6 hours)

1. **Create IGameDataProvider interface**
   ```csharp
   public interface IGameDataProvider {
       Item GetItem(uint itemId);
       Recipe GetRecipe(uint recipeId);
       List<InventoryItem> GetCharacterInventory();
   }
   ```

2. **Create IRepositoryIntegration for Akadaemia DB access**
   ```csharp
   public interface IRepositoryIntegration {
       MaterialAvailability GetMaterialAvailability(uint itemId);
       void SaveCraftingList(CraftingList list);
   }
   ```

3. **Inject dependencies into affected classes**

### Phase 3: UI Integration (4 hours)

1. **Add new tabs** (Inventory, Collections, Privacy Settings)
2. **Replace Universalis pricing with local DB lookups**
3. **Add material availability display from local inventory**

### Phase 4: Testing & Validation (4 hours)

1. **Unit test CraftingLogic components**
2. **Integration test UI tabs**
3. **Verify no external network calls** (use network monitor)

**Total Effort: 18-22 hours**

---

## RECOMMENDED APPROACH

### Integration Strategy: **SHALLOW** (Copy + Modify)

**Why**:
- Deep refactoring of GameInterop is risky and time-consuming
- Better to maintain parallel implementations
- Allows independent patches and updates

**Steps**:
1. Copy entire Artisan codebase into `AkadaemiaAnyder.Modules.Artisan/`
2. Remove privacy-sensitive modules (Universalis, Teamcraft, Discord)
3. Add abstraction layer for game data
4. Inject repository for Akadaemia queries
5. Add new UI tabs for inventory/collections/privacy

**Total Estimated Effort: 30-40 hours**

---

## NEXT STEPS

### Immediate Actions

1. ✅ **Create fork** in Akadaemia Anyder repo structure
2. ✅ **Remove privacy modules** (Universalis, Teamcraft, Discord)
3. ✅ **Refactor namespaces** to AkadaemiaAnyder.Modules.Artisan
4. ✅ **Add abstraction layers** (IGameDataProvider, IRepositoryIntegration)
5. ✅ **Implement new UI tabs** (Inventory, Collections, Privacy)
6. ✅ **Add unit tests** for CraftingLogic
7. ✅ **Create LICENSE attribution** file

### Success Criteria

- [ ] No external network calls (Universalis, Teamcraft, Discord removed)
- [ ] All unit tests pass
- [ ] Privacy settings tab functional
- [ ] Akadaemia database integration working
- [ ] New inventory/collection tabs display correctly
- [ ] Original Artisan functionality preserved
- [ ] License compliance verified

---

**Analysis Complete.** This codebase is suitable for forking with moderate effort required for privacy compliance. Shallow integration approach minimizes risk while maintaining maximum functionality.
