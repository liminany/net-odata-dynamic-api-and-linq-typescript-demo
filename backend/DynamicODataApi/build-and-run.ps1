# Dynamic OData API - Backend Build & Run Script
# 用法: 在 PowerShell 中运行 .\build-and-run.ps1

$ErrorActionPreference = "Stop"
$backendDir = "$PSScriptRoot"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Dynamic OData API - Build & Run" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. 设置 .NET SDK 路径 (如果 dotnet 不在 PATH 中)
$dotnetPath = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
if (Test-Path $dotnetPath) {
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
    Write-Host "[OK] .NET SDK found at: $env:LOCALAPPDATA\Microsoft\dotnet" -ForegroundColor Green
} else {
    Write-Host "[WARN] .NET SDK not found in LOCALAPPDATA, trying PATH..." -ForegroundColor Yellow
}

# 2. Restore NuGet packages
Write-Host "`n[1/3] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore --configfile "$backendDir\NuGet.config" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] NuGet restore failed. Try running as Administrator." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Packages restored" -ForegroundColor Green

# 3. Build
Write-Host "`n[2/3] Building project..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Build succeeded" -ForegroundColor Green

# 4. Run
Write-Host "`n[3/3] Starting API server on http://localhost:5000" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " API:       http://localhost:5000/odata" -ForegroundColor White
Write-Host " Metadata:  http://localhost:5000/odata/$metadata" -ForegroundColor White
Write-Host " Frontend:  http://localhost:5173" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan

$env:ASPNETCORE_URLS = "http://localhost:5000"
dotnet run --configuration Release --no-build
