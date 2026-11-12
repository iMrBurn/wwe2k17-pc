# Start the game in a 1600x900 window with a log in logs\run-<time>.log. Extra arguments go to the game.
#   game\              your extracted disc (tools\setup.ps1 -Iso ... -Full)
#   userdata\title_update\  your extracted Title Update 2 (tools\setup.ps1 -TitleUpdate ...)
$Root = Split-Path $PSScriptRoot
$exe = "$Root\out\build\win-amd64-release\wwe2k17.exe"
if (-not (Test-Path $exe)) { throw "wwe2k17.exe missing: run tools\build.ps1 first" }
New-Item -ItemType Directory -Force "$Root\logs", "$Root\userdata" | Out-Null
$log = "$Root\logs\run-$(Get-Date -Format yyyyMMdd-HHmmss).log"
$argsList = @("--game_data_root=`"$Root\game`"","--user_data_root=`"$Root\userdata`"",
  "--update_data_root=`"$Root\userdata\title_update`"", "--cache_root=`"$Root\userdata\cache`"","--fullscreen=false", "--window_width=1600",
  "--window_height=900", "--log_file=`"$log`"") + $args
$p = Start-Process -FilePath $exe -ArgumentList $argsList -WorkingDirectory $Root -PassThru
"pid $($p.Id) log $log"
