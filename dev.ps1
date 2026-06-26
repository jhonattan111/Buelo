# dev.ps1 — sobe BueloApi (5238) e BueloWeb (5173) em janelas separadas.
# Uso:  ./dev.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "Subindo BueloApi em http://localhost:5238 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  '-NoExit', '-Command',
  "Set-Location '$root\BueloApi'; dotnet run --project Buelo.Api"
)

Write-Host "Subindo BueloWeb em http://localhost:5173 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  '-NoExit', '-Command',
  "Set-Location '$root\BueloWeb'; if (-not (Test-Path node_modules)) { pnpm install }; pnpm dev"
)

Write-Host "Pronto. Duas janelas foram abertas (API + Web)." -ForegroundColor Green
