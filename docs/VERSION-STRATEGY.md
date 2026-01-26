# FFXIVClientStructs Version Strategy

**Project:** Akadaemia Anyder (FFXIV Collection Tracker Dalamud Plugin)
**Last Updated:** January 25, 2026
**Status:** T0 Research Complete

---

## Executive Summary

FFXIVClientStructs is **not** distributed as a standard NuGet package with semantic versioning. Instead, it's a community-maintained library distributed via:

1. **Dalamud's bundled version** (recommended for plugins)
2. **GitHub source repository** with signature-based versioning
3. **Direct DLL reference** from Dalamud's addon directory

For Akadaemia Anyder, we will use **Dalamud's bundled version** as the default strategy, with provisions for custom builds if future game updates require cutting-edge reverse engineering discoveries.

---

## Recommended Version Strategy

### Primary Approach: Use Dalamud-Bundled FFXIVClientStructs

**Recommendation:** Do NOT specify an explicit FFXIVClientStructs version number in your .csproj file. Instead, reference the Dalamud-provided DLL by its installation path.

**Rationale:**
- Dalamud updates FFXIVClientStructs with reasonable frequency
- Dalamud maintains backwards compatibility with patches
- Eliminates version mismatch risks between game client and structures
- Simplifies deployment and eliminates dependency management for this specific package

**Reference Path:**
```
$(AppData)\XIVLauncher\addon\Hooks\dev\FFXIVClientStructs.dll
```

### .csproj Configuration (Primary Method)

```xml
<Reference Include="FFXIVClientStructs">
  <HintPath>$(AppData)\XIVLauncher\addon\Hooks\dev\FFXIVClientStructs.dll</HintPath>
  <!-- Do NOT set Private=true for Dalamud version -->
  <!-- This allows the plugin to use Dalamud's version at runtime -->
</Reference>
```

---

## Version Information Reference

### Dalamud Versioning Context

- **Current Dalamud Version:** 14.x (as of January 2025)
- **FFXIVClientStructs Update Frequency:** Reasonable frequency per Dalamud documentation
- **Backwards Compatibility:** Dalamud applies patches to ensure plugins remain compatible across updates

### Assembly Version Detection

To programmatically detect the FFXIVClientStructs version at runtime:

```csharp
// Detect assembly version
var csAssembly = typeof(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject).Assembly;
var version = csAssembly.GetName().Version;
Dalamud.Logging.PluginLog.LogInfo($"FFXIVClientStructs Version: {version}");

// Check for specific type availability (reliability check)
var hasCharacterSheet = csAssembly.GetTypes()
    .Any(t => t.Name == "CharacterSheet" && t.Namespace?.StartsWith("FFXIVClientStructs.FFXIV") ?? false);
```

---

## Version Compatibility Matrix

### Supported Configurations

| Component | Version | Support | Notes |
|-----------|---------|---------|-------|
| **Dalamud API** | 14.x+ | Recommended | January 2025+ |
| **FFXIVClientStructs** | Dalamud-bundled | Primary | No explicit version pinning |
| **FFXIV Client** | Current live | Dynamic | Updates handled by Dalamud |
| **.NET Runtime** | .NET Framework 4.7.2+ or .NET 6.0+ | Required | Per Dalamud requirements |
| **C# Language** | C# 10+ | Required | Modern syntax and features |

### Game Patch Compatibility Strategy

FFXIVClientStructs maintains compatibility across FFXIV patches through:

1. **Signature-Based Resolution**: Uses function/class signatures for runtime structure location
2. **Backwards Compatibility Patches**: Dalamud applies patches to maintain plugin compatibility
3. **Version Cache System**: FFXIVClientStructs includes a signature cache mechanism for version management

**Key Implementation Detail:**
```csharp
// FFXIVClientStructs uses signature caching for version management
// The signature cache speeds up resolving on future runs
// JSON-based caching mechanism adapts to game version changes
```

---

## Version Detection Strategy

### Runtime Structure Availability Check

Since FFXIVClientStructs is loaded via Dalamud's hook system, implement detection for structure availability:

