$ErrorActionPreference = "Continue"
$base = "E:\unity\newvr_fire\VR-fire-simulation\fire_training_ui_v01"
$map = [ordered]@{
  "01_previews\pause.png" = "https://aka.doubaocdn.com/s/z1BLCcZpqK"
  "01_previews\result_success.png" = "https://aka.doubaocdn.com/s/Z9X2gmtFrn"
  "01_previews\result_failure.png" = "https://aka.doubaocdn.com/s/cBVSNSga5d"
  "01_previews\result_details.png" = "https://aka.doubaocdn.com/s/vk3t8823T7"
  "04_concepts\terminal_front_side.png" = "https://aka.doubaocdn.com/s/kDh8GhabhL"
  "03_png\panels\main_terminal_face.png" = "https://aka.doubaocdn.com/s/TiTSfIxGX5"
  "03_png\panels\main_screen_base.png" = "https://aka.doubaocdn.com/s/o9GlxT71XY"
  "03_png\panels\pause_board.png" = "https://aka.doubaocdn.com/s/NJ4UUUosco"
  "03_png\buttons\button_secondary_normal.png" = "https://aka.doubaocdn.com/s/Q3U96oVAe1"
  "03_png\buttons\button_secondary_hover.png" = "https://aka.doubaocdn.com/s/JV7YUBVP46"
  "03_png\buttons\button_secondary_pressed.png" = "https://aka.doubaocdn.com/s/NP8LghzOPz"
  "03_png\buttons\button_secondary_disabled.png" = "https://aka.doubaocdn.com/s/VBSGZyEc0Z"
  "03_png\buttons\button_square_normal.png" = "https://aka.doubaocdn.com/s/HNUFso0a9o"
  "03_png\buttons\button_square_hover.png" = "https://aka.doubaocdn.com/s/FeHgVbRMyj"
  "03_png\buttons\button_square_pressed.png" = "https://aka.doubaocdn.com/s/4RIjJVYko6"
  "03_png\buttons\button_square_disabled.png" = "https://aka.doubaocdn.com/s/6ePnGvj1V7"
  "03_png\decorations\led_off.png" = "https://aka.doubaocdn.com/s/qAiLlBVmXi"
  "03_png\decorations\led_on.png" = "https://aka.doubaocdn.com/s/HnHV8E7aFw"
  "03_png\decorations\badge_success.png" = "https://aka.doubaocdn.com/s/eq1NqjN39K"
  "03_png\decorations\badge_failure.png" = "https://aka.doubaocdn.com/s/lg1WREvtD9"
  "03_png\icons\icon_start.png" = "https://aka.doubaocdn.com/s/StS94WX4JS"
  "03_png\icons\icon_resume.png" = "https://aka.doubaocdn.com/s/ACQDnhWqia"
  "03_png\icons\icon_restart.png" = "https://aka.doubaocdn.com/s/uGYZFUXbeG"
  "03_png\icons\icon_home.png" = "https://aka.doubaocdn.com/s/beh1L1isYs"
  "03_png\icons\icon_exit.png" = "https://aka.doubaocdn.com/s/m8xhyjpesR"
  "03_png\icons\icon_details.png" = "https://aka.doubaocdn.com/s/T8OGn7UKGO"
  "03_png\icons\icon_previous.png" = "https://aka.doubaocdn.com/s/67COSnO15u"
  "03_png\icons\icon_next.png" = "https://aka.doubaocdn.com/s/01weYnpk0e"
  "03_png\icons\icon_map.png" = "https://aka.doubaocdn.com/s/NTPVdV7UUw"
  "03_png\icons\icon_tasks.png" = "https://aka.doubaocdn.com/s/CmSGSIPnaq"
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
