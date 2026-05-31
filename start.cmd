@echo off
REM ==============================================
REM  Dynamic OData API - Quick Start
REM  Starts both backend and frontend simultaneously
REM ==============================================

echo.
echo ========================================
echo   Dynamic OData API - Quick Start
echo ========================================
echo.

REM Start Backend
echo [1/2] Starting backend on http://localhost:5000 ...
start "OData Backend" cmd /c "set DOTNET_CLI_HOME=..\.dotnet && set ASPNETCORE_URLS=http://localhost:5000 && "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" run --no-build --configuration Release --project "%~dp0backend\DynamicODataApi\DynamicODataApi.csproj""

REM Wait for backend to start
timeout /t 4 /nobreak >nul

REM Start Frontend
echo [2/2] Starting frontend on http://localhost:5173 ...
start "OData Frontend" cmd /c "cd /d "%~dp0frontend\app" && npm run dev"

echo.
echo ========================================
echo   Backend:  http://localhost:5000/odata
echo   Frontend: http://localhost:5173
echo   Metadata: http://localhost:5000/odata/$metadata
echo ========================================
echo.
echo Close the console windows to stop.
pause
