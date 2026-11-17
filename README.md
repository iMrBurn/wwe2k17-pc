# WWE 2K17 PC

Unofficial native Windows x64 port of **WWE 2K17 (Xbox 360)**, made by statically recompiling the game's PowerPC code with [ReXGlue](https://github.com/rexglue/rexglue-sdk) v0.10.1. Last game of a series: [WWE '12 PC](https://github.com/iMrBurn/wwe12-pc), [WWE 2K15 PC](https://github.com/iMrBurn/wwe2k15-pc), [WWE 2K16 PC](https://github.com/iMrBurn/wwe2k16-pc).

**This repository contains project code and documentation only. No game data is included or linked.** You need your own copy of the Xbox 360 game and its Title Update 1.04. Please do not ask for game files in the issues.

> **Status: 0.1 beta, and the only release planned for 2026.** It is a beta: expect bugs, and please report what you find in the issues (without game files). Known problems are listed below.
>
> **0.1.1 beta.** The game boots to its menus and plays full matches at 60 fps with the native renderer in Vulkan (`wwe2k17_native.exe`), Direct3D 12 (`wwe2k17_d3d12.exe`) and Direct3D 11 (`wwe2k17_d3d11.exe`), and with the Xbox GPU emulation in Vulkan (`wwe2k17.exe`). `WWE2K17 Launcher.exe` installs the game from your disc image and picks the renderer (in English, French, German, Spanish or Italian, following the game language), and proposes lower settings on a Steam Deck or a small PC (untested on a real Deck).

## What works

- Boots to the menus and plays a match from them, entrances included, at 59-60 fps.
- 60 fps in the scenes the Xbox 360 locked to 30 (entrances, victory scenes), with an automatic fall back to 30 when the machine cannot hold it.
- Xbox 360 button prompts rewritten for a PC keyboard and controller.
- Movies and transition videos play at full rate.
- The EXIT menu quits the game.
- DLC packages install from `userdata\dlc_inbox\`.
- Saves are written and read back; the creation suite (created attires and superstars) and Universe start and play. (MyCAREER does not exist in the Xbox 360 version of the game.)
- Titantrons and the arena's stage movies show their own pictures during entrances.

## What it needs

Measured on the attract demo match (docs/PORTING_PLAN.md, 2026-11-17):

- **CPU:** 4 threads hold 60 fps in a match (a 2-thread CPU gets 55-56 fps). The whole game uses about one core.
- **GPU:** anything with Vulkan 1.2, Direct3D 12 or Direct3D 11 (feature level 11_0). An Intel integrated chip of
  the Raptor Lake generation needs 6 ms of its 16.7 ms frame at the automatic 720p, so older integrated chips hold
  60 too. Integrated GPUs and discrete GPUs under 3.5 GB render at 720p; larger GPUs at a multiple of 720p matching
  your screen (`svr_render_scale` to choose, 540 for the weakest chips).

## Known issues

- **Movies in the native renderers.** A movie's luma plane reaches the screen a frame after its chroma planes, which showed as small colour glitches at the edges of moving things; the native renderers now show a movie's planes one frame late so they match (`svr_movie_plane_delay`, on by default; checked by eye on an arena preview, "cleaner"). The frames the game itself picks for a 30 Hz movie at 60 fps stay slightly uneven in every build, `wwe2k17.exe` included. Written up in [docs/PORTING_PLAN.md](docs/PORTING_PLAN.md).
- **First entrance after a fresh install stutters.** The graphics driver compiles the entrance shaders the first time they are drawn, which can freeze the picture for over a second. They are remembered in `userdata\cache\pipelines.bin` and built at startup from then on, so it happens once.
- Loading times are not measured yet: the SDK's file I/O statistics are stubbed, so every `[load]` line in the log reads 0.0 MB.
- **Created superstars saved on an Intel integrated GPU with 0.1.0 or earlier keep a black silhouette** on the superstar
  select screen. The game takes that picture once, when you save the superstar, and keeps it in the save: open
  the superstar in the creation suite, change nothing and save (ACCEPT) once to get a correct one. Fixed in the
  native renderers (`svr_clear_new_depth`, on by default; checked by eye on an Intel Raptor Lake iGPU); written up
  in [docs/PORTING_PLAN.md](docs/PORTING_PLAN.md).
- **Some Titantron videos are corrupted** (coloured stripes and blocks on the stage screens, seen with Mojo Rawley and Roman Reigns; Brock Lesnar's is fine). It is the same in all four renderers, so it comes from the game's own video decoding, not from the renderer. Not fixed yet.
- Not tested: AMD GPUs.

## Linux and Steam Deck

Not tested: there is no native Linux build, and nobody here has a Linux machine. The Windows build may run through Proton; the steps to try and what to send back are in [docs/LINUX_STEAM_DECK.md](docs/LINUX_STEAM_DECK.md).

## Options

Pass them on the command line, for example `tools\run.ps1 --wwe2k17_60fps=false`.

| Option | Default | What it does |
|---|---|---|
| `wwe2k17_60fps` | `true` | 60 fps in the scenes the Xbox 360 locked to 30 |
| `wwe2k17_60fps_auto` | `true` | falls back to 30 in those scenes when the machine cannot hold 60 |
| `wwe2k17_pc_texts` | `true` | PC button prompts instead of the Xbox 360 ones |
| `wwe2k17_exit_menu` | `true` | adds a quit entry to the in-game menu |
| `wwe2k17_shadows` | `true` | character shadows |
| `wwe2k17_universe_stories_unlocked` | `true` | Universe: the Story Collection reads as fully played, clues revealed (nothing is written to your save) |

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

5. Optional, to make a folder (and a zip) someone else can unpack:

   ```powershell
   tools\package.ps1 -Zip
   ```

   It copies the four renderers, the launcher and the two seed caches built on your machine
   (`pipelines_seed.w12p`, `declarations_seed.w12d`), so the graphics work is prepared in the
   background from the first start instead of freezing the picture the first time a screen is drawn.
   It never copies game data, and refuses to finish if any ended up in the folder.

## Layout

- `wwe2k17_manifest.toml`: ReXGlue project (code generated from the Title Update 1.04 image).
- `config/`: function entry points and hooks the recompiler needs.
- `src/`: the host app.
- `tools/`: setup and build scripts, `xiso_get.py` (reads an Xbox 360 ISO), `stfs_extract.py` (reads a title update package).
- `docs/PORTING_PLAN.md`: work log, newest at the bottom.

## License

GPL-3.0 (see [LICENSE](LICENSE)). ReXGlue and its dependencies keep their own licenses. WWE 2K17 belongs to its owners; this project is not affiliated with them.
