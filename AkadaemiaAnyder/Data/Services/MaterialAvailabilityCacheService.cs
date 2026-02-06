using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data.Models;
using AkadaemiaAnyder.Data.Repositories;

namespace AkadaemiaAnyder.Data.Services
{
    /// <summary>
    /// Caching service for material availability queries with time-based expiry and thread-safe operations.
    /// Wraps MaterialAvailabilityRepository with ConcurrentDictionary-based in-memory cache.
    ///
    /// Cache Key: "{itemId}:{characterId}" (e.g., "5333:123456789")
    /// Cache TTL: 5 minutes per entry
    /// </summary>
    public class MaterialAvailabilityCacheService
    {
        private readonly MaterialAvailabilityRepository _repository;
        private readonly IPluginLog _log;
        private readonly IClientState _clientState;

        /// <summary>
        /// Concurrent dictionary storing cached MaterialAvailability results.
        /// Key format: "{itemId}:{characterId}"
        /// Value: Tuple of (MaterialAvailability, CacheTimestamp)
        /// </summary>
        private readonly ConcurrentDictionary<string, (MaterialAvailability data, DateTime timestamp)> _cache;

        /// <summary>
        /// Cache time-to-live in seconds (5 minutes = 300 seconds).
        /// </summary>
        private const int CacheTTLSeconds = 300;

        /// <summary>
        /// Lock for invalidation operations to prevent race conditions.
        /// </summary>
        private readonly object _invalidationLock = new object();

