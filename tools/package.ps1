# Build a folder (and optionally a zip) a player can unpack: the four exes, the runtime settings, the
# launcher, and the two seed caches that let the first start prepare the graphics work in advance.
#
#   tools\package.ps1 [-Out dist] [-Zip]
#
# NO GAME DATA. The player brings their own disc image and Title Update 1.04 and runs tools\setup.ps1
# themselves; nothing from game\, game_tu\ or userdata\title_update\ is copied here.
param([string]$Out = "dist", [switch]$Zip)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
$Build = Join-Path $Root "out\build\win-amd64-release"
if (-not (Test-Path (Join-Path $Build "wwe2k17.exe"))) { throw "build missing: run tools\build.ps1 first" }

$Version = (Get-Content (Join-Path $Root "VERSION") -Raw).Trim()
$Dist = Join-Path $Root "$Out\WWE2K17-PC"
if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item -ItemType Directory -Force $Dist | Out-Null

# The four renderers, their runtime settings and the libraries they load.
foreach ($f in "wwe2k17.exe", "wwe2k17_native.exe", "wwe2k17_d3d12.exe", "wwe2k17_d3d11.exe",
                "wwe2k17.toml", "rexruntime.dll", "rexgpu-xenos.dll") {
    Copy-Item (Join-Path $Build $f) $Dist
}
# plume\ holds the Vulkan/D3D backend data the native exes load next to them.
if (Test-Path (Join-Path $Build "plume")) { Copy-Item (Join-Path $Build "plume") $Dist -Recurse }

# The launcher, when the user has built one (its folder belongs to the launcher work, not here).
$Launcher = Join-Path $Root "WWE2K17 Launcher.exe"
if (Test-Path $Launcher) { Copy-Item $Launcher $Dist }

# Seed caches: the graphics pipelines and vertex declarations already met on this machine. Shipped
# next to the exe, the game builds them in the background from the first start, so a player does not
# get a freeze the first time a new screen or entrance is drawn. They hold render state and shader
# hashes, no game data.
$Seed = Join-Path $Root "userdata\cache\pipelines.bin"
if (Test-Path $Seed) {
    Copy-Item $Seed (Join-Path $Dist "pipelines_seed.w12p")
    "pipelines seed: {0:N0} bytes" -f (Get-Item $Seed).Length
} else {
    Write-Warning "No pipelines.bin: the package ships without pipelines_seed.w12p, so the first start prepares nothing."
}
$DeclSeed = Join-Path $Root "userdata\cache\declarations.bin"
if (Test-Path $DeclSeed) {
    Copy-Item $DeclSeed (Join-Path $Dist "declarations_seed.w12d")
    "declarations seed: {0:N0} bytes" -f (Get-Item $DeclSeed).Length
} else {
    Write-Warning "No declarations.bin: without it the seeded pipelines cannot be built at startup."
}

foreach ($f in "README.md", "LICENSE", "THIRD_PARTY.md") { Copy-Item (Join-Path $Root $f) $Dist }

# A last check: nothing from the game must ever end up in the package.
$banned = Get-ChildItem $Dist -Recurse -File |
    Where-Object { $_.Extension -in ".xex", ".xexp", ".pac", ".iso", ".bk2" -or $_.Name -like "default.*" }
if ($banned) { throw "game data in the package: $($banned.Name -join ', ')" }

"packaged $Version -> $Dist ({0:N1} MB)" -f ((Get-ChildItem $Dist -Recurse -File |
    Measure-Object Length -Sum).Sum / 1MB)

if ($Zip) {
    $zipPath = Join-Path $Root "$Out\WWE2K17-PC-$Version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $Dist -DestinationPath $zipPath
    "zip -> $zipPath ({0:N1} MB)" -f ((Get-Item $zipPath).Length / 1MB)
}
