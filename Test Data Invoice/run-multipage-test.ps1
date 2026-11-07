param(
  [string]$Api = 'http://localhost:5082/api',
  [string]$Email = 'demo.electronics@example.test'
  
)
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf = (Invoke-RestMethod "$Api/csrf" -WebSession $s).token
$h = @{ 'X-CSRF-TOKEN' = $csrf }
Invoke-RestMethod "$Api/auth/login" -Method Post -WebSession $s -Headers $h -ContentType 'application/json' -Body (@{ email = $Email; password = '' } | ConvertTo-Json) | Out-Null
$csrf = (Invoke-RestMethod "$Api/csrf" -WebSession $s).token
$h = @{ 'X-CSRF-TOKEN' = $csrf }
$pages = @(foreach ($n in 'Media (5).jpg', 'Media (3).jpg') { @{ contentType = 'image/jpeg'; data = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir $n))) } })
$sw = [Diagnostics.Stopwatch]::StartNew()
try { $d = Invoke-RestMethod "$Api/purchases/extract" -Method Post -WebSession $s -Headers $h -ContentType 'application/json' -Body (@{ pages = $pages } | ConvertTo-Json -Depth 5) -TimeoutSec 180 } catch { $rd = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream()); "ERR: " + $rd.ReadToEnd(); exit 1 }
"seconds=$([int]$sw.Elapsed.TotalSeconds) supplier=$($d.supplierName) inv=$($d.invoiceNumber) date=$($d.date) total=$($d.total) lines=$(@($d.lines).Count) fileId=$($d.fileId)"
$d.lines | ForEach-Object { "  {0} | qty={1} | rate={2} | amt={3}" -f $_.description, $_.quantity, $_.unitCost, $_.amount }
"sum of line amounts = " + (($d.lines | Measure-Object amount -Sum).Sum)
"warnings: " + (@($d.warnings) -join ' ; ')