        /// <summary>
        /// Initializes a new instance of the MaterialAvailabilityCacheService.
        /// </summary>
        /// <param name="repository">The underlying MaterialAvailabilityRepository for database access.</param>
        /// <param name="log">Logger for diagnostic and error messages.</param>
        /// <param name="clientState">Dalamud client state for accessing current character ID.</param>
        public MaterialAvailabilityCacheService(
            MaterialAvailabilityRepository repository,
            IPluginLog log,
            IClientState clientState)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));

            _cache = new ConcurrentDictionary<string, (MaterialAvailability, DateTime)>();

            _log.Information("MaterialAvailabilityCacheService initialized with 5-minute TTL");
        }

        /// <summary>
        /// Gets material availability for a single item with cache-first lookup strategy.
        /// Returns cached result if available and within TTL, otherwise queries database and caches result.
        /// </summary>
        /// <param name="itemId">The item ID to look up.</param>
        /// <returns>MaterialAvailability for the item, or NotFound if item not in inventory.</returns>
        /// <exception cref="ArgumentException">Thrown if itemId is 0.</exception>
        public async Task<MaterialAvailability> GetMaterialAvailabilityAsync(uint itemId)
        {
            if (itemId == 0)
            {
                throw new ArgumentException("Item ID cannot be 0", nameof(itemId));
            }

            var characterId = _clientState.LocalContentId;
            var cacheKey = BuildCacheKey(itemId, characterId);

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out var cachedEntry))
            {
                if (IsValidCacheEntry(cachedEntry.timestamp))
                {
                    _log.Debug($"Cache hit: itemId={itemId}, characterId={characterId}");
                    return cachedEntry.data;
                }
                else
                {
                    // Cache expired, remove it
                    _cache.TryRemove(cacheKey, out _);
                    _log.Debug($"Cache expired for itemId={itemId}, characterId={characterId}");
                }
            }

            // Cache miss or expired - query database
            _log.Debug($"Cache miss: itemId={itemId}, characterId={characterId}");
            var result = await _repository.GetMaterialAvailabilityAsync(itemId, characterId);

            // Cache the result
            _cache.AddOrUpdate(cacheKey, (result, DateTime.UtcNow), (key, old) => (result, DateTime.UtcNow));

            return result;
        }

        /// <summary>
        /// Gets material availability for multiple items with batch caching support.
        /// Returns cached results for available items, batches uncached items into single database query.
        /// Significantly more efficient than calling GetMaterialAvailabilityAsync multiple times.
        /// </summary>
        /// <param name="itemIds">Enumerable of item IDs to look up.</param>
        /// <returns>Dictionary mapping itemId to MaterialAvailability. Includes NotFound entries for items not in inventory.</returns>
        public async Task<Dictionary<uint, MaterialAvailability>> GetBulkMaterialAvailabilityAsync(IEnumerable<uint> itemIds)
        {
            if (itemIds == null)
            {
                throw new ArgumentNullException(nameof(itemIds));
            }

            var itemIdList = itemIds.ToList();
            if (itemIdList.Count == 0)
            {
                return new Dictionary<uint, MaterialAvailability>();
            }

            var characterId = _clientState.LocalContentId;
            var results = new Dictionary<uint, MaterialAvailability>(capacity: itemIdList.Count);
            var uncachedItems = new List<uint>(capacity: itemIdList.Count);

            // Check cache for each item
            foreach (var itemId in itemIdList)
            {
                var cacheKey = BuildCacheKey(itemId, characterId);

                if (_cache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    if (IsValidCacheEntry(cachedEntry.timestamp))
                    {
                        // Cache hit
                        results[itemId] = cachedEntry.data;
                        _log.Debug($"Bulk cache hit: itemId={itemId}");
                    }
                    else
                    {
                        // Cache expired, add to uncached list
                        _cache.TryRemove(cacheKey, out _);
                        uncachedItems.Add(itemId);
                    }
                }
                else
                {
                    // Cache miss, add to uncached list
                    uncachedItems.Add(itemId);
                }
            }

            // Query database for uncached items (batch query)
            if (uncachedItems.Count > 0)
            {
                _log.Debug($"Bulk cache miss: {uncachedItems.Count}/{itemIdList.Count} items not cached");

                var bulkResults = await _repository.GetBulkMaterialAvailabilityAsync(uncachedItems, characterId);

                // Add to cache and results
                foreach (var (itemId, availability) in bulkResults)
                {
                    var cacheKey = BuildCacheKey(itemId, characterId);
                    _cache.AddOrUpdate(cacheKey, (availability, DateTime.UtcNow), (key, old) => (availability, DateTime.UtcNow));
                    results[itemId] = availability;
                }
            }

            _log.Debug($"Bulk query complete: {results.Count} items returned, {results.Count(x => x.Value.Total > 0)} have inventory");
            return results;
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// Thread-safe operation using lock to prevent concurrent modification issues.
        /// Useful when inventory is completely refreshed or character changes.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_invalidationLock)
            {
                var count = _cache.Count;
                _cache.Clear();
                _log.Information($"Cache invalidated: cleared {count} entries");
            }
        }

        /// <summary>
        /// Invalidates cache for a single item (all characters).
        /// Thread-safe operation using lock.
        /// Useful when specific items are updated (crafted, moved, etc).
        /// </summary>
        /// <param name="itemId">The item ID to invalidate from cache.</param>
        public void InvalidateItem(uint itemId)
        {
            if (itemId == 0)
            {
                throw new ArgumentException("Item ID cannot be 0", nameof(itemId));
            }

            lock (_invalidationLock)
            {
                // Remove all cache entries for this item (all characters)
                var keysToRemove = _cache.Keys
                    .Where(key => key.StartsWith($"{itemId}:"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _log.Information($"Cache item invalidated: itemId={itemId}, cleared {keysToRemove.Count} character entries");
                }
            }
        }

        /// <summary>
        /// Invalidates cache for a specific item and character.
        /// Thread-safe operation using lock.
        /// Most granular invalidation option for minimal cache clearing.
        /// </summary>
        /// <param name="itemId">The item ID to invalidate.</param>
        /// <param name="characterId">The character content ID to invalidate.</param>
        public void InvalidateItemForCharacter(uint itemId, ulong characterId)
        {
            if (itemId == 0)
            {
                throw new ArgumentException("Item ID cannot be 0", nameof(itemId));
            }

            if (characterId == 0)
            {
                throw new ArgumentException("Character ID cannot be 0", nameof(characterId));
            }

            lock (_invalidationLock)
            {
                var cacheKey = BuildCacheKey(itemId, characterId);
                if (_cache.TryRemove(cacheKey, out _))
                {
                    _log.Debug($"Cache entry invalidated: itemId={itemId}, characterId={characterId}");
                }
            }
        }

        /// <summary>
        /// Gets cache statistics for diagnostics and monitoring.
        /// </summary>
        /// <returns>Tuple containing (TotalEntries, ValidEntries, ExpiredEntries)</returns>
        public (int total, int valid, int expired) GetCacheStats()
        {
            var total = _cache.Count;
            var now = DateTime.UtcNow;
            var valid = _cache.Count(x => IsValidCacheEntry(x.Value.timestamp));
            var expired = total - valid;

            _log.Debug($"Cache stats: {valid} valid, {expired} expired of {total} total");
            return (total, valid, expired);
        }

        /// <summary>
        /// Cleans up expired cache entries.
        /// Can be called periodically (e.g., every 60 seconds) to prevent unbounded memory growth.
        /// </summary>
        /// <returns>Number of entries cleaned up.</returns>
        public int CleanupExpiredEntries()
        {
            lock (_invalidationLock)
            {
                var keysToRemove = _cache
                    .Where(x => !IsValidCacheEntry(x.Value.timestamp))
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _log.Information($"Cleanup completed: removed {keysToRemove.Count} expired entries");
                }

                return keysToRemove.Count;
            }
        }

        /// <summary>
        /// Builds a cache key from item ID and character ID.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="characterId">The character content ID.</param>
        /// <returns>Cache key string in format "itemId:characterId".</returns>
        private static string BuildCacheKey(uint itemId, ulong characterId)
        {
            return $"{itemId}:{characterId}";
        }

        /// <summary>
        /// Determines if a cache entry is still valid based on TTL.
        /// </summary>
        /// <param name="timestamp">The timestamp when the entry was cached.</param>
        /// <returns>True if entry is within TTL window, false if expired.</returns>
        private static bool IsValidCacheEntry(DateTime timestamp)
        {
            var age = (DateTime.UtcNow - timestamp).TotalSeconds;
            return age < CacheTTLSeconds;
        }
    }
}
