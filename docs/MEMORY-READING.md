# FFXIV Memory Reading Technical Summary

## Executive Summary

**Two Approaches:**
1. **Dalamud Plugin (Recommended)** - C# code injection framework, provides high-level API, community-maintained structures
2. **Standalone Memory Reader** - Direct Windows API (ReadProcessMemory), requires manual structure discovery

**Current State:** Dalamud ecosystem has ~3+ million users with zero enforcement. Standalone tools face higher maintenance burden due to game updates invalidating memory addresses.

---

## How FFXIV Memory Reading Works

### Core Concepts

**Game Architecture:**
- FFXIV runs as `ffxiv_dx11.exe` (64-bit DirectX 11 client)
- Written in C++, compiled to machine code without debug symbols
- Game data stored in process memory space at runtime
- All UI state, character info, inventory, etc. accessible via memory reads

**Memory Address Challenge:**
- Function offsets change EVERY patch (even minor updates)
- Direct memory addresses unusable for plugins/tools
- Solution: "Signatures" - unique byte patterns that identify functions/structures
- Example: `E8 ?? ?? ?? ?? 48 3B 47` (pattern with wildcards)

**Access Methods:**
- **Read-Only**: Safe, only observes game state, no server interaction
- **Code Injection** (Dalamud): DLL injected into game process, hooks game functions
- **External Reader**: Separate process using Windows API to read game memory

---

## Approach 1: Dalamud Plugin Framework

### Architecture

```
XIVLauncher (Custom Game Launcher)
    └── Dalamud (.NET injection framework)
        └── Your Plugin (C# code)
            └── FFXIVClientStructs (Game structure library)
```

### How It Works

**Injection Process:**
1. XIVLauncher launches game
2. Injects Dalamud.dll into ffxiv_dx11.exe process
3. Dalamud loads .NET Core runtime inside game
4. Plugins run in-process with direct memory access

**API Provided:**
- Game data structures (characters, inventory, UI state)
- Hook system (intercept game function calls)
- Event system (chat, network packets, UI interactions)
- Signature scanner (find function addresses)

### Prerequisites

**Required:**
- Windows 10/11 (x64)
- FFXIV installed
- XIVLauncher (custom launcher)
- .NET SDK 8.0+ (for development)
- Visual Studio 2022 or JetBrains Rider

**Installation:**
```
1. Download XIVLauncher from https://goatcorp.github.io/
2. Install to replace standard FFXIV launcher
3. Launch game once to inject Dalamud
4. Plugins installed via /xlplugins in-game
```

### Dependencies

**NuGet Packages:**
```xml
<PackageReference Include="Dalamud" Version="10.*" />
<PackageReference Include="FFXIVClientStructs" Version="1.*" />
```

**Development Tools:**
- Visual Studio 2022 Community (free)
- Dalamud Plugin Template
- IDA Pro or Ghidra (for reverse engineering new structures)
- x64dbg or Cheat Engine (for dynamic analysis)

### Key Libraries

**FFXIVClientStructs:**
- Community-maintained C# bindings for game structures
- ~500+ structures documented and updated per patch
- Uses unsafe code + fixed-offset structs
- Example: `FFXIVClientStructs.FFXIV.Client.Game.UI.UIState`

**Signature Resolution:**
- Signatures stored as hex patterns with wildcards
- `SigScanner` service finds addresses at runtime
- Cached for performance (near-instant after first scan)

### Example Code

```csharp
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

public unsafe class CollectionPlugin : IDalamudPlugin
{
    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        // Access game state singleton
        var uiState = UIState.Instance();
        
        // Check if mount is unlocked
        bool hasFatBlackChocobo = uiState->IsUnlockLinkUnlocked(284);
        
        // Read character name
        var player = ClientState.LocalPlayer;
        string name = player?.Name.ToString();
    }
}
```

### Pros/Cons

**Advantages:**
- High-level API (no manual memory work)
- Community-maintained structures (updated for patches)
- In-game UI integration
- Hook system for event-driven code
- Extensive existing plugin ecosystem for reference

