# Deep Dive: Privacy Retrofit Details

**Topic:** Exact data Artisan collects and how to remove all privacy-sensitive code
**Complexity:** Medium
**Relevance:** CRITICAL - This is the core differentiator for Akadaemia Anyder

---

## Overview

Artisan's current implementation includes three privacy-sensitive integrations:
1. **Universalis API** - Market board pricing queries
2. **Discord Webhooks** - Craft completion notifications
3. **Teamcraft Integration** - List import/export

This document details exactly what data is collected/transmitted and how to remove it completely.

---

## 1. Universalis API Integration

### What Data Is Collected

**File:** `Universalis/UniversalisClient.cs`

**Network Request:**
```csharp
public async Task<MarketboardData> GetMarketBoardDataAsync(uint itemId, string datacenter)
{
    var url = $"https://universalis.app/api/v2/marketboard/{itemId}?datacenter={datacenter}";

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var response = await client.GetAsync(url);
    var json = await response.Content.ReadAsStringAsync();

    return JsonConvert.DeserializeObject<MarketboardData>(json);
}
```

**Data Sent to Universalis:**
| Field | Value | Privacy Impact |
|-------|-------|----------------|
| `itemId` | Recipe ingredient ID (e.g., 5333 for Titanium Ore) | **LOW** - Game data, not personal |
| `datacenter` | Player's datacenter (e.g., "Aether", "Crystal") | **MEDIUM** - Reveals general region |
| HTTP Headers | User-Agent, Accept-Language | **LOW** - Standard browser headers |

**Data Received from Universalis:**
| Field | Purpose | Storage |
|-------|---------|---------|
| `currentMinimumPrice` | Cheapest listing | Displayed in UI, not stored |
| `averagePriceNQ` | Average NQ price | Displayed in UI, not stored |
| `averagePriceHQ` | Average HQ price | Displayed in UI, not stored |
| `listings` | All current market board listings | Not used by Artisan |

