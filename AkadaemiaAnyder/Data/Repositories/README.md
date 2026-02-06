# Repository Layer - Threading Documentation

**Last Updated:** 2026-02-06
**Project:** Akadaemia Anyder (FFXIV Collection Tracker)
**Scope:** Thread-safe data access patterns and concurrency handling

---

## Overview

The repository layer handles all database access for the Akadaemia Anyder plugin. This document covers threading considerations specific to Dalamud plugins, SQLite synchronization, and cache management patterns.

---

## 1. Dalamud Threading Model

### Framework Thread vs Background Threads

**Dalamud Architecture:**
```
┌─────────────────────────────────────────────┐
│   FFXIV Game Process (ffxiv_dx11.exe)       │
│   ┌──────────────────────────────────────┐  │
│   │   Framework Thread (ImGui/UI)        │  │
│   │   - Draw() called every frame        │  │
│   │   - Updates ImGui state              │  │
│   │   - Reads plugin fields (no locks)   │  │
│   └──────────────────────────────────────┘  │
│                    ↑                         │
│        Framework.Update() → Draw()           │
│                                              │
│   ┌──────────────────────────────────────┐  │
│   │  Background Thread Pool              │  │
│   │  - Task.Run() executes here          │  │
│   │  - Async repository operations       │  │
│   │  - No ImGui access from here         │  │
│   └──────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### Key Insight: No Dispatcher Needed

Unlike WPF or Windows Forms, **Dalamud does NOT require a Dispatcher** to marshal calls back to the UI thread:

**Why:**
- The framework's `Draw()` method is **always** called on the UI thread
- Plugin fields are read on the next `Draw()` frame
- Update pattern: background thread → store result in field → `Draw()` reads field next frame

**Implication:**
- Background database operations can safely write to plugin fields
- UI thread reads those fields without locks during `Draw()`
- No cross-thread marshalling required

---

### Threading Pattern: Fire-and-Forget + Field Updates

```csharp
// CORRECT Dalamud Pattern
public class CollectionUI : Window
{
    private bool _isScanning = false;
    private ScanResult? _lastResult;
    private DateTime _lastScanTime;

    // Background scan (off UI thread)
    private void OnScanButtonClicked()
    {
        if (_isScanning) return;

        _isScanning = true;
        _lastResult = null;

        // Fire background task (no await)
        _ = Task.Run(PerformScanAsync);
    }

    // Background execution (thread pool)
    private async Task PerformScanAsync()
    {
        try
        {
            var result = await _collectionService.ScanAllCollectionsAsync();

            // Store result in field (no locks needed)
            _lastResult = result;
            _lastScanTime = DateTime.Now;
            _isScanning = false;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Scan error: {ex.Message}");
            _isScanning = false;
        }
    }

    // UI thread (Draw called from Framework)
    public override void Draw()
    {
        if (!IsOpen) return;

        // Read fields set by background thread
        if (_isScanning)
        {
            ImGui.Text("🔄 Scanning...");
        }
        else if (_lastResult?.Success == true)
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), $"✓ Scan complete: {_lastResult.ItemsScanned} items");
        }
        else if (_lastResult?.Success == false)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"✗ Error: {_lastResult.ErrorMessage}");
        }

        if (ImGui.Button("Scan Now"))
        {
            OnScanButtonClicked();
        }
    }
}
```

**Key Pattern:**
1. Background thread modifies field: `_lastResult = result;`
2. UI thread reads field in `Draw()`: `if (_lastResult?.Success == true)`
3. No `lock{}`, no `Dispatcher`, no manual marshalling
4. Dalamud handles the frame synchronization automatically

---

## 2. SQLite Threading Considerations

### SQLite Default: Single Writer, Multiple Readers

**SQLite Concurrency Model:**
```
┌──────────────────────────────────────────┐
│        SQLite Database File              │
│        (single file on disk)             │
├──────────────────────────────────────────┤
│                                          │
│  ┌─────────────┐  ┌─────────────┐      │
│  │ Connection1 │  │ Connection2 │  ... │
│  │ (Reader)    │  │ (Reader)    │      │
│  └─────────────┘  └─────────────┘      │
│        ↓                ↓               │
│   Shared lock      Shared lock          │
│                                          │
│  ┌─────────────────────────────────┐    │
│  │      Writer (Exclusive Lock)    │    │
│  │  (blocks all readers)           │    │
│  └─────────────────────────────────┘    │
│        ↓                                │
│   Exclusive lock                        │
└──────────────────────────────────────────┘
```

### PRAGMA busy_timeout

**Purpose:** Tell SQLite how long to wait if database is locked by another connection

**Setting in CollectionRepository:**
```csharp
const int BusyTimeoutMs = 5000;  // Wait up to 5 seconds