**Disadvantages:**
- Requires XIVLauncher (user must replace launcher)
- C# only (.NET ecosystem)- Updates break plugins (must wait for signature updates)
- Code injection = anti-virus flags
- Violates ToS (though not enforced)

---

## Approach 2: Standalone Memory Reader

### Architecture

```
Your Application (Separate Process)
    └── Windows API (ReadProcessMemory)
        └── ffxiv_dx11.exe (Target Process)
            └── Game Memory (Read-Only Access)
```

### How It Works

**Process Access:**
1. Find FFXIV process by name (`ffxiv_dx11.exe`)
2. Open process handle with `PROCESS_VM_READ` permission
3. Read memory regions via `ReadProcessMemory` API
4. Parse raw bytes into structured data

**Address Discovery:**
- Use Cheat Engine / x64dbg to find initial addresses
- Follow pointer chains from module base address
- Handle ASLR (Address Space Layout Randomization)
- Update addresses after every game patch

### Prerequisites

**System Requirements:**
- Windows 10/11 (x64)
- FFXIV installed and running
- Administrator privileges (sometimes required)

**Development Environment:**
- **C#**: .NET 8.0+, Visual Studio
- **Python**: Python 3.8+, ctypes (built-in)
- **C++**: Visual Studio 2022, Windows SDK

### Dependencies

**Python:**
```python
# Built-in libraries only
import ctypes
from ctypes import wintypes
```

**Python Packages (Optional):**
```
ReadWriteMemory==0.0.7    # High-level wrapper
PyMemoryEditor==1.6.0     # GUI memory scanner
```

**C#:**
```csharp
// System libraries only
using System.Diagnostics;
using System.Runtime.InteropServices;
```

**C++ Libraries:**
```cpp
#include <Windows.h>
#include <Psapi.h>
#include <TlHelp32.h>
```

### Example Code (Python)

```python
import ctypes
from ctypes import wintypes

# Windows API setup
kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)

PROCESS_VM_READ = 0x0010
PROCESS_QUERY_INFORMATION = 0x0400

class FFXIVReader:
    def __init__(self):
        self.process_handle = None
        self.base_address = None
        
    def attach(self, process_name="ffxiv_dx11.exe"):
        # Find process by name
        import psutil
        for proc in psutil.process_iter(['pid', 'name']):
            if proc.info['name'] == process_name:
                pid = proc.info['pid']
                
                # Open process
                self.process_handle = kernel32.OpenProcess(
                    PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,
                    False,
                    pid
                )
                
                # Get base address
                hModules = (wintypes.HMODULE * 1024)()
                cbNeeded = wintypes.DWORD()
                psapi = ctypes.WinDLL('psapi')
                psapi.EnumProcessModules(
                    self.process_handle,
                    ctypes.byref(hModules),
                    ctypes.sizeof(hModules),
                    ctypes.byref(cbNeeded)
                )
                self.base_address = hModules[0]
                return True
        return False
    
    def read_bytes(self, address, size):
        buffer = ctypes.create_string_buffer(size)
        bytes_read = ctypes.c_size_t()
        
        success = kernel32.ReadProcessMemory(
            self.process_handle,
            ctypes.c_void_p(address),
            buffer,
            size,
            ctypes.byref(bytes_read)
        )
        
        return buffer.raw[:bytes_read.value] if success else None
    
    def read_int(self, address):
        data = self.read_bytes(address, 4)
        return int.from_bytes(data, byteorder='little') if data else None
    
    def follow_pointer(self, base_offset, offsets):
        """Follow multi-level pointer chain"""
        addr = self.base_address + base_offset
        
        for offset in offsets[:-1]:
            value = self.read_int(addr)            if value is None:
                return None
            addr = value + offset
        
        return addr + offsets[-1]

# Usage
reader = FFXIVReader()
if reader.attach():
    # Read player HP (example with placeholder addresses)
    hp_address = reader.follow_pointer(0x1C8ADE0, [0x0, 0x1B8])
    current_hp = reader.read_int(hp_address)
    print(f"Player HP: {current_hp}")
```

