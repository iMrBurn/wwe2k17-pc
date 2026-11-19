# Build WWE2K17 Launcher.exe (framework-dependent single file, needs the .NET 8 Desktop Runtime) into the repo root,
# where tools\package.ps1 takes it. Its version comes from the VERSION file (launcher\WWE2K17Launcher.csproj): run
# this after every change of VERSION, the launcher's updater compares that version with the newest GitHub release.
#
#   tools\build-launcher.ps1
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot
$Out = Join-Path $Root "launcher\dist"
dotnet publish (Join-Path $Root "launcher\WWE2K17Launcher.csproj") -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o $Out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
Copy-Item (Join-Path $Out "WWE2K17Launcher.exe") (Join-Path $Root "WWE2K17 Launcher.exe") -Force
$v = (Get-Item (Join-Path $Root "WWE2K17 Launcher.exe")).VersionInfo.ProductVersion
"WWE2K17 Launcher.exe $v"
