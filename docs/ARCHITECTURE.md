# Akadaemia Anyder - Architecture Documentation

**Project:** FFXIV Collection Tracker (Dalamud Plugin)
**Last Updated:** January 25, 2026
**Status:** Architecture specification with database fallback strategy

---

## Table of Contents

1. [Database Fallback Strategy](#database-fallback-strategy)
2. [System Architecture](#system-architecture)
3. [Database Schema](#database-schema)
4. [Error Handling](#error-handling)

---

## Database Fallback Strategy

### Overview

The plugin uses a multi-tier fallback strategy for database initialization. This ensures the plugin remains functional even when the primary storage mechanism fails, providing graceful degradation and automatic recovery.

---

### Tier 1: Normal SQLite File

**Status**: Primary initialization strategy (normal operation)

**Connection String**:
```
Data Source={AppData}\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db
```

**When Used**:
- First plugin load (most common)
- Normal operation on all subsequent loads
- Database file is accessible and writable

**Success Condition**:
1. Database file created or opened successfully
2. File is writable by the plugin process
3. Schema initialized without errors
4. Connection remains open for operations

**User Messaging**:
- None (silent success)
- Plugin logs: "Database initialized successfully"

**Implementation**:
```csharp
private bool TryInitializeDatabase()
{
    string configDir = _pluginInterface.GetPluginConfigDirectory();
    string dbPath = Path.Combine(configDir, "akadaemia.db");

    PluginLog.Information($"Database path: {dbPath}");

    // Tier 1: Normal initialization
    try
    {
        _database = new DatabaseContext(dbPath);
        _database.InitializeDatabaseAsync().Wait();
        PluginLog.Information("Database initialized successfully");
        return true;
    }
    catch (Exception ex)
    {
        PluginLog.Warning($"Initial database init failed: {ex.Message}");
    }

    // If Tier 1 fails, fall through to Tier 2
    return false;
}
```

**Typical Flow**:
```
User launches plugin
    ↓
[Tier 1 Init Attempted]
    ↓
File exists? Yes
    ↓
Can open connection? Yes
    ↓
✅ SUCCESS - Use file-based database
```

---

### Tier 2: Delete Corrupted + Retry

**Status**: Automatic recovery from corruption

**When Triggered**:
- Tier 1 initialization throws an exception
- Database file exists but is corrupted or locked
- Schema creation fails on existing database

**Action Sequence**:
1. Detect Tier 1 failure (catch exception)
2. Log: "Attempting recovery: deleting corrupted database..."
3. Delete the corrupted `.db` file
4. **ONE retry attempt** of Tier 1 initialization (with fresh database)
5. If retry succeeds → proceed with new database
6. If retry fails → fall through to Tier 3

**Success Condition**:
1. Corrupted file deleted
2. Fresh database file created
3. Schema initialized without errors
4. Connection active and ready

**Data Loss**:
- Previous database data is lost
- User warned with toast notification

**User Messaging**:
```
Toast Notification (5 seconds):
Title: "Database Recovery"
Message: "Database recovered from corruption. Previous data has been reset."
Type: Warning
```

**Implementation**:
```csharp
// Tier 2: Delete corrupted + retry
try
{
    PluginLog.Information("Attempting recovery: deleting corrupted database...");

    if (File.Exists(dbPath))
    {
        File.Delete(dbPath);
        PluginLog.Information("Deleted corrupted database file");
    }

    // Retry Tier 1 (ONE attempt only)
    _database = new DatabaseContext(dbPath);
    _database.InitializeDatabaseAsync().Wait();
    PluginLog.Information("Database re-initialized after recovery");

    // Notify user of recovery
    ShowRecoveryToast();
    return true;
}
catch (Exception ex)
{
    PluginLog.Error($"Database recovery failed: {ex.Message}");
}

// If Tier 2 fails, fall through to Tier 3
return false;
```

**Typical Flow**:
```
[Tier 1 Failed]
    ↓
File corrupted or unreadable
    ↓
[Tier 2 Init Attempted]
    ↓
Delete corrupted file? Yes
    ↓
Retry file-based init? Yes
    ↓
✅ SUCCESS - Use fresh file-based database
    ↓
Show toast: "Database recovered from corruption"
```

**When Tier 2 Fails**:
- File permissions prevent deletion
- Filesystem is read-only
- Disk space exhausted (can't create new file)
- Retry still fails (structural issue)
→ Fall through to Tier 3

---

### Tier 3: In-Memory SQLite Database

**Status**: Fallback for persistent storage failure

**When Triggered**:
- Tier 2 failed (filesystem issues)
- File-based database initialization impossible
- Read-only filesystem or permission errors
- Disk space unavailable

**Connection String**:
```
Data Source=:memory:
```

**Success Condition**:
1. In-memory SQLite database allocated
2. Schema initialized in memory
3. Connection active

**Data Persistence**:
- **NO data persistence across sessions**
- Data exists for current plugin session only
- Lost when plugin unloads or game exits
- Recreated fresh on next plugin load

**User Messaging**:
```
Toast Notification (7 seconds):
Title: "Data Loss Warning"
Message: "Running in memory-only mode - data will not persist across game sessions.
Check plugin logs for file system errors."
Type: Error
```

**Implementation**:
```csharp
// Tier 3: In-memory fallback
try
{
    PluginLog.Warning(
        "Falling back to in-memory database (data will NOT persist). " +
        "This indicates a filesystem or permissions issue.");

    _database = new DatabaseContext(":memory:");
    _database.InitializeDatabaseAsync().Wait();
    PluginLog.Information("In-memory database initialized");

    // Notify user of severe degradation
    ShowMemoryFallbackToast();

    _databaseTier = DatabaseTier.Tier3;
    return true;
}
catch (Exception ex)
{
    PluginLog.Error($"In-memory database failed: {ex.Message}");
}

// If Tier 3 fails, enter Degraded Mode
return false;
```

**Typical Flow**:
```
[Tier 1 Failed] (file-based init error)
    ↓
[Tier 2 Failed] (recovery unsuccessful)
    ↓
[Tier 3 Init Attempted]
    ↓
Allocate in-memory database? Yes
    ↓
✅ SUCCESS - Use in-memory database
    ↓
Show toast: "Running in memory-only mode - data will not persist"
    ↓
Track: _databaseTier = DatabaseTier.Tier3
```

**When Tier 3 Fails**:
- Out of memory (extreme case)
- SQLite in-memory not supported (should not happen)
→ Enter Degraded Mode

---

### Tier 4: Degraded Mode (All Tiers Failed)

**Status**: Error state - plugin loaded but non-functional

**When Triggered**:
- All 3 tiers throw exceptions
- Plugin cannot initialize any database storage
- Both file and memory databases failed

**Plugin Behavior**:
- ✅ Plugin loads without crash
- ✅ UI renders with error message
- ✅ User can view error details
- ❌ No data persistence layer
- ❌ Scanning disabled (would write to database)
- ❌ Export/import disabled (no storage)
- ❌ Configuration not saved

**User Messaging**:

**Error Window (Always Visible)**:
```
┌─────────────────────────────────────────────────┐
│ Akadaemia Anyder - DATABASE ERROR               │
├─────────────────────────────────────────────────┤
│                                                 │
│ ⚠️  DATABASE ERROR                              │
│                                                 │
│ The plugin failed to initialize the database.   │
│ Running in fallback mode (no data persistence). │
│                                                 │
│ What this means:                                │
│ • Scanning is disabled                          │
│ • Data cannot be saved                          │
│ • Export/import unavailable                     │
│                                                 │
│ What you can do:                                │
│ • Check plugin logs for details                 │
│ • Verify file permissions in plugin folder     │
│ • Verify sufficient disk space available        │
│ • Restart the plugin                            │
│                                                 │
│ [View Logs]  [Retry]                            │
│                                                 │
└─────────────────────────────────────────────────┘
```

**Plugin Logs**:
```
[FATAL] Failed to initialize database on all tiers
[ERROR] Tier 1 (File): {error message}
[ERROR] Tier 2 (Recovery): {error message}
[ERROR] Tier 3 (Memory): {error message}
[FATAL] Plugin loaded in degraded mode - database unavailable
```

**Implementation**:
```csharp
// Degraded Mode: All tiers failed
private void EnterDegradedMode(Exception finalException)
{
    PluginLog.Fatal(
        $"Failed to initialize database on all tiers. " +
        $"Final error: {finalException.Message}");

    _isInDegradedMode = true;
    _databaseTier = DatabaseTier.Degraded;
    _lastError = finalException;

    // Show error UI on next draw
    _shouldShowErrorWindow = true;
}

public void DrawErrorWindow()
{
    if (!_shouldShowErrorWindow) return;

    if (ImGui.Begin("Akadaemia Anyder - DATABASE ERROR",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
    {
        ImGui.TextColored(new Vector4(1, 0, 0, 1), "⚠ DATABASE ERROR");
        ImGui.Separator();

        ImGui.TextWrapped(
            "The plugin failed to initialize the database on all three tiers. " +
            "Running in degraded mode (no data persistence). Check the plugin logs for details.");

        ImGui.Spacing();
        ImGui.Text("Error details:");
        ImGui.TextWrapped(_lastError?.Message ?? "Unknown error");

        ImGui.Spacing();
        if (ImGui.Button("Retry Initialization"))
        {
            AttemptRecoveryRetry();
        }

        ImGui.SameLine();
        if (ImGui.Button("View Logs"))
        {
            // Open log viewer
        }

        ImGui.End();
    }
}

public void OnScanClicked()
{
    if (_isInDegradedMode)
    {
        ShowErrorToast("Database unavailable - scanning disabled");
        PluginLog.Warning("Scan attempt in degraded mode - blocked");
        return;
    }

    // Normal scan logic
}

public void OnExportClicked()
{
    if (_isInDegradedMode)
    {
        ShowErrorToast("Database unavailable - export disabled");
        return;
    }

    // Normal export logic
}
```

**Typical Flow**:
```
[Tier 1 Failed]
    ↓
[Tier 2 Failed]
    ↓
[Tier 3 Failed]
    ↓
[Degraded Mode Entered]
    ↓
✅ UI loads with error message
    ✅ User can read error
    ❌ Scan disabled
    ❌ Export disabled
    ❌ Data not persisted
```

---

### Health Check API

**Purpose**:
- Determine current database tier at runtime
- Display health status in UI
- Log startup status
- Make operational decisions based on tier

**Method Signature**:
```csharp
public DatabaseTier GetHealthStatus()
{
    return _databaseTier;
}

public enum DatabaseTier
{
    /// <summary>
    /// Tier 1: File-based SQLite database (normal operation)
    /// </summary>
    Tier1 = 0,

    /// <summary>
    /// Tier 2: Recovery succeeded - fresh file-based database
    /// </summary>
    Tier2 = 1,

    /// <summary>
    /// Tier 3: In-memory SQLite (session-only, no persistence)
    /// </summary>
    Tier3 = 2,

    /// <summary>
    /// Degraded: All tiers failed - no persistence layer
    /// </summary>
    Degraded = 3
}
```

**DatabaseContext Implementation**:
```csharp
public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private DatabaseTier _tier = DatabaseTier.Degraded;

    public DatabaseContext(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        // Determine tier based on connection string
        if (dbPath == ":memory:")
        {
            _tier = DatabaseTier.Tier3;
        }
        else
        {
            _tier = DatabaseTier.Tier1; // File-based (Tier 1 or 2)
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        // Execute schema creation SQL
        await ExecuteSchemaAsync();
    }

    public DatabaseTier GetHealthStatus()
    {
        return _tier;
    }

    public void SetTier(DatabaseTier tier)
    {
        _tier = tier;
    }

    public SqliteConnection Connection => _connection;

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

**Usage in Plugin.cs**:
```csharp
public void Initialize(IDalamudPluginInterface pluginInterface)
{
    _pluginInterface = pluginInterface;

    try
    {
        TryInitializeDatabase();

        // Log health status
        var healthStatus = _database?.GetHealthStatus();
        switch (healthStatus)
        {
            case DatabaseTier.Tier1:
                PluginLog.Information("Database: Normal (File-based)");
                break;
            case DatabaseTier.Tier2:
                PluginLog.Warning("Database: Recovered (fresh file after corruption)");
                break;
            case DatabaseTier.Tier3:
                PluginLog.Error("Database: In-memory (no persistence)");
                break;
            case DatabaseTier.Degraded:
                PluginLog.Fatal("Database: Degraded (all tiers failed)");
                break;
        }

        // Pass health status to UI
        if (_mainWindow != null)
        {
            _mainWindow.SetDatabaseTier(healthStatus ?? DatabaseTier.Degraded);
        }
    }
    catch (Exception ex)
    {
        PluginLog.Fatal($"Fatal initialization error: {ex}");
        throw;
    }
}
```

**UI Display**:
```csharp
public class MainWindow : Window
{
    private DatabaseTier _databaseTier = DatabaseTier.Degraded;

    public void SetDatabaseTier(DatabaseTier tier)
    {
        _databaseTier = tier;
    }

    public override void Draw()
    {
        // Display tier indicator
        string tierDisplay = _databaseTier switch
        {
            DatabaseTier.Tier1 => "✓ Database: Normal",
            DatabaseTier.Tier2 => "⚠ Database: Recovered",
            DatabaseTier.Tier3 => "✗ Database: Memory-only",
            DatabaseTier.Degraded => "✗ Database: Error",
            _ => "? Database: Unknown"
        };

        Vector4 tierColor = _databaseTier switch
        {
            DatabaseTier.Tier1 => new Vector4(0, 1, 0, 1),      // Green
            DatabaseTier.Tier2 => new Vector4(1, 1, 0, 1),      // Yellow
            DatabaseTier.Tier3 => new Vector4(1, 0.5f, 0, 1),   // Orange
            DatabaseTier.Degraded => new Vector4(1, 0, 0, 1),   // Red
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1)               // Gray
        };

        ImGui.TextColored(tierColor, tierDisplay);
    }
}
```

---

## Summary Table

| Tier | Type | Persistence | Failure Handling | User Impact |
|------|------|-------------|------------------|------------|
| **Tier 1** | File-based SQLite | Yes | N/A | Normal operation |
| **Tier 2** | File-based (recovered) | Yes | Delete + retry | Data loss, silent recovery |
| **Tier 3** | In-memory SQLite | No | N/A | Session-only data, warning toast |
| **Degraded** | None | No | Error UI shown | Scanning disabled, error window |

---

## Initialization Decision Tree

```
Plugin Initialize()
    ↓
Attempt Tier 1 (File-based)
    ├─ Success → ✅ Use Tier 1
    │
    └─ Exception → Attempt Tier 2
        ↓
        Delete corrupted file + Retry Tier 1
        ├─ Success → ⚠ Use Tier 2 (show recovery toast)
        │
        └─ Exception → Attempt Tier 3
            ↓
            Allocate in-memory database
            ├─ Success → ✗ Use Tier 3 (show memory-only warning)
            │
            └─ Exception → Degraded Mode
                ↓
                ✗ Show error UI, disable scanning
```

---

## System Architecture

The plugin follows a strict layered architecture with the database fallback strategy integrated at the infrastructure layer.

```
┌─────────────────────────────────────────────┐
│         Presentation Layer (UI)             │
│  - ImGui Windows with tier status display   │
│  - Error window for degraded mode           │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Application Layer (Logic)           │
│  - Collection aggregator                    │
│  - Progress calculator                      │
│  - Service with null-safety for degraded    │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Data Access Layer                   │
│  - Memory readers (FFXIVClientStructs)      │
│  - Database repository (with tier check)    │
│  - Export/import handlers                   │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Infrastructure Layer                │
│  - SQLite database (Tier 1, 2, or 3)        │
│  - Game memory (via Dalamud)                │
│  - File system (exports)                    │
│  - Multi-tier fallback strategy             │
└─────────────────────────────────────────────┘
```

---

## Database Schema

The database schema is identical across all three tiers (Tier 1, 2, and 3). The only difference is persistence:

```sql
CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS collections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    character_id INTEGER NOT NULL,
    character_name TEXT NOT NULL,
    world TEXT NOT NULL,
    collection_type TEXT NOT NULL,
    item_id INTEGER NOT NULL,
    item_name TEXT,
    is_unlocked BOOLEAN NOT NULL,
    unlocked_at TEXT,
    first_seen_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(character_id, collection_type, item_id)
);

CREATE TABLE IF NOT EXISTS recipes (
    collection_id INTEGER PRIMARY KEY,
    recipe_id INTEGER NOT NULL UNIQUE,
    recipe_level INTEGER,
    crafting_class TEXT NOT NULL,
    is_master_recipe BOOLEAN NOT NULL DEFAULT 0,
    master_book_id INTEGER,
    item_level INTEGER,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS gathering_nodes (
    collection_id INTEGER PRIMARY KEY,
    node_id INTEGER NOT NULL UNIQUE,
    gathering_class TEXT NOT NULL,
    zone TEXT,
    folklore_book_id INTEGER,
    node_level INTEGER,
    is_legendary BOOLEAN NOT NULL DEFAULT 0,
    is_ephemeral BOOLEAN NOT NULL DEFAULT 0,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS fishing_holes (
    collection_id INTEGER PRIMARY KEY,
    fish_id INTEGER NOT NULL UNIQUE,
    fishing_hole_id INTEGER NOT NULL,
    zone TEXT,
    bait TEXT,
    is_big_fish BOOLEAN NOT NULL DEFAULT 0,
    weather_requirements TEXT,
    time_requirements TEXT,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS completion_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    character_id INTEGER NOT NULL,
    snapshot_date TEXT NOT NULL,
    collection_type TEXT NOT NULL,
    total_items INTEGER NOT NULL,
    unlocked_count INTEGER NOT NULL,
    completion_percentage REAL NOT NULL,
    UNIQUE(character_id, snapshot_date, collection_type)
);

CREATE INDEX IF NOT EXISTS idx_collections_character ON collections(character_id, collection_type);
CREATE INDEX IF NOT EXISTS idx_collections_unlocked ON collections(is_unlocked, collection_type);
CREATE INDEX IF NOT EXISTS idx_recipes_class ON recipes(crafting_class);
CREATE INDEX IF NOT EXISTS idx_gathering_class ON gathering_nodes(gathering_class);
CREATE INDEX IF NOT EXISTS idx_snapshots_date ON completion_snapshots(character_id, snapshot_date DESC);
```

---

## Error Handling

The plugin's error handling strategy is integrated with the database fallback tiers:

1. **Tier 1 errors** → Logged, trigger Tier 2 attempt
2. **Tier 2 errors** → Logged, trigger Tier 3 attempt
3. **Tier 3 errors** → Logged, enter Degraded Mode
4. **Degraded Mode** → Show error UI, disable destructive operations

All errors are logged with full context for debugging.

---

**Document Status**: Complete
**Architecture Specification**: Database Fallback Strategy documented
**Implementation Ready**: Yes