### Example Code (C#)

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class FFXIVReader
{
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    
    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, 
        byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);
    
    private const uint PROCESS_VM_READ = 0x0010;
    private IntPtr processHandle;
    private IntPtr baseAddress;
    
    public bool Attach(string processName = "ffxiv_dx11")
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0) return false;
        
        var process = processes[0];
        processHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);
        baseAddress = process.MainModule.BaseAddress;
        
        return processHandle != IntPtr.Zero;
    }
    
    public byte[] ReadBytes(IntPtr address, int size)
    {
        byte[] buffer = new byte[size];
        ReadProcessMemory(processHandle, address, buffer, size, out int bytesRead);
        return buffer;
    }
    
    public int ReadInt32(IntPtr address)
    {
        return BitConverter.ToInt32(ReadBytes(address, 4), 0);
    }
    
    public IntPtr FollowPointer(long baseOffset, long[] offsets)
    {
        IntPtr addr = IntPtr.Add(baseAddress, (int)baseOffset);
        
        foreach (var offset in offsets[0..^1])
        {
            int value = ReadInt32(addr);
            addr = IntPtr.Add(new IntPtr(value), (int)offset);
        }
        
        return IntPtr.Add(addr, (int)offsets[^1]);
    }
}
```

### Pros/Cons

**Advantages:**
- No launcher replacement required
- Any programming language (Python, C#, C++, Rust)
- Smaller attack surface (external process)
- Can run on minimal privileges
- Educational value (learn Windows internals)

**Disadvantages:**
- Manual structure discovery (very time-consuming)
- High maintenance (every patch breaks addresses)
- No community structure library
- More complex pointer chains
- Performance overhead (cross-process reads)
- Must reverse-engineer everything yourself

---

## Comparison Matrix

| Feature | Dalamud Plugin | Standalone Reader |
|---------|---------------|-------------------|
| **Setup Difficulty** | Medium (XIVLauncher) | Easy (just code) |
| **Development Speed** | Fast (high-level API) | Slow (manual RE) |
| **Maintenance** | Low (community updates) | High (manual per patch) |
| **Performance** | Excellent (in-process) | Good (IPC overhead) |
| **Language Support** | C# only | Any (Python, C++, Rust) |
| **Structure Library** | Yes (FFXIVClientStructs) | No (DIY) |
| **ToS Risk** | High (code injection) | Medium (memory reading) |
| **Detection Risk** | None (millions of users) | None |
| **User Requirements** | XIVLauncher | Game running |

---

## Recommended Approach

**For Akadaemia Anyder: Dalamud Plugin**

**Rationale:**
1. **Maintenance Burden**: Community updates structures every patch
2. **Development Speed**: High-level API vs. weeks of reverse engineering
3. **Data Coverage**: FFXIVClientStructs has 500+ structures already documented
4. **Reference Code**: Thousands of existing plugins to learn from
5. **User Base**: 3M+ users = proven stability
**Trade-offs:**
- Users must install XIVLauncher (acceptable for target audience)
- C# required (good ecosystem, strong tooling)
- Code injection (already normalized in community)

---

## Technical Deep Dive

### FFXIVClientStructs Structure

**Organization:**
```
FFXIVClientStructs/
├── FFXIV/
│   ├── Client/
│   │   ├── Game/
│   │   │   ├── UI/
│   │   │   │   ├── UIState.cs (main UI state singleton)
│   │   │   │   ├── RecipeNote.cs (crafting recipes)
│   │   │   │   ├── FishingState.cs (fishing log)
│   │   │   │   └── ...
│   │   │   ├── Character/
│   │   │   │   ├── Character.cs (player character)
│   │   │   │   └── BattleChara.cs (combat entities)
│   │   │   ├── InventoryManager.cs
│   │   │   └── QuestManager.cs
│   │   ├── UI/
│   │   │   ├── AddonInventory.cs
│   │   │   └── AddonRecipeNote.cs
│   │   └── System/
│   └── Component/
```

**Example Structure:**
```csharp
[StructLayout(LayoutKind.Explicit, Size = 0x1A360)]
public unsafe partial struct UIState
{
    // Singleton accessor via signature
    [StaticAddress("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ??", 3)]
    public static partial UIState* Instance();
    
