using System;
using System.Collections.Generic;

namespace SamplePlugin.MemoryReaders;

/// <summary>
/// Manual test verification for SafeMemoryReader exception handling.
/// Run these tests to verify the safety framework.
/// </summary>
public static class SafeMemoryReaderTests
{
    public static void RunAllTests()
    {
        var results = new List<(string TestName, bool Passed)>();

        results.Add(("AccessViolationException handling", TestAccessViolationException()));
        results.Add(("NullReferenceException handling", TestNullReferenceException()));
        results.Add(("PointerValidator bounds validation", TestPointerValidation()));
        results.Add(("MockMemoryReader test harness", TestMockMemoryReader()));

        Console.WriteLine("\n=== Test Results ===");
        foreach (var (testName, passed) in results)
        {
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")}: {testName}");
        }
    }

    private static bool TestAccessViolationException()
    {
        var errorLogged = false;
        var mockReader = new MockMemoryReader<string>("test data")
        {
            CurrentFailureMode = MockMemoryReader<string>.FailureMode.AccessViolation
        };

        var safeReader = new SafeMemoryReader<string>(
            mockReader,
            msg => errorLogged = true,
            msg => { }
        );

        var result = safeReader.ReadData();
        return result == null && errorLogged;
    }

    private static bool TestNullReferenceException()
    {
        var errorLogged = false;
        var mockReader = new MockMemoryReader<string>("test data")
        {
            CurrentFailureMode = MockMemoryReader<string>.FailureMode.NullReference
        };

        var safeReader = new SafeMemoryReader<string>(
            mockReader,
            msg => errorLogged = true,
            msg => { }
        );

        var result = safeReader.ReadData();
        return result == null && errorLogged;
    }

    private static bool TestPointerValidation()
    {
        var nullPtr = IntPtr.Zero;
        var validPtr = new IntPtr(0x1000);

        var test1 = !PointerValidator.IsValidPointer(nullPtr);
        var test2 = PointerValidator.IsValidPointer(validPtr);
        var test3 = !PointerValidator.ValidatePointerRange(nullPtr, 100);
        var test4 = PointerValidator.ValidatePointerRange(validPtr, 100);
        var test5 = !PointerValidator.ValidatePointerRange(validPtr, -1);

        return test1 && test2 && test3 && test4 && test5;
    }

    private static bool TestMockMemoryReader()
    {
        var mockReader = new MockMemoryReader<int>(42, 10, 5);

        // Test normal operation
        var data = mockReader.ReadData();
        var total = mockReader.GetTotalCount();
        var unlocked = mockReader.GetUnlockedCount();

        if (data != 42 || total != 10 || unlocked != 5)
            return false;

        // Test exception throwing
        mockReader.CurrentFailureMode = MockMemoryReader<int>.FailureMode.AccessViolation;
        try
        {
            mockReader.ReadData();
            return false; // Should have thrown
        }
        catch (AccessViolationException)
        {
            return true; // Expected
        }
    }
}
