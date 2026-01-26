# FFXIVClientStructs Memory Structures Documentation

## Overview

This document details the memory structures used by Akadaemia Anyder to track collectible unlocks in Final Fantasy XIV. All structures are accessed via `UIState.Instance()` from the FFXIVClientStructs library bundled with Dalamud.

## UIState Structure

**Namespace:** `FFXIVClientStructs.FFXIV.Client.Game.UI`
**Total Size:** 0x1A360 bytes (107,360 bytes)

### Key Field Offsets

| Offset | Field Name | Type | Size | Purpose |
|--------|-----------|------|------|---------|
| 0x5058 | GatheringNote | GatheringNote | 0x6A0 (1,696 bytes) | Tracks gathering node unlocks |
| 0x5758 | RecipeNote | RecipeNote | 0xB40 (2,880 bytes) | Tracks crafting recipe unlocks |
| 0x6378 | FishingNote | FishingNote | 0xE0 (224 bytes) | Tracks fishing hole data |

### Access Pattern

```csharp
using FFXIVClientStructs.FFXIV.Client.Game.UI;

unsafe
{
    var uiState = UIState.Instance();
    if (uiState == null)
    {
        // UIState not available
        return;
    }

    var recipeNote = &uiState->RecipeNote;
    var gatheringNote = &uiState->GatheringNote;
    var fishingNote = &uiState->FishingNote;
}
```

**Important:** These are embedded structs within UIState, not pointers. They are accessed by reference (`&uiState->StructName`).

---

## RecipeNote Structure

**Size:** 0xB40 bytes (2,880 bytes)
**Purpose:** Manages all crafting recipe data and unlock status

### Key Components

| Offset | Field Name | Type | Description |
|--------|-----------|------|-------------|
| 0x00 | _jobs | FixedSizeArray8&lt;uint&gt; | Maps CraftType to ClassJob ID |
| 0xB8 | RecipeList | RecipeData* | Pointer to recipe data arrays |
| 0x110 | ActiveCraftRecipeId | ushort | Currently selected recipe |
| 0x112 | ActiveCraftCraftType | byte | Active job (0-7 for 8 crafting classes) |

### Recipe Organization

**8 Crafting Classes:**
1. Carpenter (CRP)
2. Blacksmith (BSM)
3. Armorer (ARM)
4. Goldsmith (GSM)
5. Leatherworker (LTW)
6. Weaver (WVR)
7. Alchemist (ALC)
8. Culinarian (CUL)

**Recipe Capacity:**
- NoteBookDivisionIdsArray contains 66 entries per craft type
- Actual recipe count varies by class and patch
- RecipeList->RecipeCount contains total available recipes

### RecipeEntry Structure

**Size:** 0x400 bytes (1,024 bytes per recipe)

Each recipe entry contains:
- **RecipeId** (ushort): Unique recipe identifier
- **ItemId** (uint): Resulting item ID
- **Materials:** 6 ingredient slots (RecipeIngredient, 0x88 bytes each)
  - ItemId, NQ count, HQ count
- **Crystals:** 2 crystal slots (ID + amount)
- **Stats:**
  - Difficulty (base value × factor ÷ 100)
  - Quality (base value × factor ÷ 100)
  - Durability (base value × factor ÷ 100)
- **Requirements:**
  - RequiredCraftsmanship
  - RequiredControl
  - RecipeLevel
  - Stars (0-3+)
- **Metadata:**
  - IconId
  - PatchNumber
  - Priority ranking

### Unlock Checking

```csharp
bool IsRecipeUnlocked(ushort recipeId);
```

**Note:** The internal implementation of unlock tracking is not fully documented in FFXIVClientStructs. The method signature exists but the underlying bit array or storage mechanism is not exposed in the public API.

---

## GatheringNote Structure

**Size:** 0x6A0 bytes (1,696 bytes)
**Purpose:** Tracks gathering node discovery and unlocks

### Expected Data Coverage

**2 Gathering Classes:**
1. Miner (MIN)
2. Botanist (BTN)

**Estimated Capacity:**
- 128 nodes per gathering class
- Total: 256 gathering nodes

### Current Documentation Status

**WARNING:** GatheringNote is currently a **stub definition** in FFXIVClientStructs. The structure exists with the correct size allocation, but individual field offsets and methods are not yet documented in the public repository.

**What we know:**
- Total size: 0x6A0 bytes (1,696 bytes)
- Embedded in UIState at offset 0x5058
- Accessible via `UIState.Instance()->GatheringNote`

**What is undocumented:**
- Individual field offsets
- Bit array layout for unlocked nodes
- Methods for checking node unlock status
- Node-to-index mapping

### Expected Structure (Hypothesis)

