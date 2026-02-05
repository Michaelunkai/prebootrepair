# PreBootRepair - WinPE ISO Builder
# Creates a bootable WinPE environment with disk repair tools
#
# Requirements:
# - Windows ADK (Assessment and Deployment Kit)
# - Windows ADK WinPE add-on
# - Administrator privileges

param(
    [string]$OutputPath = ".\PreBootRepair.iso",
    [switch]$IncludeTestDisk
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PreBootRepair - WinPE ISO Builder" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check for admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] This script requires Administrator privileges." -ForegroundColor Red
    Write-Host "[!] Please run as Administrator." -ForegroundColor Red
    exit 1
}

# Find Windows ADK
$adkPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\Assessment and Deployment Kit",
    "$env:ProgramFiles\Windows Kits\10\Assessment and Deployment Kit"
)

$adkPath = $null
foreach ($path in $adkPaths) {
    if (Test-Path $path) {
        $adkPath = $path
        break
    }
}

if (-not $adkPath) {
    Write-Host "[!] Windows ADK not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Windows ADK from:" -ForegroundColor Yellow
    Write-Host "https://docs.microsoft.com/en-us/windows-hardware/get-started/adk-install" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Required components:" -ForegroundColor Yellow
    Write-Host "  - Deployment Tools" -ForegroundColor White
    Write-Host "  - Windows Preinstallation Environment (WinPE)" -ForegroundColor White
    exit 1
}

Write-Host "[+] Found Windows ADK at: $adkPath" -ForegroundColor Green

# Set up environment
$deploymentTools = "$adkPath\Deployment Tools"
$winpePath = "$adkPath\Windows Preinstallation Environment"

# Check for copype.cmd
$copype = "$deploymentTools\DandISetEnv.bat"
if (-not (Test-Path $copype)) {
    Write-Host "[!] Deployment Tools not properly installed." -ForegroundColor Red
    exit 1
}

Write-Host "[*] Setting up WinPE environment..." -ForegroundColor Yellow

# Create working directory
$workDir = "$env:TEMP\PreBootRepair_WinPE"
$mountDir = "$workDir\mount"

if (Test-Path $workDir) {
    Write-Host "[*] Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $workDir | Out-Null
New-Item -ItemType Directory -Force -Path $mountDir | Out-Null

# Run copype to create WinPE files
Write-Host "[*] Creating WinPE base (this may take a few minutes)..." -ForegroundColor Yellow

$arch = "amd64"
$copypeCmd = "$deploymentTools\copype.cmd"

if (Test-Path $copypeCmd) {
    & cmd /c "`"$copypeCmd`" $arch `"$workDir`""
} else {
    # Manual copy if copype not available
    Write-Host "[*] Manually copying WinPE files..." -ForegroundColor Yellow
    Copy-Item "$winpePath\$arch\en-us\winpe.wim" "$workDir\media\sources\boot.wim" -Force
}

# Mount the WIM
Write-Host "[*] Mounting WinPE image..." -ForegroundColor Yellow
$wimPath = "$workDir\media\sources\boot.wim"

if (-not (Test-Path $wimPath)) {
    Write-Host "[!] WinPE image not found at expected location." -ForegroundColor Red
    exit 1
}

dism /Mount-Wim /WimFile:$wimPath /Index:1 /MountDir:$mountDir

# Add our scripts
Write-Host "[*] Adding PreBootRepair scripts..." -ForegroundColor Yellow
$scriptsDest = "$mountDir\Windows\System32"

Copy-Item ".\Scripts\PreBootRepair.cmd" "$scriptsDest\" -Force

# Create startnet.cmd to auto-launch our tool
$startnetContent = @"
@echo off
wpeinit
cd /d %SystemRoot%\System32
call PreBootRepair.cmd
"@
$startnetContent | Out-File -FilePath "$scriptsDest\startnet.cmd" -Encoding ascii -Force

# Add Tools directory
Write-Host "[*] Adding repair tools..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$mountDir\Tools" | Out-Null

# Check if TestDisk is available locally
$testdiskPath = ".\Tools\testdisk_win.exe"
if (Test-Path $testdiskPath) {
    Copy-Item ".\Tools\*" "$mountDir\Tools\" -Recurse -Force
    Write-Host "[+] TestDisk/PhotoRec added" -ForegroundColor Green
} else {
    Write-Host "[*] TestDisk not found - downloading..." -ForegroundColor Yellow
    # Download TestDisk if not present
    try {
        $testdiskUrl = "https://www.cgsecurity.org/testdisk-7.2.win64.zip"
        $zipPath = "$workDir\testdisk.zip"
        Invoke-WebRequest -Uri $testdiskUrl -OutFile $zipPath -UseBasicParsing
        Expand-Archive -Path $zipPath -DestinationPath "$workDir\testdisk_temp"
        Copy-Item "$workDir\testdisk_temp\testdisk-7.2\*.exe" "$mountDir\Tools\" -Force
        Write-Host "[+] TestDisk downloaded and added" -ForegroundColor Green
    } catch {
        Write-Host "[!] Could not download TestDisk: $_" -ForegroundColor Yellow
        Write-Host "[!] You can manually add testdisk_win.exe and photorec_win.exe to Tools folder" -ForegroundColor Yellow
    }
}

# Unmount and commit
Write-Host "[*] Finalizing WinPE image..." -ForegroundColor Yellow
dism /Unmount-Wim /MountDir:$mountDir /Commit

# Create ISO
Write-Host "[*] Creating bootable ISO..." -ForegroundColor Yellow

$oscdimg = "$deploymentTools\$arch\Oscdimg\oscdimg.exe"
$efisys = "$deploymentTools\$arch\Oscdimg\efisys.bin"
$etfsboot = "$deploymentTools\$arch\Oscdimg\etfsboot.com"

if (Test-Path $oscdimg) {
    & $oscdimg -m -o -u2 -udfver102 -bootdata:"2#p0,e,b$etfsboot#pEF,e,b$efisys" "$workDir\media" $OutputPath
} else {
    Write-Host "[!] oscdimg.exe not found. Cannot create ISO." -ForegroundColor Red
    Write-Host "[!] The WinPE files are available at: $workDir\media" -ForegroundColor Yellow
    exit 1
}

if (Test-Path $OutputPath) {
    $iso = Get-Item $OutputPath
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "  BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "ISO created: $($iso.FullName)" -ForegroundColor White
    Write-Host "Size: $([math]::Round($iso.Length/1MB)) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "To use:" -ForegroundColor Cyan
    Write-Host "1. Burn to USB with Rufus or similar tool" -ForegroundColor White
    Write-Host "2. Boot from USB" -ForegroundColor White
    Write-Host "3. PreBootRepair will launch automatically" -ForegroundColor White
} else {
    Write-Host "[!] ISO creation may have failed. Check for errors above." -ForegroundColor Red
}

# Cleanup
Write-Host ""
Write-Host "[*] Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue

Write-Host "[+] Done!" -ForegroundColor Green
