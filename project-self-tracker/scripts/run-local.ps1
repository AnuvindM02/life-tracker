# Local "prod": builds the SPA into the API's wwwroot and runs one process on http://localhost:5080.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path ".env")) { Copy-Item ".env.example" ".env"; Write-Warning "Created .env from .env.example. Set a strong MSSQL_SA_PASSWORD, then run again."; exit 1 }

docker compose up -d --wait db
if (-not (Test-Path "web/node_modules")) { npm --prefix web ci }
npm --prefix web run build
$env:ASPNETCORE_ENVIRONMENT = "Production"
Write-Host "Open http://localhost:5080"
dotnet run -c Release --project src/SelfTracker.Api -- --migrate
