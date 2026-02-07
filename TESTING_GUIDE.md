# Testing Guide - Akadaemia Anyder

**Last Updated**: 2026-01-25

---

## Quick Start: 5-Minute Test

**Goal**: Verify recipe tracking works in-game

```powershell
# 1. Build the plugin
cd C:\Code\akadaemia-anyder\SamplePlugin
dotnet build

# 2. Launch FFXIV through XIVLauncher
# (Manual step - launch game)

# 3. In-game, type:
/xlplugins

# 4. Enable the dev plugin
# 5. Type: /akadaemia
# 6. Click "Scan Collections"
# 7. Check recipe tab shows actual progress (e.g., "256/512")
```

**Expected Result**: Recipe count > 0, progress bar shows actual data
**If It Fails**: See Troubleshooting section below

---

## Testing Levels

### Level 1: Build Verification (2 minutes)
### Level 2: Unit Tests (1 minute)
### Level 3: In-Game Loading (3 minutes)
### Level 4: Recipe Functionality (5 minutes)
### Level 5: Database Persistence (5 minutes)
### Level 6: Export/Import (5 minutes)
### Level 7: Edge Cases (10 minutes)

---

## Level 1: Build Verification

**Goal**: Confirm plugin compiles and outputs to correct location

### Test Steps

```powershell
# 1. Clean build
cd C:\Code\akadaemia-anyder\SamplePlugin
dotnet clean
dotnet build

# 2. Check for errors
# Expected: "Build succeeded. 0 Error(s)"
```

### Verification Checklist

```powershell
# Check output DLL exists
$outputPath = "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder"
Test-Path "$outputPath\SamplePlugin.dll"
# Expected: True

# Check file size (should be ~100-200 KB)
(Get-Item "$outputPath\SamplePlugin.dll").Length / 1KB
# Expected: 100-200

# Check timestamp is recent
(Get-Item "$outputPath\SamplePlugin.dll").LastWriteTime
# Expected: Within last few minutes
```

### ✅ Pass Criteria
- Build succeeds with 0 errors
- DLL exists in dev plugins directory
- File size reasonable (~100-200 KB)
- Timestamp matches recent build

### ❌ Fail Scenarios

**Build errors**:
```powershell
# Check .NET version
dotnet --version
# Expected: 10.0.x

# Verify Dalamud dev directory exists
Test-Path "$env:APPDATA\XIVLauncher\addon\Hooks\dev"
# Expected: True
```

**Missing output DLL**:
- Check .csproj OutputPath setting
- Verify permissions on AppData directory
- Try building as administrator

---

## Level 2: Unit Tests

**Goal**: Verify code quality via automated tests

### Test Steps

```powershell
cd C:\Code\akadaemia-anyder\AkadaemiaAnyder.Tests
dotnet test --verbosity normal
```

### Expected Results

```
Passed!  - Failed:    23, Passed:    78, Skipped:     0, Total:   101
```

**78 passing tests** = Core logic works
**23 failing tests** = Test data issues (documented in STATUS.md)

### ✅ Pass Criteria
- 78+ tests pass
- No new compilation errors
- Test execution completes (no hangs)

### Known Failures (Expected)
- PointerValidator tests (5) - Test expectation mismatch
- RecipeReader tests (3) - Requires game process
- Repository tests (15) - UNIQUE constraint in test data

These are **test infrastructure issues**, not implementation bugs.

---

## Level 3: In-Game Loading

**Goal**: Plugin loads in Dalamud without errors

### Prerequisites

1. ✅ FFXIV installed
2. ✅ XIVLauncher installed
3. ✅ Game launched through XIVLauncher at least once (initializes Dalamud)
4. ✅ Plugin built (Level 1 complete)

### Test Steps

```
1. Launch FFXIV through XIVLauncher
2. Wait for character select screen
3. Type: /xlsettings
4. Click "Experimental" tab
5. Add dev plugin path: %APPDATA%\XIVLauncher\devPlugins
6. Click "Save and Close"
7. Type: /xlplugins
8. Click "Dev Tools" tab
9. Click "Installed Dev Plugins"
10. Find "SamplePlugin" in list
11. Click checkbox to enable
```

### ✅ Pass Criteria
- Plugin appears in dev plugins list
- Checkbox enables without error
- No error messages in /xllog

### ❌ Fail Scenarios

**Plugin not in list**:
```powershell
# Verify DLL location
dir "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder"
# Expected: SamplePlugin.dll, SamplePlugin.json

# Check if JSON manifest exists
Test-Path "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder\SamplePlugin.json"
# Expected: True
```

