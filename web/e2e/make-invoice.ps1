Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 900, 700
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$f = New-Object System.Drawing.Font 'Arial', 22
$s = New-Object System.Drawing.Font 'Arial', 16
$b = [System.Drawing.Brushes]::Black
$g.DrawString('SRI LAKSHMI ELECTRONICS', $f, $b, 40, 30)
$g.DrawString('Invoice No: INV-2043     Date: 12/05/2025', $s, $b, 40, 90)
$g.DrawString('Item                         Qty    Rate     Amount', $s, $b, 40, 160)
$g.DrawString('E2E Invoice Cable             10     50.00    500.00', $s, $b, 40, 210)
$g.DrawString('E2E Invoice Charger            4    120.00    480.00', $s, $b, 40, 260)
$g.DrawString('E2E Invoice Earphones          6     80.00    480.00', $s, $b, 40, 310)
$g.DrawString('Total                                         1460.00', $s, $b, 40, 400)
$bmp.Save('C:\Users\A1137066\Personal-Lab\projects\retail-business-os\web\e2e\test-invoice.png', [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
