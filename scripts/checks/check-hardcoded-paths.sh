#!/bin/bash
# Check for hardcoded paths in the codebase

set -e

echo "Checking for hardcoded paths..."

# Patterns to detect hardcoded paths
PATTERNS=(
  # Windows absolute paths (C:, D:, etc.) - but exclude common .NET metadata paths in csproj
  'C:\\(?!Users\\APPDATA|Program Files)'
  'D:\\'
  'E:\\'
  # Unix absolute paths - but exclude common build output references
  '/home/[a-z]'
  '/Users/[A-Z]'
)

FOUND=0
EXCLUDE_DIRS="(.git|bin|obj|node_modules|TestResults)"

for pattern in "${PATTERNS[@]}"; do
  echo "Checking pattern: $pattern"

  # Search in C# files
  if grep -rE "$pattern" --include="*.cs" --exclude-dir="$EXCLUDE_DIRS" .; then
    echo "ERROR: Found hardcoded path matching: $pattern"
    FOUND=1
  fi

  # Search in PowerShell files (but allow %APPDATA% and $env: references)
  if grep -rE "$pattern" --include="*.ps1" --exclude-dir="$EXCLUDE_DIRS" . | grep -v '%APPDATA%' | grep -v '\$env:'; then
    echo "ERROR: Found hardcoded path in PowerShell matching: $pattern"
    FOUND=1
  fi

  # Search in project files (but exclude OutputPath which legitimately uses APPDATA)
  if grep -rE "$pattern" --include="*.csproj" --exclude-dir="$EXCLUDE_DIRS" . | grep -v 'OutputPath' | grep -v 'HintPath'; then
    echo "ERROR: Found hardcoded path in project file matching: $pattern"
    FOUND=1
  fi
done

if [ $FOUND -eq 1 ]; then
  echo ""
  echo "FAILED: Hardcoded paths detected"
  echo ""
  echo "Fix by using:"
  echo "  - C#: Environment.GetFolderPath() or Path.Combine()"
  echo "  - PowerShell: \$env:APPDATA, \$PSScriptRoot, or Join-Path"
  echo "  - .csproj: \$(APPDATA) for OutputPath and HintPath"
  exit 1
fi

echo "SUCCESS: No hardcoded paths found"
exit 0
