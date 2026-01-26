namespace AkadaemiaAnyder.Data
{
    /// <summary>
    /// Represents the operational tier of the database system.
    /// Used for diagnostics and health monitoring.
    /// </summary>
    public enum DatabaseTier
    {
        /// <summary>Normal file-based database operation</summary>
        Tier1,

        /// <summary>Recovered from corruption - database was reset</summary>
        Tier2,

        /// <summary>In-memory only - persistent storage unavailable</summary>
        Tier3,

        /// <summary>All initialization tiers failed - no database available</summary>
        Degraded
    }
}
