using System;
using System.Collections.Generic;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Models;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Services
{
    /// <summary>
    /// In-memory telemetry service for tracking plugin metrics.
    /// No external transmission - metrics stored locally only.
    /// </summary>
    public class TelemetryService
    {
        private readonly IPluginLog _log;

        // Scan tracking
        private int _recipeScanSuccessCount = 0;
        private int _recipeScanFailureCount = 0;
        private int _gatheringScanSuccessCount = 0;
        private int _gatheringScanFailureCount = 0;
        private int _fishingScanSuccessCount = 0;
        private int _fishingScanFailureCount = 0;

        // Database tier tracking
        private DatabaseTier _currentDatabaseTier = DatabaseTier.Degraded;
        private readonly Dictionary<DatabaseTier, int> _databaseTierChanges = new();

        // Memory read tracking
        private readonly Dictionary<string, int> _memoryReadFailures = new();

        // Session start time
        private readonly DateTime _sessionStartTime;

        public TelemetryService(IPluginLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _sessionStartTime = DateTime.UtcNow;

            // Initialize tier change tracking
            foreach (DatabaseTier tier in Enum.GetValues(typeof(DatabaseTier)))
            {
                _databaseTierChanges[tier] = 0;
            }

            _log.Debug("TelemetryService initialized");
        }

        /// <summary>
        /// Records the result of a collection scan.
        /// </summary>
        /// <param name="type">The collection type that was scanned</param>
        /// <param name="success">Whether the scan succeeded</param>
        public void RecordScan(CollectionType type, bool success)
        {
            switch (type)
            {
                case CollectionType.Recipe:
                    if (success)
                        _recipeScanSuccessCount++;
                    else
                        _recipeScanFailureCount++;
                    break;

                case CollectionType.GatheringNode:
                    if (success)
                        _gatheringScanSuccessCount++;
                    else
                        _gatheringScanFailureCount++;
                    break;

                case CollectionType.FishingHole:
                    if (success)
                        _fishingScanSuccessCount++;
                    else
                        _fishingScanFailureCount++;
                    break;
            }

            _log.Debug($"RecordScan: {type} - Success={success}");
        }

        /// <summary>
        /// Records a database tier change event.
        /// </summary>
        /// <param name="tier">The new database tier</param>
        public void RecordDatabaseTierChange(DatabaseTier tier)
        {
            _currentDatabaseTier = tier;
            _databaseTierChanges[tier]++;
            _log.Information($"RecordDatabaseTierChange: Tier changed to {tier}");
        }

        /// <summary>
        /// Records a memory read failure for a specific reader.
        /// </summary>
        /// <param name="readerName">Name of the reader that failed</param>
        public void RecordMemoryReadFailure(string readerName)
        {
            if (string.IsNullOrEmpty(readerName))
            {
                _log.Warning("RecordMemoryReadFailure called with null/empty readerName");
                return;
            }

            if (!_memoryReadFailures.ContainsKey(readerName))
            {
                _memoryReadFailures[readerName] = 0;
            }

            _memoryReadFailures[readerName]++;
            _log.Debug($"RecordMemoryReadFailure: {readerName} (total: {_memoryReadFailures[readerName]})");
        }

        /// <summary>
        /// Gets a snapshot of current telemetry metrics.
        /// </summary>
        /// <returns>Immutable snapshot of current metrics</returns>
        public TelemetrySnapshot GetMetrics()
        {
            return new TelemetrySnapshot
            {
                // Session info
                SessionStartTime = _sessionStartTime,
                SessionDuration = DateTime.UtcNow - _sessionStartTime,

                // Scan metrics
                RecipeScanSuccessCount = _recipeScanSuccessCount,
                RecipeScanFailureCount = _recipeScanFailureCount,
                GatheringScanSuccessCount = _gatheringScanSuccessCount,
                GatheringScanFailureCount = _gatheringScanFailureCount,
                FishingScanSuccessCount = _fishingScanSuccessCount,
                FishingScanFailureCount = _fishingScanFailureCount,

                // Database metrics
                CurrentDatabaseTier = _currentDatabaseTier,
                DatabaseTierChanges = new Dictionary<DatabaseTier, int>(_databaseTierChanges),

                // Memory read metrics
                MemoryReadFailures = new Dictionary<string, int>(_memoryReadFailures)
            };
        }

        /// <summary>
        /// Resets all telemetry counters.
        /// </summary>
        public void ResetMetrics()
        {
            _recipeScanSuccessCount = 0;
            _recipeScanFailureCount = 0;
            _gatheringScanSuccessCount = 0;
            _gatheringScanFailureCount = 0;
            _fishingScanSuccessCount = 0;
            _fishingScanFailureCount = 0;

            _databaseTierChanges.Clear();
            foreach (DatabaseTier tier in Enum.GetValues(typeof(DatabaseTier)))
            {
                _databaseTierChanges[tier] = 0;
            }

            _memoryReadFailures.Clear();

            _log.Information("TelemetryService metrics reset");
        }
    }

    /// <summary>
    /// Immutable snapshot of telemetry metrics at a point in time.
    /// </summary>
    public class TelemetrySnapshot
    {
        // Session info
        public DateTime SessionStartTime { get; init; }
        public TimeSpan SessionDuration { get; init; }

        // Scan metrics
        public int RecipeScanSuccessCount { get; init; }
        public int RecipeScanFailureCount { get; init; }
        public int GatheringScanSuccessCount { get; init; }
        public int GatheringScanFailureCount { get; init; }
        public int FishingScanSuccessCount { get; init; }
        public int FishingScanFailureCount { get; init; }

        // Computed scan metrics
        public int TotalScans => RecipeScanSuccessCount + RecipeScanFailureCount +
                                  GatheringScanSuccessCount + GatheringScanFailureCount +
                                  FishingScanSuccessCount + FishingScanFailureCount;

        public int TotalSuccessfulScans => RecipeScanSuccessCount + GatheringScanSuccessCount + FishingScanSuccessCount;
        public int TotalFailedScans => RecipeScanFailureCount + GatheringScanFailureCount + FishingScanFailureCount;

        public double OverallSuccessRate => TotalScans > 0 ? (TotalSuccessfulScans / (double)TotalScans) * 100.0 : 0.0;

        // Database metrics
        public DatabaseTier CurrentDatabaseTier { get; init; }
        public Dictionary<DatabaseTier, int> DatabaseTierChanges { get; init; } = new();

        // Memory read metrics
        public Dictionary<string, int> MemoryReadFailures { get; init; } = new();
        public int TotalMemoryReadFailures
        {
            get
            {
                int total = 0;
                foreach (var count in MemoryReadFailures.Values)
                {
                    total += count;
                }
                return total;
            }
        }
    }
}
