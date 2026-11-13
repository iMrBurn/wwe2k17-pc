# Translate the game code (codegen) and compile wwe2k17.exe.
#   tools\build.ps1            codegen + release build
#   tools\build.ps1 -NoCodegen build only (after a source-only change)
param([switch]$NoCodegen, [string]$Preset = "win-amd64-release")
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot "env.ps1")
# The SDK built from source by tools\build-sdk.ps1 (Vulkan, null GPU) when present, else the prebuilt release.
$Sdk = Join-Path $Root "sdk\rexglue-install"
if (-not (Test-Path "$Sdk\bin\rexglue.exe")) { $Sdk = Join-Path $Root "sdk\rexglue-sdk-0.10.1\win-amd64" }
$rexglue = "$Sdk\bin\rexglue.exe"
if (-not (Test-Path $rexglue)) { throw "ReXGlue SDK missing: run tools\build-sdk.ps1 (or tools\setup.ps1) first" }
Write-Host "SDK: $Sdk"
if (-not (Test-Path "$Root\game_tu\default.xexp")) { throw "game_tu\ is not ready: run tools\setup.ps1 -Iso ... -TitleUpdate ..." }

Push-Location $Root
try {
    if (-not $NoCodegen) {
        New-Item -ItemType Directory -Force "$Root\logs" | Out-Null
        $log = Join-Path $Root ("logs\codegen-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
        Write-Host "Translating the game code (about 2 minutes)... log: $log"
        cmd /c "`"$rexglue`" codegen wwe2k17_manifest.toml > `"$log`" 2>&1"
        if ($LASTEXITCODE -ne 0) { Get-Content $log -Tail 20; throw "codegen failed, see $log" }
        if (-not (Select-String -Path $log -Pattern "XEX patch applied successfully" -Quiet)) {
            throw "Title Update 1.04 did not apply to your default.xex (see $log)"
        }
    }
    Invoke-Checked cmake --preset $Preset "-DCMAKE_PREFIX_PATH=$Sdk"
    Invoke-Checked cmake --build --preset $Preset
    Copy-Item "$Root\wwe2k17_runtime.toml" "$Root\out\build\$Preset\wwe2k17.toml" -Force
} finally { Pop-Location }
Write-Host "Done: out\build\$Preset\wwe2k17.exe" -ForegroundColor Green
