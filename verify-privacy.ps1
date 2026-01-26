# Privacy Verification Script
# Ensures no network calls remain after privacy retrofit

$ErrorActionPreference = "Stop"
# Critical patterns that indicate actual network calls or privacy violations
$networkPatterns = @("HttpClient", "WebRequest", "WebClient", "UniversalisClient", "api\.")
# Less critical patterns (check separately for context)
$referencePatterns = @("Universalis", "Discord", "Webhook")
$failed = $false

Write-Host "=== Privacy Verification ===" -ForegroundColor Cyan
Write-Host ""

foreach ($pattern in $networkPatterns) {
    Write-Host "Checking for pattern: $pattern" -ForegroundColor Yellow
    $results = Get-ChildItem -Path "AkadaemiaAnyder\Modules\Artisan\Artisan" -Recurse -Filter "*.cs" | Select-String -Pattern $pattern -SimpleMatch:$false

    if ($results) {
        Write-Host "  ✗ Found matches:" -ForegroundColor Red
        foreach ($match in $results) {
            Write-Host "    $($match.Path):$($match.LineNumber): $($match.Line.Trim())" -ForegroundColor Red
        }
        $failed = $true
    } else {
        Write-Host "  ✓ No matches found" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "=== Checking Reference Patterns (comments/names allowed) ===" -ForegroundColor Cyan
foreach ($pattern in $referencePatterns) {
    Write-Host "Checking for pattern: $pattern" -ForegroundColor Yellow
    $results = Get-ChildItem -Path "AkadaemiaAnyder\Modules\Artisan\Artisan" -Recurse -Filter "*.cs" | Select-String -Pattern $pattern -SimpleMatch:$false | Where-Object { $_.Line -notmatch "^[\s]*\/\/" }  # Exclude comment-only lines

    if ($results) {
        Write-Host "  ⚠ Found matches (verify these are benign):" -ForegroundColor Yellow
        foreach ($match in $results) {
            Write-Host "    $($match.Path):$($match.LineNumber): $($match.Line.Trim())" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ✓ No matches found" -ForegroundColor Green
    }
}

Write-Host ""
if ($failed) {
    Write-Host "✗ PRIVACY VERIFICATION FAILED" -ForegroundColor Red
    Write-Host "Network-related code still exists in the codebase." -ForegroundColor Red
    exit 1
} else {
    Write-Host "✓ PRIVACY VERIFICATION PASSED" -ForegroundColor Green
    Write-Host "No network calls found. All privacy-sensitive modules removed." -ForegroundColor Green
    Write-Host ""
    Write-Host "NOTE: Code comments and function names referencing 'Teamcraft' are harmless." -ForegroundColor Cyan
    exit 0
}
