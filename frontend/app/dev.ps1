# Frontend - Build & Dev Script
param([switch]$build)

$frontendDir = "$PSScriptRoot"

if ($build) {
    Write-Host "Building frontend..." -ForegroundColor Yellow
    npm run build
    Write-Host "Build output: $frontendDir\dist\" -ForegroundColor Green
} else {
    Write-Host "Starting dev server on http://localhost:5173" -ForegroundColor Cyan
    Write-Host "API backend should be running on http://localhost:5000" -ForegroundColor Yellow
    npm run dev
}
