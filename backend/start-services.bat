@echo off
echo ========================================
echo Starting Game Engine and RGS Services
echo ========================================
echo.
echo NOTE: Projects will be built first, then started with --no-build
echo       to avoid conflicts if services are already running.
echo.
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

REM Build both projects FIRST (before starting any services)
REM This avoids build conflicts when services are already running
echo [0/3] Building projects...
echo    Building Game Engine Host...
cd /d %CD%\GameEngineHost
dotnet build >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo    WARNING: Game Engine Host build had issues. Check for errors above.
) else (
    echo    Game Engine Host build complete.
)
cd /d %~dp0

echo    Building RGS Service (this will also build GameEngineHost as dependency)...
cd /d %CD%\RGS\RGS
dotnet build >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo    WARNING: RGS build had issues. Check for errors above.
) else (
    echo    RGS build complete.
)
cd /d %~dp0
echo.

REM Start Game Engine Host on port 5102 (RGS will be configured to use this)
echo [1/3] Starting Game Engine Host on port 5102...
echo    Working directory: %CD%\GameEngineHost
start "GameEngineHost - Port 5102" cmd /k "title GameEngineHost - Port 5102 && cd /d %CD%\GameEngineHost && echo Starting Game Engine Host... && dotnet run --no-build --urls http://localhost:5102"
if %ERRORLEVEL% NEQ 0 (
    echo    WARNING: Failed to start Game Engine Host. Check the window for errors.
) else (
    echo    Game Engine Host window opened.
)

REM Wait for Game Engine to start
echo [2/3] Waiting for Game Engine to initialize (10 seconds)...
timeout /t 10 /nobreak >nul

REM Start RGS on port 5101 (frontend expects this port)
echo [3/3] Starting RGS Service on port 5101...
echo    Working directory: %CD%\RGS\RGS
start "RGS - Port 5101" cmd /k "title RGS - Port 5101 && cd /d %CD%\RGS\RGS && echo Starting RGS Service... && dotnet run --no-build --urls http://localhost:5101"
if %ERRORLEVEL% NEQ 0 (
    echo    WARNING: Failed to start RGS Service. Check the window for errors.
) else (
    echo    RGS Service window opened.
)

echo.
echo ========================================
echo Services Status:
echo ========================================
echo Game Engine Host: http://localhost:5102
echo RGS Service:      http://localhost:5101
echo.
echo IMPORTANT: Check the service windows for:
echo   - "Now listening on: http://localhost:XXXX" messages
echo   - Any error messages or exceptions
echo.
echo If you see errors, the services may need to be built first:
echo   cd GameEngineHost && dotnet build
echo   cd ..\RGS\RGS && dotnet build
echo.
echo Both services are running in separate windows.
echo Close those windows to stop the services.
echo.
echo Press any key to close this window (services will continue running)...
pause >nul