**Privacy Analysis:**
- ✅ No character names sent
- ✅ No world ID sent (only datacenter)
- ✅ Query is anonymized
- ⚠️ **Reveals crafting patterns** (which items you're researching)
- ⚠️ Universalis logs could theoretically correlate queries by IP/timestamp

**Universalis Privacy Policy:**
- Universalis itself is privacy-focused (no account required)
- Data is crowdsourced and anonymized
- BUT: Query logs may exist (IP addresses, timestamps)

**Verdict:** Low-medium privacy risk, but unnecessary for local-only plugin

### Where Universalis Is Used

**1. Recipe Window UI** (`UI/RecipeWindowUI.cs`)
```csharp
// Line ~500-600
private void DrawIngredientCosts(Recipe recipe)
{
    if (Configuration.UseUniversalis)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            var pricing = await UniversalisClient.GetMarketBoardDataAsync(
                ingredient.ItemId,
                GetPlayerDatacenter()
            );

            ImGui.Text($"{GetItemName(ingredient.ItemId)}:");
            ImGui.SameLine(300);
            ImGui.Text($"{pricing.currentMinimumPrice:N0} gil");
        }

        var totalCost = ingredients.Sum(i => i.Quantity * i.Price);
        ImGui.Text($"Total: {totalCost:N0} gil");
    }
}
```

**2. List Editor UI** (`UI/ListEditor.cs`)
```csharp
// Line ~800-900
private void DrawListCostEstimate(CraftingList list)
{
    if (Configuration.UseUniversalis)
    {
        var totalMaterialCost = 0;

        foreach (var item in list.Items)
        {
            var recipe = GetRecipe(item.RecipeID);
            foreach (var ingredient in recipe.Ingredients)
            {
                var pricing = await UniversalisClient.GetMarketBoardDataAsync(
                    ingredient.ItemId,
                    GetPlayerDatacenter()
                );

                totalMaterialCost += ingredient.Quantity * pricing.currentMinimumPrice;
            }
        }

        ImGui.Text($"Estimated cost: {totalMaterialCost:N0} gil");
    }
}
```

### Removal Strategy

**Step 1: Delete Universalis Module**
```bash
rm -rf Universalis/
```

**Step 2: Remove From Project File**
```xml
<!-- Remove from Artisan.csproj if present -->
<PackageReference Include="Universalis" Version="*" />
```

**Step 3: Stub Out UI Display**
```csharp
// In RecipeWindowUI.cs
private void DrawIngredientCosts(Recipe recipe)
{
    // REMOVED: Universalis pricing
    ImGui.TextDisabled("Market pricing disabled (privacy mode)");

    // NEW: Local inventory availability
    var availability = _repository.GetMaterialAvailability(ingredient.ItemId);
    ImGui.Text($"{GetItemName(ingredient.ItemId)}:");
    ImGui.SameLine(300);
    ImGui.Text($"Have: {availability.Total}");

    if (ImGui.IsItemHovered())
    {
        ImGui.SetTooltip(
            $"Inventory: {availability.InInventory}\n" +
            $"Saddlebag: {availability.InSaddlebag}\n" +
            $"Retainers: {availability.InRetainers}"
        );
    }
}
```

**Step 4: Remove Configuration Properties**
```csharp
// In Configuration.cs, DELETE:
public bool UseUniversalis { get; set; }
public bool UseSolverEstimates { get; set; }  // Uses Universalis for cost estimation
```

**Step 5: Verify No References Remain**
```bash
# Search for Universalis usage
grep -r "Universalis" --include="*.cs" .

# Expected: Zero results
```

**Estimated Effort:** 2 hours

---

## 2. Discord Webhook Integration

### What Data Is Transmitted

**File:** `Configuration.cs` (webhook URL storage), usage unclear from analysis

**Configuration:**
```csharp
public class Configuration
{
    public bool UsingDiscordHooks { get; set; } = false;
    public string? DiscordWebhookUrl { get; set; } = null;
}
```

**Likely Payload** (inferred from typical Discord webhook usage):
```json
{
  "content": "Craft completed!",
  "embeds": [{
    "title": "Artisan Notification",
    "description": "Crafted 10x High Steel Ingot (HQ: 8)",
    "color": 3447003,
    "fields": [
      {"name": "Character", "value": "Player Name"},
      {"name": "Job", "value": "Blacksmith"},
      {"name": "Time", "value": "15:30:42"}
    ]
  }]
}
```

**Privacy Analysis:**
- ⚠️ **Character name** likely included in notification
- ⚠️ **Job/class** revealed
- ⚠️ **Crafting activity** logged (what items, when)
- ⚠️ Webhook URL stored in plaintext in config
- ⚠️ Discord logs all webhook calls (IP, timestamp, payload)

**Risk Level:** MEDIUM
- User provides webhook URL voluntarily
- But character name transmission may be unexpected
- Discord retains logs indefinitely

### Where Webhooks Are Used

**Search Results:**
```bash
grep -r "Discord" --include="*.cs" .

# Found in:
# - Configuration.cs (storage)
# - Likely in Autocraft/Endurance.cs (completion events)
# - Possibly in CraftingProcessor.cs (success/failure events)
```

**Likely Usage Pattern:**
```csharp
// In Autocraft/Endurance.cs
private void OnCraftComplete(CraftResult result)
{
    if (Configuration.UsingDiscordHooks && !string.IsNullOrEmpty(Configuration.DiscordWebhookUrl))
    {
        SendDiscordNotification(new
        {
            content = $"Craft completed: {result.ItemName} (HQ: {result.WasHQ})",
            username = "Artisan Notifier"
        });
    }
}

private async void SendDiscordNotification(object payload)
{
    using var client = new Discord.Net.Webhook.DiscordWebhookClient(Configuration.DiscordWebhookUrl);
    await client.SendMessageAsync(JsonConvert.SerializeObject(payload));
}
```

### Removal Strategy

**Step 1: Remove NuGet Package**
```bash
dotnet remove package Discord.Net.Webhook
```

**Step 2: Remove Configuration Properties**
```csharp
// In Configuration.cs, DELETE:
public bool UsingDiscordHooks { get; set; }
public string? DiscordWebhookUrl { get; set; }
```

**Step 3: Remove Webhook Calls**
```bash
# Find all SendDiscordNotification or DiscordWebhookClient usage
grep -r "Discord\|Webhook" --include="*.cs" .

# Remove those method calls
```

**Step 4: Remove Settings UI**
```csharp
// In UI/PluginUI.cs Settings tab, DELETE:
ImGui.Checkbox("Enable Discord notifications", ref config.UsingDiscordHooks);
ImGui.InputText("Webhook URL", ref config.DiscordWebhookUrl, 512);
```

**Estimated Effort:** 1 hour

---

## 3. Teamcraft List Integration

### What Data Is Shared

**File:** `CraftingList/Teamcraft.cs`

**Export Format:**
```csharp
public string ExportToTeamcraft(CraftingList list)
{
    var teamcraftData = new
    {
        name = list.Name,
        items = list.Items.Select(item => new
        {
            id = item.RecipeID,
            amount = item.Quantity,
            done = item.QuantityCrafted,
            // Other metadata
        }).ToList()
    };

    var json = JsonConvert.SerializeObject(teamcraftData);
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
}
```

**Import Process:**
1. User visits teamcraft.fr website
2. User creates crafting list on website
3. User copies "share code" from website
4. User pastes into Artisan import dialog
5. Artisan decodes Base64 → parses JSON → creates local list

**Privacy Analysis:**
- ✅ No automatic upload (user-initiated copy/paste)
- ⚠️ **List names** may contain personal info ("Guild event", "Client order for XYZ")
- ⚠️ **Recipe patterns** reveal gameplay style
- ⚠️ Teamcraft.fr stores lists in cloud (if user saves them there)

**Risk Level:** LOW
- User explicitly chooses to share
- No automatic background transmission
- But enables data exfiltration if user doesn't understand privacy implications

### Where Teamcraft Is Used

**UI Integration:**
```csharp
// In UI/ListEditor.cs or CraftingListUI.cs
if (ImGui.Button("Import from Teamcraft"))
{
    ImGui.SetClipboardText("");
    ImGui.OpenPopup("TeamcraftImportPopup");
}

if (ImGui.BeginPopup("TeamcraftImportPopup"))
{
    ImGui.Text("Paste Teamcraft share code:");
    ImGui.InputTextMultiline("##teamcraft", ref _importBuffer, 4096);

    if (ImGui.Button("Import"))
    {
        var list = Teamcraft.ImportList(_importBuffer);
        Configuration.CraftingLists.Add(list);
        ImGui.CloseCurrentPopup();
    }

    ImGui.EndPopup();
}

if (ImGui.Button("Export to Teamcraft"))
{
    var shareCode = Teamcraft.ExportList(currentList);
    ImGui.SetClipboardText(shareCode);
    // Show tooltip: "Copied to clipboard! Paste into teamcraft.fr"
}
```

### Removal Strategy

**Step 1: Delete Teamcraft.cs**
```bash
rm CraftingList/Teamcraft.cs
```

**Step 2: Remove UI Buttons**
```csharp
// In CraftingListUI.cs or ListEditor.cs, DELETE:
if (ImGui.Button("Import from Teamcraft")) { ... }
if (ImGui.Button("Export to Teamcraft")) { ... }
```

**Step 3: Add Local Import/Export Instead**
```csharp
// NEW: Local file import/export (privacy-safe)
if (ImGui.Button("Import from JSON"))
{
    var filePath = FileDialog.OpenFile("*.json");
    if (!string.IsNullOrEmpty(filePath))
    {
        var json = File.ReadAllText(filePath);
        var list = JsonConvert.DeserializeObject<CraftingList>(json);
        _repository.SaveCraftingList(list);
    }
}

if (ImGui.Button("Export to JSON"))
{
    var filePath = FileDialog.SaveFile("*.json");
    if (!string.IsNullOrEmpty(filePath))
    {
        var json = JsonConvert.SerializeObject(currentList, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }
}
```

**Estimated Effort:** 1 hour

---

## 4. Configuration Data Privacy

### What's Stored in Configuration

**File:** `Configuration.cs`, serialized to `Artisan.json`

**Privacy-Sensitive Data:**
```csharp
public class Configuration
{
    // Character identification
    public string? CharacterName { get; set; }
    public uint WorldId { get; set; }
    public string? Datacenter { get; set; }

    // Retainer identification
    public List<RetainerInfo> Retainers { get; set; } = new();

    // External service credentials
    public string? DiscordWebhookUrl { get; set; }  // ⚠️ REMOVE

    // Crafting history
    public Dictionary<uint, RecipeConfig> RecipeConfigs { get; set; }  // Recipe preferences

    // Lists (potentially sensitive)
    public List<CraftingList> CraftingLists { get; set; }  // May contain personal notes
}

public class RetainerInfo
{
    public ulong RetainerId { get; set; }
    public string Name { get; set; }  // ⚠️ User-chosen names may be personal
}
```

**Privacy Analysis:**

| Data Field | Risk | Reason | Action |
|------------|------|--------|--------|
| CharacterName | MEDIUM | Identifies player | Make optional with consent |
| WorldId | LOW | Game data | OK to keep |
| Datacenter | LOW | General region | OK to keep |
| RetainerId | LOW | Anonymous ID | OK to keep |
| RetainerName | LOW-MEDIUM | User-chosen name | Make optional |
| DiscordWebhookUrl | HIGH | Third-party credential | **REMOVE** |
| CraftingLists | LOW | Offline data | OK to keep |
| RecipeConfigs | LOW | Gameplay preferences | OK to keep |

### Storage Location

**Default Path:**
```
%APPDATA%\XIVLauncher\pluginConfigs\Artisan\Artisan.json
```

**File Permissions:**
- Read/Write by current user
- Not encrypted (plaintext JSON)
- Backed up by XIVLauncher settings sync (if enabled)

**Privacy Improvement:**
```csharp
public class PrivacySettings
{
    // All default to privacy-maximizing
    public bool StoreCharacterName { get; set; } = false;
    public bool StoreRetainerNames { get; set; } = false;
    public bool AllowExternalSharing { get; set; } = false;

    // Explicit consent required
    public bool HasAcknowledgedPrivacyPolicy { get; set; } = false;
}
```

---

## 5. Comparison: Teamcraft vs Artisan vs Akadaemia Anyder

### Data Collection Comparison

| Data Type | Teamcraft Desktop | Artisan (Original) | Akadaemia Anyder (Fork) |
|-----------|-------------------|--------------------|-------------------------|
| **Character name** | ✅ Uploaded to Gubal | ⚠️ Stored locally | ❌ Not stored (opt-in) |
| **World/DC** | ✅ Uploaded | ⚠️ Stored locally | ✅ Stored locally |
| **GPS coordinates** | ✅ Uploaded (fishing) | ❌ Not collected | ❌ Not collected |
| **Player stats** | ✅ Uploaded (fishing) | ❌ Not collected | ❌ Not collected |
| **Market queries** | ✅ Universalis (auto) | ⚠️ Universalis (opt-in) | ❌ None |
| **Crafting lists** | ✅ Cloud sync | ⚠️ Local + Teamcraft export | ✅ Local only |
| **Analytics** | ✅ Pirsch | ❌ None | ❌ None |
| **User IDs** | ✅ Firebase UID | ❌ None | ❌ None |

### Network Request Comparison

| Tool | Endpoints | Data Sent | Frequency |
|------|-----------|-----------|-----------|
| **Teamcraft** | Gubal GraphQL, Universalis, Firebase, Pirsch | Character name, fishing data, GPS, stats | Continuous (packet capture) |
| **Artisan** | Universalis API | Item IDs, datacenter | On-demand (user triggers) |
| **Akadaemia Anyder** | **None** | **Nothing** | **Never** |

---

## 6. Privacy Verification Strategies

### Static Analysis (Pre-Runtime)

**Script:** `tests/verify-privacy.ps1`

```powershell
param(
    [string]$ProjectRoot = "C:\Code\akadaemia-anyder\SamplePlugin\Modules\Artisan"
)

Write-Host "Running privacy verification..." -ForegroundColor Cyan

# 1. Check for HttpClient usage
Write-Host "`n[1/5] Checking for HTTP clients..." -ForegroundColor Yellow
$httpResults = Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.cs" |
    Select-String -Pattern "HttpClient|WebRequest|WebClient|RestClient"

if ($httpResults) {
    Write-Host "  ✗ Found HTTP client usage:" -ForegroundColor Red
    $httpResults | ForEach-Object { Write-Host "    $($_.Filename):$($_.LineNumber)" }
    $failed = $true
} else {
    Write-Host "  ✓ No HTTP clients found" -ForegroundColor Green
}

# 2. Check for external API endpoints
Write-Host "`n[2/5] Checking for external API endpoints..." -ForegroundColor Yellow
$apiResults = Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.cs" |
    Select-String -Pattern "https?://|api\."

if ($apiResults) {
    Write-Host "  ✗ Found API endpoints:" -ForegroundColor Red
    $apiResults | ForEach-Object { Write-Host "    $($_.Filename):$($_.LineNumber) - $($_.Line.Trim())" }
    $failed = $true
} else {
    Write-Host "  ✓ No API endpoints found" -ForegroundColor Green
}

# 3. Check for Discord/webhook references
Write-Host "`n[3/5] Checking for Discord/webhook code..." -ForegroundColor Yellow
$discordResults = Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.cs" |
    Select-String -Pattern "Discord|Webhook" -CaseSensitive:$false

if ($discordResults) {
    Write-Host "  ✗ Found Discord/webhook references:" -ForegroundColor Red
    $discordResults | ForEach-Object { Write-Host "    $($_.Filename):$($_.LineNumber)" }
    $failed = $true
} else {
    Write-Host "  ✓ No Discord/webhook code found" -ForegroundColor Green
}

# 4. Check for Teamcraft references
Write-Host "`n[4/5] Checking for Teamcraft integration..." -ForegroundColor Yellow
$teamcraftResults = Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.cs" |
    Select-String -Pattern "Teamcraft|teamcraft\.fr" -CaseSensitive:$false

if ($teamcraftResults) {
    Write-Host "  ✗ Found Teamcraft references:" -ForegroundColor Red
    $teamcraftResults | ForEach-Object { Write-Host "    $($_.Filename):$($_.LineNumber)" }
    $failed = $true
} else {
    Write-Host "  ✓ No Teamcraft integration found" -ForegroundColor Green
}

# 5. Check NuGet packages
Write-Host "`n[5/5] Checking NuGet packages..." -ForegroundColor Yellow
$csprojContent = Get-Content "$ProjectRoot/../*.csproj" -Raw

$privacyPackages = @("Discord.Net", "Universalis", "Analytics", "Telemetry")
foreach ($package in $privacyPackages) {
    if ($csprojContent -match $package) {
        Write-Host "  ✗ Found privacy-sensitive package: $package" -ForegroundColor Red
        $failed = $true
    }
}

if (!$failed) {
    Write-Host "  ✓ No privacy-sensitive packages found" -ForegroundColor Green
}

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
if ($failed) {
    Write-Host "PRIVACY VERIFICATION FAILED" -ForegroundColor Red
    Write-Host "Please remove flagged code before proceeding." -ForegroundColor Red
    exit 1
} else {
    Write-Host "✓ PRIVACY VERIFICATION PASSED" -ForegroundColor Green
    Write-Host "No external network calls detected." -ForegroundColor Green
    exit 0
}
```

### Dynamic Analysis (Runtime)

**Network Monitoring During In-Game Testing:**

**Tool 1: Wireshark**
```
1. Start Wireshark capture on network interface
2. Filter: http || https || tcp.port == 443
3. Launch FFXIV with Akadaemia Anyder plugin
4. Perform crafting operations
5. Check capture: Should see ONLY:
   - FFXIV game server connections (expected)
   - NO api.universalis.app
   - NO teamcraft.fr
   - NO discord.com
   - NO analytics services
```

**Tool 2: Process Monitor (Sysinternals)**
```
1. Launch Process Monitor
2. Filter: Process Name is "ffxiv_dx11.exe"
3. Filter: Operation is "TCP Connect" or "TCP Send"
4. Start FFXIV with plugin
5. Perform crafting
6. Verify: No unexpected TCP connections
```

**Tool 3: Built-in Logging**
```csharp
// Add to ArtisanPlugin.cs constructor
#if DEBUG
public ArtisanPlugin()
{
    // Log all network attempts (should be zero)
    AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
    {
        if (args.Exception is HttpRequestException ||
            args.Exception is WebException)
        {
            Log.Error($"[PRIVACY] Network exception detected: {args.Exception.Message}");
            Log.Error($"[PRIVACY] Stack trace: {args.Exception.StackTrace}");
        }
    };
}
#endif
```

---

## 7. Privacy Policy Documentation

### User-Facing Privacy Statement

**File:** `docs/PRIVACY_POLICY.md`

```markdown
# Akadaemia Anyder Privacy Policy

**Last Updated:** 2026-01-26

## Our Privacy Commitment

**Your data never leaves your computer. Period.**

Akadaemia Anyder is designed from the ground up to be privacy-first:

✅ **100% Local Storage** - All data stored in SQLite database on your machine
✅ **No Network Requests** - Zero external API calls or data uploads
✅ **No User IDs** - No tracking, no accounts, no identifiers
✅ **No Analytics** - We don't know you're using our plugin
✅ **Open Source** - Audit the code yourself to verify

## What Data We Store Locally

### Required Data (Cannot Disable)
- **Recipe configurations** - Your preferred solver settings per recipe
- **Crafting lists** - Your queue of items to craft
- **Collection progress** - Your unlocked mounts, minions, etc. (anonymous counts)
- **Fishing/gathering logs** - Items you've caught/gathered with timestamps

### Optional Data (Disabled by Default)
- **Character name** - Only if you enable "Store character names" in Privacy Settings
- **Retainer names** - Only if you enable "Store retainer names"

### Data We NEVER Store
- ❌ User IDs or account identifiers
- ❌ Server/world names (unless you enable it)
- ❌ GPS coordinates (zone IDs only)
- ❌ IP addresses
- ❌ Hardware information
- ❌ Session tokens or credentials

## Data You Control

### Export Options
You can export your data at any time:
- **JSON Export** - Machine-readable format
- **CSV Export** - Spreadsheet-friendly format
- **Anonymization** - Enabled by default (strips character names, server info)

### Data Deletion
Delete your data at any time:
1. Go to Privacy Settings tab
2. Click "Delete All Data"
3. Confirm deletion
4. All local databases are erased

---

## Comparison to Other Tools

| Tool | Local Storage | Cloud Sync | Character Name | User IDs | Network Calls |
|------|---------------|------------|----------------|----------|---------------|
| **Akadaemia Anyder** | ✅ Yes | ❌ No | ⚠️ Optional | ❌ No | ❌ None |
| Teamcraft Desktop | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Continuous |
| FFXIV Collect | ❌ Web only | ✅ Required | ✅ Yes | ✅ Yes | ✅ Auto-sync |
| Artisan (Original) | ✅ Yes | ❌ No | ⚠️ Stored | ❌ No | ⚠️ Universalis |

---

## Technical Implementation

### How We Enforce Privacy

**1. Database Schema Enforces No PII:**
```sql
-- NO character_name column
-- NO user_id column
-- NO account_id column

CREATE TABLE fishing_log (
    id INTEGER PRIMARY KEY,
    timestamp INTEGER NOT NULL,
    item_id INTEGER NOT NULL,
    spot_id INTEGER NOT NULL,  -- Zone spot ID, NOT GPS coordinates
    -- [Other anonymous fields]
);
```

**2. Code Review Blocks Network Calls:**
- All pull requests reviewed for HTTP usage
- CI/CD pipeline runs privacy verification script
- Any HttpClient usage triggers build failure

**3. Open Source Verification:**
- Full source code available on GitHub
- No obfuscation, no closed-source components
- MIT + BSD-3-Clause licensed (auditable)

---

## Contact

Questions about privacy? [GitHub Issues](https://github.com/[repo]/issues)

We will NEVER ask for:
- Your character name
- Your login credentials
- Personal information
- Payment information (plugin is free and open source)
```

---

## 8. GDPR/CCPA Compliance

### Do We Need Compliance?

**GDPR Applicability:**
- ✅ **No personal data processed** - Character names are opt-in, world IDs are not PII
- ✅ **No data transmission** - All processing is local
- ✅ **No data controllers** - We don't store data on servers
- ✅ **User has full control** - Can delete all data at any time

**Verdict:** GDPR does not apply (no personal data processing by controllers)

**CCPA Applicability:**
- ✅ **No sale of personal information** - Nothing is sold or shared
- ✅ **No business purpose** - Free, open-source plugin

**Verdict:** CCPA does not apply (no commercial data processing)

### Best Practices Anyway

Even though regulations don't apply, follow privacy best practices:

1. **Data Minimization** - Only collect what's necessary
2. **Purpose Limitation** - Use data only for stated purpose (craft automation)
3. **Storage Limitation** - Don't keep data longer than needed
4. **Security** - Protect data from unauthorized access
5. **Transparency** - Clear privacy policy in plain language
6. **User Control** - Easy data deletion and export

---

## 9. Verification Checklist

### Pre-Release Privacy Audit

**Code Review:**
- [ ] No HttpClient instantiations
- [ ] No WebRequest or WebClient usage
- [ ] No external API endpoints in code
- [ ] No analytics libraries (Google Analytics, Mixpanel, etc.)
- [ ] No telemetry frameworks
- [ ] No user tracking code

**Configuration Review:**
- [ ] No Discord webhook fields
- [ ] No Universalis enable flag
- [ ] No Teamcraft integration settings
- [ ] Character name storage is opt-in (default: disabled)
- [ ] Privacy policy acknowledged checkbox added

**Database Review:**
- [ ] No character_name columns (unless explicitly opt-in table)
- [ ] No user_id columns
- [ ] No GPS coordinate storage (zone IDs only)
- [ ] No IP address storage
- [ ] All timestamps are UTC epoch (not identifiable)

**Dependency Review:**
- [ ] Discord.Net.Webhook removed from .csproj
- [ ] No analytics NuGet packages
- [ ] No telemetry packages
- [ ] All remaining packages are privacy-safe

**Runtime Testing:**
- [ ] Wireshark shows zero unexpected connections
- [ ] Process Monitor shows zero TCP connects to external services
- [ ] Privacy verification script passes
- [ ] No exceptions related to missing network code

**Documentation Review:**
- [ ] Privacy policy published
- [ ] README explicitly states "no network calls"
- [ ] Attribution to Artisan (BSD-3-Clause) included
- [ ] User guide mentions privacy features

---

## 10. Privacy Marketing

### How to Position Privacy Features

**Tagline Options:**
1. "Your data, your computer. Period."
2. "Crafting automation without the data exfiltration"
3. "Privacy-first FFXIV toolkit - zero network requests"

**README Badge:**
```markdown
[![Privacy-First](https://img.shields.io/badge/Privacy-Local%20Only-green)](docs/PRIVACY_POLICY.md)
[![No Network Calls](https://img.shields.io/badge/Network-None-success)](docs/PRIVACY_POLICY.md)
[![GDPR Compliant](https://img.shields.io/badge/GDPR-N%2FA%20(No%20Data%20Processing)-blue)](docs/PRIVACY_POLICY.md)
```

**Feature List:**
```markdown
## Privacy Features

- 🔒 **100% Local Storage** - All data in SQLite database on your PC
- 🚫 **Zero Network Requests** - No external API calls, ever
- 👤 **Anonymous by Default** - Character names not stored (opt-in only)
- 📊 **No Analytics** - We don't track usage
- 🔓 **Open Source** - Audit the code yourself (MIT + BSD-3-Clause)
- 📦 **Data Portability** - Export to JSON/CSV anytime
- 🗑️ **Easy Deletion** - One-click data wipe
```

**Reddit Post Template:**
```markdown
I forked Artisan and made it privacy-first

After reviewing Teamcraft's packet capture (uploads character names, GPS coordinates, stats to cloud database), I wanted crafting automation that keeps my data local.

So I forked Artisan (with permission - BSD-3-Clause licensed) and removed:
- Universalis API (market pricing queries)
- Discord webhooks (completion notifications)
- Teamcraft integration (list sharing)

Added instead:
- Local inventory tracking (saddlebags, retainers, all storage)
- Universal search ("Where are my glamour prisms?")
- Collection progress (mounts, minions, fishing logs)
- 100% local SQLite database (no character names, no user IDs)

All crafting automation still works. But your data never leaves your computer.

Open source, MIT + BSD-3-Clause licensed: [GitHub link]

For anyone worried about FFXIV tool privacy, give this a try.
```

---

**End of Privacy Retrofit Deep Dive**

Key takeaway: Privacy retrofit is straightforward - remove 3 modules (Universalis, Discord, Teamcraft), verify with automated scripts, document clearly in privacy policy. Total effort: ~4 hours.
