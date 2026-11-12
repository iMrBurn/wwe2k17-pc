# WWE 2K17 PC

Unofficial native Windows x64 port of **WWE 2K17 (Xbox 360)**, made by statically recompiling the game's PowerPC code with [ReXGlue](https://github.com/rexglue/rexglue-sdk) v0.10.1. Last game of a series: [WWE '12 PC](https://github.com/iMrBurn/wwe12-pc), [WWE 2K15 PC](https://github.com/iMrBurn/wwe2k15-pc), [WWE 2K16 PC](https://github.com/iMrBurn/wwe2k16-pc).

**This repository contains project code and documentation only. No game data is included or linked.** You need your own copy of the Xbox 360 game and its Title Update 2. Please do not ask for game files in the issues.

> **Status: early work in progress.** The game code translates (codegen) from the updated game; nothing is playable yet. No release.

## Building

Needs Windows 10/11 x64, Visual Studio 2022 (C++ workload), LLVM/Clang 20+, CMake 3.25+, Ninja and Python 3.

1. Get the SDK and prepare your game files (your own disc image and Title Update 2 package, `tu00000001_00000000`):

   ```powershell
   tools\setup.ps1 -Iso "D:\WWE 2K17.iso" -TitleUpdate "D:\tu00000001_00000000"
   ```

   This downloads the ReXGlue SDK v0.10.1 release into `sdk\` and writes `game_tu\default.xex` + `game_tu\default.xexp` (ignored by Git).
2. Translate and compile:

   ```powershell
   tools\build.ps1
   ```

## Layout

- `wwe2k17_manifest.toml`: ReXGlue project (code generated from the Title Update 2 image).
- `config/`: function entry points and hooks the recompiler needs.
- `src/`: the host app.
- `tools/`: setup and build scripts, `xiso_get.py` (reads an Xbox 360 ISO), `stfs_extract.py` (reads a title update package).
- `docs/PORTING_PLAN.md`: work log, newest at the bottom.

## License

GPL-3.0 (see [LICENSE](LICENSE)). ReXGlue and its dependencies keep their own licenses. WWE 2K17 belongs to its owners; this project is not affiliated with them.
