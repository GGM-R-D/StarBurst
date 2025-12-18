@echo off
echo ========================================
echo Building and Starting Services
echo ========================================
echo.

REM Change to the backend directory
cd /d "%~dp0"

REM Check if dotnet is available
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: dotnet CLI not found in PATH!
    echo Please ensure .NET SDK is installed and in your PATH.
    pause
    exit /b 1
)

echo [Step 1/3] Building Game Engine Host...
cd GameEngineHost
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build Game Engine Host!
    pause
    exit /b 1
)
cd ..

echo [Step 2/3] Building RGS Service...
cd RGS\RGS
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build RGS Service!
    pause
    exit /b 1
)
cd ..\..

echo [Step 3/3] Starting services...
call start-services.bat

