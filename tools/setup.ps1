# Download the prebuilt ReXGlue SDK release into sdk\ and prepare game_tu\ from YOUR game files.
#   tools\setup.ps1 -Iso "D:\WWE 2K17.iso" -TitleUpdate "D:\tu00000001_00000000"
# game_tu\ gets your disc's default.xex and Title Update 2's default.xexp (the code is generated
# from the updated game only). Both parameters are optional once game_tu\ is filled.
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

$GameTu = Join-Path $Root "game_tu"
if ($Iso) {
    python -I (Join-Path $PSScriptRoot "xiso_get.py") $Iso $GameTu default.xex
    if ($LASTEXITCODE -ne 0) { throw "could not read $Iso" }
}
if ($TitleUpdate) {
    python -I (Join-Path $PSScriptRoot "stfs_extract.py") $TitleUpdate $GameTu default.xexp
    if ($LASTEXITCODE -ne 0) { throw "could not read $TitleUpdate" }
}
foreach ($f in "default.xex", "default.xexp") {
    if (-not (Test-Path "$GameTu\$f")) { Write-Warning "game_tu\$f is missing: pass -Iso and -TitleUpdate" }
}
