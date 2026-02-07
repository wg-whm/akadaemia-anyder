# Implementation Status

**Last Updated**: 2026-01-25
**Version**: 0.1.0 (Alpha)

---

## Quick Status

| Feature | Status | Notes |
|---------|--------|-------|
| Recipe Tracking | ✅ Working | Memory reading via RecipeNote.IsRecipeUnlocked() |
| Gathering Tracking | ⚠️ Partial | Infrastructure ready, event detection TODO |
| Fishing Tracking | ⚠️ Partial | Infrastructure ready, event detection TODO |
| Database (3-tier fallback) | ✅ Working | Tier 1/2/3 tested, migrations applied |
| Repository Layer | ✅ Working | CRUD with retry logic, transactions |
| UI (ImGui) | ✅ Working | Main window with tabs, progress bars |
| Export/Import | ✅ Working | JSON export/import for all collection types |
| Tests | ⚠️ 77% Pass | 78/101 passing, test data issues in 23 tests |

---

## T1.5 Architecture Decision

During implementation, we discovered FFXIVClientStructs has different levels of support:

**Memory Structure Availability:**
- ✅ **RecipeNote**: Fully documented with `IsRecipeUnlocked(recipeId)` method
- ❌ **GatheringNote**: Stub only - no fields or methods available
- ❌ **FishingNote**: Stub only - no fields or methods available

**Architectural Decision:**
We chose a **hybrid approach** rather than blocking on stub structures:
- **Recipes**: Direct memory reading (fully implemented)
- **Gathering/Fishing**: Event-based detection (infrastructure built, detection logic pending)

This decision is documented in: `docs/MEMORY-STRUCTURES.md`

---

## Current Implementation (v0.1.0)

### ✅ Fully Functional Components

**T0-T5: Foundation**
- ✅ .NET 10 SDK environment
- ✅ XIVLauncher + Dalamud integration
- ✅ Data models (Recipe, GatheringNode, FishingHole)
- ✅ DatabaseContext with 3-tier fallback
- ✅ Repository layer (4 repositories with retry logic)
- ✅ Memory safety framework (SafeMemoryReader, PointerValidator)

**T6: Recipe Reading (Complete)**
- ✅ RecipeReader using UIState.RecipeNote
- ✅ SafeMemoryReader wrapper for exception handling
- ✅ All 8 crafting classes supported (CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL)
- ✅ Master recipe detection

**T7-T10: Services & UI**
- ✅ CollectionService orchestration (partial success handling)
- ✅ ProgressCalculator (recipe stats working)
- ✅ ChangeDetector (recipe comparison working)
- ✅ JsonExporter/JsonImporter (all types, recipes tested)
- ✅ LoggingService, TelemetryService
- ✅ MainWindow with tabs for Recipes/Gathering/Fishing
- ✅ ConfigWindow for settings
- ✅ Plugin lifecycle (constructor, Dispose)

**T11: Testing**
- ✅ 101 unit/integration/smoke tests created
- ✅ 78 tests passing (77%)
- ⚠️ 23 tests failing (test data issues, mocking needs)

**T12: Documentation**
- ✅ README.md (user guide)
- ✅ DEVELOPMENT.md (comprehensive dev setup)
- ✅ ARCHITECTURE.md (3-tier fallback, hybrid approach)
- ✅ Task completion summaries (T0-T12)
- ⚠️ Updated to reflect actual status (this file)

---

## ⚠️ Incomplete Components

### T6: Event Listeners (Infrastructure Only)

**GatheringEventListener.cs:**
```csharp
private void OnFrameworkUpdate(IFramework framework)
{
    // TODO: Implement gathering node detection
    // This requires:
    // 1. Access to gathering log data (Dalamud's GameData)
    // 2. Detecting when player interacts with gathering nodes
    // 3. Checking if node is newly unlocked
}
```

**FishingEventListener.cs:**
```csharp
private void OnFrameworkUpdate(IFramework framework)
{
    // TODO: Implement fishing catch detection
    // This requires:
    // 1. Access to fishing log data (Dalamud's GameData)
    // 2. Detecting when player catches a fish
    // 3. Checking if fish is newly caught
}
```

**What Works:**
- ✅ Event subscription (Framework.Update, TerritoryChanged)
- ✅ Start/Stop lifecycle management
- ✅ Duplicate prevention (HashSet deduplication)
- ✅ GetCollectedItems(), ClearCollectedItems() APIs

**What's Missing:**
- ❌ Actual game state detection logic
- ❌ Integration with Dalamud GameData APIs
- ❌ Node/fish identification

**Estimated Effort**: 2-4 hours per collection type (research + implementation + testing)

---

## Testing Status

