using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using AkadaemiaAnyder.Data.Migrations;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Manages SQLite database connection with 3-tier fallback strategy.
    /// Tier 1: Normal file-based database
    /// Tier 2: Delete corrupted database and retry
    /// Tier 3: In-memory database (:memory:)
    /// </summary>
    public sealed class DatabaseContext : IDisposable
    {
        private readonly IPluginLog log;
        private readonly string databasePath;
        private SqliteConnection? connection;
        private DatabaseTier currentTier;
        private bool disposed;

        public DatabaseContext(IPluginLog pluginLog, string pluginConfigDirectory)
        {
            log = pluginLog;
            databasePath = Path.Combine(pluginConfigDirectory, "akadaemia.db");
            currentTier = DatabaseTier.Degraded;

            Initialize();
        }

        /// <summary>
        /// Gets the current operational tier of the database.
        /// </summary>
        public DatabaseTier GetHealthStatus() => currentTier;

        /// <summary>
        /// Gets the active database connection.
        /// Returns null if all initialization tiers failed.
        /// </summary>
        public SqliteConnection? Connection => connection;

        private void Initialize()
        {
            // Try Tier 1: Normal file-based database
            if (TryInitializeTier1())
            {
                currentTier = DatabaseTier.Tier1;
                log.Information("Database initialized successfully (Tier 1: file-based)");
                return;
            }

            // Try Tier 2: Delete corrupted DB and retry
            if (TryInitializeTier2())
            {
                currentTier = DatabaseTier.Tier2;
                log.Warning("Database recovered from corruption (Tier 2: reset file)");
                return;
            }

            // Try Tier 3: In-memory fallback
            if (TryInitializeTier3())
            {
                currentTier = DatabaseTier.Tier3;
                log.Error("Database fallback to in-memory mode (Tier 3: no persistence)");
                return;
            }

            // All tiers failed
            currentTier = DatabaseTier.Degraded;
            log.Fatal("All database initialization tiers failed - plugin will operate in degraded mode");
        }

        private bool TryInitializeTier1()
        {
            try
            {
                log.Debug($"Tier 1: Attempting file-based database at {databasePath}");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create connection
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                connection = new SqliteConnection(connectionString);
                connection.Open();

                // Apply migrations
                ApplyMigrations(connection);

                // Verify database is functional
                if (!VerifyDatabaseIntegrity(connection))
                {
                    log.Warning("Tier 1: Database integrity check failed");
                    connection.Dispose();
                    connection = null;
                    return false;
                }

                log.Debug("Tier 1: Initialization successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Warning($"Tier 1 initialization failed: {ex.Message}");
                connection?.Dispose();
                connection = null;
                return false;
            }
        }

        private bool TryInitializeTier2()
        {
            try
            {
                log.Debug("Tier 2: Attempting database recovery by deletion");

                // Delete potentially corrupted database file
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                    log.Information($"Tier 2: Deleted corrupted database at {databasePath}");
                }

                // Retry Tier 1 logic
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                connection = new SqliteConnection(connectionString);
                connection.Open();

                // Apply migrations
                ApplyMigrations(connection);

                // Verify database is functional
                if (!VerifyDatabaseIntegrity(connection))
                {
                    log.Warning("Tier 2: Database integrity check failed after recovery");
                    connection.Dispose();
                    connection = null;
                    return false;
                }

                log.Debug("Tier 2: Recovery successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Tier 2 recovery failed: {ex.Message}");
                connection?.Dispose();
                connection = null;
                return false;
            }
        }

        private bool TryInitializeTier3()
        {
            try
            {
                log.Debug("Tier 3: Attempting in-memory database");

                // Create in-memory database
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = ":memory:",
                    Mode = SqliteOpenMode.Memory,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                connection = new SqliteConnection(connectionString);
                connection.Open();

                // Apply migrations
                ApplyMigrations(connection);

                log.Debug("Tier 3: In-memory initialization successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Tier 3 in-memory fallback failed: {ex.Message}");
                connection?.Dispose();
                connection = null;
                return false;
            }
        }

        private void ApplyMigrations(SqliteConnection conn)
        {
            var currentVersion = GetCurrentSchemaVersion(conn);
            log.Debug($"Current schema version: {currentVersion}");

            if (currentVersion < Migration_v1.Version)
            {
                log.Information("Applying migration v1...");
                Migration_v1.Apply(conn);
                log.Information("Migration v1 applied successfully");
            }

            // Future migrations would be checked here:
            // if (currentVersion < Migration_v2.Version) { Migration_v2.Apply(conn); }
        }

        private int GetCurrentSchemaVersion(SqliteConnection conn)
        {
            try
            {
                // Check if schema_version table exists
                var checkTableSql = @"
                    SELECT COUNT(*)
                    FROM sqlite_master
                    WHERE type='table' AND name='schema_version'";

                using var checkCommand = conn.CreateCommand();
                checkCommand.CommandText = checkTableSql;
                var tableExists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;

                if (!tableExists)
                {
                    return 0;
                }

                // Get latest version
                var getVersionSql = "SELECT MAX(version) FROM schema_version";
                using var versionCommand = conn.CreateCommand();
                versionCommand.CommandText = getVersionSql;
                var result = versionCommand.ExecuteScalar();

                return result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch
            {
                return 0;
            }
        }

        private bool VerifyDatabaseIntegrity(SqliteConnection conn)
        {
            try
            {
                // Run PRAGMA integrity_check
                using var command = conn.CreateCommand();
                command.CommandText = "PRAGMA integrity_check";
                var result = command.ExecuteScalar()?.ToString();

                if (result != "ok")
                {
                    log.Warning($"Database integrity check returned: {result}");
                    return false;
                }

                // Verify required tables exist
                var requiredTables = new[] { "collections", "recipes", "gathering_nodes", "fishing_holes", "schema_version" };
                foreach (var table in requiredTables)
                {
                    using var checkCommand = conn.CreateCommand();
                    checkCommand.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
                    checkCommand.Parameters.AddWithValue("@tableName", table);
                    var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;

                    if (!exists)
                    {
                        log.Warning($"Required table '{table}' not found");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Warning($"Database integrity verification failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try
            {
                connection?.Dispose();
                log.Debug("Database connection disposed");
            }
            catch (Exception ex)
            {
                log.Error($"Error disposing database connection: {ex.Message}");
            }
            finally
            {
                connection = null;
                disposed = true;
            }
        }
    }
}