using var cmd = context.Connection.CreateCommand();
cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs}";
cmd.ExecuteNonQuery();
```

**When It Triggers:**
- Thread A is writing (has exclusive lock)
- Thread B tries to read/write
- SQLite waits up to 5000ms for Thread A to finish
- If Thread A finishes before timeout, Thread B proceeds
- If timeout expires, "database is locked" exception

**Trade-off:**
- ✅ 5000ms: Tolerant of slow writes (covers temporary contention)
- ❌ 5000ms: Can feel unresponsive if main thread is blocked
- **Better approach:** Use async + RetryOnBusy pattern (see below)

---

### RetryOnBusy Pattern with Exponential Backoff

**Problem:** Busy timeout waits synchronously, blocking the thread

**Solution:** Catch SQLiteErrorCode = 5 (SQLITE_BUSY) and retry with backoff

```csharp
private const int MaxRetryAttempts = 3;
private const int BaseRetryDelayMs = 100;

private async Task<T> RetryOnBusy<T>(Func<Task<T>> operation)
{
    int attempts = 0;

    while (attempts < MaxRetryAttempts)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
        {
            attempts++;

            if (attempts >= MaxRetryAttempts)
            {
                log.Error($"Database locked after {MaxRetryAttempts} attempts");
                throw;
            }

            // Exponential backoff: 100ms, 200ms, 400ms
            int delayMs = BaseRetryDelayMs * (int)Math.Pow(2, attempts - 1);
            log.Warning($"Database busy, retrying in {delayMs}ms (attempt {attempts}/{MaxRetryAttempts})");

            await Task.Delay(delayMs);
        }
    }

    throw new InvalidOperationException("RetryOnBusy loop should not reach here");
}

