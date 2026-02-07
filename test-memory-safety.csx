#!/usr/bin/env dotnet-script
#r "C:/Code/akadaemia-anyder/SamplePlugin/bin/Debug/net8.0-windows7.0/SamplePlugin.dll"

using System;
using SamplePlugin.MemoryReaders;

Console.WriteLine("=== SafeMemoryReader Test Suite ===\n");

// Test 1: AccessViolationException handling
Console.WriteLine("Test 1: AccessViolationException handling");
bool test1Passed = TestAccessViolationException();
Console.WriteLine($"Result: {(test1Passed ? "PASS" : "FAIL")}\n");

// Test 2: NullReferenceException handling
Console.WriteLine("Test 2: NullReferenceException handling");
bool test2Passed = TestNullReferenceException();
Console.WriteLine($"Result: {(test2Passed ? "PASS" : "FAIL")}\n");

// Test 3: PointerValidator bounds validation
Console.WriteLine("Test 3: PointerValidator bounds validation");
bool test3Passed = TestPointerValidation();
Console.WriteLine($"Result: {(test3Passed ? "PASS" : "FAIL")}\n");

// Test 4: MockMemoryReader test harness
Console.WriteLine("Test 4: MockMemoryReader test harness");
bool test4Passed = TestMockMemoryReader();
Console.WriteLine($"Result: {(test4Passed ? "PASS" : "FAIL")}\n");

// Summary
bool allPassed = test1Passed && test2Passed && test3Passed && test4Passed;
Console.WriteLine("=== Test Summary ===");
Console.WriteLine($"Overall: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")}");
Environment.Exit(allPassed ? 0 : 1);

bool TestAccessViolationException()
{
    bool errorLogged = false;
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
    bool passed = result == null && errorLogged;
    Console.WriteLine($"  ReadData returned null: {result == null}");
    Console.WriteLine($"  Error was logged: {errorLogged}");
    return passed;
}

bool TestNullReferenceException()
{
    bool errorLogged = false;
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
    bool passed = result == null && errorLogged;
    Console.WriteLine($"  ReadData returned null: {result == null}");
    Console.WriteLine($"  Error was logged: {errorLogged}");
    return passed;
}

bool TestPointerValidation()
{
    var nullPtr = IntPtr.Zero;
    var validPtr = new IntPtr(0x1000);

    bool test1 = !PointerValidator.IsValidPointer(nullPtr);
    Console.WriteLine($"  Null pointer rejected: {test1}");

    bool test2 = PointerValidator.IsValidPointer(validPtr);
    Console.WriteLine($"  Valid pointer accepted: {test2}");

    bool test3 = !PointerValidator.ValidatePointerRange(nullPtr, 100);
    Console.WriteLine($"  Null pointer range rejected: {test3}");

    bool test4 = PointerValidator.ValidatePointerRange(validPtr, 100);
    Console.WriteLine($"  Valid pointer range accepted: {test4}");

    bool test5 = !PointerValidator.ValidatePointerRange(validPtr, -1);
    Console.WriteLine($"  Negative size rejected: {test5}");

    return test1 && test2 && test3 && test4 && test5;
}

bool TestMockMemoryReader()
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
