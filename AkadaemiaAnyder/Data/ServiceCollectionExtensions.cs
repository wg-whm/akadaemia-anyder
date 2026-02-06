using System;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data.Repositories;
using AkadaemiaAnyder.Data.Services;
using AkadaemiaAnyder.Modules.Core.Interfaces;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Dependency injection registration extensions for database-related services.
    ///
    /// Provides static factory methods to instantiate and configure all data access
    /// and persistence services with proper dependency resolution and initialization order.
    ///
    /// Note: Dalamud plugins typically don't use Microsoft.Extensions.DependencyInjection directly.
    /// This class serves as a static factory for service construction instead.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all database-related services with proper initialization order.
        ///
        /// Service instantiation order (dependency-aware):
        /// 1. DatabaseContext - Core SQLite connection and schema management
        /// 2. Repositories - Data access layer (depends on DatabaseContext)
        /// 3. Cache/Utility Services - Business logic services (depends on repositories)
        ///
        /// Example usage:
        /// <code>
        /// var configDir = pluginInterface.GetPluginConfigDirectory();
        /// var services = ServiceCollectionExtensions.AddAkadaemiaDatabase(
        ///     configDir,
        ///     pluginLog,
        ///     clientState
        /// );
        ///
        /// // Access individual services
        /// var repository = services.Repository;
        /// var cacheService = services.CacheService;
        /// </code>
        /// </summary>
        /// <param name="configDirectory">
        /// Plugin configuration directory path where akadaemia.db will be stored.
        /// Typically: %APPDATA%\XIVLauncher\pluginConfigs\AkadaemiaAnyder
        /// </param>
        /// <param name="log">Dalamud plugin logging service for diagnostic output</param>
        /// <param name="clientState">Dalamud client state service for game integration</param>
        /// <returns>
        /// AkadaemiaServices instance containing all configured services.
        /// Returns null if database initialization fails on all tiers (degraded mode).
        /// </returns>
        public static AkadaemiaServices? AddAkadaemiaDatabase(
            string configDirectory,
            IPluginLog log,
            IClientState clientState)
        {
            try
            {
                log.Information("Initializing Akadaemia database services...");

                // TIER 1: DatabaseContext initialization with 3-tier fallback
                var databaseContext = InitializeDatabaseContext(configDirectory, log);
                if (databaseContext == null)
                {
                    log.Fatal("All database initialization tiers failed");
                    return null;
                }

                // TIER 2: Repositories (depend on DatabaseContext)
                log.Debug("Initializing repository services...");
                var collectionRepository = new CollectionRepository(databaseContext, log);
                var materialAvailabilityRepository = new MaterialAvailabilityRepository(databaseContext, log);
                var craftingListRepository = new CraftingListRepository(databaseContext, log);

                // TIER 3: Cache and utility services (depend on repositories)
                log.Debug("Initializing cache and utility services...");
                var cacheService = new MaterialAvailabilityCacheService(
                    materialAvailabilityRepository,
                    log,
                    clientState);

                // TIER 4: Repository integration adapter (depends on all repositories + clientState)
                var repositoryIntegration = new RepositoryIntegrationAdapter(
                    materialAvailabilityRepository,
                    craftingListRepository,
                    collectionRepository,
                    clientState,
                    log);

                log.Information("All database services initialized successfully");

                return new AkadaemiaServices(
                    databaseContext,
                    collectionRepository,
                    materialAvailabilityRepository,
                    craftingListRepository,
                    cacheService,
                    repositoryIntegration,
                    log);
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected error during service initialization: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Initializes DatabaseContext with 3-tier fallback strategy:
        /// 1. Normal file-based SQLite database
        /// 2. Delete corrupted file and retry
        /// 3. In-memory SQLite fallback (session-only, no persistence)
        /// </summary>
        private static DatabaseContext? InitializeDatabaseContext(
            string configDirectory,
            IPluginLog log)
        {
            // Tier 1: Normal file-based initialization
            try
            {
                log.Debug("Attempting Tier 1: Normal file-based database initialization");
                var context = new DatabaseContext(log, configDirectory);
                log.Information("Database initialized successfully (Tier 1: file-based)");
                return context;
            }
            catch (Exception ex)
            {
                log.Warning($"Tier 1 initialization failed: {ex.Message}");
            }

            // Tier 2: Delete corrupted database and retry
            try
            {
                log.Debug("Attempting Tier 2: Recovery from corruption");
                var dbPath = System.IO.Path.Combine(configDirectory, "akadaemia.db");

                if (System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Delete(dbPath);
                    log.Information("Deleted corrupted database file");
                }

                var context = new DatabaseContext(log, configDirectory);
                log.Warning("Database recovered from corruption (Tier 2: reset file)");
                return context;
            }
            catch (Exception ex)
            {
                log.Error($"Tier 2 recovery failed: {ex.Message}");
            }

            // Tier 3: In-memory fallback (no persistence)
            try
            {
                log.Debug("Attempting Tier 3: In-memory database fallback");
                var context = new DatabaseContext(log, ":memory:");
                log.Error(
                    "Database fallback to in-memory mode (Tier 3: no persistence). " +
                    "Data will be lost when plugin unloads.");
                return context;
            }
            catch (Exception ex)
            {
                log.Error($"Tier 3 in-memory fallback failed: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Container for all registered database services.
    /// Provides organized access to all data layer components.
    /// </summary>
    public class AkadaemiaServices : IDisposable
    {
        /// <summary>
        /// Core SQLite database connection and schema manager.
        /// Handles 3-tier fallback initialization and lifecycle.
        /// </summary>
        public DatabaseContext DatabaseContext { get; }

        /// <summary>
        /// Generic repository for CRUD operations on all collection types.
        /// Handles RecipeEntry, GatheringNodeEntry, FishingHoleEntry generically.
        /// </summary>
        public ICollectionRepository CollectionRepository { get; }

        /// <summary>
        /// Specialized repository for material availability queries and tracking.
        /// Provides location-based inventory searches (inventory, saddlebag, retainers).
        /// </summary>
        public MaterialAvailabilityRepository MaterialAvailabilityRepository { get; }

        /// <summary>
        /// Repository for crafting list persistence and management.
        /// Handles list CRUD and historical tracking.
        /// </summary>
        public CraftingListRepository CraftingListRepository { get; }

        /// <summary>
        /// Cache service for material availability queries.
        /// Reduces database load by caching frequently-accessed data.
        /// </summary>
        public MaterialAvailabilityCacheService CacheService { get; }

        /// <summary>
        /// Unified repository integration interface.
        /// Adapts all specialized repositories to a common interface for business logic layers.
        /// </summary>
        public IRepositoryIntegration RepositoryIntegration { get; }

        /// <summary>
        /// Dalamud logging service (captured for reference).
        /// </summary>
        private readonly IPluginLog _log;

        public AkadaemiaServices(
            DatabaseContext databaseContext,
            ICollectionRepository collectionRepository,
            MaterialAvailabilityRepository materialAvailabilityRepository,
            CraftingListRepository craftingListRepository,
            MaterialAvailabilityCacheService cacheService,
            IRepositoryIntegration repositoryIntegration,
            IPluginLog log)
        {
            DatabaseContext = databaseContext;
            CollectionRepository = collectionRepository;
            MaterialAvailabilityRepository = materialAvailabilityRepository;
            CraftingListRepository = craftingListRepository;
            CacheService = cacheService;
            RepositoryIntegration = repositoryIntegration;
            _log = log;
        }

        /// <summary>
        /// Disposes all service resources in proper order (reverse of initialization).
        /// Should be called in plugin Dispose() method.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _log.Debug("Disposing database services...");

                // Dispose in reverse order of initialization
                // Note: CacheService doesn't implement IDisposable (in-memory cache only)
                DatabaseContext?.Dispose();

                _log.Information("Database services disposed");
            }
            catch (Exception ex)
            {
                _log.Error($"Error disposing services: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Cache statistics for monitoring and performance optimization.
    /// </summary>
    public class CacheStatistics
    {
        public int CachedItems { get; set; }
        public int TotalHits { get; set; }
        public int TotalMisses { get; set; }
        public double HitRate => TotalHits + TotalMisses > 0
            ? (double)TotalHits / (TotalHits + TotalMisses)
            : 0;
        public DateTime LastClearedAt { get; set; }
    }

    /// <summary>
    /// Historical snapshot of material availability.
    /// Used for tracking inventory changes over time.
    /// </summary>
    public class MaterialAvailabilitySnapshot
    {
        public uint ItemId { get; set; }
        public uint CharacterId { get; set; }
        public DateTime SnapshotTime { get; set; }
        public int InInventory { get; set; }
        public int InSaddlebag { get; set; }
        public int InRetainers { get; set; }
    }
}
