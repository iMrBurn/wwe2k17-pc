# Prepare the game's shaders for the D3D12 / D3D11 native builds (wwe2k17_d3d12.exe, wwe2k17_d3d11.exe).
#   tools\dump_shaders.ps1
# 1. starts wwe2k17_native.exe (Vulkan, built by tools\build.ps1) with --wwe2k17_dump_shaders: the game creates all its
#    shaders at boot (611 with Title Update 1.04) and each container is written to work\shaders\xsc; the game is closed
#    once no new one came for 15 s. (WWE '12 PC extracts them from its .pac files instead; 2K17's are XCTD-compressed.)
# 2. builds reblue-XenosRecomp's command-line tool (work\xenosrecomp-build) and converts them: generated\native\
#    shader_cache.cpp (DXIL + SPIR-V) and work\shaders\d3d11\hlsl (input of the DXBC conversion).
# Then run tools\build.ps1 again: the D3D12 / D3D11 exes are built when generated\native\shader_cache.cpp exists.
# Everything written here is game-derived and stays out of Git.
param([switch]$KeepContainers)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot "env.ps1")
$Exe = "$Root\out\build\win-amd64-release\wwe2k17_native.exe"
$Xsc = "$Root\work\shaders\xsc"
if (-not (Test-Path $Exe)) { throw "wwe2k17_native.exe missing: run tools\build.ps1 first" }

# 1. Dump
if (-not $KeepContainers -or -not (Get-ChildItem $Xsc -Filter *.xsc -ErrorAction SilentlyContinue)) {
    Remove-Item $Xsc -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $Xsc, "$Root\logs" | Out-Null
    # The shaders already converted for the Vulkan exe never reach the dump: start from an empty cache.
    Remove-Item "$Root\out\build\win-amd64-release\cache\native_shaders.bin" -ErrorAction SilentlyContinue
    $log = "$Root\logs\dump-shaders-$(Get-Date -Format yyyyMMdd-HHmmss).log"
    $argsList = @("--game_data_root=`"$Root\game`"", "--user_data_root=`"$Root\userdata`"",
        "--update_data_root=`"$Root\userdata\title_update`"", "--fullscreen=false", "--window_width=960",
        "--window_height=540", "--log_file=`"$log`"", "--wwe2k17_dump_shaders=`"$Xsc`"")
    Write-Host "== Starting the game to collect its shaders (about a minute)..."
    $p = Start-Process -FilePath $Exe -ArgumentList $argsList -WorkingDirectory $Root -PassThru
    $last = -1; $quiet = 0
    while (-not $p.HasExited -and $quiet -lt 15) {
        Start-Sleep 1
        $n = (Get-ChildItem $Xsc -Filter *.xsc -ErrorAction SilentlyContinue).Count
        if ($n -eq $last -and $n -gt 0) { $quiet++ } else { $quiet = 0 }
        $last = $n
    }
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
$count = (Get-ChildItem $Xsc -Filter *.xsc).Count
if ($count -eq 0) { throw "no shader collected (see logs\dump-shaders-*.log)" }
Write-Host "$count shader containers"

# 2. XenosRecomp
$Xr = "$Root\native-deps\reblue-XenosRecomp"
$XrBuild = "$Root\work\xenosrecomp-build"
$XrExe = "$XrBuild\XenosRecomp\XenosRecomp.exe"
if (-not (Test-Path $XrExe)) {
    Write-Host "== Building XenosRecomp"
    $env:CXXFLAGS = "-DREBLUE_RECOMP"  # the re:Blue changes of the patched XenosRecomp
    try { Invoke-Checked cmake -S $Xr -B $XrBuild -G Ninja -DCMAKE_BUILD_TYPE=Release -Wno-dev }
    finally { Remove-Item Env:CXXFLAGS -ErrorAction SilentlyContinue }
    Invoke-Checked cmake --build $XrBuild --target XenosRecomp
}
$Gen = "$Root\generated\native"
$Hlsl = "$Root\work\shaders\d3d11\hlsl"
Remove-Item $Hlsl -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Gen, $Hlsl | Out-Null
Write-Host "== Converting with XenosRecomp"
Invoke-Checked $XrExe $Xsc "$Gen\shader_cache.cpp" "$Xr\XenosRecomp\shader_common.h" $Hlsl
Write-Host "Shaders ready ($count). Run tools\build.ps1 to build wwe2k17_d3d12.exe and wwe2k17_d3d11.exe." -ForegroundColor Green