    // Unlocked mounts/minions/etc (bit array)
    [FieldOffset(0x19F5C), FixedSizeArray(isBitArray: true, bitCount: 736)]
    internal FixedSizeArray92<byte> _unlockLinks;
    
    // Check if unlock link is unlocked
    [MemberFunction("E8 ?? ?? ?? ?? 84 C0 74 A2")]
    public partial bool IsUnlockLinkUnlockedOrQuestCompleted(
        uint unlockLinkOrQuestId, 
        byte minQuestProgression = 0, 
        bool a4 = true
    );
}
```

### Signature Scanning

**How Signatures Work:**
```
Game code: E8 4A 3B 12 00 48 8B 8B A0 01 00 00
           ^^ [CALL instruction]
              ^^^^^^^^^^ [Relative offset - changes every patch]
                         ^^ ^^ ^^ ^^ ^^ ^^ ^^ [Stable code]

Signature: "E8 ?? ?? ?? ?? 48 8B 8B A0 01 00 00"
           ?? = wildcard (matches any byte)
```

**Signature Resolution Process:**
1. Load game binary into memory
2. Scan for pattern match
3. Calculate actual function address
4. Cache result for performance

**IDA/Ghidra Scripts:**
- FFXIVClientStructs provides scripts to auto-populate disassembler
- Imports community-found addresses and signatures
- Saves weeks of manual reverse engineering

### Memory Layout Challenges

**ASLR (Address Space Layout Randomization):**
- Base address randomized each game launch
- Absolute addresses unusable
- Solution: Relative offsets from module base

**Pointer Chains:**
```
Base + 0x1C8ADE0 → [Ptr A] + 0x10 → [Ptr B] + 0x1B8 → Player HP
```

**Struct Alignment:**
- Game uses C++ struct alignment rules
- Critical for accurate field offsets
- FFXIVClientStructs handles this with FieldOffset attributes

### Reverse Engineering Workflow

**Static Analysis (IDA Pro / Ghidra):**
1. Load ffxiv_dx11.exe
2. Run FFXIVClientStructs import script
3. Find function by string reference or known pattern
4. Decompile to pseudo-C++
5. Identify struct layouts

**Dynamic Analysis (x64dbg / Cheat Engine):**
1. Attach debugger to running game
2. Search for known value (e.g., current HP = 5000)
3. Modify value in-game, filter results
4. Find memory address
5. "Find what accesses this address"
6. Locate function that reads/writes value

**Example: Finding Mount Unlock Status**
1. Use mount in-game (Fat Black Chocobo)
2. Search memory for mount ID (284)
3. Set breakpoint on memory access
4. Trigger mount menu open
5. Follow call stack to unlock check function
6. Reverse engineer function to find bit array location

### Collection Data Locations

**Based on FFXIVClientStructs:**

**Crafting Recipes:**
- `UIState.RecipeNote` structure
- Bit array for recipe completion
- Separate arrays per crafting class

**Gathering Logs:**
- `UIState.GatheringNote` structure
- Fish caught stored in separate tracking

**Quest Progress:**
- `QuestManager.QuestData` array
- Current quest steps tracked

**Mounts/Minions:**
- `UIState._unlockLinks` bit array
- Single bit per collectible (1 = owned)

---

## Development Roadmap

### Phase 1: Environment Setup
1. Install XIVLauncher + Dalamud
2. Set up Visual Studio 2022 with .NET 8.0
3. Clone Dalamud Plugin Template
4. Add FFXIVClientStructs NuGet package
5. Build and test "Hello World" plugin

### Phase 2: Data Access
1. Study UIState structure in FFXIVClientStructs repo
2. Access craft recipe completion bit arrays
3. Read gathering log completion
4. Query quest progress data
5. Test data accuracy in-game

### Phase 3: Data Export
1. Serialize to JSON/SQLite
2. Build local data store
3. Track changes over time
4. Create backup/restore functionality
### Phase 4: UI/Reporting
1. In-game overlay (ImGui via Dalamud)
2. Completion percentages
3. Missing items lists
4. Progress tracking

---

## Critical Dependencies

### Core Infrastructure

**XIVLauncher (Required):**
- Repo: https://github.com/goatcorp/FFXIVQuickLauncher
- Purpose: Custom launcher that injects Dalamud
- Version: Latest stable (auto-updates)

**Dalamud (Required):**
- Repo: https://github.com/goatcorp/Dalamud
- Purpose: .NET plugin framework
- API Version: 10.x (as of Jan 2026)
- Auto-managed by XIVLauncher

**FFXIVClientStructs (Required):**
- Repo: https://github.com/aers/FFXIVClientStructs
- Purpose: Game structure definitions
- NuGet: FFXIVClientStructs
- Updates: Every major game patch

### Development Tools

**Visual Studio 2022 (Recommended):**
- Community edition (free)
- .NET desktop development workload
- C# 12.0+ support

**Alternative IDEs:**
- JetBrains Rider (paid, excellent for plugin dev)
- VS Code (requires manual setup, not recommended)

**Reverse Engineering Tools:**
- IDA Pro (gold standard, expensive) OR
- Ghidra (free, NSA-developed, very capable)
- x64dbg (free, lightweight debugger)
- Cheat Engine (free, memory scanner)

### Optional Libraries

**JSON Serialization:**
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.*" />
```

