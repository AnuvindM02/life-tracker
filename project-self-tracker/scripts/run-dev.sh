#!/usr/bin/env bash
# Dev mode: SQL Server in Docker, API on :5080 (Development: migrates + seeds), Vite on :5173.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then cp .env.example .env; echo "Created .env from .env.example. Set a strong MSSQL_SA_PASSWORD, then run again." >&2; exit 1; fi

docker compose up -d --wait db
[ -d web/node_modules ] || npm --prefix web ci

dotnet run --project src/SelfTracker.Api &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT
echo "API starting on http://localhost:5080 (Scalar docs at /scalar). Web on http://localhost:5173"
npm --prefix web run dev
