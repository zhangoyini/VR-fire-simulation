$ErrorActionPreference = "Continue"
$base = "E:\unity\newvr_fire\VR-fire-simulation\fire_training_ui_v01\03_png\panels"
$map = [ordered]@{
  "report_sheet_plain.png"  = "https://aka.doubaocdn.com/s/GoAb5yiSrH"
  "lesson_card_large.png"  = "https://aka.doubaocdn.com/s/cxDrNE44UM"
  "lesson_card_compact.png" = "https://aka.doubaocdn.com/s/HQOwRyPObO"
  "notice_card.png"         = "https://aka.doubaocdn.com/s/3Sg0DgmjKG"
  "warning_card.png"        = "https://aka.doubaocdn.com/s/BZ054HKufI"
  "task_clipboard.png"      = "https://aka.doubaocdn.com/s/hgG7pVyITq"
}

function Test-PngComplete($path) {
  if (-not (Test-Path $path)) { return $false }
  $fs = [System.IO.File]::OpenRead($path)
  try {
    if ($fs.Length -lt 64) { return $false }
    $fs.Seek($fs.Length - 24, 'Begin') | Out-Null
    $buf = New-Object byte[] 24
    $fs.Read($buf, 0, 24) | Out-Null
    return [System.Text.Encoding]::ASCII.GetString($buf).Contains("IEND")
  } finally { $fs.Close() }
}

foreach ($k in $map.Keys) {
  $out = Join-Path $base $k
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