**SQLite Database:**
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
```

**ImGui (included in Dalamud):**
- Already available via Dalamud.Interface
- No additional package needed

---

## Common Challenges & Solutions

### Challenge 1: Game Updates Break Everything

**Problem:** Every patch changes memory addresses

**Solution (Dalamud):**
- Community updates FFXIVClientStructs within 24-48 hours
- Wait for Dalamud compatibility before patching
- Subscribe to Discord announcements (#dev channel)

**Solution (Standalone):**
- Maintain signature database
- Test immediately after patch
- Update addresses via Cheat Engine
- Store signatures, not absolute addresses

### Challenge 2: Data Not Available in Memory

**Problem:** Some data only exists server-side

**Example:** Total number of items crafted across all characters

**Solution:**
- Only track what's in client memory
- Accept limitations
- Document what's trackable vs. not

### Challenge 3: Performance Impact

**Problem:** Reading memory every frame tanks FPS

**Solution:**
- Poll on events (menu opened), not every frame
- Cache results, invalidate on known changes
- Use Dalamud's event system for triggers
- Benchmark and profile

### Challenge 4: Anti-Virus False Positives

**Problem:** Dalamud flagged as malware (code injection)

**Solution:**
- Whitelist XIVLauncher directory
- Common with code injection tools
- Sign plugins with certificate (if distributing)
- Document in README for users

---

## Legal & Risk Assessment

### Terms of Service Status

**Square Enix Position (§2.5):**
- All third-party tools prohibited
- No exceptions for read-only tools
- Includes memory reading, packet sniffing, automation

**Enforcement Reality:**
- Zero documented bans for Dalamud (3M+ users since 2019)
- Zero documented bans for ACT (parsing tool, 10+ years)
- Enforcement targets: cheating, harassment, public streaming, RMT

**Risk Factors:**
| Action | Risk Level | Enforcement History |
|--------|-----------|---------------------|
| Memory reading (read-only) | Very Low | None documented |
| Dalamud plugin use | Very Low | None documented |
| Mentioning in-game | Medium | Chat bans reported |
| Streaming with visible tools | High | Warnings/bans |
| Automated gameplay | Critical | Immediate ban |
| Posting unreleased content | Critical | DMCA + ban |

### Risk Mitigation

**Development:**
- Read-only operations ONLY
- No server interaction
- No automated actions (no button pressing)
- Local data storage only

**Usage:**
- Never mention tool in-game
- Never stream with tool visible
- Personal use only (no service/website)
- No datamining unreleased content

**Distribution:**
- Clear ToS disclaimer in README
- "Use at own risk" language
- No monetization
- Open source (transparency)

---

## Resources & References

### Official Documentation

**Dalamud API Docs:**
- https://dalamud.dev/
- https://goatcorp.github.io/faq/

**FFXIVClientStructs:**
- https://github.com/aers/FFXIVClientStructs
- https://ffxiv.wildwolf.dev/ (API reference)

### Community Resources

**XIV Dev Wiki:**
- https://xiv.dev/
- Game internals, network protocols, file formats

**Reverse Engineering Guides:**
- https://dalamud.dev/plugin-development/reverse-engineering/
- https://github.com/aers/FFXIVClientStructs/tree/main/ida

**Discord Servers:**
- Goat Place (XIVLauncher/Dalamud): https://discord.gg/3NMcUV5
- XIV Dev: Community for reverse engineering

### Example Plugins (Source Code)

**Good Memory** - Ownership indicators:
- https://github.com/Sevii77/ffxiv_visland (similar functionality)

**Collections** - Full tracking:
- Proprietary, but demonstrates feasibility

**Simple Tweaks** - Quality of life:
- https://github.com/Caraxi/SimpleTweaksPlugin
- Excellent code quality, learn from this

---

## Quick Start Template

```csharp
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AkadaemiaAnyder
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Akadaemia Anyder";
        
        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            // Plugin initialization
            CheckCollectionStatus();
        }
        
        private unsafe void CheckCollectionStatus()
        {
            var uiState = UIState.Instance();
            if (uiState == null) return;
            
            // Example: Check mount unlock
            uint fatBlackChocoboId = 284;
            bool isUnlocked = uiState->IsUnlockLinkUnlockedOrQuestCompleted(
                fatBlackChocoboId
            );
            
            DalamudApi.PluginLog.Info($"Fat Black Chocobo: {isUnlocked}");
        }
        
        public void Dispose()
        {
            // Cleanup
        }
    }
}
```

---

## Summary
**Best Approach:** Dalamud Plugin
- C# development with FFXIVClientStructs
- Community-maintained structure updates
- High-level API, event-driven architecture
- 3M+ user base proves stability

**Prerequisites:**
- Windows 10/11 (x64)
- XIVLauncher + Dalamud
- Visual Studio 2022 + .NET 8.0
- FFXIVClientStructs NuGet package

**Key Dependencies:**
- Dalamud (plugin framework)
- FFXIVClientStructs (game structures)
- ImGui (UI rendering, included)

**Risk Assessment:**
- ToS violation: Yes (all third-party tools)
- Enforcement risk: Very low (zero documented bans)
- Mitigation: Read-only, never mention in-game

**Development Path:**
1. Set up Dalamud dev environment
2. Study FFXIVClientStructs UIState
3. Access recipe/gathering/quest data
4. Export to local storage
5. Build UI for tracking

**Timeline Estimate:**
- Environment setup: 2-4 hours
- Data access implementation: 1-2 weeks
- UI development: 1-2 weeks
- Testing & refinement: 1 week

**Total:** 3-5 weeks for MVP (crafting recipes + gathering logs)

---

## Next Steps for Akadaemia Anyder

1. **Install Development Environment** (Today)
   - Download/install XIVLauncher
   - Launch game once to inject Dalamud
   - Install Visual Studio 2022 Community
   - Install .NET 8.0 SDK

2. **Set Up Project** (Day 1-2)
   - Clone Dalamud plugin template
   - Add FFXIVClientStructs reference
   - Build hello world plugin
   - Test in-game loading

3. **Explore Data Structures** (Week 1)
   - Read FFXIVClientStructs UIState.cs
   - Identify recipe completion bit arrays
   - Test accessing data in-game
   - Document findings

4. **Build Core Functionality** (Week 2-3)
   - Implement data readers
   - Create JSON export
   - Add change detection
   - Build local database

5. **Create User Interface** (Week 3-4)
   - ImGui overlay design
   - Completion tracking display
   - Missing items list
   - Export/import features

---

**Document Version:** 1.0  
**Last Updated:** January 2026  
**Next Review:** After first game patch