# Run the attract demo (no input) for a fixed time and average the [perf] lines of the demo match.
#   tools\perf_bench.ps1 [-Exe wwe2k17_native] [-Seconds 160] [-From 100] [-Cores N] [-Probe] [extra game args]
# -Cores N pins the game to its first N logical processors (a small CPU, roughly).
# -Probe prints CPU per thread (tools\perf_probe.ps1) over 20 s of the match.
# The demo match starts about 80-90 s after launch on this machine; lines logged after -From s count.
# Plug the laptop in: on battery Windows hides the discrete GPU and throttles the CPU.
[CmdletBinding(PositionalBinding = $false)]
param([string]$Exe = "wwe2k17_native", [int]$Seconds = 160, [int]$From = 100, [int]$Cores = 0,
      [switch]$Probe,
      [Parameter(ValueFromRemainingArguments = $true)] $GameArgs)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
if (-not $GameArgs) { $GameArgs = @() }
Get-Process -Name $Exe -ErrorAction SilentlyContinue | Stop-Process
$env:WWE2K17_EXE = $Exe
$out = & "$PSScriptRoot\run.ps1" --mnk_mode=true @GameArgs
$log = ($out -split " log ")[1]
$start = Get-Date
if ($Cores -gt 0) {
    Start-Sleep -Seconds 1
    (Get-Process -Name $Exe).ProcessorAffinity = [IntPtr]((1L -shl $Cores) - 1)
}
if ($Probe) {
    Start-Sleep -Seconds ($From + 10)
    & "$PSScriptRoot\perf_probe.ps1" -Name $Exe -Seconds 20
}
$left = $Seconds - ((Get-Date) - $start).TotalSeconds
if ($left -gt 0) { Start-Sleep -Seconds $left }
Get-Process -Name $Exe -ErrorAction SilentlyContinue | Stop-Process
Start-Sleep -Seconds 2
$lines = Get-Content $log | Where-Object {
    $_ -match '^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)' -and ([datetime]$Matches[1] - $start).TotalSeconds -ge $From
}
function Avg($pattern) {
    $v = $lines | ForEach-Object { if ($_ -match $pattern) { [double]$Matches[1] } }
    if ($v) { "{0:N1}" -f ($v | Measure-Object -Average).Average } else { "-" }
}
$gpu = ($lines | Select-String "Vulkan GPU|D3D12 GPU|adapter" | Select-Object -First 1)
$dev = (Get-Content $log | Select-String "GPU caps: \S+ \S+ on (.+?) \(driver" | Select-Object -First 1)
[pscustomobject]@{
    log = Split-Path $log -Leaf; args = ($GameArgs -join " ")
    gpu = if ($dev) { $dev.Matches[0].Groups[1].Value.Trim() } else { "?" }
    work_ms = Avg 'work ([\d.]+) ms \(max'; interval_ms = Avg 'interval ([\d.]+) ms'
    hitches = ($lines | ForEach-Object { if ($_ -match 'hitches >25ms (\d+)') { [int]$Matches[1] } } | Measure-Object -Sum).Sum
    render_cpu_ms = Avg 'render thread ([\d.]+) ms \(work'; game_cpu_ms = Avg 'whole game ([\d.]+) ms'
    draws_ms = Avg 'draws ([\d.]+) ms'; constants_ms = Avg 'constants ([\d.]+) of it'
    textures_ms = Avg '\| textures ([\d.]+) ms \| geometry'; geometry_ms = Avg 'geometry ([\d.]+) ms \| present'
    guest_d3d_ms = Avg 'game D3D in hooks ([\d.]+) ms'; fps = Avg 'Guest pacing: .*\(([\d.]+) fps\)'
} | Format-List
