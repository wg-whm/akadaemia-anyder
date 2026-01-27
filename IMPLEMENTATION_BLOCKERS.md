# Implementation Blockers for Gathering/Fishing Detection

**Created**: 2026-01-25
**Status**: Partially Implemented

---

## What We Built (Option B Progress)

### ✅ Completed
1. **Player State Validation**
   - Detects when player is logged in
   - Identifies gathering class (MIN=16, BTN=17)
   - Identifies fishing class (FSH=18)
   - Safe exception handling

2. **UIState Access**
   - Successfully accesses `UIState.Instance()`
   - Validates UIState availability before use
   - Follows same pattern as working RecipeReader

3. **Framework Integration**
   - Event listeners subscribe to Framework.Update
   - Updates run every frame when player in appropriate class
   - Logging for debugging

### ⚠️ Blocked by FFXIVClientStructs

**The Core Issue:**

```csharp
// This works (RecipeNote):
var uiState = UIState.Instance();
bool isUnlocked = uiState->RecipeNote->IsRecipeUnlocked(recipeId);  // ✅ Available

// This doesn't (GatheringNote):
bool isUnlocked = uiState->GatheringNote->IsNodeUnlocked(nodeId);   // ❌ Method doesn't exist

// This doesn't (FishingNote):
bool isCaught = uiState->FishingNote->IsFishCaught(fishId);          // ❌ Method doesn't exist
```

**FFXIVClientStructs Status:**
- **RecipeNote**: Fully documented struct with methods
- **GatheringNote**: Stub definition, no accessible fields/methods
- **FishingNote**: Stub definition, no accessible fields/methods

**Proof** (from T1.5 research):
```
docs/MEMORY-STRUCTURES.md:
RecipeNote: IsRecipeUnlocked() available
GatheringNote: stub only
FishingNote: stub only
```

---

## Current Implementation

### GatheringEventListener.cs
```csharp
private unsafe void OnFrameworkUpdate(IFramework framework)
{
    // ✅ Player validation works
    if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null) return;

    // ✅ UIState access works
    var uiState = UIState.Instance();
    if (uiState == null) return;

    // ✅ Class detection works
    var classJobId = player.ClassJob.RowId;
    bool isGatheringClass = classJobId == 16 || classJobId == 17;
    if (!isGatheringClass) return;

    // ❌ BLOCKED: Cannot access gathering log data
    // Future: when FFXIVClientStructs adds API
    // for (ushort nodeId = 0; nodeId < maxNodes; nodeId++)
    // {
    //     if (uiState->GatheringNote->IsNodeUnlocked(nodeId))
    //     {
    //         AddNode(CreateGatheringNodeFromId(nodeId));
    //     }
    // }
}
```

### FishingEventListener.cs
Same pattern - player state works, UIState works, but no FishingNote API available.

---

## Alternative Approaches Considered

### 1. Memory Scanning (Not Recommended)
**Idea**: Find gathering/fishing log data in memory directly without FFXIVClientStructs

**Pros**:
- Could work around struct limitations
- Direct memory access

**Cons**:
- Fragile (breaks every game patch)
- Requires reverse engineering
- Safety concerns (AccessViolationException risk)
- Against Dalamud best practices
- RecipeNote proves proper API is possible

**Verdict**: ❌ Rejected - wait for proper API

### 2. Action/Chat Event Hooks (Investigated)
**Idea**: Hook "you gathered X" or "you caught X" chat messages

**Pros**:
- Events exist in Dalamud
- Real-time detection

**Cons**:
- Chat messages can be delayed/filtered
- Doesn't tell you WHICH items are unlocked in log
- Can't scan full log on demand (only live catches)
- Misses data from before plugin installed

**Verdict**: ⚠️ Possible supplement but doesn't solve the core need

### 3. GameData Sheet Access (Partial Solution)
**Idea**: Use IDataManager to read gathering/fishing sheet data

**Pros**:
- Can get item/node IDs
- Can get node locations
- Stable API

