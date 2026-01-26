using System;
using SamplePlugin.MemoryReaders;

namespace SamplePlugin;

/// <summary>
/// Simple test runner to verify the memory reader safety framework.
/// </summary>
public static class TestRunner
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== SafeMemoryReader Test Suite ===\n");

        // Test 1: AccessViolationException handling
        Console.WriteLine("Test 1: AccessViolationException handling");
        var test1Passed = TestAccessViolationException();
        Console.WriteLine($"Result: {(test1Passed ? "PASS" : "FAIL")}\n");

        // Test 2: NullReferenceException handling
        Console.WriteLine("Test 2: NullReferenceException handling");
        var test2Passed = TestNullReferenceException();
        Console.WriteLine($"Result: {(test2Passed ? "PASS" : "FAIL")}\n");

        // Test 3: PointerValidator bounds validation
        Console.WriteLine("Test 3: PointerValidator bounds validation");
        var test3Passed = TestPointerValidation();
        Console.WriteLine($"Result: {(test3Passed ? "PASS" : "FAIL")}\n");

        // Test 4: MockMemoryReader test harness
        Console.WriteLine("Test 4: MockMemoryReader test harness");
        var test4Passed = TestMockMemoryReader();
        Console.WriteLine($"Result: {(test4Passed ? "PASS" : "FAIL")}\n");

        // Summary
        var allPassed = test1Passed && test2Passed && test3Passed && test4Passed;
        Console.WriteLine("=== Test Summary ===");
        Console.WriteLine($"Overall: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")}");
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
            msg => { errorLogged = true; Console.WriteLine($"  Error logged: {msg}"); },
            msg => { Console.WriteLine($"  Warning logged: {msg}"); }
        );

        var result = safeReader.ReadData();
        var passed = result == null && errorLogged;
        Console.WriteLine($"  ReadData returned null: {result == null}");
        Console.WriteLine($"  Error was logged: {errorLogged}");
        return passed;
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
            msg => { errorLogged = true; Console.WriteLine($"  Error logged: {msg}"); },
            msg => { Console.WriteLine($"  Warning logged: {msg}"); }
        );

        var result = safeReader.ReadData();
        var passed = result == null && errorLogged;
        Console.WriteLine($"  ReadData returned null: {result == null}");
        Console.WriteLine($"  Error was logged: {errorLogged}");
        return passed;
    }

    private static bool TestPointerValidation()
    {
        var nullPtr = IntPtr.Zero;
        var validPtr = new IntPtr(0x1000);

        var test1 = !PointerValidator.IsValidPointer(nullPtr);
        Console.WriteLine($"  Null pointer rejected: {test1}");

        var test2 = PointerValidator.IsValidPointer(validPtr);
        Console.WriteLine($"  Valid pointer accepted: {test2}");

        var test3 = !PointerValidator.ValidatePointerRange(nullPtr, 100);
        Console.WriteLine($"  Null pointer range rejected: {test3}");

        var test4 = PointerValidator.ValidatePointerRange(validPtr, 100);
        Console.WriteLine($"  Valid pointer range accepted: {test4}");

        var test5 = !PointerValidator.ValidatePointerRange(validPtr, -1);
        Console.WriteLine($"  Negative size rejected: {test5}");

        return test1 && test2 && test3 && test4 && test5;
    }

    private static bool TestMockMemoryReader()
    {
        var mockReader = new MockMemoryReader<int>(42, 10, 5);

        // Test normal operation
        var data = mockReader.ReadData();
        var total = mockReader.GetTotalCount();
        var unlocked = mockReader.GetUnlockedCount();

        Console.WriteLine($"  Normal operation - Data: {data}, Total: {total}, Unlocked: {unlocked}");

        if (data != 42 || total != 10 || unlocked != 5)
        {
            Console.WriteLine($"  ERROR: Expected data=42, total=10, unlocked=5");
            return false;
        }

        // Test exception throwing
        mockReader.CurrentFailureMode = MockMemoryReader<int>.FailureMode.AccessViolation;
        try
        {
            mockReader.ReadData();
            Console.WriteLine($"  ERROR: Should have thrown AccessViolationException");
            return false;
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"  Correctly threw AccessViolationException: {ex.Message}");
            return true;
        }
    }
}
