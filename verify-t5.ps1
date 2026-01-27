# T5 Verification Script
# Tests the memory safety framework

Write-Host "=== T5 Memory Safety Framework Verification ===" -ForegroundColor Cyan
Write-Host ""

# Check files exist
$files = @(
    "SamplePlugin/MemoryReaders/IMemoryReader.cs",
    "SamplePlugin/MemoryReaders/SafeMemoryReader.cs",
    "SamplePlugin/MemoryReaders/PointerValidator.cs",
    "SamplePlugin/MemoryReaders/MockMemoryReader.cs"
)

Write-Host "Checking files..." -ForegroundColor Yellow
$allFilesExist = $true
foreach ($file in $files) {
    $fullPath = "C:/Code/akadaemia-anyder/$file"
    if (Test-Path $fullPath) {
        Write-Host "  [OK] $file" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $file" -ForegroundColor Red
        $allFilesExist = $false
    }
}
Write-Host ""

# Verify build
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = & dotnet build C:/Code/akadaemia-anyder/SamplePlugin/SamplePlugin.csproj 2>&1
$buildSuccess = $LASTEXITCODE -eq 0

if ($buildSuccess) {
    Write-Host "  [OK] Build succeeded" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Build failed" -ForegroundColor Red
    Write-Host $buildResult
}
Write-Host ""

# Check key patterns in SafeMemoryReader
Write-Host "Verifying SafeMemoryReader implementation..." -ForegroundColor Yellow
$safeReaderContent = Get-Content "C:/Code/akadaemia-anyder/SamplePlugin/MemoryReaders/SafeMemoryReader.cs" -Raw

$patterns = @{
    "AccessViolationException catch" = "catch \(AccessViolationException"
    "NullReferenceException catch" = "catch \(NullReferenceException"
    "Returns default on error" = "return default"
    "Dependency injection" = "Action<string> _logError"
    "Wrapper pattern" = "IMemoryReader<T> _inner"
}

$allPatternsFound = $true
foreach ($pattern in $patterns.GetEnumerator()) {
    if ($safeReaderContent -match $pattern.Value) {
        Write-Host "  [OK] $($pattern.Key)" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $($pattern.Key)" -ForegroundColor Red
        $allPatternsFound = $false
    }
}
Write-Host ""

# Check MockMemoryReader
Write-Host "Verifying MockMemoryReader test harness..." -ForegroundColor Yellow
$mockReaderContent = Get-Content "C:/Code/akadaemia-anyder/SamplePlugin/MemoryReaders/MockMemoryReader.cs" -Raw

$mockPatterns = @{
    "FailureMode enum" = "enum FailureMode"
    "AccessViolation mode" = "AccessViolation"
    "NullReference mode" = "NullReference"
    "Throws exceptions" = "throw new AccessViolationException"
}

$allMockPatternsFound = $true
foreach ($pattern in $mockPatterns.GetEnumerator()) {
    if ($mockReaderContent -match $pattern.Value) {
        Write-Host "  [OK] $($pattern.Key)" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $($pattern.Key)" -ForegroundColor Red
        $allMockPatternsFound = $false
    }
}
Write-Host ""

# Check PointerValidator
Write-Host "Verifying PointerValidator..." -ForegroundColor Yellow
$validatorContent = Get-Content "C:/Code/akadaemia-anyder/SamplePlugin/MemoryReaders/PointerValidator.cs" -Raw

$validatorPatterns = @{
    "IsValidPointer method" = "IsValidPointer\(IntPtr"
    "ValidatePointerRange method" = "ValidatePointerRange\(IntPtr ptr, int size\)"
    "Null check" = "IntPtr.Zero"
}

$allValidatorPatternsFound = $true
foreach ($pattern in $validatorPatterns.GetEnumerator()) {
    if ($validatorContent -match $pattern.Value) {
        Write-Host "  [OK] $($pattern.Key)" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $($pattern.Key)" -ForegroundColor Red
        $allValidatorPatternsFound = $false
    }
}
Write-Host ""

# Summary
Write-Host "=== Verification Summary ===" -ForegroundColor Cyan
Write-Host ""

$results = @{
    "Files created" = $allFilesExist
    "Build success" = $buildSuccess
    "SafeMemoryReader patterns" = $allPatternsFound
    "MockMemoryReader test harness" = $allMockPatternsFound
    "PointerValidator implementation" = $allValidatorPatternsFound
}

$allPassed = $true
foreach ($result in $results.GetEnumerator()) {
    $status = if ($result.Value) { "PASS" } else { "FAIL" }
    $color = if ($result.Value) { "Green" } else { "Red" }
    Write-Host "  [$status] $($result.Key)" -ForegroundColor $color
    if (-not $result.Value) { $allPassed = $false }
}
Write-Host ""

if ($allPassed) {
    Write-Host "[T5 COMPLETE] All verification criteria met!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[T5 INCOMPLETE] Some criteria failed" -ForegroundColor Red
    exit 1
}