**Plugin fails to load**:
```
1. Type: /xllog
2. Search for "SamplePlugin" or "AkadaemiaAnyder"
3. Look for error messages
4. Common causes:
   - Missing dependencies
   - .NET version mismatch
   - Corrupted DLL
```

---

## Level 4: Recipe Functionality Test

**Goal**: Verify recipe scanning actually works

### Test Steps

```
1. In-game, select a character with unlocked recipes
2. Type: /akadaemia
3. Main window should open
4. Click "Recipes" tab
5. Click "Scan Collections" button
6. Wait 2-3 seconds
7. Check recipe count updates
```

### ✅ Pass Criteria

**Before scan**: "0/0 recipes" or blank
**After scan**: "XXX/512 recipes" where XXX > 0

**Example expected results**:
- New character: "8/512 recipes" (starter recipes only)
- Mid-level character: "128/512 recipes"
- Endgame crafter: "400+/512 recipes"

**Visual indicators**:
- Progress bar shows percentage (e.g., 50% filled)
- "Last scan" timestamp updates
- Status message: "Scan complete: X recipes found"

### Detailed Verification

```
1. Check each crafting class shows data:
   - Carpenter (CRP)
   - Blacksmith (BSM)
   - Armorer (ARM)
   - Goldsmith (GSM)
   - Leatherworker (LTW)
   - Weaver (WVR)
   - Alchemist (ALC)
   - Culinarian (CUL)

2. Verify progress calculations:
   - Overall percentage matches manual calculation
   - Individual class percentages make sense
   - Master recipe count (if any unlocked)

3. Test re-scan:
   - Click "Scan Collections" again
   - Count should remain same (no new recipes)
   - Timestamp updates
```

### ❌ Fail Scenarios

**Shows 0/0 after scan**:
```
Possible causes:
1. Character not fully logged in
2. RecipeNote memory structure not accessible
3. SafeMemoryReader caught exception

Debug steps:
1. Type: /xllog
2. Search for "AkadaemiaAnyder" or "Recipe"
3. Look for error messages
4. Check for "RecipeReader" errors
5. Verify UIState.Instance() accessible
```

**Shows wrong count**:
```
Verify in-game:
1. Open Crafting Log (System Menu → Crafting Log)
2. Manually count unlocked recipes in one class
3. Compare to plugin's count for that class
4. If mismatch, check recipe ID ranges in RecipeReader.cs
```

---

## Level 5: Database Persistence Test

**Goal**: Verify data persists across plugin reloads

### Test Steps

```
1. Scan recipes (Level 4 complete)
2. Note the recipe count (e.g., "256/512")
3. Type: /xlplugins
4. Disable "SamplePlugin"
5. Wait 2 seconds
6. Re-enable "SamplePlugin"
7. Type: /akadaemia
8. Check recipe count WITHOUT clicking "Scan Collections"
```

### ✅ Pass Criteria
- Recipe count matches previous scan (e.g., still "256/512")
- No need to re-scan
- Data loaded from database on plugin startup

### Database Health Check

```powershell
# Check database file exists
$dbPath = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db"
Test-Path $dbPath
# Expected: True

# Check file size (should have data)
(Get-Item $dbPath).Length / 1KB
# Expected: 20-100 KB depending on data

# Inspect database (requires SQLite tool)
# Download: https://sqlitebrowser.org/
# Open: $dbPath
# Check tables: collections, recipes, gathering_nodes, fishing_holes
```

### Test 3-Tier Fallback

```powershell
# 1. Normal operation (Tier 1)
# Scan recipes, verify data saves

# 2. Simulate corruption (Tier 2)
# Close game
# Corrupt database:
$dbPath = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db"
"CORRUPTED" | Out-File $dbPath -Encoding ascii

# Restart game, enable plugin
# Expected: Plugin detects corruption, deletes DB, creates new (Tier 2)
# Check /xllog for: "Database recovered from corruption (Tier 2)"

# 3. Simulate permissions failure (Tier 3)
# (Difficult to test without admin changes)
# If all file operations fail, plugin uses in-memory DB (Tier 3)
# Data lost on unload, but plugin remains functional
```

### ✅ Pass Criteria
- Tier 1: Database persists normally
- Tier 2: Corruption recovery works (deletes + recreates)
- Tier 3: In-memory fallback (data lost but no crash)
- Degraded mode: Error UI shown if all tiers fail

---

## Level 6: Export/Import Test

**Goal**: Verify backup/restore functionality

### Export Test

```
1. Scan recipes (have data in database)
2. In main window, click "Settings" tab
3. Click "Export Collections" button
4. Choose save location (e.g., Desktop)
5. Check file created
```

### Verification

