$ErrorActionPreference = "Continue"
$base = "E:\unity\newvr_fire\VR-fire-simulation\fire_training_ui_v01"
$map = [ordered]@{
  "01_previews\main_menu.png" = "https://aka.doubaocdn.com/s/lmnJcUU5Um"
  "03_png\panels\report_sheet.png" = "https://aka.doubaocdn.com/s/Oxsk2SdkUy"
  "03_png\buttons\button_primary_normal.png" = "https://aka.doubaocdn.com/s/nDfMOtKp5W"
  "03_png\buttons\button_primary_hover.png" = "https://aka.doubaocdn.com/s/4orJWAiaLm"
  "03_png\buttons\button_primary_pressed.png" = "https://aka.doubaocdn.com/s/tF9QTG5IIt"
  "03_png\buttons\button_primary_disabled.png" = "https://aka.doubaocdn.com/s/sfkmjuqUbN"
  "03_png\icons\icon_check.png" = "https://aka.doubaocdn.com/s/8cv2v9bzk3"
  "03_png\icons\icon_cross.png" = "https://aka.doubaocdn.com/s/d3Fc454MDD"
  "03_png\icons\icon_warning.png" = "https://aka.doubaocdn.com/s/2V5nBto6id"
  "03_png\icons\icon_clock.png" = "https://aka.doubaocdn.com/s/M2VZlGDWkh"
  "03_png\icons\icon_health.png" = "https://aka.doubaocdn.com/s/sZ3SIpzpR3"
  "03_png\icons\icon_protection.png" = "https://aka.doubaocdn.com/s/W6Vd4fKzVZ"
}

function Test-PngComplete($path) {
  if (-not (Test-Path $path)) { return $false }
  $fs = [System.IO.File]::OpenRead($path)
  try {
    if ($fs.Length -lt 64) { return $false }
    $fs.Seek($fs.Length - 24, 'Begin') | Out-Null
    $buf = New-Object byte[] 24
    $fs.Read($buf, 0, 24) | Out-Null
    $tail = [System.Text.Encoding]::ASCII.GetString($buf)
    return $tail.Contains("IEND")
  } finally { $fs.Close() }
}

foreach ($k in $map.Keys) {
  $out = Join-Path $base $k
  $dir = Split-Path $out -Parent
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $ok = $false
  for ($attempt = 1; $attempt -le 3 -and -not $ok; $attempt++) {
    if (Test-PngComplete $out) { $ok = $true; break }
    if (Test-Path $out) { Remove-Item $out -Force }
    Write-Output "[$attempt] $k"
    & curl.exe -L --http1.1 --retry 3 --retry-all-errors --connect-timeout 15 --max-time 300 -sS -o $out $map[$k]
    if (Test-PngComplete $out) { $ok = $true }
  }
  $size = if (Test-Path $out) { (Get-Item $out).Length } else { 0 }
  Write-Output ("{0}  ok={1}  bytes={2}" -f $k, $ok, $size)
}
Write-Output "DONE"
