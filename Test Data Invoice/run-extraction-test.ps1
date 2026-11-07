param(
  [string]$Api = 'http://localhost:5082/api',
  [string]$Email = 'demo.electronics@example.test',
  [int]$DelaySeconds = 6
)
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf = (Invoke-RestMethod "$Api/csrf" -WebSession $s).token
$h = @{ 'X-CSRF-TOKEN' = $csrf }
Invoke-RestMethod "$Api/auth/login" -Method Post -WebSession $s -Headers $h -ContentType 'application/json' -Body (@{ email = $Email; password = '' } | ConvertTo-Json) | Out-Null
$csrf = (Invoke-RestMethod "$Api/csrf" -WebSession $s).token
$h = @{ 'X-CSRF-TOKEN' = $csrf }

$results = @()
foreach ($f in Get-ChildItem $dir -File | Where-Object { $_.Extension -match '\.(jpg|jpeg|png|webp|pdf)$' } | Sort-Object Name) {
  $mime = if ($f.Extension -eq '.pdf') { 'application/pdf' } elseif ($f.Extension -eq '.png') { 'image/png' } elseif ($f.Extension -eq '.webp') { 'image/webp' } else { 'image/jpeg' }
  $body = @{ contentType = $mime; data = [Convert]::ToBase64String([IO.File]::ReadAllBytes($f.FullName)) } | ConvertTo-Json
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try {
	$d = Invoke-RestMethod "$Api/purchases/extract" -Method Post -WebSession $s -Headers $h -ContentType 'application/json' -Body $body -TimeoutSec 120
	$results += [pscustomobject]@{ file = $f.Name; ok = $true; seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1); supplier = $d.supplierName; supplierMatched = [bool]$d.supplierId; invoiceNumber = $d.invoiceNumber; date = $d.date; total = $d.total; lineCount = @($d.lines).Count; matchedLines = @($d.lines | Where-Object { $_.productId }).Count; warnings = @($d.warnings); lines = @($d.lines | ForEach-Object { "$($_.description) | qty $($_.quantity) | rate $($_.unitCost) | amt $($_.amount)" }) }
  } catch {
	$results += [pscustomobject]@{ file = $f.Name; ok = $false; seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1); error = $_.Exception.Message }
  }
  Write-Host ("{0}: {1}" -f $f.Name, $(if ($results[-1].ok) { 'ok' } else { 'FAILED' }))
  Start-Sleep -Seconds $DelaySeconds
}
$out = Join-Path $dir 'extraction-report.json'
$results | ConvertTo-Json -Depth 6 | Set-Content $out -Encoding UTF8
Write-Host "Report: $out"
