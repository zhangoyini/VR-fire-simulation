$ErrorActionPreference = "Continue"
$base = "E:\unity\newvr_fire\VR-fire-simulation\fire_training_ui_v01"
$map = [ordered]@{
  "01_previews\lesson_large.png" = "https://aka.doubaocdn.com/s/KWyDupYiof"
  "01_previews\lesson_compact.png" = "https://aka.doubaocdn.com/s/sN5TJZq4iK"
  "01_previews\tutorial_controller.png" = "https://aka.doubaocdn.com/s/vIxwUIJ6bd"
  "01_previews\tasks_panel.png" = "https://aka.doubaocdn.com/s/KhS6VVOPXE"
  "01_previews\map_panel.png" = "https://aka.doubaocdn.com/s/cCUSa4OV48"
  "01_previews\gameplay_hud.png" = "https://aka.doubaocdn.com/s/a17JTA95fW"
  "01_previews\tooltip_notice.png" = "https://aka.doubaocdn.com/s/BPNTbNrlRg"
  "01_previews\warning_rescue.png" = "https://aka.doubaocdn.com/s/EptTC8hbSq"
  "03_png\panels\tooltip_card.png" = "https://aka.doubaocdn.com/s/CL9cA2er1t"
  "03_png\panels\map_frame.png" = "https://aka.doubaocdn.com/s/QO8CLK90TX"
  "03_png\panels\task_tab.png" = "https://aka.doubaocdn.com/s/aOZza5K0wp"
  "03_png\panels\map_tab.png" = "https://aka.doubaocdn.com/s/CkKNjS1hvT"
  "03_png\hud\hud_shell.png" = "https://aka.doubaocdn.com/s/oHhqlr8VOM"
  "03_png\hud\hud_track.png" = "https://aka.doubaocdn.com/s/iP9QZhD2bl"
  "03_png\hud\hud_fill.png" = "https://aka.doubaocdn.com/s/hnFPLvJyWo"
  "03_png\hud\reading_track.png" = "https://aka.doubaocdn.com/s/LUjUfjyT0Z"
  "03_png\hud\reading_fill.png" = "https://aka.doubaocdn.com/s/wnRtHGOhTH"
  "03_png\controller\controller_left_base.png" = "https://aka.doubaocdn.com/s/KGnUj2BAG2"
  "03_png\controller\controller_right_base.png" = "https://aka.doubaocdn.com/s/VmtMHAADGB"
  "03_png\controller\overlay_stick.png" = "https://aka.doubaocdn.com/s/XCCwtckMqw"
  "03_png\controller\overlay_trigger.png" = "https://aka.doubaocdn.com/s/SIVVTlRl96"
  "03_png\controller\overlay_grip.png" = "https://aka.doubaocdn.com/s/L0ljU56wUU"
  "03_png\controller\overlay_buttons.png" = "https://aka.doubaocdn.com/s/3HLu2yOEl6"
  "03_png\icons\icon_pending.png" = "https://aka.doubaocdn.com/s/3sPUZcNDU2"
  "03_png\icons\icon_active.png" = "https://aka.doubaocdn.com/s/UCfyvgJ1DI"
  "03_png\icons\icon_towel.png" = "https://aka.doubaocdn.com/s/pLM0ZEr09E"
  "03_png\icons\icon_water.png" = "https://aka.doubaocdn.com/s/evx0sdeWYs"
  "03_png\icons\icon_smoke.png" = "https://aka.doubaocdn.com/s/vgUJkARX1h"
  "03_png\icons\icon_stairs.png" = "https://aka.doubaocdn.com/s/XO7UFGCAIX"
  "03_png\icons\icon_elevator.png" = "https://aka.doubaocdn.com/s/1QU4FUjhSK"
  "03_png\icons\icon_no_elevator.png" = "https://aka.doubaocdn.com/s/I4EalHEszF"
  "03_png\icons\icon_no_jump.png" = "https://aka.doubaocdn.com/s/BKd6TnqUiV"
  "03_png\icons\icon_safe_exit.png" = "https://aka.doubaocdn.com/s/WFuj5dl0eY"
  "03_png\icons\icon_wheelchair.png" = "https://aka.doubaocdn.com/s/LxciPZimSz"
  "03_png\icons\icon_refuge.png" = "https://aka.doubaocdn.com/s/rtz3peylUJ"
  "03_png\icons\icon_player_arrow.png" = "https://aka.doubaocdn.com/s/Ed2ieXB9g2"
  "03_png\icons\icon_target.png" = "https://aka.doubaocdn.com/s/PWJy2mV6fz"
  "03_png\icons\icon_north.png" = "https://aka.doubaocdn.com/s/zQ4hGKkHu4"
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
    & "C:\Windows\System32\curl.exe" -L --http1.1 --retry 3 --retry-all-errors --connect-timeout 15 --max-time 300 -sS -o $out $map[$k]
    if (Test-PngComplete $out) { $ok = $true }
  }
  $size = if (Test-Path $out) { (Get-Item $out).Length } else { 0 }
  Write-Output ("{0}  ok={1}  bytes={2}" -f $k, $ok, $size)
}
Write-Output "DONE"
