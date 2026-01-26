using System;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Manual test runner for repository integration tests.
    /// Call this from a command or window to execute tests.
    /// </summary>
    public static class RunRepositoryTests
    {
        public static async Task<bool> Execute(IPluginLog log)
        {
            try
            {
                log.Information("=== Repository Integration Test Suite ===");
                log.Information("Starting automated test execution...");
                log.Information("");

                var testRunner = new RepositoryIntegrationTest(log);
                var result = await testRunner.RunAllTests();

                log.Information("");
                log.Information($"Test suite completed: {(result ? "SUCCESS" : "FAILURE")}");

                return result;
            }
            catch (Exception ex)
            {
                log.Error($"Test runner failed with exception: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
