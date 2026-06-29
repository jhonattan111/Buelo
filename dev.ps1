# dev.ps1 — starts BueloApi (5238) and BueloWeb (5173) in separate windows.
# Usage:  ./dev.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "Starting BueloApi at http://localhost:5238 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  '-NoExit', '-Command',
  "Set-Location '$root\BueloApi'; dotnet run --project Buelo.Api"
)

Write-Host "Starting BueloWeb at http://localhost:5173 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  '-NoExit', '-Command',
  "Set-Location '$root\BueloWeb'; if (-not (Test-Path node_modules)) { pnpm install }; pnpm dev"
)

Write-Host "Done. Two windows opened (API + Web)." -ForegroundColor Green