**Cons**:
- Tells you what CAN be gathered, not what YOU HAVE gathered
- No "unlocked" state accessible
- Doesn't solve detection problem

**Verdict**: ⚠️ Useful for enrichment but doesn't detect state

---

## What's Needed to Complete

### Option 1: Wait for FFXIVClientStructs Update (Recommended)
**When**: FFXIVClientStructs maintainers document GatheringNote/FishingNote

**What Changes**:
```csharp
// In GatheringEventListener.cs line ~130, replace:
_log.Debug("GatheringNote API not available");

// With:
for (ushort nodeId = 0; nodeId < 1000; nodeId++)  // Adjust max as needed
{
    if (uiState->GatheringNote->IsNodeUnlocked(nodeId) && !_seenNodeIds.Contains(nodeId))
    {
        var node = CreateGatheringNodeFromGameData(nodeId);
        AddNode(node);
    }
}
```

**Estimated Effort**: 30 minutes once API available

**Helper Method Needed**:
```csharp
private GatheringNode CreateGatheringNodeFromGameData(ushort nodeId)
{
    // Use IDataManager to lookup node details
    // Return populated GatheringNode object
}
```

### Option 2: Community Help
**Post on FFXIVClientStructs Discord/GitHub**:
- Request GatheringNote/FishingNote documentation
- Reference RecipeNote as precedent
- Offer to test once available

### Option 3: Hybrid Chat Hook (Supplement)
**Implement now for partial functionality**:

```csharp
[PluginService] internal static IChatGui ChatGui { get; set; }

// In constructor:
ChatGui.ChatMessage += OnChatMessage;

private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message)
{
    // Parse "You obtain X" messages for gathering/fishing
    // Add to collection in real-time (won't backfill historical data)
}
```

**Pros**: Something working now
**Cons**: Incomplete (no historical scan)

---

## Build Status

✅ **Compiles**: 0 errors, 16 warnings (deprecated API usage)
✅ **Player Detection**: Works
✅ **Class Detection**: Works
✅ **UIState Access**: Works
❌ **Log Reading**: Blocked by FFXIVClientStructs

---

## Testing Plan (When Unblocked)

1. **Manual In-Game Test**:
   - Switch to Miner (MIN)
   - Gather from a node
   - Check if detection fires
   - Verify database update

2. **Unit Test Update**:
   - Mock GatheringNote/FishingNote when available
   - Test node/fish ID iteration logic
   - Test deduplication (HashSet)

3. **Integration Test**:
   - Full scan with mock data
   - Verify database persistence
   - Test progress calculator

---

## Timeline

| Task | Status | Blocker |
|------|--------|---------|
| Infrastructure | ✅ Complete | - |
| Player state validation | ✅ Complete | - |
| UIState access | ✅ Complete | - |
| Class detection | ✅ Complete | - |
| GatheringNote API | ❌ Blocked | FFXIVClientStructs stub |
| FishingNote API | ❌ Blocked | FFXIVClientStructs stub |
| Chat hook supplement | ⚠️ Optional | 1-2 hours effort |

---

## References

- **T1.5 Decision**: `docs/MEMORY-STRUCTURES.md`
- **RecipeReader** (working example): `SamplePlugin/MemoryReaders/RecipeReader.cs`
- **Current code**: `SamplePlugin/EventListeners/` (both listeners)
- **FFXIVClientStructs**: https://github.com/aers/FFXIVClientStructs

---

## Conclusion

We've implemented **everything possible** without the FFXIVClientStructs API. The code:
- ✅ Compiles and runs
- ✅ Validates player state
- ✅ Detects appropriate class
- ✅ Accesses UIState safely
- ❌ Cannot read gathering/fishing logs (API doesn't exist)

**Next step**: Wait for FFXIVClientStructs to document GatheringNote/FishingNote, then uncomment and complete the scanning logic (30 minute task).

**Alternative**: Implement chat hook for partial real-time detection (1-2 hours).
