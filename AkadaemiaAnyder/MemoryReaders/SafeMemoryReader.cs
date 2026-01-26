using System;

namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Wrapper that provides exception handling and graceful degradation for memory readers.
/// Catches AccessViolationException, NullReferenceException, and other failures.
/// </summary>
/// <typeparam name="T">The type of data being read</typeparam>
public class SafeMemoryReader<T> : IMemoryReader<T>
{
    private readonly IMemoryReader<T> _inner;
    private readonly Action<string> _logError;
    private readonly Action<string> _logWarning;

    /// <summary>
    /// Creates a new SafeMemoryReader wrapper.
    /// </summary>
    /// <param name="inner">The underlying memory reader to wrap</param>
    /// <param name="logError">Action to log error messages</param>
    /// <param name="logWarning">Action to log warning messages</param>
    public SafeMemoryReader(
        IMemoryReader<T> inner,
        Action<string> logError,
        Action<string> logWarning)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
    }

    /// <summary>
    /// Safely checks if memory is available, catching all exceptions.
    /// </summary>
    /// <returns>True if available, false on any error</returns>
    public bool IsAvailable()
    {
        try
        {
            return _inner.IsAvailable();
        }
        catch (AccessViolationException ex)
        {
            _logError($"Memory access violation in IsAvailable: {ex.Message}");
            return false;
        }
        catch (NullReferenceException ex)
        {
            _logError($"Null reference in IsAvailable: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logError($"Unexpected error in IsAvailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Safely reads data from memory, returning null/default on failure.
    /// </summary>
    /// <returns>Data if successful, null/default if failed</returns>
    public T? ReadData()
    {
        try
        {
            return _inner.ReadData();
        }
        catch (AccessViolationException ex)
        {
            _logError($"Memory access violation in ReadData: {ex.Message}");
            return default;
        }
        catch (NullReferenceException ex)
        {
            _logError($"Null reference in ReadData: {ex.Message}");
            return default;
        }
        catch (Exception ex)
        {
            _logError($"Unexpected read error: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Safely gets total count, returning 0 on failure.
    /// </summary>
    /// <returns>Count if successful, 0 if failed</returns>
    public int GetTotalCount()
    {
        try
        {
            return _inner.GetTotalCount();
        }
        catch (AccessViolationException ex)
        {
            _logError($"Memory access violation in GetTotalCount: {ex.Message}");
            return 0;
        }
        catch (NullReferenceException ex)
        {
            _logError($"Null reference in GetTotalCount: {ex.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            _logWarning($"Error getting total count: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Safely gets unlocked count, returning 0 on failure.
    /// </summary>
    /// <returns>Count if successful, 0 if failed</returns>
    public int GetUnlockedCount()
    {
        try
        {
            return _inner.GetUnlockedCount();
        }
        catch (AccessViolationException ex)
        {
            _logError($"Memory access violation in GetUnlockedCount: {ex.Message}");
            return 0;
        }
        catch (NullReferenceException ex)
        {
            _logError($"Null reference in GetUnlockedCount: {ex.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            _logWarning($"Error getting unlocked count: {ex.Message}");
            return 0;
        }
    }
}
