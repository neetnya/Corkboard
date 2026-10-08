# Generate Corkboard icon (PNG preview + ICO)
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assets = Join-Path $root "Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$pngPath = Join-Path $assets "Corkboard.png"
$icoPath = Join-Path $assets "Corkboard.ico"

$S = 256
$bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# background (cork brown)
$bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 172, 120, 66))
$g.FillRectangle($bg, 0, 0, 256, 256)

# cork texture lines
$tex = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 255, 240, 208))
foreach ($y in 54, 104, 154, 204) { $g.FillRectangle($tex, 22, $y, 212, 5) }

# card shadow
$sh = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 0, 0, 0))
$g.FillRectangle($sh, 54, 96, 160, 120)

# photo card (white)
$card = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.FillRectangle($card, 46, 88, 160, 120)

# sky
$sky = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 118, 178, 228))
$g.FillRectangle($sky, 46, 88, 160, 72)

# sun
$sun = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 208, 66))
$g.FillEllipse($sun, 148, 96, 32, 32)

# ground
$grass = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 118, 178, 96))
$g.FillRectangle($grass, 46, 160, 160, 48)

# pin (red)
$pin = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 60, 60))
$g.FillEllipse($pin, 104, 54, 48, 48)

# pin highlight
$hi = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
$g.FillEllipse($hi, 118, 66, 16, 14)

# save PNG
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# build ICO (single 256x256 PNG entry)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ms.Close()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length)
$bw.Write([UInt32]22)
$bw.Write($png)
$bw.Close()
$fs.Close()

$g.Dispose()
$bmp.Dispose()
Write-Host "OK: $icoPath"
Write-Host "OK: $pngPath"
