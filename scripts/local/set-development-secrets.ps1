#requires -Version 5.1
<#
Sets .NET user-secrets for Retail Business OS local PostgreSQL connections.
Prompts privately for the rbos_migrator and rbos_app passwords, and also sets those role
passwords in the local PostgreSQL (using the DPAPI-protected superuser password).
Never prints or stores password literals in files. Prints only secret key names.
#>
[CmdletBinding()]
param(
	[int]$Port = 5432,
	[string]$PgBin = "$env:USERPROFILE\pg17\pgsql\bin",
	[string]$ApiProject = (Join-Path $PSScriptRoot '..\..\src\Rbos.Api')
)
$ErrorActionPreference = 'Stop'

function ConvertTo-Plain([securestring]$s) {
	$b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
	try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}

if (-not (Select-String -Path (Join-Path $ApiProject 'Rbos.Api.csproj') -Pattern '<UserSecretsId>' -Quiet)) {
	throw 'User-secrets are not initialized. Run: dotnet user-secrets init --project src/Rbos.Api'
}

$migPw = Read-Host 'Password for rbos_migrator' -AsSecureString
$appPw = Read-Host 'Password for rbos_app' -AsSecureString
$workerPw = Read-Host 'Password for rbos_worker (WhatsApp queue access only)' -AsSecureString
$mig = ConvertTo-Plain $migPw
$app = ConvertTo-Plain $appPw
$worker = ConvertTo-Plain $workerPw
if ($mig.Length -lt 12 -or $app.Length -lt 12 -or $worker.Length -lt 12) { throw 'Use passwords of at least 12 characters.' }
if ($mig -eq $app -or $mig -eq $worker -or $app -eq $worker) { throw 'Migrator, app and worker passwords must differ.' }
if ($mig -match "['\x22;=\s]" -or $app -match "['\x22;=\s]" -or $worker -match "['\x22;=\s]") { throw 'Avoid quotes, semicolons, equals signs and spaces in passwords.' }

# Set role passwords in local PostgreSQL via psql stdin (never on a command line).
$su = Get-Content "$env:USERPROFILE\.rbos-local\pg-superuser.dpapi" | ConvertTo-SecureString
$env:PGPASSWORD = ConvertTo-Plain $su
try {
	$sql = "ALTER ROLE rbos_migrator PASSWORD '$mig';`nALTER ROLE rbos_app PASSWORD '$app';`nALTER ROLE rbos_worker PASSWORD '$worker';"
	$sql | & "$PgBin\psql.exe" -h localhost -p $Port -U postgres -d postgres -w -q -v ON_ERROR_STOP=1 | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Failed to set role passwords.' }
} finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }

$map = @{
	'ConnectionStrings:RbosDevMigrator'  = @('rbos_migrator', $mig, 'rbos_dev')
	'ConnectionStrings:RbosDevApp'       = @('rbos_app',      $app, 'rbos_dev')
	'ConnectionStrings:RbosTestMigrator' = @('rbos_migrator', $mig, 'rbos_test')
	'ConnectionStrings:RbosTestApp'      = @('rbos_app',      $app, 'rbos_test')
	'ConnectionStrings:RbosDevWhatsAppWorker'  = @('rbos_worker', $worker, 'rbos_dev')
	'ConnectionStrings:RbosTestWhatsAppWorker' = @('rbos_worker', $worker, 'rbos_test')
}
foreach ($k in $map.Keys) {
	$u, $p, $d = $map[$k]
	$cs = "Host=localhost;Port=$Port;Database=$d;Username=$u;Password=$p"
	dotnet user-secrets set $k $cs --project $ApiProject | Out-Null
	if ($LASTEXITCODE -ne 0) { throw "Failed to set $k" }
	Write-Host "Configured: $k"
}
$mig = $null; $app = $null; $worker = $null
Write-Host 'Done. No password values were displayed or stored in files.'
