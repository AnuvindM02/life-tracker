#!/usr/bin/env bash
# Local "prod": builds the SPA into the API's wwwroot and runs one process on http://localhost:5080.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then cp .env.example .env; echo "Created .env from .env.example. Set a strong MSSQL_SA_PASSWORD, then run again." >&2; exit 1; fi

docker compose up -d --wait db
[ -d web/node_modules ] || npm --prefix web ci
npm --prefix web run build
echo "Open http://localhost:5080"
ASPNETCORE_ENVIRONMENT=Production dotnet run -c Release --project src/SelfTracker.Api -- --migrate
