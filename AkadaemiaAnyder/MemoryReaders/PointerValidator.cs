using System;

namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Provides validation utilities for unsafe pointer operations.
/// </summary>
public static class PointerValidator
{
    /// <summary>
    /// Validates that a pointer is not null.
    /// </summary>
    /// <param name="ptr">The pointer to validate</param>
    /// <returns>True if the pointer is non-null</returns>
    public static bool IsValidPointer(IntPtr ptr)
    {
        return ptr != IntPtr.Zero;
    }

    /// <summary>
    /// Validates that a pointer and size represent a valid memory range.
    /// </summary>
    /// <param name="ptr">The base pointer</param>
    /// <param name="size">The size of the memory region in bytes</param>
    /// <returns>True if the pointer is valid and size is positive</returns>
    public static bool ValidatePointerRange(IntPtr ptr, int size)
    {
        if (!IsValidPointer(ptr))
            return false;

        if (size <= 0)
            return false;

        // Check for potential overflow
        try
        {
            var endAddress = ptr.ToInt64() + size;
            return endAddress > ptr.ToInt64();
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that a pointer falls within expected bounds.
    /// </summary>
    /// <param name="ptr">The pointer to validate</param>
    /// <param name="minAddress">Minimum valid address</param>
    /// <param name="maxAddress">Maximum valid address</param>
    /// <returns>True if pointer is within bounds</returns>
    public static bool IsWithinBounds(IntPtr ptr, long minAddress, long maxAddress)
    {
        if (!IsValidPointer(ptr))
            return false;

        var address = ptr.ToInt64();
        return address >= minAddress && address <= maxAddress;
    }
}
