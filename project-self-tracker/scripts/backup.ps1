# Backups.
#   ./scripts/backup.ps1           Full app backup (.zip: every table as JSON + files) via the running API -> data/backups
#   ./scripts/backup.ps1 -Native   SQL Server BACKUP DATABASE (.bak) -> data/sqlbackups (API not needed)
param([switch]$Native)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $Native) {
    $port = (Get-Content .env | Where-Object { $_ -match '^API_PORT=' }) -replace 'API_PORT=', ''
    if (-not $port) { $port = 5080 }
    $r = Invoke-RestMethod -Method Post -Uri "http://localhost:$port/api/v1/data/backups" -Headers @{ "X-SelfTracker" = "1" }
    Write-Host "Backup written: data/backups/$($r.fileName) ($($r.fileCount) files)"
    exit 0
}

$env_ = @{}; Get-Content .env | Where-Object { $_ -match '^[A-Z_]+=' } | ForEach-Object { $k, $v = $_ -split '=', 2; $env_[$k] = $v }
$db = if ($env_["DB_NAME"]) { $env_["DB_NAME"] } else { "SelfTracker" }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$file = "$db-$stamp.bak"
# SQL Server cannot write to a Windows bind mount, so back up inside the container volume, then copy out.
docker exec -u 0 selftracker-db sh -c "mkdir -p /var/opt/mssql/backups && chown mssql /var/opt/mssql/backups"
docker exec selftracker-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $env_["MSSQL_SA_PASSWORD"] -b `
    -Q "BACKUP DATABASE [$db] TO DISK=N'/var/opt/mssql/backups/$file' WITH INIT, COMPRESSION, CHECKSUM"
New-Item -ItemType Directory -Force "data/sqlbackups" | Out-Null
docker cp "selftracker-db:/var/opt/mssql/backups/$file" "data/sqlbackups/$file"
docker exec selftracker-db rm -f "/var/opt/mssql/backups/$file"
Write-Host "Native backup written: data/sqlbackups/$file (uploaded files are not inside a .bak; use the app backup for those)"
