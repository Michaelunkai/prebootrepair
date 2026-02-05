@echo off
echo ==========================================
echo   PreBootRepair - Release Build
echo ==========================================
echo.

REM Check for .NET SDK
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [1/3] Restoring packages...
dotnet restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Package restore failed!
    pause
    exit /b 1
)

echo.
echo [2/3] Building release...
dotnet publish -c Release -o dist
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo [3/3] Creating release package...
if not exist "release_v1.0.0" mkdir release_v1.0.0
copy /Y dist\PreBootRepair.exe release_v1.0.0\
copy /Y README.md release_v1.0.0\
copy /Y LICENSE release_v1.0.0\
copy /Y QUICKSTART.txt release_v1.0.0\ 2>nul

echo.
echo ==========================================
echo   BUILD SUCCESSFUL!
echo ==========================================
echo.
echo Output: release_v1.0.0\PreBootRepair.exe
echo.
echo To run: release_v1.0.0\PreBootRepair.exe
echo.
pause
