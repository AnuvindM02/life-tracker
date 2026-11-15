# Restore a native SQL Server backup made by backup.ps1 -Native. This REPLACES the current database.
# Stop the API first. (Restoring an app .zip backup into an empty database ships with milestone M8.)
#   ./scripts/restore.ps1 data/sqlbackups/SelfTracker-20261115-101500.bak
param([Parameter(Mandatory = $true)][string]$BakFile)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not (Test-Path $BakFile)) { throw "Not found: $BakFile" }

$env_ = @{}; Get-Content .env | Where-Object { $_ -match '^[A-Z_]+=' } | ForEach-Object { $k, $v = $_ -split '=', 2; $env_[$k] = $v }
$db = if ($env_["DB_NAME"]) { $env_["DB_NAME"] } else { "SelfTracker" }
$answer = Read-Host "This replaces database '$db' with $BakFile. Type the database name to continue"
if ($answer -ne $db) { Write-Host "Cancelled."; exit 1 }

$name = Split-Path -Leaf $BakFile
docker exec -u 0 selftracker-db sh -c "mkdir -p /var/opt/mssql/backups && chown mssql /var/opt/mssql/backups"
docker cp $BakFile "selftracker-db:/var/opt/mssql/backups/$name"
docker exec selftracker-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $env_["MSSQL_SA_PASSWORD"] -b `
    -Q "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$db] FROM DISK=N'/var/opt/mssql/backups/$name' WITH REPLACE, CHECKSUM; ALTER DATABASE [$db] SET MULTI_USER;"
docker exec selftracker-db rm -f "/var/opt/mssql/backups/$name"
Write-Host "Restored $db. Start the API again, then run Settings > Data > Verify files."
