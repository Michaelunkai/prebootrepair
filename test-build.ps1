# PreBootRepair Build Validation Script
# Run this to verify the build is correct

param([switch]$Verbose)

$ErrorActionPreference = "Continue"
$script:passed = 0
$script:failed = 0

function Test-Item {
    param($Name, $Condition, $Details = "")
    if ($Condition) {
        Write-Host "  [PASS] $Name" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "  [FAIL] $Name" -ForegroundColor Red
        if ($Details) { Write-Host "         $Details" -ForegroundColor Yellow }
        $script:failed++
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  PreBootRepair Build Validation" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Check source files
Write-Host "Source Files:" -ForegroundColor White
Test-Item "PreBootRepair.csproj exists" (Test-Path "F:\Downloads\PreBootRepair\PreBootRepair.csproj")
Test-Item "MainWindow.xaml exists" (Test-Path "F:\Downloads\PreBootRepair\MainWindow.xaml")
Test-Item "MainWindow.xaml.cs exists" (Test-Path "F:\Downloads\PreBootRepair\MainWindow.xaml.cs")
Test-Item "DiskService.cs exists" (Test-Path "F:\Downloads\PreBootRepair\Services\DiskService.cs")
Test-Item "DiskInfo.cs exists" (Test-Path "F:\Downloads\PreBootRepair\Models\DiskInfo.cs")
Test-Item "app.manifest exists" (Test-Path "F:\Downloads\PreBootRepair\app.manifest")

# 2. Check release files
Write-Host "`nRelease Package:" -ForegroundColor White
$exePath = "F:\Downloads\PreBootRepair\release_v1.0.0\PreBootRepair.exe"
Test-Item "PreBootRepair.exe exists" (Test-Path $exePath)
if (Test-Path $exePath) {
    $exe = Get-Item $exePath
    $sizeMB = [math]::Round($exe.Length / 1MB, 1)
    Test-Item "Executable size reasonable (>50MB)" ($sizeMB -gt 50) "Size: $sizeMB MB"
}
Test-Item "README.md exists" (Test-Path "F:\Downloads\PreBootRepair\release_v1.0.0\README.md")
Test-Item "LICENSE exists" (Test-Path "F:\Downloads\PreBootRepair\release_v1.0.0\LICENSE")
Test-Item "QUICKSTART.txt exists" (Test-Path "F:\Downloads\PreBootRepair\release_v1.0.0\QUICKSTART.txt")

# 3. Check documentation
Write-Host "`nDocumentation:" -ForegroundColor White
Test-Item "README.md has content" ((Get-Content "F:\Downloads\PreBootRepair\README.md" -Raw).Length -gt 1000)
Test-Item "build.ps1 exists" (Test-Path "F:\Downloads\PreBootRepair\build.ps1")
Test-Item "build-release.bat exists" (Test-Path "F:\Downloads\PreBootRepair\build-release.bat")
Test-Item ".gitignore exists" (Test-Path "F:\Downloads\PreBootRepair\.gitignore")

# 4. Check executable properties
Write-Host "`nExecutable Properties:" -ForegroundColor White
if (Test-Path $exePath) {
    $versionInfo = (Get-Item $exePath).VersionInfo
    Test-Item "Has file description" ($versionInfo.FileDescription -ne $null)
    Test-Item "Has product name" ($versionInfo.ProductName -ne $null)
    Test-Item "Has company name" ($versionInfo.CompanyName -ne $null)
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
if ($script:failed -eq 0) {
    Write-Host "  ALL TESTS PASSED ($script:passed/$($script:passed))" -ForegroundColor Green
} else {
    Write-Host "  SOME TESTS FAILED ($script:passed passed, $script:failed failed)" -ForegroundColor Yellow
}
Write-Host "========================================`n" -ForegroundColor Cyan
