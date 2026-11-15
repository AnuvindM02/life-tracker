# Dev mode: SQL Server in Docker, API on http://localhost:5080 (Development: migrates + seeds),
# Vite on http://localhost:5173 with hot reload. Ctrl+C stops the web server; the API window stays open.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path ".env")) { Copy-Item ".env.example" ".env"; Write-Warning "Created .env from .env.example. Set a strong MSSQL_SA_PASSWORD, then run again."; exit 1 }

docker compose up -d --wait db
if (-not (Test-Path "web/node_modules")) { npm --prefix web ci }

Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/SelfTracker.Api" -WorkingDirectory $root
Write-Host "API starting on http://localhost:5080 (Scalar docs at /scalar). Web on http://localhost:5173"
npm --prefix web run dev