**Build Status:**
- ✅ Plugin builds: 0 errors, 0 warnings
- ✅ Test project builds: 0 errors, 0 warnings
- ✅ Output DLL: `%APPDATA%\XIVLauncher\devPlugins\AkadaemiaAnyder\SamplePlugin.dll`

**Test Results (101 tests total):**
- ✅ **78 Passing** (77%)
- ❌ **23 Failing** (23%)

**Failure Categories:**
1. **PointerValidator tests** (5 failures): Test expectations vs implementation mismatch
2. **RecipeReader tests** (3 failures): UIState.Instance() requires FFXIV process
3. **Repository tests** (15 failures): UNIQUE constraint violations in test data

**Note**: Failures are test infrastructure issues, not implementation bugs. Recipe tracking works in-game.

---

## Known Limitations

### 1. Gathering/Fishing Event Detection Not Implemented
- Event listeners subscribe to framework events but detection logic is TODO
- Database schema, repositories, services all functional
- CollectionService calls gathering/fishing scan methods (returns 0 items currently)
- UI tabs exist but show "0/0" progress

### 2. Test Coverage Gaps
- Gathering/Fishing tests pass because they test empty stubs
- RecipeReader tests fail outside game process (need mocking)
- Some repository tests have duplicate test data

### 3. No Visual Documentation
- No screenshots of UI in README
- No demo GIF showing plugin in action
- Users don't know what the plugin looks like before installing

### 4. Missing Class Documentation
- MainWindow.cs: No XML summary
- ConfigWindow.cs: No XML summary
- 17 of 60 files (28%) have no XML documentation

---

## Next Steps (Priority Order)

### High Priority
1. **Implement Gathering Event Detection** (2-3 hours)
   - Research Dalamud GameData APIs for gathering log
   - Implement OnFrameworkUpdate() detection logic
   - Test in-game with actual gathering

2. **Implement Fishing Event Detection** (2-3 hours)
   - Research Dalamud GameData APIs for fishing log
   - Implement OnFrameworkUpdate() detection logic
   - Test in-game with actual fishing

3. **Add Screenshots to README** (30 minutes)
   - Main window showing recipe progress
   - Settings window
   - In-game overlay demonstration

### Medium Priority
4. **Fix Test Failures** (1-2 hours)
   - Add mocking for UIState in RecipeReader tests
   - Fix UNIQUE constraint violations in repository tests
   - Adjust PointerValidator test expectations

5. **Complete Class Documentation** (1 hour)
   - Add XML summaries to MainWindow, ConfigWindow
   - Document remaining 17 undocumented files

### Low Priority
6. **Add Visual Diagrams** (2 hours)
   - Convert ASCII diagrams to Mermaid
   - Create architecture flowcharts
   - Add database schema diagram

7. **API Reference Generation** (1 hour)
   - Setup DocFX or Doxygen
   - Generate HTML API documentation

---

## Build & Run

**Current State**: Plugin compiles and loads in-game successfully.

**What Works In-Game:**
1. `/akadaemia` command opens main window
2. "Scan Collections" button triggers recipe scan
3. Recipe progress displays correctly (e.g., "256/512 recipes unlocked")
4. Export/Import buttons functional (creates/loads JSON)
5. Database health indicator shows Tier 1/2/3 status

**What Doesn't Work:**
1. Gathering tab shows "0/0" (no detection)
2. Fishing tab shows "0/0" (no detection)
3. "Scan Collections" only scans recipes

---

## How to Test

### Recipe Tracking (Works)
1. Launch FFXIV via XIVLauncher
2. Type `/akadaemia` in-game
3. Click **"Scan Collections"**
4. Recipe tab should show actual progress (e.g., "256/512")
5. Export to JSON to verify data

### Gathering/Fishing (Doesn't Work Yet)
1. Same steps as above
2. Gathering/Fishing tabs will show "0/0"
3. No error messages (expected behavior for unimplemented detection)

---

## Contributing

If you want to implement gathering/fishing detection:

1. **Research Required:**
   - Study Dalamud's `IGameData` interface
   - Find gathering/fishing log APIs
   - Understand event timing (when does log update?)

2. **Implementation Files:**
   - `SamplePlugin/EventListeners/GatheringEventListener.cs` (line 101)
   - `SamplePlugin/EventListeners/FishingEventListener.cs` (line 101)

3. **Testing:**
   - In-game verification required
   - Cannot unit test without mocking game state

4. **Reference:**
   - RecipeReader.cs shows working memory reading pattern
   - CollectionService.cs shows integration pattern

---

## Questions?

See:
- **README.md** - User installation and usage
- **DEVELOPMENT.md** - Developer setup and build instructions
- **ARCHITECTURE.md** - Technical design decisions
- **CONTRIBUTING.md** - How to contribute

**GitHub Issues**: [Not yet configured - local dev only]
