# Where one thread of the running game spends its time: suspends it about every millisecond, reads its
# instruction pointer, and counts samples per function (symbols from the SDK's PDBs where they exist).
#   tools\thread_sampler.ps1 -Thread "GPU Commands" [-Name wwe2k17_native] [-Samples 4000] [-Top 25]
# -Thread is a thread name (as tools\perf_probe.ps1 prints it) or a thread id. -Stack N also counts the
# N return addresses found above the stack pointer (rough, no unwinding), to see the callers of a leaf.
param([Parameter(Mandatory = $true)][string]$Thread, [string]$Name = "wwe2k17_native",
      [int]$Samples = 4000, [int]$Top = 25, [int]$Stack = 0)
$ErrorActionPreference = "Stop"
$p = Get-Process -Name $Name | Select-Object -First 1

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class Sampler {
  [DllImport("kernel32.dll")] static extern IntPtr OpenThread(int a, bool i, int id);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int a, bool i, int id);
  [DllImport("kernel32.dll")] static extern int SuspendThread(IntPtr h);
  [DllImport("kernel32.dll")] static extern int ResumeThread(IntPtr h);
  [DllImport("kernel32.dll")] static extern bool GetThreadContext(IntPtr h, IntPtr ctx);
  [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, int n, out IntPtr r);
  [DllImport("kernel32.dll")] public static extern int GetThreadDescription(IntPtr h, out IntPtr d);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("dbghelp.dll", CharSet = CharSet.Unicode)] static extern bool SymInitializeW(IntPtr p, string path, bool invade);
  [DllImport("dbghelp.dll")] static extern int SymSetOptions(int o);
  [DllImport("dbghelp.dll", CharSet = CharSet.Unicode)] static extern bool SymFromAddrW(IntPtr p, ulong a, out ulong d, IntPtr info);

  public static string Name(int id) {
    IntPtr h = OpenThread(0x0040, false, id);
    if (h == IntPtr.Zero) return "";
    IntPtr d; string n = "";
    if (GetThreadDescription(h, out d) >= 0 && d != IntPtr.Zero) n = Marshal.PtrToStringUni(d);
    CloseHandle(h); return n;
  }

  // Returns instruction pointers (and, per sample, up to `stack` code-looking return addresses).
  public static List<ulong[]> Run(int pid, int tid, int samples, int stack, ulong lo, ulong hi) {
    var res = new List<ulong[]>();
    IntPtr th = OpenThread(0x0002 | 0x0008 | 0x0040, false, tid);  // SUSPEND_RESUME | GET_CONTEXT | QUERY
    IntPtr ph = OpenProcess(0x0010, false, pid);                     // VM_READ
    if (th == IntPtr.Zero) throw new Exception("OpenThread failed");
    IntPtr raw = Marshal.AllocHGlobal(1232 + 16);
    IntPtr ctx = new IntPtr((raw.ToInt64() + 15) & ~15L);
    byte[] buf = new byte[stack * 8 * 4 + 8];
    for (int i = 0; i < samples; i++) {
      Marshal.WriteInt32(ctx, 0x30, 0x100001);  // CONTEXT_AMD64 | CONTEXT_CONTROL
      if (SuspendThread(th) < 0) break;
      bool ok = GetThreadContext(th, ctx);
      ulong rip = 0, rsp = 0;
      if (ok) { rip = (ulong)Marshal.ReadInt64(ctx, 0xF8); rsp = (ulong)Marshal.ReadInt64(ctx, 0x98); }
      var frames = new List<ulong>(); frames.Add(rip);
      if (ok && stack > 0) {
        IntPtr r;
        if (ReadProcessMemory(ph, new IntPtr((long)rsp), buf, buf.Length, out r)) {
          for (int k = 0; k + 8 <= buf.Length && frames.Count <= stack; k += 8) {
            ulong v = BitConverter.ToUInt64(buf, k);
            if (v >= lo && v < hi) frames.Add(v);
          }
        }
      }
      ResumeThread(th);
      if (ok) res.Add(frames.ToArray());
      System.Threading.Thread.Sleep(1);
    }
    Marshal.FreeHGlobal(raw); CloseHandle(th); CloseHandle(ph);
    return res;
  }

  static IntPtr s_proc = IntPtr.Zero;
  public static void InitSymbols(int pid, string path) {
    s_proc = OpenProcess(0x0400 | 0x0010, false, pid);
    SymSetOptions(0x2 | 0x4 | 0x10);  // UNDNAME | DEFERRED_LOADS | LOAD_LINES
    SymInitializeW(s_proc, path, true);
  }
  public static string Symbol(ulong a) {
    IntPtr info = Marshal.AllocHGlobal(88 + 2 * 512);
    for (int i = 0; i < 88 + 2 * 512; i++) Marshal.WriteByte(info, i, 0);
    Marshal.WriteInt32(info, 0, 88);
    Marshal.WriteInt32(info, 80, 511);
    ulong disp; string s = null;
    if (SymFromAddrW(s_proc, a, out disp, info)) s = Marshal.PtrToStringUni(new IntPtr(info.ToInt64() + 84));
    Marshal.FreeHGlobal(info); return s;
  }
}
'@

$tid = 0
if ($Thread -match '^\d+$') { $tid = [int]$Thread } else {
    foreach ($t in $p.Threads) { if ([Sampler]::Name($t.Id) -like "*$Thread*") { $tid = $t.Id; break } }
}
if (-not $tid) { throw "no thread named '$Thread' in $Name" }
$mods = $p.Modules | ForEach-Object { [pscustomobject]@{ N = $_.ModuleName; B = [uint64]$_.BaseAddress.ToInt64(); E = [uint64]$_.BaseAddress.ToInt64() + [uint64]$_.ModuleMemorySize } }
$lo = ($mods | Measure-Object B -Minimum).Minimum; $hi = ($mods | Measure-Object E -Maximum).Maximum
$sdk = Join-Path (Split-Path $PSScriptRoot) "sdk\rexglue-src\out\win-amd64\Release"
[Sampler]::InitSymbols($p.Id, "$sdk;$(Split-Path $p.Path)")

$res = [Sampler]::Run($p.Id, $tid, $Samples, $Stack, [uint64]$lo, [uint64]$hi)
$cache = @{}
function Label([uint64]$a) {
    if ($cache.ContainsKey($a)) { return $cache[$a] }
    $m = $mods | Where-Object { $a -ge $_.B -and $a -lt $_.E } | Select-Object -First 1
    $s = [Sampler]::Symbol($a)
    $l = if ($s) { "$($m.N)!$s" } elseif ($m) { "{0}+0x{1:X}" -f $m.N, ($a - $m.B) } else { "?" }
    $cache[$a] = $l; $l
}
$self = @{}; $incl = @{}
foreach ($f in $res) {
    $l = Label $f[0]; $self[$l] = 1 + [int]$self[$l]
    $seen = @{}
    foreach ($a in $f) { $x = Label $a; if (-not $seen[$x]) { $seen[$x] = 1; $incl[$x] = 1 + [int]$incl[$x] } }
}
$n = $res.Count
"thread $tid '$Thread': $n samples"
"-- where the thread is (self)"
$self.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top |
    ForEach-Object { "{0,6:N1}%  {1}" -f (100.0 * $_.Value / $n), $_.Key }
if ($Stack -gt 0) {
    "-- on the stack (rough inclusive)"
    $incl.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top |
        ForEach-Object { "{0,6:N1}%  {1}" -f (100.0 * $_.Value / $n), $_.Key }
}
