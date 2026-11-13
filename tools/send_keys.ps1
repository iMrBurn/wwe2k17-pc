# Send key presses to the WWE 2K17 window only (PostMessage WM_KEYDOWN / WM_KEYUP to its window), not to whatever
# window has the focus: safe while other programs (another game, a terminal) are in use. The game must run with
# --mnk_mode=true. Keys are .NET Keys names: Return = Start/A, Escape = B, Up/Down/Left/Right = D-pad.
#   tools\send_keys.ps1 -Keys Return,Down,Down,Return [-HoldMs 120] [-GapMs 400]
param([string[]]$Keys = @(), [int]$HoldMs = 120, [int]$GapMs = 400,
      [string[]]$Process = @("wwe2k17", "wwe2k17_native", "wwe2k17_d3d12", "wwe2k17_d3d11"))
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class KeyPost {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@
$p = Get-Process $Process -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
if (-not $p) { "game window not found"; exit 1 }
$h = $p.MainWindowHandle
foreach ($k in $Keys) {
    $vk = [uint32][System.Windows.Forms.Keys]$k
    $scan = [KeyPost]::MapVirtualKey($vk, 0)
    $down = [IntPtr](1 -bor ($scan -shl 16))
    $up = [IntPtr]([int64](1 -bor ($scan -shl 16) -bor (1 -shl 30)) -bor ([int64]1 -shl 31))
    [void][KeyPost]::PostMessage($h, 0x0100, [IntPtr]$vk, $down)   # WM_KEYDOWN
    Start-Sleep -Milliseconds $HoldMs
    [void][KeyPost]::PostMessage($h, 0x0101, [IntPtr]$vk, $up)     # WM_KEYUP
    Start-Sleep -Milliseconds $GapMs
}
"sent $($Keys.Count) key(s) to $($p.ProcessName) ($($p.Id))"
