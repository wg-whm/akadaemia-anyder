using System;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Centralized logging wrapper around Dalamud's IPluginLog.
    /// Provides structured logging with timestamps and consistent formatting.
    /// </summary>
    public class LoggingService
    {
        private readonly IPluginLog _log;
        private const string LogPrefix = "[AkadaemiaAnyder]";

        public LoggingService(IPluginLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogInfo(string message)
        {
            var formattedMessage = FormatMessage("INFO", message);
            _log.Information(formattedMessage);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogWarning(string message)
        {
            var formattedMessage = FormatMessage("WARNING", message);
            _log.Warning(formattedMessage);
        }

        /// <summary>
        /// Logs an error message with optional exception details.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="ex">Optional exception to include in the log</param>
        public void LogError(string message, Exception? ex = null)
        {
            var formattedMessage = FormatMessage("ERROR", message);

            if (ex != null)
            {
                formattedMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\nStack Trace: {ex.StackTrace}";
            }

            _log.Error(formattedMessage);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogDebug(string message)
        {
            var formattedMessage = FormatMessage("DEBUG", message);
            _log.Debug(formattedMessage);
        }

        /// <summary>
        /// Formats a log message with timestamp and level.
        /// Example: [AkadaemiaAnyder] [INFO] [2026-01-25 15:30:00] Scan completed: 256/512 recipes
        /// </summary>
        private string FormatMessage(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return $"{LogPrefix} [{level}] [{timestamp}] {message}";
        }
    }
}
