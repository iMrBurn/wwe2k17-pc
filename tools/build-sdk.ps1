# Build the ReXGlue SDK v0.10.1 from source with this project's patches (patches\sdk\*.patch) and the Vulkan backend,
# and install it into sdk\rexglue-install. The prebuilt v0.10.1 release has no Vulkan in its Xenos plugin (D3D12 hangs
# WWE 2K15/2K16/2K17 when the first 3D scene loads) and no "null" GPU backend (needed by the native renderer).
#   tools\build-sdk.ps1          clone (first time), patch, build, install
#   tools\build-sdk.ps1 -Jobs 8  limit the parallel compile jobs
# Downloads the SDK source from github.com/rexglue/rexglue-sdk (tag v0.10.1, with submodules, ~650 MB) the first time.
param([int]$Jobs = 0)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot "env.ps1")
$Tag = "v0.10.1"
$Src = Join-Path $Root "sdk\rexglue-src"
$Install = Join-Path $Root "sdk\rexglue-install"

# Without Windows developer mode git checks symlinks out as small text files holding the target path. Replace each one
# with a copy of its target (from WWE 2K16 PC's tools/setup.ps1).
function Repair-GitSymlinks([string]$Repo) {
    $repos = @($Repo) + @(git -C $Repo submodule foreach --recursive --quiet 'echo $displaypath' |
        Where-Object { $_ -notlike "*moltenvk*" } | ForEach-Object { Join-Path $Repo $_ })
    foreach ($pass in 1..3) {  # a link can point at another link
        foreach ($r in $repos) {
            foreach ($entry in (git -C $r ls-files -s | Where-Object { $_ -like "120000*" })) {
                $rel = ($entry -split "`t", 2)[1]
                $link = Join-Path $r $rel
                $dest = ((git -C $r cat-file -p ($entry -split " ")[1]) -join "`n").Trim()
                if (-not (Test-Path $link -PathType Leaf) -or (Get-Content $link -Raw).Trim() -ne $dest) { continue }
                $target = Join-Path (Split-Path $link) $dest
                if (-not (Test-Path $target)) {
                    if ($pass -eq 3) { Write-Warning "symlink target missing: $r/$rel" }
                    continue
                }
                Remove-Item $link -Force
                Copy-Item $target $link -Recurse -Force
                git -C $r update-index --assume-unchanged $rel
            }
        }
    }
}

if (-not (Test-Path "$Src\.git")) {
    Invoke-Checked git -c core.autocrlf=false clone --branch $Tag --depth 1 --recurse-submodules --shallow-submodules `
        https://github.com/rexglue/rexglue-sdk.git $Src
}
# Apply the patches once (a local commit marks them as applied).
if (-not (git -C $Src log --oneline -1 | Select-String "WWE 2K17 PC: SDK patches" -Quiet)) {
    foreach ($patch in (Get-ChildItem "$Root\patches\sdk\*.patch" | Sort-Object Name)) {
        cmd /c "git -C `"$Src`" apply --check --whitespace=nowarn `"$($patch.FullName)`" >nul 2>nul"
        if ($LASTEXITCODE -eq 0) { Invoke-Checked git -C $Src apply --whitespace=nowarn $patch.FullName }
        else { Write-Host "already applied or not applicable: $($patch.Name)" }
    }
    Invoke-Checked git -C $Src add -A
    Invoke-Checked git -C $Src -c user.name="WWE 2K17 PC" -c user.email="wwe2k17@local" commit -q -m "WWE 2K17 PC: SDK patches"
}
Repair-GitSymlinks $Src

$Build = Join-Path $Src "out\build\win-amd64"
Push-Location $Src
try {
    if (-not (Test-Path "$Build\CMakeCache.txt")) {
        Invoke-Checked cmake --preset win-amd64 -DCMAKE_C_COMPILER=clang-cl -DCMAKE_CXX_COMPILER=clang-cl `
            -DREXGLUE_USE_VULKAN=ON -DREXGLUE_USE_D3D12=ON -DREXGLUE_ENABLE_TRACY=OFF -DREXGLUE_BUILD_TESTS=OFF `
            "-DCMAKE_INSTALL_PREFIX=$Install" `
            "-DCMAKE_C_FLAGS=/clang:-march=x86-64-v2 /clang:-Wno-everything /Z7" `
            "-DCMAKE_CXX_FLAGS=/EHsc /Zc:char8_t- /clang:-march=x86-64-v2 /clang:-Wno-everything /Z7" `
            "-DCMAKE_SHARED_LINKER_FLAGS=/machine:x64 /DEBUG /OPT:REF /OPT:ICF"
    }
    $jobArgs = if ($Jobs -gt 0) { @("-j", $Jobs) } else { @() }
    Invoke-Checked cmake --build $Build --config Release @jobArgs
    Invoke-Checked cmake --install $Build --config Release
} finally { Pop-Location }
Write-Host "ReXGlue SDK $Tag (patched, Vulkan) installed to $Install" -ForegroundColor Green
