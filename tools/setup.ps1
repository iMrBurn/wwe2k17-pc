# Download the prebuilt ReXGlue SDK release into sdk\ and prepare YOUR game files:
#   tools\setup.ps1 -Iso "D:\WWE 2K17.iso" -TitleUpdate "D:\tu00000001_00000000"
# Writes (all ignored by Git):
#   game\                    the whole disc, plus Title Update 1.04's default.xexp beside default.xex
#                            (the loader applies the update only when the patch sits there)
#   userdata\title_update\   the whole Title Update 1.04 (the game reads its pac files through update:\)
#   game_tu\                 default.xex + default.xexp, the codegen input
param([string]$Iso = "", [string]$TitleUpdate = "")
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
$Version = "0.10.1"
$SdkDir = Join-Path $Root "sdk\rexglue-sdk-$Version"
$Zip = Join-Path $Root "sdk\rexglue-sdk-$Version-win-amd64.zip"

if (-not (Test-Path "$SdkDir\win-amd64\bin\rexglue.exe")) {
    New-Item -ItemType Directory -Force (Split-Path $Zip) | Out-Null
    if (-not (Test-Path $Zip)) {
        $url = "https://github.com/rexglue/rexglue-sdk/releases/download/v$Version/rexglue-sdk-$Version-win-amd64.zip"
        Write-Host "Downloading $url"
        Invoke-WebRequest $url -OutFile $Zip
    }
    Expand-Archive $Zip -DestinationPath $SdkDir -Force
}
Write-Host "ReXGlue SDK ${Version}: $SdkDir\win-amd64"

$Game = Join-Path $Root "game"
$Update = Join-Path $Root "userdata\title_update"
$GameTu = Join-Path $Root "game_tu"
if ($Iso) {
    Write-Host "Extracting the disc (8.7 GB, a few minutes)..."
    python -I (Join-Path $PSScriptRoot "xiso_get.py") $Iso $Game --all
    if ($LASTEXITCODE -ne 0) { throw "could not read $Iso" }
}
if ($TitleUpdate) {
    python -I (Join-Path $PSScriptRoot "stfs_extract.py") $TitleUpdate $Update --all
    if ($LASTEXITCODE -ne 0) { throw "could not read $TitleUpdate" }
}
if ((Test-Path "$Game\default.xex") -and (Test-Path "$Update\default.xexp")) {
    New-Item -ItemType Directory -Force $GameTu | Out-Null
    Copy-Item "$Update\default.xexp" "$Game\default.xexp" -Force
    Copy-Item "$Game\default.xex" "$GameTu\default.xex" -Force
    Copy-Item "$Update\default.xexp" "$GameTu\default.xexp" -Force
}
foreach ($f in "default.xex", "default.xexp") {
    if (-not (Test-Path "$GameTu\$f")) { Write-Warning "game_tu\$f is missing: pass -Iso and -TitleUpdate" }
}
