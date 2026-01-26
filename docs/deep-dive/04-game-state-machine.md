# Deep Dive: Game State Machine

**Topic:** How Artisan's Crafting.cs state machine executes actions in-game
**Complexity:** High
**Relevance:** Understanding this is critical if debugging action execution issues

---

## Crafting State Machine

**File:** `GameInterop/Crafting.cs` (43 KB)

### State Diagram

```
┌──────────┐
│IdleNormal│ (Not crafting)
└────┬─────┘
     │ User starts craft
     ▼
┌──────────┐
│WaitStart │ (Waiting for craft window to open)
└────┬─────┘
     │ Craft window detected
     ▼
┌───────────┐
│InProgress │ (Crafting active)
└────┬──────┘
     │ Action selected by solver
     ▼
┌────────────┐
│WaitAction  │ (Executing action, waiting for game response)
└────┬───────┘
     │ Action completed
     ├─ Craft finished? → WaitFinish
     └─ More actions? → InProgress

┌──────────┐
│WaitFinish│ (Craft complete, closing window)
└────┬─────┘
     │
     ▼
┌──────────┐
│IdleNormal│
└──────────┘
```

### State Transitions

```csharp
private enum CraftingState
{
    IdleNormal,      // Not crafting
    WaitStart,       // Initiated craft, waiting for window
    InProgress,      // Craft active, can execute actions
    WaitAction,      // Action queued, waiting for game processing
    WaitFinish,      // Craft complete, waiting for window close
    QuickCraft       // Quick synthesis mode
}

private void OnAddonUpdate(AddonEvent type, string addonName)
{
    if (addonName != "Synthesis") return;

    switch (_state)
    {
        case CraftingState.IdleNormal:
            if (type == AddonEvent.Setup)
            {
                _state = CraftingState.InProgress;
                OnCraftStart();
            }
            break;

        case CraftingState.InProgress:
            if (CanExecuteAction())
            {
                var action = GetNextAction();  // From solver
                ExecuteAction(action);
                _state = CraftingState.WaitAction;
            }
            break;

        case CraftingState.WaitAction:
            if (ActionCompleted())
            {
                if (CraftFinished())
                    _state = CraftingState.WaitFinish;
                else
                    _state = CraftingState.InProgress;
            }
            break;

        case CraftingState.WaitFinish:
            if (type == AddonEvent.Finalize)
            {
                _state = CraftingState.IdleNormal;
                OnCraftEnd();
            }
            break;
    }
}
```

---

## FFXIVClientStructs Memory Access

### Reading Craft Progress

```csharp
using FFXIVClientStructs.FFXIV.Client.UI;

private unsafe int GetCurrentProgress()
{
    var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis", 1);
    if (addon == null) return 0;

    return addon->CurrentProgress;
}

private unsafe int GetCurrentQuality()
{
    var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis", 1);
    if (addon == null) return 0;

    return addon->CurrentQuality;
}
```

### Action Execution

```csharp
using FFXIVClientStructs.FFXIV.Client.Game;

private unsafe void ExecuteAction(CraftAction action)
{
    var actionId = GetActionId(action);  // Map enum to game action ID

    var actionManager = ActionManager.Instance();
    if (actionManager == null) return;

    // Execute action
    actionManager->UseAction(ActionType.CraftAction, actionId, 0xE0000000, 0, 0, 0, null);

    Log.Info($"Executed action: {action} (ID: {actionId})");
}

private uint GetActionId(CraftAction action)
{
    return action switch
    {
        CraftAction.BasicSynthesis => 100001,
        CraftAction.BasicTouch => 100002,
        CraftAction.MastersMend => 100003,
        CraftAction.Innovation => 19004,
        // ... map all 40+ actions
        _ => 0
    };
}
```

---

## Patch Compatibility Concerns

### What Breaks With Game Patches

**High Risk (Breaks every major patch):**
- Memory offsets in FFXIVClientStructs
- Addon field names/positions
- Action IDs (rarely, but happens)

**Medium Risk (Breaks occasionally):**
- Recipe data structure
- Inventory container layout
- Buff ID mappings

**Low Risk (Stable):**
- Item IDs (never change)
- Recipe IDs (rarely change)
- Core crafting mechanics

### Mitigation Strategies

**1. Use FFXIVClientStructs (Community-Maintained)**
```csharp
// GOOD: Use community-maintained structs
using FFXIVClientStructs.FFXIV.Client.UI;
var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis", 1);

// BAD: Manual memory offsets
var addon = (AddonSynthesis*)Marshal.ReadIntPtr(baseAddress + 0x12345678);
```

**2. Graceful Degradation**
```csharp
private unsafe int GetCurrentProgress()
{
    try
    {
        var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis", 1);
        if (addon == null) return 0;

        return addon->CurrentProgress;
    }
    catch (Exception ex)
    {
        Log.Warning($"Failed to read progress (patch incompatibility?): {ex.Message}");
        return 0;  // Fail gracefully
    }
}
```

**3. Version Checking**
```csharp
private bool IsGameVersionSupported()
{
    var gameVersion = Service.ClientState.GameVersion;
    var supportedVersions = new[] { "2024.01.01.0000.0000", "2024.02.15.0000.0000" };

    if (!supportedVersions.Contains(gameVersion))
    {
        Log.Warning($"Game version {gameVersion} may not be supported");
        return false;
    }

    return true;
}
```

---

## Recommendations

1. **Don't modify state machine** - It's fragile and well-tuned
2. **Use FFXIVClientStructs** - Don't write manual offsets
3. **Add try-catch** - Graceful degradation for patch incompatibility
4. **Test after patches** - Always test plugin after game updates
5. **Subscribe to FFXIVClientStructs updates** - Watch GitHub for patch-day updates

**End of Game State Machine Deep Dive**