Based on the size and purpose, the structure likely contains:
- Bit arrays or byte arrays tracking unlocked nodes per gathering class
- Current selected node information
- Node availability flags
- Metadata about gathering zones

**Action Required:** Reverse engineering or community research needed to document the complete field layout.

---

## FishingNote Structure

**Size:** 0xE0 bytes (224 bytes)
**Purpose:** Tracks fishing hole discovery and fish catches

### Current Documentation Status

**WARNING:** FishingNote is currently a **stub definition** in FFXIVClientStructs. The structure exists with the correct size allocation, but individual field offsets and methods are not yet documented in the public repository.

**What we know:**
- Total size: 0xE0 bytes (224 bytes)
- Embedded in UIState at offset 0x6378
- Accessible via `UIState.Instance()->FishingNote`
- Companion structure: FishRecord.cs exists in same directory

**What is undocumented:**
- Individual field offsets
- How fishing holes are tracked
- How caught fish are recorded
- Fish-to-index mapping

### Expected Functionality

Based on the size and companion FishRecord structure, FishingNote likely tracks:
- Discovered fishing holes
- Fish caught status per hole
- Legendary fish catches
- Big fish records

**Action Required:** Reverse engineering or community research needed to document the complete field layout.

---

## Data Storage Patterns

### Bit Array Storage (Hypothesis)

For efficient unlock tracking across hundreds of items, FFXIV likely uses bit arrays:

**Example Pattern:**
- 512 crafting recipes → 64 bytes (512 bits ÷ 8)
- 256 gathering nodes → 32 bytes (256 bits ÷ 8)
- Each bit = 1 unlock state (0 = locked, 1 = unlocked)

**Access Pattern:**
```csharp
// Hypothetical bit array access
byte[] unlockBits = ...;
int byteIndex = itemId / 8;
int bitOffset = itemId % 8;
bool isUnlocked = (unlockBits[byteIndex] & (1 << bitOffset)) != 0;
```

---

## Critical Findings

### Structure Availability: ✅ CONFIRMED

All three structures are **embedded structs within UIState**, not pointers:
- `UIState.Instance()->RecipeNote` - Valid embedded struct
- `UIState.Instance()->GatheringNote` - Valid embedded struct
- `UIState.Instance()->FishingNote` - Valid embedded struct

**Implication:** These structures cannot be null. If UIState.Instance() is valid, all three note structures are accessible.

### Documentation Status: ⚠️ PARTIAL

| Structure | Documentation | Methods | Field Offsets | Usability |
|-----------|--------------|---------|---------------|-----------|
| RecipeNote | Partial | IsRecipeUnlocked exists | Some documented | Usable for recipe checking |
| GatheringNote | Stub only | None documented | None documented | **Blocked - reverse engineering needed** |
| FishingNote | Stub only | None documented | None documented | **Blocked - reverse engineering needed** |

---

## GO/NO-GO Assessment

### RecipeNote: ✅ GO
- Structure accessible
- IsRecipeUnlocked method exists
- Sufficient documentation for implementation

### GatheringNote: ⚠️ NO-GO (Documentation Gap)
- Structure accessible but fields undocumented
- No public methods for unlock checking
- **Requires reverse engineering to use**

### FishingNote: ⚠️ NO-GO (Documentation Gap)
- Structure accessible but fields undocumented
- No public methods for data access
- **Requires reverse engineering to use**

---

## Research Resources

### FFXIVClientStructs Repository
- Main repo: https://github.com/aers/FFXIVClientStructs
- UIState: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs
- RecipeNote: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/RecipeNote.cs
- GatheringNote: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/GatheringNote.cs
- FishingNote: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/FishingNote.cs

### Community Resources
- [Dalamud Documentation](https://dalamud.dev/)
- [Dalamud Reverse Engineering Guide](https://dalamud.dev/plugin-development/reverse-engineering/)
- [GatherBuddyReborn Plugin](https://github.com/FFXIV-CombatReborn/GatherBuddyReborn) - Example gathering plugin
- [XIV Dev Wiki](https://xiv.dev/) - Community reverse engineering resources

### Alternative Approaches

If direct memory access is blocked:
1. **Game Events:** Listen to Dalamud framework events for recipe/gathering unlocks
2. **Excel Sheets:** Cross-reference with game data sheets for validation
3. **Network Packets:** Monitor network traffic for unlock notifications
4. **Community APIs:** Use XIVAPI or similar services for unlock data

---

## Next Steps

1. **Implement RecipeNote tracking** - This is fully documented and usable
2. **Reverse engineer GatheringNote** - Use memory inspection tools to map fields
3. **Reverse engineer FishingNote** - Use memory inspection tools to map fields
4. **Consider hybrid approach** - Use RecipeNote directly, track gathering/fishing via events

**Recommendation:** Start with RecipeNote implementation while investigating alternative approaches for gathering and fishing data access.
