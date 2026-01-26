namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Generic interface for reading game memory data structures.
/// </summary>
/// <typeparam name="T">The type of data being read from memory</typeparam>
public interface IMemoryReader<T>
{
    /// <summary>
    /// Checks if the memory region is accessible and valid.
    /// </summary>
    /// <returns>True if memory can be safely read</returns>
    bool IsAvailable();

    /// <summary>
    /// Reads data from the memory region.
    /// </summary>
    /// <returns>The data read from memory, or null if unavailable</returns>
    T? ReadData();

    /// <summary>
    /// Gets the total count of items in the memory structure.
    /// </summary>
    /// <returns>Total item count, or 0 if unavailable</returns>
    int GetTotalCount();

    /// <summary>
    /// Gets the count of unlocked items in the memory structure.
    /// </summary>
    /// <returns>Unlocked item count, or 0 if unavailable</returns>
    int GetUnlockedCount();
}
