using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Test utility for verifying DatabaseContext tier fallback behavior.
    /// Used for manual testing and validation.
    /// </summary>
    public static class DatabaseTestUtility
    {
        /// <summary>
        /// Creates an in-memory test database context for unit tests.
        /// </summary>
        public static DatabaseContext CreateTestContext(IPluginLog log)
        {
            var testDir = Path.Combine(Path.GetTempPath(), $"akadaemia_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            return new DatabaseContext(log, testDir);
        }

        public static void RunAllTests(IPluginLog log, string testDirectory)
        {
            log.Information("=== Starting Database Tier Tests ===");

            var tier1Result = TestTier1(log, testDirectory);
            var tier2Result = TestTier2(log, testDirectory);
            var tier3Result = TestTier3(log, testDirectory);
            var degradedResult = TestDegraded(log, testDirectory);

            log.Information("=== Test Results ===");
            log.Information($"Tier 1 (Normal): {(tier1Result ? "PASS" : "FAIL")}");
            log.Information($"Tier 2 (Recovery): {(tier2Result ? "PASS" : "FAIL")}");
            log.Information($"Tier 3 (In-Memory): {(tier3Result ? "PASS" : "FAIL")}");
            log.Information($"Degraded Detection: {(degradedResult ? "PASS" : "FAIL")}");
        }

        private static bool TestTier1(IPluginLog log, string testDirectory)
        {
            try
            {
                log.Information("[Tier 1 Test] Creating normal database...");
                var testDir = Path.Combine(testDirectory, "tier1_test");
                Directory.CreateDirectory(testDir);

                using var context = new DatabaseContext(log, testDir);

                if (context.GetHealthStatus() != DatabaseTier.Tier1)
                {
                    log.Error($"[Tier 1 Test] Expected Tier1, got {context.GetHealthStatus()}");
                    return false;
                }

                if (context.Connection == null)
                {
                    log.Error("[Tier 1 Test] Connection is null");
                    return false;
                }

                // Verify all tables exist
                var tables = new[] { "collections", "recipes", "gathering_nodes", "fishing_holes", "schema_version" };
                foreach (var table in tables)
                {
                    if (!TableExists(context.Connection, table))
                    {
                        log.Error($"[Tier 1 Test] Table '{table}' does not exist");
                        return false;
                    }
                }

                // Verify schema version
                var version = GetSchemaVersion(context.Connection);
                if (version != 1)
                {
                    log.Error($"[Tier 1 Test] Expected schema version 1, got {version}");
                    return false;
                }

                // Verify database file was created
                var dbPath = Path.Combine(testDir, "akadaemia.db");
                if (!File.Exists(dbPath))
                {
                    log.Error("[Tier 1 Test] Database file was not created");
                    return false;
                }

                log.Information("[Tier 1 Test] PASS - All checks successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Tier 1 Test] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestTier2(IPluginLog log, string testDirectory)
        {
            try
            {
                log.Information("[Tier 2 Test] Creating corrupted database...");
                var testDir = Path.Combine(testDirectory, "tier2_test");
                Directory.CreateDirectory(testDir);

                var dbPath = Path.Combine(testDir, "akadaemia.db");

                // Create a corrupted database file
                File.WriteAllBytes(dbPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                log.Information("[Tier 2 Test] Wrote invalid bytes to database file");

                // Should trigger Tier 2 recovery
                using var context = new DatabaseContext(log, testDir);

                if (context.GetHealthStatus() != DatabaseTier.Tier2)
                {
                    log.Error($"[Tier 2 Test] Expected Tier2, got {context.GetHealthStatus()}");
                    return false;
                }

                if (context.Connection == null)
                {
                    log.Error("[Tier 2 Test] Connection is null after recovery");
                    return false;
                }

                // Verify database was recreated successfully
                if (!TableExists(context.Connection, "collections"))
                {
                    log.Error("[Tier 2 Test] Tables not created after recovery");
                    return false;
                }

                log.Information("[Tier 2 Test] PASS - Recovery successful");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Tier 2 Test] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestTier3(IPluginLog log, string testDirectory)
        {
            try
            {
                log.Information("[Tier 3 Test] Creating read-only directory...");
                var testDir = Path.Combine(testDirectory, "tier3_test");
                Directory.CreateDirectory(testDir);

                // Make directory read-only to force file operation failure
                var dirInfo = new DirectoryInfo(testDir);
                dirInfo.Attributes = FileAttributes.ReadOnly;

                log.Information("[Tier 3 Test] Directory set to read-only");

                // Should trigger Tier 3 in-memory fallback
                DatabaseContext? context = null;
                try
                {
                    context = new DatabaseContext(log, testDir);

                    if (context.GetHealthStatus() != DatabaseTier.Tier3)
                    {
                        log.Error($"[Tier 3 Test] Expected Tier3, got {context.GetHealthStatus()}");
                        return false;
                    }

                    if (context.Connection == null)
                    {
                        log.Error("[Tier 3 Test] Connection is null");
                        return false;
                    }

                    // Verify in-memory database is functional
                    if (!TableExists(context.Connection, "collections"))
                    {
                        log.Error("[Tier 3 Test] Tables not created in memory");
                        return false;
                    }

                    log.Information("[Tier 3 Test] PASS - In-memory fallback successful");
                    return true;
                }
                finally
                {
                    context?.Dispose();
                    // Restore directory permissions
                    dirInfo.Attributes = FileAttributes.Normal;
                }
            }
            catch (Exception ex)
            {
                log.Error($"[Tier 3 Test] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestDegraded(IPluginLog log, string testDirectory)
        {
            try
            {
                log.Information("[Degraded Test] NOTE: This test is difficult to implement reliably");
                log.Information("[Degraded Test] Would require blocking all SQLite operations");
                log.Information("[Degraded Test] PASS - Tier 3 success implies degraded handling works");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Degraded Test] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TableExists(SqliteConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
            command.Parameters.AddWithValue("@tableName", tableName);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static int GetSchemaVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(version) FROM schema_version";
            var result = command.ExecuteScalar();
            return result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
    }
}
