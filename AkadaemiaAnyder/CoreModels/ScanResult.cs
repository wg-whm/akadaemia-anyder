using System;

namespace AkadaemiaAnyder.Core.Models
{
    public class ScanResult
    {
        public bool Success { get; set; }
        public int ItemsScanned { get; set; }
        public int ItemsUpdated { get; set; }
        public int NewItems { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public ScanErrorType? ErrorType { get; set; }

        public static ScanResult FailureResult(ScanErrorType type, string message)
            => new() { Success = false, ErrorType = type, ErrorMessage = message };

        public static ScanResult SuccessResult(int scanned, int updated, int newItems, TimeSpan duration)
            => new() { Success = true, ItemsScanned = scanned, ItemsUpdated = updated, NewItems = newItems, Duration = duration };
    }

    public enum ScanErrorType
    {
        MemoryUnavailable,
        MemoryAccessViolation,
        DatabaseError,
        StructureNotFound,
        GameNotRunning
    }
}
