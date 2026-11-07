<#
Backup and restore drill for the local RBOS PostgreSQL databases.
Dumps a source database (read-only) and restores it into a scratch database, then compares row counts.
Requires pg_dump, pg_restore and psql on PATH and a role with BYPASSRLS + CREATEDB (rbos_backup) with its password in PGPASSWORD.
Usage: $env:PGPASSWORD='...'; ./scripts/database/backup-restore-drill.ps1 -Source rbos_test -User rbos_backup
#>
param(
	[string]$Source = 'rbos_test',
	[string]$Scratch = 'rbos_restore_drill',
	[string]$PgHost = 'localhost',
	[string]$User = 'postgres',
	[string]$OutDir = (Join-Path $env:TEMP 'rbos-backups')
)
$ErrorActionPreference = 'Stop'
if ($Scratch
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$file = Join-Path $OutDir ("$Source-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dump')

function Run { & $args[0] @($args[1..($args.Count-1)]) 2>&1 | Out-Host; if ($LASTEXITCODE -ne 0) { throw ('Command failed: ' + $args[0]) } }
Run pg_dump -h $PgHost -U $User -d $Source -Fc -f $file
Run psql -h $PgHost -U $User -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS $Scratch" -c "CREATE DATABASE $Scratch"
Run pg_restore -h $PgHost -U $User -d $Scratch --no-owner --no-privileges $file

$sql = "select table_schema||'.'||table_name from information_schema.tables where table_schema in ('identity','core','catalog','inventory','procurement','sales','accounting') and table_type='BASE TABLE' order by 1"
$tables = psql -h $PgHost -U $User -d $Source -At -c $sql
$bad = 0
foreach ($t in $tables) {
	$a = psql -h $PgHost -U $User -d $Source -At -c "select count(*) from $t"
	$b = psql -h $PgHost -U $User -d $Scratch -At -c "select count(*) from $t"
	if ($a -ne $b) { Write-Host "MISMATCH $t source=$a restored=$b"; $bad++ }
}
Write-Host "Dump: $file"
Write-Host ($(if ($bad -eq 0) { "Restore verified: $($tables.Count) tables match." } else { "$bad tables differ." }))
Run psql -h $PgHost -U $User -d postgres -c "DROP DATABASE $Scratch"
if ($bad -ne 0) { exit 1 }