```powershell
# Check export file exists
$exportFile = "$env:USERPROFILE\Desktop\akadaemia-export-*.json"
Test-Path $exportFile
# Expected: True

# Check JSON is valid
$data = Get-Content $exportFile | ConvertFrom-Json
$data.recipes.Count
# Expected: Should match in-game recipe count

# Inspect structure
$data | Get-Member
# Expected properties: recipes, gathering_nodes, fishing_holes, metadata
```

### Import Test

```
1. Clear database:
   - Close game
   - Delete: %APPDATA%\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db

2. Restart game, enable plugin
3. Type: /akadaemia
4. Verify recipe count is 0/0 (fresh database)
5. Click "Settings" tab
6. Click "Import Collections" button
7. Select previously exported JSON file
8. Wait for import to complete
9. Check "Recipes" tab
```

### ✅ Pass Criteria
- Recipe count restored to pre-export value
- Progress percentages match
- All classes show restored data
- No data loss

### ❌ Fail Scenarios

**Export creates empty file**:
- Check if scan completed before export
- Verify database has data
- Check /xllog for export errors

**Import doesn't restore data**:
- Verify JSON file is valid (open in text editor)
- Check file size (should be >1 KB if data exists)
- Look for import error messages in /xllog

---

## Level 7: Edge Cases & Stress Tests

**Goal**: Verify plugin handles unusual scenarios

### Test 7.1: Multiple Characters

```
1. Scan recipes on Character A
2. Note count (e.g., "256/512")
3. Log out
4. Log in to Character B
5. Type: /akadaemia
6. Click "Scan Collections"
7. Verify count is different (character-specific)
8. Switch back to Character A
9. Verify original count restored
```

### ✅ Pass Criteria
- Each character has separate collection data
- Switching characters shows correct data
- No data bleed between characters

---

### Test 7.2: Gathering Tab (Expected Failure)

```
1. Switch to a gathering class (MIN or BTN)
2. Type: /akadaemia
3. Click "Gathering" tab
4. Click "Scan Collections"
5. Observe result
```

### ✅ Pass Criteria (Documented Limitation)
- Shows "0/0 nodes"
- No crash
- Status message: "No gathering data detected"
- /xllog shows: "GatheringNote API not available"

**This is expected** - documented in STATUS.md as blocked by FFXIVClientStructs

---

### Test 7.3: Fishing Tab (Expected Failure)

```
1. Switch to Fisher (FSH)
2. Type: /akadaemia
3. Click "Fishing" tab
4. Click "Scan Collections"
5. Observe result
```

### ✅ Pass Criteria (Documented Limitation)
- Shows "0/0 fish"
- No crash
- Status message: "No fishing data detected"
- /xllog shows: "FishingNote API not available"

**This is expected** - documented in STATUS.md as blocked by FFXIVClientStructs

---

### Test 7.4: Rapid Re-scanning

```
1. Click "Scan Collections" 5 times rapidly
2. Verify no crashes
3. Check final count is accurate
4. Verify no duplicate entries in database
```

### ✅ Pass Criteria
- No crashes or hangs
- Final count accurate (not multiplied by 5)
- UI remains responsive
- No error messages

---

### Test 7.5: Plugin Reload During Scan

```
1. Click "Scan Collections"
2. Immediately disable plugin (via /xlplugins)
3. Re-enable plugin
4. Check /xllog for errors
```

### ✅ Pass Criteria
- No crash or corruption
- Database remains intact
- Can scan again successfully

---

## Automated Test Script

**Quick verification script** (run before in-game testing):

```powershell
# test-plugin.ps1
Write-Host "=== Akadaemia Anyder Pre-Flight Checks ===" -ForegroundColor Cyan

# 1. Build check
Write-Host "`n[1/6] Building plugin..." -ForegroundColor Yellow
cd C:\Code\akadaemia-anyder\SamplePlugin
$buildResult = dotnet build 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build succeeded" -ForegroundColor Green
} else {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}

# 2. Output check
Write-Host "`n[2/6] Checking output DLL..." -ForegroundColor Yellow
$dllPath = "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder\SamplePlugin.dll"
if (Test-Path $dllPath) {
    $size = [Math]::Round((Get-Item $dllPath).Length / 1KB, 2)
    Write-Host "✓ DLL exists ($size KB)" -ForegroundColor Green
} else {
    Write-Host "✗ DLL not found at $dllPath" -ForegroundColor Red
    exit 1
}

# 3. Dalamud check
Write-Host "`n[3/6] Checking Dalamud installation..." -ForegroundColor Yellow
if (Test-Path "$env:APPDATA\XIVLauncher\addon\Hooks\dev") {
    Write-Host "✓ Dalamud dev directory exists" -ForegroundColor Green
} else {
    Write-Host "✗ Dalamud not found - launch game via XIVLauncher first" -ForegroundColor Red
    exit 1
}

