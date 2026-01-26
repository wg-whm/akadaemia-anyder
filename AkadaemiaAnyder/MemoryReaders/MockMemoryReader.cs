using System;

namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Test harness for verifying SafeMemoryReader exception handling.
/// Simulates various failure modes on demand.
/// </summary>
/// <typeparam name="T">The type of data being mocked</typeparam>
public class MockMemoryReader<T> : IMemoryReader<T>
{
    private readonly T? _mockData;
    private readonly int _totalCount;
    private readonly int _unlockedCount;

    /// <summary>
    /// Simulates various failure modes for testing.
    /// </summary>
    public enum FailureMode
    {
        None,
        AccessViolation,
        NullReference,
        Timeout,
        GenericException
    }

    public FailureMode CurrentFailureMode { get; set; } = FailureMode.None;

    /// <summary>
    /// Creates a mock memory reader with test data.
    /// </summary>
    /// <param name="mockData">The data to return when successful</param>
    /// <param name="totalCount">Total count to return</param>
    /// <param name="unlockedCount">Unlocked count to return</param>
    public MockMemoryReader(T? mockData = default, int totalCount = 0, int unlockedCount = 0)
    {
        _mockData = mockData;
        _totalCount = totalCount;
        _unlockedCount = unlockedCount;
    }

    /// <summary>
    /// Simulates checking memory availability, with optional failures.
    /// </summary>
    public bool IsAvailable()
    {
        ThrowIfFailureModeSet();
        return _mockData != null;
    }

    /// <summary>
    /// Simulates reading data, with optional failures.
    /// </summary>
    public T? ReadData()
    {
        ThrowIfFailureModeSet();
        return _mockData;
    }

    /// <summary>
    /// Simulates getting total count, with optional failures.
    /// </summary>
    public int GetTotalCount()
    {
        ThrowIfFailureModeSet();
        return _totalCount;
    }

    /// <summary>
    /// Simulates getting unlocked count, with optional failures.
    /// </summary>
    public int GetUnlockedCount()
    {
        ThrowIfFailureModeSet();
        return _unlockedCount;
    }

    /// <summary>
    /// Throws the appropriate exception based on current failure mode.
    /// </summary>
    private void ThrowIfFailureModeSet()
    {
        switch (CurrentFailureMode)
        {
            case FailureMode.AccessViolation:
                throw new AccessViolationException("Simulated memory access violation");
            case FailureMode.NullReference:
                throw new NullReferenceException("Simulated null reference");
            case FailureMode.Timeout:
                throw new TimeoutException("Simulated timeout");
            case FailureMode.GenericException:
                throw new InvalidOperationException("Simulated generic exception");
            case FailureMode.None:
            default:
                // No exception
                break;
        }
    }
}
