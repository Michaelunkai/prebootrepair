@echo off
:: PreBootRepair - True Pre-Boot Disk Repair Script
:: Runs in WinPE environment before Windows loads
:: Includes custom NTFS repair tool + standard Windows tools

title PreBootRepair v1.0 - Pre-Boot Disk Repair
color 1F

echo.
echo ============================================================
echo   PreBootRepair v1.0 - True Pre-Boot Disk Repair
echo ============================================================
echo.
echo   This repair runs BEFORE Windows loads.
echo   Your system drive is not mounted by Windows.
echo.

:: Check for our tools
if exist X:\Tools\NtfsRepairTool.exe (
    echo   [+] NtfsRepairTool found
) else (
    echo   [!] NtfsRepairTool not found - some features unavailable
)

:: Detect drives
echo.
echo [*] Detecting drives...
echo.

:: List available volumes
echo Available volumes:
echo.
wmic logicaldisk get caption,description,filesystem,size 2>nul
echo.

:: Ask user which drive to repair
set /p DRIVE="Enter drive letter to repair (e.g., C): "
set DRIVE=%DRIVE::=%

echo.
echo [*] Selected drive: %DRIVE%:
echo.

:: Menu
:MENU
echo.
echo ============================================================
echo   SELECT REPAIR OPTION
echo ============================================================
echo.
echo   === CUSTOM NTFS REPAIR (Our Tool) ===
echo   1. NTFS Deep Analysis (boot sector, MFT, system files)
echo   2. NTFS Auto-Repair (fix detected issues)
echo.
echo   === WINDOWS TOOLS ===
echo   3. Quick Scan (CHKDSK /scan)
echo   4. Standard Repair (CHKDSK /F)
echo   5. Deep Scan with Bad Sector Recovery (CHKDSK /R)
echo   6. Boot Repair (bootrec /fixmbr, /fixboot, /rebuildbcd)
echo.
echo   === ADVANCED TOOLS ===
echo   7. Run TestDisk (partition recovery)
echo   8. Run PhotoRec (file recovery)
echo   9. Command Prompt
echo.
echo   0. Reboot
echo.
set /p CHOICE="Enter choice (0-9): "

if "%CHOICE%"=="1" goto NTFS_ANALYZE
if "%CHOICE%"=="2" goto NTFS_REPAIR
if "%CHOICE%"=="3" goto QUICKSCAN
if "%CHOICE%"=="4" goto STANDARD
if "%CHOICE%"=="5" goto DEEPSCAN
if "%CHOICE%"=="6" goto BOOTREPAIR
if "%CHOICE%"=="7" goto TESTDISK
if "%CHOICE%"=="8" goto PHOTOREC
if "%CHOICE%"=="9" goto CMDPROMPT
if "%CHOICE%"=="0" goto REBOOT
goto MENU

:NTFS_ANALYZE
echo.
echo [*] Running NTFS Deep Analysis on %DRIVE%:...
echo.
if exist X:\Tools\NtfsRepairTool.exe (
    X:\Tools\NtfsRepairTool.exe %DRIVE% /force
) else (
    echo [!] NtfsRepairTool.exe not found!
    echo [!] Please ensure it's in X:\Tools\
)
echo.
pause
goto MENU

:NTFS_REPAIR
echo.
echo [*] Running NTFS Auto-Repair on %DRIVE%:...
echo [*] This will attempt to fix detected issues.
echo.
if exist X:\Tools\NtfsRepairTool.exe (
    X:\Tools\NtfsRepairTool.exe %DRIVE% /repair /force
) else (
    echo [!] NtfsRepairTool.exe not found!
)
echo.
pause
goto MENU

:QUICKSCAN
echo.
echo [*] Running CHKDSK quick scan on %DRIVE%:...
echo.
chkdsk %DRIVE%: /scan
echo.
echo [*] Quick scan complete.
pause
goto MENU

:STANDARD
echo.
echo [*] Running CHKDSK standard repair on %DRIVE%:...
echo [*] This will fix filesystem errors.
echo.
chkdsk %DRIVE%: /F /X
echo.
echo [*] Standard repair complete.
pause
goto MENU

:DEEPSCAN
echo.
echo [*] Running CHKDSK deep scan on %DRIVE%:...
echo [*] This includes bad sector recovery and may take a long time.
echo.
chkdsk %DRIVE%: /R /X
echo.
echo [*] Deep scan complete.
pause
goto MENU

:BOOTREPAIR
echo.
echo [*] Running boot repair...
echo.
echo [*] Step 1: Fixing Master Boot Record...
bootrec /fixmbr
echo.
echo [*] Step 2: Fixing boot sector...
bootrec /fixboot
echo.
echo [*] Step 3: Scanning for Windows installations...
bootrec /scanos
echo.
echo [*] Step 4: Rebuilding Boot Configuration Data...
bootrec /rebuildbcd
echo.
echo [*] Boot repair complete.
pause
goto MENU

:TESTDISK
echo.
echo [*] Launching TestDisk...
if exist X:\Tools\testdisk_win.exe (
    X:\Tools\testdisk_win.exe
) else (
    echo [!] TestDisk not found in X:\Tools\
    echo [!] Download from: https://www.cgsecurity.org/testdisk-7.2.win64.zip
)
pause
goto MENU

:PHOTOREC
echo.
echo [*] Launching PhotoRec for file recovery...
if exist X:\Tools\photorec_win.exe (
    X:\Tools\photorec_win.exe
) else (
    echo [!] PhotoRec not found in X:\Tools\
)
pause
goto MENU

:CMDPROMPT
echo.
echo [*] Dropping to command prompt...
echo [*] Type 'exit' to return to menu.
echo.
echo Available tools:
echo   - NtfsRepairTool.exe (in X:\Tools\)
echo   - chkdsk, bootrec, diskpart
echo   - bcdedit, sfc, dism (if available)
echo.
cmd
goto MENU

:REBOOT
echo.
echo [*] Rebooting system in 5 seconds...
echo [*] Press Ctrl+C to cancel.
timeout /t 5
wpeutil reboot
