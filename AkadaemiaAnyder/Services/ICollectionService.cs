using System.Threading.Tasks;
using AkadaemiaAnyder.Core.Models;
using AkadaemiaAnyder.Data.Models;

namespace SamplePlugin.Services
{
    /// <summary>
    /// Service interface for orchestrating collection scanning across all collection types.
    /// Provides high-level scanning operations with partial success handling.
    /// </summary>
    public interface ICollectionService
    {
        /// <summary>
        /// Scans all collection types (Recipes, Gathering, Fishing) and aggregates results.
        /// Implements partial success: if 1+ scanners succeed, Success=true with errors logged.
        /// </summary>
        /// <returns>Aggregated scan result with combined statistics</returns>
        Task<ScanResult> ScanAllCollectionsAsync();

        /// <summary>
        /// Scans recipe unlocks using direct memory reading.
        /// Uses RecipeReader wrapped in SafeMemoryReader for safety.
        /// </summary>
        /// <returns>Scan result for recipes</returns>
        Task<ScanResult> ScanRecipesAsync();

        /// <summary>
        /// Scans gathering node unlocks using event-based listener.
        /// Collects nodes discovered via game events since listener was started.
        /// </summary>
        /// <returns>Scan result for gathering nodes</returns>
        Task<ScanResult> ScanGatheringAsync();

        /// <summary>
        /// Scans fishing catches using event-based listener.
        /// Collects fish discovered via game events since listener was started.
        /// </summary>
        /// <returns>Scan result for fishing</returns>
        Task<ScanResult> ScanFishingAsync();

        /// <summary>
        /// Gets collection statistics for a specific type.
        /// </summary>
        /// <param name="type">The collection type to query</param>
        /// <returns>Tuple of (total items, unlocked items, completion percentage)</returns>
        Task<(int total, int unlocked, double percentage)> GetStatsAsync(AkadaemiaAnyder.Data.Models.CollectionType type);
    }
}
