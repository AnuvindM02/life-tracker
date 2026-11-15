#!/usr/bin/env bash
# Restore a native SQL Server backup made by `backup.sh --native`. This REPLACES the current database.
# Stop the API first. (Restoring an app .zip backup into an empty database ships with milestone M8.)
#   scripts/restore.sh data/sqlbackups/SelfTracker-20261115-101500.bak
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a
export MSYS_NO_PATHCONV=1
BAK="${1:?usage: restore.sh <file.bak>}"
[ -f "$BAK" ] || { echo "Not found: $BAK" >&2; exit 1; }
DB="${DB_NAME:-SelfTracker}"
read -r -p "This replaces database '$DB' with $BAK. Type the database name to continue: " ANSWER
[ "$ANSWER" = "$DB" ] || { echo "Cancelled."; exit 1; }

NAME="$(basename "$BAK")"
docker exec -u 0 selftracker-db sh -c "mkdir -p /var/opt/mssql/backups && chown mssql /var/opt/mssql/backups"
docker cp "$BAK" "selftracker-db:/var/opt/mssql/backups/$NAME"
docker exec selftracker-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b \
  -Q "ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$DB] FROM DISK=N'/var/opt/mssql/backups/$NAME' WITH REPLACE, CHECKSUM; ALTER DATABASE [$DB] SET MULTI_USER;"
docker exec selftracker-db rm -f "/var/opt/mssql/backups/$NAME"
echo "Restored $DB. Start the API again, then run Settings > Data > Verify files."