```csharp
public class FFXIVClientStructsDetector
{
    /// <summary>
    /// Check if a required structure/class is available in loaded FFXIVClientStructs
    /// </summary>
    public static bool IsStructureAvailable(string typeName, string? namespaceName = null)
    {
        try
        {
            var assembly = typeof(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject).Assembly;
            var fullName = namespaceName != null
                ? $"{namespaceName}.{typeName}"
                : typeName;

            var type = assembly.GetType(fullName);
            return type != null;
        }
        catch (Exception ex)
        {
            Dalamud.Logging.PluginLog.LogWarning($"Error detecting structure {typeName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get assembly version information
    /// </summary>
    public static string GetFFXIVClientStructsVersion()
    {
        try
        {
            var assembly = typeof(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject).Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Detection Failed";
        }
    }

    /// <summary>
    /// Validate core required structures for plugin operation
    /// </summary>
    public static bool ValidateCoreStructures()
    {
        var requiredStructures = new[]
        {
            ("Character", "FFXIVClientStructs.FFXIV.Client.Game.Character.Character"),
            ("CraftingLog", "FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyRecipeNote"),
            ("GatheringLog", "FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyGatheringNotes"),
            ("InventoryManager", "FFXIVClientStructs.FFXIV.Client.Game.InventoryManager"),
        };

        var missingStructures = requiredStructures
            .Where(s => !IsStructureAvailable(s.Item1, s.Item2))
            .ToList();

        if (missingStructures.Any())
        {
            Dalamud.Logging.PluginLog.LogError(
                $"Missing required structures: {string.Join(", ", missingStructures.Select(s => s.Item1))}");
            return false;
        }

        return true;
    }
}
```

---

## Fallback Strategy for Version Mismatches

### Detection and Recovery Flow

```
[Plugin Startup]
    ↓
[Validate Core Structures Available]
    ├─ Success → [Normal Operation]
    └─ Failure → [Activate Fallback Chain]
        ↓
    [Fallback 1: Graceful Degradation]
    - Log warnings for unavailable features
    - Disable affected UI elements
    - Continue with available functionality
        ↓
    [Fallback 2: Service Layer Adaptation]
    - Catch struct access exceptions
    - Return safe default values
    - Queue manual scan for next retry
        ↓
    [Fallback 3: User Notification]
    - Display warning: "FFXIVClientStructs version mismatch"
    - Suggest plugin restart after game patch
    - Recommend reporting to plugin maintainer
        ↓
    [Fallback 4: Plugin Disable]
    - If critical structures missing
    - Display error: "Plugin incompatible with current game version"
    - Prevent data corruption from misaligned memory reads
```

### Implementation Pattern

```csharp
public class StructureSafeAccessor
{
    /// <summary>
    /// Safely access game memory structures with fallback handling
    /// </summary>
    public static T? SafeRead<T>(Func<T> memoryReadFunc, string structureName)
        where T : class
    {
        try
        {
            // Attempt memory read
            return memoryReadFunc();
        }
        catch (NullReferenceException ex)
        {
            Dalamud.Logging.PluginLog.LogWarning(
                $"Structure mismatch detected in {structureName}: {ex.Message}");

            // Check if structure is still available
            if (!FFXIVClientStructsDetector.IsStructureAvailable(structureName))
            {
                Dalamud.Logging.PluginLog.LogError(
                    $"Required structure {structureName} not found in loaded FFXIVClientStructs");

                // Trigger validation and potential UI warning
                OnStructureMissing(structureName);
                return default;
            }

            // Structure exists but offset/field changed - retry may work after reload
            return default;
        }
        catch (AccessViolationException ex)
        {
            Dalamud.Logging.PluginLog.LogError(
                $"Memory access violation in {structureName}: {ex.Message}");
            return default;
        }
        catch (Exception ex)
        {
            Dalamud.Logging.PluginLog.LogError(
                $"Unexpected error accessing {structureName}: {ex.Message}");
            return default;
        }
    }

    private static void OnStructureMissing(string structureName)
    {
        // Notify UI layer to display warning
        // Queue for retry on next game patch detection
        // Potentially disable features using this structure
    }
}
```

### Graceful Degradation UI Pattern

```csharp
public class PluginStatusManager
{
    private bool _isVersionCompatible = true;
    private List<string> _unavailableStructures = new();

    public void DrawCompatibilityWarning()
    {
        if (!_isVersionCompatible)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.0f, 1.0f)); // Orange
            ImGui.TextWrapped("⚠ FFXIVClientStructs version mismatch detected");
            ImGui.TextWrapped(
                $"The following features are unavailable: {string.Join(", ", _unavailableStructures)}");
            ImGui.TextWrapped("This usually resolves after restarting the plugin following a game patch.");
            ImGui.PopStyleColor();
        }
    }

    public void CheckCompatibilityOnStartup()
    {
        _isVersionCompatible = FFXIVClientStructsDetector.ValidateCoreStructures();

        if (!_isVersionCompatible)
        {
            // Log version info for troubleshooting
            Dalamud.Logging.PluginLog.LogWarning(
                $"FFXIVClientStructs version: {FFXIVClientStructsDetector.GetFFXIVClientStructsVersion()}");
        }
    }
}
```

