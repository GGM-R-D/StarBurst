@echo off
echo ========================================
echo Starting Game Engine Host
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

echo Starting Game Engine Host on port 5102...
echo Working directory: %CD%\GameEngineHost
echo.
echo The service will start in this window.
echo Press Ctrl+C to stop the service.
echo.

cd GameEngineHost
dotnet run --urls http://localhost:5102

