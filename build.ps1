# PreBootRepair Build Script
# Usage: .\build.ps1 [-Release] [-Clean]

param(
    [switch]$Release,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot

Write-Host "================================" -ForegroundColor Cyan
Write-Host "  PreBootRepair Build Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

Set-Location $ProjectDir

# Clean
if ($Clean) {
    Write-Host "[*] Cleaning..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    Write-Host "[+] Clean complete" -ForegroundColor Green
}

# Restore
Write-Host "[*] Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { 
    Write-Host "[!] Restore failed" -ForegroundColor Red
    exit 1 
}

if ($Release) {
    # Release build - single file
    Write-Host "[*] Building Release (single-file)..." -ForegroundColor Yellow
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o "dist"
    
    if ($LASTEXITCODE -eq 0) {
        $exe = Get-ChildItem "dist\PreBootRepair.exe"
        $sizeMB = [math]::Round($exe.Length / 1MB, 1)
        Write-Host ""
        Write-Host "================================" -ForegroundColor Green
        Write-Host "  BUILD SUCCESSFUL!" -ForegroundColor Green
        Write-Host "================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Output: $($exe.FullName)" -ForegroundColor White
        Write-Host "Size:   $sizeMB MB" -ForegroundColor White
        Write-Host ""
        Write-Host "To run: .\dist\PreBootRepair.exe" -ForegroundColor Cyan
    } else {
        Write-Host "[!] Build failed" -ForegroundColor Red
        exit 1
    }
} else {
    # Debug build
    Write-Host "[*] Building Debug..." -ForegroundColor Yellow
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "[+] Debug build successful" -ForegroundColor Green
        Write-Host "To run: dotnet run" -ForegroundColor Cyan
    } else {
        Write-Host "[!] Build failed" -ForegroundColor Red
        exit 1
    }
}