---

## Update Strategy

### When to Update FFXIVClientStructs

**Automatic Updates:**
- Handled by Dalamud framework
- No action required from plugin developer
- New Dalamud versions include updated FFXIVClientStructs

**Manual Update Triggers:**
1. **Game Patch Breaks Plugin**: If a major FFXIV patch breaks plugin functionality
   - Check Dalamud updates first (most likely fixes included)
   - If issue persists, may need to rebuild with custom FFXIVClientStructs

2. **New Feature Requires Newer Structures**: If implementing new FFXIV features
   - Monitor FFXIVClientStructs GitHub for relevant struct additions
   - Consider waiting for Dalamud integration
   - Document custom struct needs for future reference

### Custom FFXIVClientStructs Build (If Needed)

**Only pursue this if:**
- Dalamud doesn't include needed structures
- Waiting for Dalamud integration is not feasible
- You have specific game memory reverse engineering discoveries

**Configuration:**
```xml
<!-- Custom FFXIVClientStructs Reference -->
<Reference Include="FFXIVClientStructs">
  <HintPath>C:\Path\To\Your\Custom\FFXIVClientStructs.dll</HintPath>
  <Private>true</Private>  <!-- IMPORTANT: Set to true for custom builds -->
</Reference>
```

**Important Notes:**
- Setting `Private=true` copies the DLL to plugin output folder
- Custom builds must match game version at deployment time
- Dalamud discourages shipping custom ClientStructs to end users
- If custom build needed, contact Dalamud maintainer team first

---

## Implementation Checklist for T1 (.csproj)

When creating the .csproj file in Task T1, use this checklist:

- [ ] Add FFXIVClientStructs reference with Dalamud path
- [ ] Do NOT set `Private=true` (use Dalamud's version)
- [ ] Add Dalamud API reference (for logging and other services)
- [ ] Configure .NET target framework (4.7.2+ or .NET 6.0+)
- [ ] Ensure C# language version set to 10 or later
- [ ] Add reference to `System.Reflection` for version detection
- [ ] Document custom struct detection helper class location

---

## Troubleshooting Guide

### Symptom: "Could not find FFXIVClientStructs"
**Cause:** Dalamud not installed or path incorrect
**Solution:** Ensure XIVLauncher is installed with Dalamud addon system active

### Symptom: "Structure XXX not found in FFXIVClientStructs"
**Cause:** Game patch broke structure or structure moved
**Solution:**
1. Check compatibility with FFXIVClientStructsDetector
2. Restart plugin after game patch
3. Check for Dalamud updates

### Symptom: "AccessViolationException when reading memory"
**Cause:** Memory offset changed or invalid pointer
**Solution:**
1. Use SafeAccessor wrapper for all memory reads
2. Validate core structures on startup
3. Log struct version info for debugging
4. Implement fallback UI state

### Symptom: "Plugin works on Dev but not live FFXIV"
**Cause:** Version mismatch between dev environment and game
**Solution:**
1. Verify Dalamud version matches between environments
2. Clear plugin cache and reinstall
3. Check that you're using Dalamud-bundled FFXIVClientStructs, not custom build

---

## References

- **Dalamud Getting Started**: https://dalamud.dev/faq/getting-started/
- **Using Custom ClientStructs**: https://dalamud.dev/plugin-development/reverse-engineering/using-custom-cs/
- **FFXIVClientStructs Repository**: https://github.com/aers/FFXIVClientStructs
- **Reverse Engineering Guide**: https://dalamud.dev/plugin-development/reverse-engineering/

---

## Decision Log

**January 25, 2026 - T0 Research Complete**

- **Finding 1**: FFXIVClientStructs is NOT a NuGet package with semantic versioning
- **Finding 2**: Dalamud bundles FFXIVClientStructs and handles updates
- **Finding 3**: Version compatibility managed through Dalamud's patch system
- **Decision**: Use Dalamud-bundled version as primary strategy
- **Rationale**: Eliminates version management complexity, ensures compatibility
- **Fallback Plan**: Implement runtime detection and graceful degradation
- **Next Task**: T1 - Create .csproj with proper FFXIVClientStructs reference