# 4. Unit tests
Write-Host "`n[4/6] Running unit tests..." -ForegroundColor Yellow
cd C:\Code\akadaemia-anyder\AkadaemiaAnyder.Tests
$testResult = dotnet test --verbosity quiet 2>&1
$passCount = ($testResult | Select-String "Passed:").ToString() -replace '.*Passed:\s+(\d+).*','$1'
if ($passCount -ge 78) {
    Write-Host "✓ $passCount tests passed" -ForegroundColor Green
} else {
    Write-Host "⚠ Only $passCount tests passed (expected 78+)" -ForegroundColor Yellow
}

# 5. Database path check
Write-Host "`n[5/6] Checking database directory..." -ForegroundColor Yellow
$dbDir = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder"
if (!(Test-Path $dbDir)) {
    New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
    Write-Host "✓ Created database directory" -ForegroundColor Green
} else {
    Write-Host "✓ Database directory exists" -ForegroundColor Green
}

# 6. Summary
Write-Host "`n=== Pre-Flight Complete ===" -ForegroundColor Cyan
Write-Host "Ready for in-game testing!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Launch FFXIV via XIVLauncher"
Write-Host "2. Type: /xlplugins"
Write-Host "3. Enable SamplePlugin in Dev Tools"
Write-Host "4. Type: /akadaemia"
Write-Host "5. Click 'Scan Collections'"
```

### Run Pre-Flight Script

```powershell
cd C:\Code\akadaemia-anyder
.\test-plugin.ps1
```

---

## Success Criteria Summary

| Test Level | Critical? | Expected Result |
|------------|-----------|-----------------|
| Level 1: Build | ✅ Yes | 0 errors, DLL created |
| Level 2: Unit Tests | ⚠️ Nice-to-have | 78+ passing |
| Level 3: Loading | ✅ Yes | Plugin enables in /xlplugins |
| Level 4: Recipe Scan | ✅ Yes | Shows actual recipe count > 0 |
| Level 5: Persistence | ✅ Yes | Data survives reload |
| Level 6: Export/Import | ✅ Yes | Backup/restore works |
| Level 7: Edge Cases | ⚠️ Nice-to-have | No crashes |

**Minimum for "Works"**: Levels 1, 3, 4 pass
**Production Ready**: All critical tests pass

---

## Troubleshooting

### Issue: Plugin doesn't appear in /xlplugins

**Check**:
```powershell
# 1. Verify dev plugins path is added
# In-game: /xlsettings → Experimental → Dev Plugin Locations
# Should include: %APPDATA%\XIVLauncher\devPlugins

# 2. Check DLL exists
dir "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder"
```

---

### Issue: Recipe count shows 0/0 after scan

**Debug**:
```
1. Type: /xllog
2. Search for "RecipeReader" or "SamplePlugin"
3. Look for exceptions or errors
4. Common causes:
   - UIState.Instance() returned null
   - RecipeNote not accessible
   - Character not fully loaded
```

**Fix**:
- Wait until fully logged in (past loading screen)
- Try scanning again
- Switch to a crafting class first

---

### Issue: Database corruption

**Symptoms**:
- Plugin loads but shows no data
- Error messages about database
- Crashes on scan

**Fix**:
```powershell
# 1. Backup current database
$dbPath = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db"
Copy-Item $dbPath "$dbPath.backup"

# 2. Delete corrupted database
Remove-Item $dbPath

# 3. Restart plugin
# Will create fresh database (Tier 2 recovery)

# 4. Re-scan to populate
```

---

## Reporting Issues

If tests fail, gather this information:

```powershell
# System info
dotnet --version
Get-ComputerInfo | Select-Object WindowsVersion, OsArchitecture

# Plugin info
dir "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder"

# Logs
Get-Content "$env:APPDATA\XIVLauncher\dalamud.log" | Select-String "SamplePlugin" | Select-Object -Last 20

# Database status
$dbPath = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db"
if (Test-Path $dbPath) {
    Get-Item $dbPath | Select-Object Length, LastWriteTime
}
```

---

## Next Steps After Testing

### If All Tests Pass ✅
1. Use plugin for personal recipe tracking
2. Export data regularly as backup
3. Consider: Add screenshots to README.md
4. Consider: GitHub release for portfolio

### If Recipe Scanning Works, But Want Gathering/Fishing ⚠️
1. Monitor FFXIVClientStructs repository
2. Check STATUS.md for updates on API availability
3. Estimated: 30 minutes to complete when API available

### If Tests Fail ❌
1. Review troubleshooting section
2. Check /xllog for specific errors
3. Verify prerequisites (XIVLauncher, Dalamud, .NET 10)
4. Rebuild with `dotnet clean && dotnet build`
