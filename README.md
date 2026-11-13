# WWE 2K17 PC

Unofficial native Windows x64 port of **WWE 2K17 (Xbox 360)**, made by statically recompiling the game's PowerPC code with [ReXGlue](https://github.com/rexglue/rexglue-sdk) v0.10.1. Last game of a series: [WWE '12 PC](https://github.com/iMrBurn/wwe12-pc), [WWE 2K15 PC](https://github.com/iMrBurn/wwe2k15-pc), [WWE 2K16 PC](https://github.com/iMrBurn/wwe2k16-pc).

**This repository contains project code and documentation only. No game data is included or linked.** You need your own copy of the Xbox 360 game and its Title Update 1.04. Please do not ask for game files in the issues.

> **Status: early work in progress.** The game boots and plays its demo matches at 60 fps with the native renderer in Vulkan (`wwe2k17_native.exe`), Direct3D 12 (`wwe2k17_d3d12.exe`) and Direct3D 11 (`wwe2k17_d3d11.exe`), and with the Xbox GPU emulation in Vulkan (`wwe2k17.exe`). A match from the menus is not tested yet. No release.

## Building

Needs Windows 10/11 x64, Visual Studio 2022 (C++ workload), LLVM/Clang 20+, CMake 3.25+, Ninja and Python 3.

0. Build the ReXGlue SDK v0.10.1 from source with this project's patches (Vulkan, null GPU; downloads the SDK source the first time):

   ```powershell
   tools\build-sdk.ps1
   ```

1. Get the SDK and prepare your game files (your own disc image and Title Update 1.04 package, `tu00000001_00000000`, 109 MB; the older 28 MB TU2 is not enough for the Future Stars and Hall of Fame DLC):

   ```powershell
   tools\setup.ps1 -Iso "D:\WWE 2K17.iso" -TitleUpdate "D:\tu00000001_00000000"
   ```

   This downloads the ReXGlue SDK v0.10.1 release into `sdk\`, extracts the disc to `game\` and the update to `userdata\title_update\`, and prepares `game_tu\` for the code generator (all ignored by Git).
2. Translate and compile:

   ```powershell
   tools\build.ps1
   ```

DLC: copy your own WWE 2K17 DLC packages into `userdata\dlc_inbox\`; the game installs them at the next start.

3. Optional, for `wwe2k17_d3d12.exe` / `wwe2k17_d3d11.exe`: collect and convert the game's shaders (starts the game once for about a minute), then build again:

   ```powershell
   tools\dump_shaders.ps1
   tools\build.ps1 -NoCodegen
   ```

4. Run in a window (log in `logs\`; `$env:WWE2K17_EXE` = `wwe2k17_native`, `wwe2k17_d3d12` or `wwe2k17_d3d11` for the native renderer):

   ```powershell
   tools\run.ps1
   ```

## Layout

- `wwe2k17_manifest.toml`: ReXGlue project (code generated from the Title Update 1.04 image).
- `config/`: function entry points and hooks the recompiler needs.
- `src/`: the host app.
- `tools/`: setup and build scripts, `xiso_get.py` (reads an Xbox 360 ISO), `stfs_extract.py` (reads a title update package).
- `docs/PORTING_PLAN.md`: work log, newest at the bottom.

## License

GPL-3.0 (see [LICENSE](LICENSE)). ReXGlue and its dependencies keep their own licenses. WWE 2K17 belongs to its owners; this project is not affiliated with them.
