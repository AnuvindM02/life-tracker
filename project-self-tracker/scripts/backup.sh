#!/usr/bin/env bash
# Backups.
#   scripts/backup.sh            Full app backup (.zip: every table as JSON + files) via the running API -> data/backups
#   scripts/backup.sh --native   SQL Server BACKUP DATABASE (.bak) -> data/sqlbackups (API not needed)
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a
export MSYS_NO_PATHCONV=1 # Git Bash: keep container paths as-is

if [ "${1:-}" != "--native" ]; then
  curl -fsS -X POST -H "X-SelfTracker: 1" "http://localhost:${API_PORT:-5080}/api/v1/data/backups"
  echo
  exit 0
fi

DB="${DB_NAME:-SelfTracker}"
FILE="$DB-$(date +%Y%m%d-%H%M%S).bak"
# SQL Server cannot write to a Docker Desktop bind mount on Windows, so back up inside the volume and copy out.
docker exec -u 0 selftracker-db sh -c "mkdir -p /var/opt/mssql/backups && chown mssql /var/opt/mssql/backups"
docker exec selftracker-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b \
  -Q "BACKUP DATABASE [$DB] TO DISK=N'/var/opt/mssql/backups/$FILE' WITH INIT, COMPRESSION, CHECKSUM"
mkdir -p data/sqlbackups
docker cp "selftracker-db:/var/opt/mssql/backups/$FILE" "data/sqlbackups/$FILE"
docker exec selftracker-db rm -f "/var/opt/mssql/backups/$FILE"
echo "Native backup written: data/sqlbackups/$FILE (uploaded files are not inside a .bak; use the app backup for those)"
