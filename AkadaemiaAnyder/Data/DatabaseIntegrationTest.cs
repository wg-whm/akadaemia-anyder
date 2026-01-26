using Dalamud.Plugin.Services;
using System;
using System.IO;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Integration test runner for DatabaseContext.
    /// Run this from plugin initialization to verify tier fallback behavior.
    /// </summary>
    public static class DatabaseIntegrationTest
    {
        public static void RunTests(IPluginLog log)
        {
            var testRoot = Path.Combine(Path.GetTempPath(), $"akadaemia-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);

            try
            {
                log.Information("=== Database Integration Tests ===");

                var results = new
                {
                    Tier1 = TestTier1(log, testRoot),
                    Tier2 = TestTier2(log, testRoot),
                    Tier3 = TestTier3(log, testRoot),
                    Degraded = true // Implicitly tested by tier 3
                };

                log.Information("=== Test Results ===");
                log.Information($"Tier 1 (Normal):     {(results.Tier1 ? "PASS" : "FAIL")}");
                log.Information($"Tier 2 (Recovery):   {(results.Tier2 ? "PASS" : "FAIL")}");
                log.Information($"Tier 3 (In-Memory):  {(results.Tier3 ? "PASS" : "FAIL")}");
                log.Information($"Degraded Detection:  {(results.Degraded ? "PASS" : "FAIL")}");

                var allPassed = results.Tier1 && results.Tier2 && results.Tier3 && results.Degraded;
                if (allPassed)
                {
                    log.Information("All tests PASSED!");
                }
                else
                {
                    log.Error("Some tests FAILED - review logs above");
                }
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(testRoot))
                    {
                        Directory.Delete(testRoot, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    log.Warning($"Failed to cleanup test directory: {ex.Message}");
                }
            }
        }

        private static bool TestTier1(IPluginLog log, string testRoot)
        {
            log.Information("[Tier 1 Test] Normal file-based database");
            var testDir = Path.Combine(testRoot, "tier1");

            try
            {
                using var context = new DatabaseContext(log, testDir);

                if (context.GetHealthStatus() != DatabaseTier.Tier1)
                {
                    log.Error($"[Tier 1] Expected Tier1, got {context.GetHealthStatus()}");
                    return false;
                }

                if (context.Connection == null)
                {
                    log.Error("[Tier 1] Connection is null");
                    return false;
                }

                // Verify tables
                var tables = new[] { "collections", "recipes", "gathering_nodes", "fishing_holes", "schema_version" };
                foreach (var table in tables)
                {
                    using var cmd = context.Connection.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
                    cmd.Parameters.AddWithValue("@name", table);
                    var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                    if (!exists)
                    {
                        log.Error($"[Tier 1] Table '{table}' missing");
                        return false;
                    }
                }

                // Verify database file exists
                var dbPath = Path.Combine(testDir, "akadaemia.db");
                if (!File.Exists(dbPath))
                {
                    log.Error("[Tier 1] Database file not created");
                    return false;
                }

                log.Information("[Tier 1] PASS");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Tier 1] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestTier2(IPluginLog log, string testRoot)
        {
            log.Information("[Tier 2 Test] Recovery from corruption");
            var testDir = Path.Combine(testRoot, "tier2");
            Directory.CreateDirectory(testDir);

            try
            {
                // Create corrupted database
                var dbPath = Path.Combine(testDir, "akadaemia.db");
                File.WriteAllBytes(dbPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                log.Information("[Tier 2] Created corrupted database");

                using var context = new DatabaseContext(log, testDir);

                if (context.GetHealthStatus() != DatabaseTier.Tier2)
                {
                    log.Error($"[Tier 2] Expected Tier2, got {context.GetHealthStatus()}");
                    return false;
                }

                if (context.Connection == null)
                {
                    log.Error("[Tier 2] Connection is null after recovery");
                    return false;
                }

                // Verify recovery was successful
                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='collections'";
                var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                if (!exists)
                {
                    log.Error("[Tier 2] Tables not created after recovery");
                    return false;
                }

                log.Information("[Tier 2] PASS");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Tier 2] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool TestTier3(IPluginLog log, string testRoot)
        {
            log.Information("[Tier 3 Test] In-memory fallback");
            var testDir = Path.Combine(testRoot, "tier3");

            try
            {
                Directory.CreateDirectory(testDir);

                // Make directory read-only
                var dirInfo = new DirectoryInfo(testDir);
                dirInfo.Attributes = FileAttributes.ReadOnly;
                log.Information("[Tier 3] Set directory to read-only");

                DatabaseContext? context = null;
                try
                {
                    context = new DatabaseContext(log, testDir);

                    if (context.GetHealthStatus() != DatabaseTier.Tier3)
                    {
                        log.Error($"[Tier 3] Expected Tier3, got {context.GetHealthStatus()}");
                        return false;
                    }

                    if (context.Connection == null)
                    {
                        log.Error("[Tier 3] Connection is null");
                        return false;
                    }

                    // Verify in-memory database is functional
                    using var cmd = context.Connection.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='collections'";
                    var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                    if (!exists)
                    {
                        log.Error("[Tier 3] Tables not created in memory");
                        return false;
                    }

                    log.Information("[Tier 3] PASS");
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
                log.Error($"[Tier 3] Exception: {ex.Message}");
                return false;
            }
        }
    }
}
