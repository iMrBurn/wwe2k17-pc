# Where a running game's time goes, from outside: CPU per thread and the GPU's 3D engine load.
#   tools\perf_probe.ps1 [-Name wwe2k17_native] [-Seconds 10] [-Repeat 1]
# Thread ids match the [tNNNN] tags of the game's log, so a busy thread can be named from the log.
# GPU load is Windows' own "GPU Engine" counter for the process (any vendor): near 100% = GPU bound.
param([string]$Name = "wwe2k17_native", [int]$Seconds = 10, [int]$Repeat = 1)
$ErrorActionPreference = "Stop"
$p = Get-Process -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw "$Name is not running" }
$cores = [Environment]::ProcessorCount

function Snapshot($proc) {
    $proc.Refresh()
    $t = @{}
    foreach ($th in $proc.Threads) { try { $t[$th.Id] = $th.TotalProcessorTime.TotalMilliseconds } catch {} }
    @{ When = Get-Date; Total = $proc.TotalProcessorTime.TotalMilliseconds; Threads = $t }
}

function GpuLoad($procId, $secs) {
    # One sample per second; the busiest engine type of this process ("engtype_3D" usually).
    $path = "\GPU Engine(pid_${procId}_*)\Utilization Percentage"
    try {
        $samples = Get-Counter -Counter $path -SampleInterval 1 -MaxSamples $secs -ErrorAction Stop
    } catch { return "n/a" }
    $byEngine = @{}
    foreach ($s in $samples) {
        $sum = @{}
        foreach ($c in $s.CounterSamples) {
            if ($c.InstanceName -match 'engtype_(\w+)') {
                $e = $Matches[1]; $sum[$e] = [double]$sum[$e] + $c.CookedValue
            }
        }
        foreach ($e in $sum.Keys) { $byEngine[$e] = @($byEngine[$e]) + $sum[$e] }
    }
    ($byEngine.GetEnumerator() | Where-Object { ($_.Value | Measure-Object -Average).Average -ge 0.5 } |
        ForEach-Object { "{0} {1:N0}%" -f $_.Key, ($_.Value | Measure-Object -Average).Average }) -join ", "
}

for ($r = 0; $r -lt $Repeat; $r++) {
    $a = Snapshot $p
    $gpu = GpuLoad $p.Id $Seconds
    $b = Snapshot $p
    $wall = ($b.When - $a.When).TotalMilliseconds
    $rows = foreach ($id in $b.Threads.Keys) {
        if ($a.Threads.ContainsKey($id)) {
            [pscustomobject]@{ Thread = $id; Pct = 100.0 * ($b.Threads[$id] - $a.Threads[$id]) / $wall }
        }
    }
    $proc = 100.0 * ($b.Total - $a.Total) / $wall
    "{0:HH:mm:ss} process {1:N0}% of one core ({2} cores) | GPU {3}" -f $b.When, $proc, $cores, $gpu
    $rows | Sort-Object Pct -Descending | Select-Object -First 8 |
        ForEach-Object { "  t{0,-6} {1,5:N1}%" -f $_.Thread, $_.Pct }
}
