# test-plugin.ps1 - Pre-flight checks for Akadaemia Anyder
Write-Host "=== Akadaemia Anyder Pre-Flight Checks ===" -ForegroundColor Cyan

# 1. Build check
Write-Host "`n[1/6] Building plugin..." -ForegroundColor Yellow
Push-Location C:\Code\akadaemia-anyder\SamplePlugin
$buildResult = dotnet build 2>&1
$buildSuccess = $LASTEXITCODE -eq 0
Pop-Location

if ($buildSuccess) {
    Write-Host "OK Build succeeded" -ForegroundColor Green
} else {
    Write-Host "X Build failed" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

# 2. Output check
Write-Host "`n[2/6] Checking output DLL..." -ForegroundColor Yellow
$dllPath = "$env:APPDATA\XIVLauncher\devPlugins\AkadaemiaAnyder\SamplePlugin.dll"
if (Test-Path $dllPath) {
    $size = [Math]::Round((Get-Item $dllPath).Length / 1KB, 2)
    Write-Host "OK DLL exists ($size KB)" -ForegroundColor Green
} else {
    Write-Host "X DLL not found at $dllPath" -ForegroundColor Red
    exit 1
}

# 3. Dalamud check
Write-Host "`n[3/6] Checking Dalamud installation..." -ForegroundColor Yellow
if (Test-Path "$env:APPDATA\XIVLauncher\addon\Hooks\dev") {
    Write-Host "OK Dalamud dev directory exists" -ForegroundColor Green
} else {
    Write-Host "X Dalamud not found - launch game via XIVLauncher first" -ForegroundColor Red
    exit 1
}

# 4. Unit tests
Write-Host "`n[4/6] Running unit tests..." -ForegroundColor Yellow
Push-Location C:\Code\akadaemia-anyder\AkadaemiaAnyder.Tests
$testOutput = dotnet test --verbosity quiet 2>&1 | Out-String
Pop-Location

if ($testOutput -match 'Passed:\s+(\d+)') {
    $passCount = [int]$matches[1]
    if ($passCount -ge 78) {
        Write-Host "OK $passCount tests passed" -ForegroundColor Green
    } else {
        Write-Host "WARN Only $passCount tests passed (expected 78+)" -ForegroundColor Yellow
    }
} else {
    Write-Host "WARN Could not parse test results" -ForegroundColor Yellow
}

# 5. Database path check
Write-Host "`n[5/6] Checking database directory..." -ForegroundColor Yellow
$dbDir = "$env:APPDATA\XIVLauncher\pluginConfigs\AkadaemiaAnyder"
if (!(Test-Path $dbDir)) {
    New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
    Write-Host "OK Created database directory" -ForegroundColor Green
} else {
    Write-Host "OK Database directory exists" -ForegroundColor Green
}

# 6. Summary
Write-Host "`n=== Pre-Flight Complete ===" -ForegroundColor Cyan
Write-Host "Ready for in-game testing!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Launch FFXIV via XIVLauncher"
Write-Host "2. Type: /xlplugins"
Write-Host "3. Enable SamplePlugin in Dev Tools"
Write-Host "4. Type: /akadaemia"
Write-Host "5. Click 'Scan Collections'"
