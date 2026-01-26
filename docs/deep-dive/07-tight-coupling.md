# Deep Dive: Tight Coupling Issues

**Topic:** Why shallow integration is recommended over deep refactoring
**Complexity:** High
**Relevance:** Prevents wasted effort on risky refactoring

---

## Coupling Analysis

### Tightly Coupled Components (DON'T REFACTOR)

| Component | Coupled To | Refactoring Effort | ROI |
|-----------|------------|-------------------|-----|
| `GameInterop/Crafting.cs` | FFXIVClientStructs, AddonSynthesis | 8-12 hours | **LOW** |
| `GameInterop/PreCrafting.cs` | Task chain, gear system | 4-6 hours | **LOW** |
| `RawInformation/MemoryHelper.cs` | Lumina sheets, game constants | 6-8 hours | **LOW** |

**Why Low ROI:**
- Code works well as-is
- Refactoring introduces bugs
- Patch compatibility risk increases
- No user-facing benefit

**Recommendation:** **Copy as-is, don't refactor**

### Loosely Coupled Components (SAFE TO REFACTOR)

| Component | Reason | Effort | ROI |
|-----------|--------|--------|-----|
| `CraftingList/*` | Pure data structures | 2-3 hours | **HIGH** |
| `UI/*` | Presentation layer | 2-4 hours | **HIGH** |
| `Universalis/*` | Isolated module | 1 hour | **HIGH** |

**Recommendation:** **Refactor these for database integration**

---

## Shallow vs Deep Integration

### Shallow Integration (RECOMMENDED)

**Approach:**
1. Copy entire Artisan codebase
2. Remove privacy modules (3 files)
3. Add abstraction layer at boundaries only
4. Inject dependencies into UI/CraftingList
5. Leave GameInterop/RawInformation untouched

**Effort:** 18-22 hours
**Risk:** LOW

### Deep Integration (NOT RECOMMENDED)

**Approach:**
1. Refactor GameInterop to use interfaces
2. Abstract all FFXIVClientStructs access
3. Rewrite MemoryHelper for testability
4. Create comprehensive abstraction layers

**Effort:** 60-80 hours
**Risk:** HIGH (patch incompatibility)

**Verdict:** Not worth it

---

## Patch Compatibility Strategy

### Monitor FFXIVClientStructs Updates

```bash
# Subscribe to FFXIVClientStructs GitHub releases
# https://github.com/aers/FFXIVClientStructs

# After game patch:
dotnet add package FFXIVClientStructs --version [latest]
dotnet build
# Test in-game immediately
```

### Graceful Degradation

```csharp
private unsafe void UpdateCraftState()
{
    try
    {
        var addon = GetSynthesisAddon();
        if (addon == null)
        {
            _log.Warning("Synthesis addon not found");
            return;
        }

        _currentProgress = addon->CurrentProgress;
        _currentQuality = addon->CurrentQuality;
    }
    catch (AccessViolationException ex)
    {
        _log.Error($"Memory access failed (patch incompatibility?): {ex.Message}");
        // Disable crafting automation until fixed
        _state = CraftingState.IdleNormal;
    }
}
```

---

## Recommendations

1. **Use shallow integration** - Don't refactor tight coupling
2. **Abstract at UI boundaries** - IGameDataProvider, IRepositoryIntegration
3. **Leave game interop alone** - It's fragile but functional
4. **Monitor patch compatibility** - Test after every game update
5. **Fail gracefully** - Catch memory access exceptions

**End of Tight Coupling Deep Dive**

Key takeaway: Don't refactor GameInterop. Copy as-is, abstract at boundaries, focus effort on privacy and database integration.