// Usage in async methods:
public async Task<List<T>> GetAllAsync<T>() where T : CollectionEntry
{
    return await RetryOnBusy(async () =>
    {
        // ... query execution code ...
    });
}
```

**Retry Pattern:**
- **Attempt 1 fails:** Wait 100ms, retry
- **Attempt 2 fails:** Wait 200ms, retry
- **Attempt 3 fails:** Wait 400ms, retry
- **All fail:** Throw exception to caller

**Why This Works:**
- Other threads holding locks typically finish quickly
- 100-400ms backoff is acceptable for plugin operations
- Prevents "hammering" the database with busy retries
- Gives UI thread time to complete concurrent operations

---

### Single Connection Pattern

**Rule:** Use a **single SqliteConnection** per DatabaseContext across all threads

**Why:**
- SQLite connection pooling is per-connection
- Multiple connections = multiple locks = more contention
- Single connection serializes access (forced by SQLite)
- WAL (Write-Ahead Logging) mode mitigates read blocking

**Implementation:**
```csharp
public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;  // Single instance

    public DatabaseContext(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        // Enable WAL mode for better concurrency
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL";
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection Connection => _connection;

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

**WAL (Write-Ahead Logging) Mode:**
- Readers don't block writers (different files: main.db vs main.db-wal)
- Writers don't block readers
- Improves throughput for concurrent access
- Trade-off: Slightly slower writes, faster reads

---

### Async Operations: ExecuteNonQueryAsync / ExecuteReaderAsync

**All database operations must be async** to prevent blocking:

```csharp
// ✅ CORRECT: Async database operations
public async Task<int> InsertAsync<T>(T entry) where T : CollectionEntry
{
    using var command = _connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO collections (character_id, item_id, is_unlocked)
        VALUES (@charId, @itemId, @unlocked)
    ";
    command.Parameters.AddWithValue("@charId", entry.CharacterId);
    command.Parameters.AddWithValue("@itemId", entry.ItemId);
    command.Parameters.AddWithValue("@unlocked", entry.IsUnlocked);

    // ✅ Async: Doesn't block thread pool
    int rowsAffected = await command.ExecuteNonQueryAsync();
    return rowsAffected;
}

// ❌ WRONG: Synchronous database operations
public int InsertSync<T>(T entry) where T : CollectionEntry
{
    // ❌ BLOCKS thread - never do this
    int rowsAffected = command.ExecuteNonQuery();
    return rowsAffected;
}
```

**Async Library:** Microsoft.Data.Sqlite supports async:
- `ExecuteNonQueryAsync()` - INSERT/UPDATE/DELETE
- `ExecuteReaderAsync()` - SELECT queries
- `ExecuteScalarAsync()` - Single value returns

**Thread Behavior:**
- **Async:** Releases thread while waiting for I/O → other work can run
- **Sync:** Blocks thread → wastes thread pool resource

---

## 3. Cache Service Threading

### ConcurrentDictionary for Lock-Free Caching

**Pattern:** In-memory cache with automatic expiry

```csharp
public class CacheService<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _cache = new();
    private readonly TimeSpan _expiry;

    public CacheService(TimeSpan? expiry = null)
    {
        _expiry = expiry ?? TimeSpan.FromHours(1);
    }

    // ✅ Thread-safe: Uses ConcurrentDictionary atomicity
    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            // Check expiry on access
            if (DateTime.UtcNow - entry.CreatedAt > _expiry)
            {
                // Expired - remove and return null
                _cache.TryRemove(key, out _);
                value = default;
                return false;
            }

            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    // ✅ Thread-safe: AddOrUpdate is atomic
    public void Set(TKey key, TValue value)
    {
        var entry = new CacheEntry<TValue>(value, DateTime.UtcNow);
        _cache.AddOrUpdate(key, entry, (k, old) => entry);
    }

    // ✅ Thread-safe: Uses AddOrUpdate (not check-then-act)
    public TValue GetOrAdd(TKey key, Func<TValue> factory)
    {
        var entry = _cache.AddOrUpdate(
            key,
            _ => new CacheEntry<TValue>(factory(), DateTime.UtcNow),
            (_, existing) =>
            {
                // Check if expired
                if (DateTime.UtcNow - existing.CreatedAt > _expiry)
                {
                    return new CacheEntry<TValue>(factory(), DateTime.UtcNow);
                }
                return existing;
            }
        );

        return entry.Value;
    }

    private class CacheEntry<T>
    {
        public T Value { get; }
        public DateTime CreatedAt { get; }

        public CacheEntry(T value, DateTime createdAt)
        {
            Value = value;
            CreatedAt = createdAt;
        }
    }
}
```

**Why ConcurrentDictionary:**
- **AddOrUpdate:** Atomic operation (no race conditions)
- **TryGetValue:** Thread-safe read
- **No manual locks:** Lock-free implementation via compare-and-swap
- **Scales:** Multiple threads reading simultaneously (no contention)

**Time-Based Expiry:**
- Checked on every access
- Expired entries removed lazily (on read attempt)
- No background cleanup thread needed

---

## 4. Best Practices for Callers

### Pattern 1: Background Scan with UI Update

```csharp
// In UI (MainWindow.cs)
public class CollectionUI : Window
{
    private readonly ICollectionService _service;
    private bool _isScanning;
    private ScanResult? _lastResult;

    private void OnScanButtonClicked()
    {
        if (_isScanning) return;
        _isScanning = true;

        // Fire background task
        _ = Task.Run(async () =>
        {
            try
            {
                // ✅ Blocking database access is OK here (off UI thread)
                var result = await _service.ScanAllAsync();

                // ✅ Store result in field (no locks needed)
                _lastResult = result;
                _isScanning = false;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Scan failed: {ex}");
                _isScanning = false;
            }
        });
    }

    public override void Draw()
    {
        // ✅ Read fields on UI thread
        if (_isScanning)
        {
            ImGui.Text("Scanning...");
        }
        else if (_lastResult?.Success == true)
        {
            ImGui.Text($"Found {_lastResult.ItemsScanned} items");
        }

        if (ImGui.Button("Scan"))
        {
            OnScanButtonClicked();
        }
    }
}
```

**Correct Flow:**
1. ✅ Click button → fire Task.Run (doesn't block UI)
2. ✅ Background thread runs database queries (blocking OK here)
3. ✅ Store result in `_lastResult` field
4. ✅ Next Draw() frame reads `_lastResult`
5. ✅ UI updates automatically

**Incorrect Anti-Pattern:**
```csharp
// ❌ WRONG - blocks UI thread
private void OnScanButtonClicked()
{
    var result = _service.ScanAllAsync().Wait();  // ❌ BLOCKS UI!
    _lastResult = result;
}

// ❌ WRONG - tries to access UI from background thread
private async Task PerformScan()
{
    var result = await _service.ScanAllAsync();
    ImGui.Text(result.ToString());  // ❌ ImGui from background thread!
}
```

---

### Pattern 2: Fire-and-Forget Background Operations

```csharp
// Safe background operations that don't need UI feedback
private void LogCraftingSession(CraftingSessionData session)
{
    // ✅ Fire and forget - caller doesn't wait for completion
    _ = Task.Run(async () =>
    {
        await _repository.SaveSessionAsync(session);
        PluginLog.Information("Session logged");
    });
}

// With error handling
private void AutoSaveSettings()
{
    _ = Task.Run(async () =>
    {
        try
        {
            await _repository.SaveConfigAsync(_currentConfig);
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Auto-save failed: {ex.Message}");
            // Don't crash on save failure
        }
    });
}
```

---

### Pattern 3: Cache Lookups with Thread Safety

```csharp
// In CollectionService
private readonly CacheService<uint, Recipe> _recipeCache = new(TimeSpan.FromHours(1));

public async Task<Recipe?> GetRecipeAsync(uint recipeId)
{
    // ✅ Try cache first (lock-free read)
    if (_recipeCache.TryGetValue(recipeId, out var cached))
    {
        return cached;
    }

    // ✅ Load from database (async, blocks only this task)
    var recipe = await _repository.GetRecipeAsync(recipeId);

    if (recipe != null)
    {
        // ✅ Update cache (atomic)
        _recipeCache.Set(recipeId, recipe);
    }

    return recipe;
}

// Multiple threads can read cache simultaneously without contention
```

---

### Pattern 4: Bulk Operations with Transaction

```csharp
// ✅ Atomic bulk insert (all-or-nothing)
public async Task<int> BulkInsertAsync<T>(List<T> entries) where T : CollectionEntry
{
    using var transaction = _connection.BeginTransaction();
    try
    {
        int total = 0;
        foreach (var entry in entries)
        {
            // ✅ Retry pattern for each insert
            var count = await RetryOnBusy(async () =>
                await InsertAsync(entry)
            );
            total += count;
        }

        await transaction.CommitAsync();
        return total;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

// ✅ Multiple callers can bulk insert simultaneously
// RetryOnBusy pattern handles contention automatically
```

---

### Anti-Pattern: Synchronous Waits from UI Thread

```csharp
// ❌ NEVER DO THIS - Blocks UI thread, causes freezes
public override void Draw()
{
    if (ImGui.Button("Get Recipe"))
    {
        // ❌ .Wait() blocks the entire UI
        var recipe = _service.GetRecipeAsync(recipeId).Wait();

        ImGui.Text(recipe.Name);
    }
}

// ❌ WRONG - .Result blocks, can deadlock
var recipe = _service.GetRecipeAsync(recipeId).Result;

// ✅ CORRECT - Use fire-and-forget pattern (see Pattern 1)
```

---

## Complete Example: Thread-Safe Repository Access

```csharp
public class RecipeRepository : ICollectionRepository
{
    private readonly DatabaseContext _context;
    private readonly IPluginLog _log;
    private const int BusyTimeoutMs = 5000;

    public RecipeRepository(DatabaseContext context, IPluginLog log)
    {
        _context = context;
        _log = log;

        // Set busy timeout for all operations
        using var cmd = _context.Connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs}";
        cmd.ExecuteNonQuery();
    }

    // ✅ Retry pattern for busy database
    private async Task<T> RetryOnBusy<T>(Func<Task<T>> operation)
    {
        const int maxAttempts = 3;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            try
            {
                return await operation();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
                attempts++;
                if (attempts >= maxAttempts) throw;

                int delayMs = 100 * (int)Math.Pow(2, attempts - 1);
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    // ✅ Async bulk insert with transaction
    public async Task BulkInsertAsync(List<CraftingRecipe> recipes)
    {
        await RetryOnBusy(async () =>
        {
            using var transaction = _context.Connection.BeginTransaction();
            try
            {
                foreach (var recipe in recipes)
                {
                    using var cmd = _context.Connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO recipes
                        (recipe_id, crafting_class, is_master)
                        VALUES (@id, @class, @master)
                    ";
                    cmd.Parameters.AddWithValue("@id", recipe.RecipeId);
                    cmd.Parameters.AddWithValue("@class", recipe.CraftingClass);
                    cmd.Parameters.AddWithValue("@master", recipe.IsMasterRecipe);

                    // ✅ Async database operation
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return 0;  // Satisfy return type
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    // ✅ Read pattern with caching
    private readonly ConcurrentDictionary<uint, Recipe> _recipeCache = new();

    public async Task<Recipe?> GetRecipeAsync(uint recipeId)
    {
        // Try cache first (lock-free)
        if (_recipeCache.TryGetValue(recipeId, out var cached))
        {
            return cached;
        }

        return await RetryOnBusy(async () =>
        {
            using var cmd = _context.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM recipes WHERE recipe_id = @id";
            cmd.Parameters.AddWithValue("@id", recipeId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var recipe = new Recipe
                {
                    RecipeId = reader.GetUInt32(0),
                    CraftingClass = (CraftingClass)reader.GetInt32(1),
                    IsMasterRecipe = reader.GetBoolean(2)
                };

                // Cache the result (atomic)
                _recipeCache.TryAdd(recipeId, recipe);

                return recipe;
            }

            return null;
        });
    }
}

// ✅ Usage from UI
public class CollectionUI : Window
{
    private readonly RecipeRepository _repository;
    private bool _isLoading;
    private Recipe? _currentRecipe;

    private void OnRecipeSelected(uint recipeId)
    {
        if (_isLoading) return;
        _isLoading = true;

        // Fire background load
        _ = Task.Run(async () =>
        {
            try
            {
                // ✅ Blocking database access OK (off UI thread)
                _currentRecipe = await _repository.GetRecipeAsync(recipeId);
                _isLoading = false;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Load failed: {ex}");
                _isLoading = false;
            }
        });
    }

    public override void Draw()
    {
        if (_isLoading)
        {
            ImGui.Text("Loading...");
        }
        else if (_currentRecipe != null)
        {
            // ✅ Read field on UI thread
            ImGui.Text($"Recipe: {_currentRecipe.RecipeId}");
        }
    }
}
```

---

## Summary: Threading Checklist

- ✅ **Dalamud Threading:**
  - Fire database operations with `Task.Run()`, no waiting
  - Store results in plugin fields
  - Read fields in `Draw()` (next frame)
  - No `Dispatcher` needed

- ✅ **SQLite Concurrency:**
  - Set `PRAGMA busy_timeout = 5000`
  - Use `RetryOnBusy` pattern for `SQLITE_BUSY` errors
  - Single `SqliteConnection` per DatabaseContext
  - All operations async (`ExecuteNonQueryAsync`, `ExecuteReaderAsync`)

- ✅ **Cache Threading:**
  - Use `ConcurrentDictionary` for lock-free access
  - Atomicity via `AddOrUpdate`
  - Time-based expiry checked on access

- ✅ **Caller Best Practices:**
  - `Task.Run()` for background queries
  - Never `.Wait()` or `.Result` from UI thread
  - Store results in fields
  - Read fields in `Draw()`

---

**For questions or clarifications, refer to specific patterns in sections 1-4.**
