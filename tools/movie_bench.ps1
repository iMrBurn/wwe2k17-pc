# Run the game for a while with the movie cadence log on, close it, and report how evenly the movies advanced.
# The intro movies play on their own at every start, so this needs nobody at the keyboard.
#
#   tools\movie_bench.ps1 [-Seconds 70] [-Exe wwe2k17_native] [-GameArgs "--svr_hooks_off=D3DDevice_Swap", ...]
#
# Compare runs of the SAME build and flags first to see the run-to-run noise, then change one flag at a time.
# Needs the machine to itself: it opens the game window and closes it again when the time is up.
param([int]$Seconds = 70, [string]$Exe = "wwe2k17_native", [string[]]$GameArgs = @())
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
if (Get-Process wwe2k17, wwe2k17_native, wwe2k17_d3d12, wwe2k17_d3d11 -ErrorAction SilentlyContinue) {
    throw "a game is already running: close it first"
}
$env:WWE2K17_EXE = $Exe
# The native exes log from their renderer, the Xenos one (no renderer of ours) from the game's SetTexture.
$logFlag = if ($Exe -eq "wwe2k17") { "--wwe2k17_log_movie_binds=true" } else { "--svr_log_texture_cadence=true" }
$line = & "$PSScriptRoot\run.ps1" $logFlag @GameArgs
if ($line -notmatch "pid (\d+) log (.+)$") { throw "could not read the launch line: $line" }
$gamePid = [int]$Matches[1]
$log = $Matches[2].Trim()
Start-Sleep -Seconds $Seconds
Stop-Process -Id $gamePid -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
"flags: $($GameArgs -join ' ')"
"log:   $log"
python -I "$PSScriptRoot\flip_cadence.py" $log
