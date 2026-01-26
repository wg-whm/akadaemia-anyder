using System;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Simple console logger for standalone testing.
    /// </summary>
    public class ConsoleLogger : IPluginLog
    {
        public Serilog.ILogger Logger => throw new NotImplementedException();
        public Serilog.Events.LogEventLevel MinimumLogLevel { get; set; }

        public void Verbose(string messageTemplate, params object[] values) { }
        public void Verbose(Exception? exception, string messageTemplate, params object[] values) { }

        public void Debug(string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[DEBUG] {string.Format(messageTemplate, values)}");
        }

        public void Debug(Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[DEBUG] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }

        public void Information(string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[INFO] {string.Format(messageTemplate, values)}");
        }

        public void Information(Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[INFO] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }

        public void Info(string messageTemplate, params object[] values)
        {
            Information(messageTemplate, values);
        }

        public void Info(Exception? exception, string messageTemplate, params object[] values)
        {
            Information(exception, messageTemplate, values);
        }

        public void Warning(string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[WARN] {string.Format(messageTemplate, values)}");
        }

        public void Warning(Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[WARN] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }

        public void Error(string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[ERROR] {string.Format(messageTemplate, values)}");
        }

        public void Error(Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[ERROR] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }

        public void Fatal(string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[FATAL] {string.Format(messageTemplate, values)}");
        }

        public void Fatal(Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[FATAL] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }

        public void Write(Serilog.Events.LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
        {
            Console.WriteLine($"[{level}] {string.Format(messageTemplate, values)} | {exception?.Message}");
        }
    }

    /// <summary>
    /// Standalone test runner for repository integration tests.
    /// Can be executed from a console application or test harness.
    /// </summary>
    public class TestRunner
    {
        public static async Task<int> Main(string[] args)
        {
            var log = new ConsoleLogger();

            log.Information("=== Akadaemia Anyder Repository Test Suite ===");
            log.Information("");

            try
            {
                var testRunner = new RepositoryIntegrationTest(log);
                var result = await testRunner.RunAllTests();

                log.Information("");
                log.Information("===========================================");
                log.Information($"FINAL RESULT: {(result ? "SUCCESS" : "FAILURE")}");
                log.Information("===========================================");

                return result ? 0 : 1;
            }
            catch (Exception ex)
            {
                log.Error($"Test runner crashed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                return 2;
            }
        }
    }
}
