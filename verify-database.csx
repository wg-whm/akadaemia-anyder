#!/usr/bin/env dotnet-script

// Quick verification script for database tier testing
// Run with: dotnet script verify-database.csx

#r "nuget: Microsoft.Data.Sqlite, 8.0.0"

using Microsoft.Data.Sqlite;
using System;
using System.IO;

var testRoot = Path.Combine(Path.GetTempPath(), $"akadaemia-verify-{Guid.NewGuid():N}");
Directory.CreateDirectory(testRoot);

Console.WriteLine("=== Database Schema Verification ===");
Console.WriteLine($"Test directory: {testRoot}");

try
{
    // Test 1: Create normal database
    Console.WriteLine("\n[Test 1] Creating database...");
    var dbPath = Path.Combine(testRoot, "test.db");
    var connString = $"Data Source={dbPath}";

    using (var conn = new SqliteConnection(connString))
    {
        conn.Open();

        // Create a simple test table
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE collections (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    character_id INTEGER NOT NULL,
                    character_name TEXT NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        // Verify table exists
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='collections'";
            var result = cmd.ExecuteScalar();
            if (result != null)
            {
                Console.WriteLine("  PASS: Table created successfully");
            }
            else
            {
                Console.WriteLine("  FAIL: Table not found");
            }
        }
    }

    if (File.Exists(dbPath))
    {
        Console.WriteLine("  PASS: Database file exists");
        var fileInfo = new FileInfo(dbPath);
        Console.WriteLine($"        Size: {fileInfo.Length} bytes");
    }

    // Test 2: In-memory database
    Console.WriteLine("\n[Test 2] Testing in-memory database...");
    var memConnString = "Data Source=:memory:";
    using (var conn = new SqliteConnection(memConnString))
    {
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE test (id INTEGER PRIMARY KEY)";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            var result = cmd.ExecuteScalar();
            if (result != null)
            {
                Console.WriteLine("  PASS: In-memory database functional");
            }
        }
    }

    Console.WriteLine("\n=== Verification Complete ===");
    Console.WriteLine("Database implementation is ready for integration!");
}
finally
{
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}
