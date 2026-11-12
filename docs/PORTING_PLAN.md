# WWE 2K17 PC: porting plan and work log

Goal: same result as WWE 2K15 PC / WWE 2K16 PC (game + Title Update 2 + DLC, 60 fps, launcher, native renderer),
on the stock ReXGlue v0.10.1 release.

## Game facts

- Title ID 545408BA, media ID 4F44F525, disc v0.0.0.1 (XGD2, XDVDFS partition at 0x2080000).
- Title Update 2: package `tu00000001_00000000` (28,569,600 bytes), patches the image to v0.0.2.1. Besides
  `default.xexp` it carries updated `pac/` files: the runtime needs them as an overlay on the disc files.
- DLC: content type 00000002, six packages.

## Open work

- [x] Project created with `rexglue init`, codegen from the TU2 image passes.
- [x] First build of `wwe2k17.exe`.
- [x] Boot to the menus.
- [x] setjmp / longjmp addresses.
- [ ] TU2 `pac/` overlay and DLC mounting.
- [ ] Port from 2K15 / 2K16: launcher, crash reports, framerate hook, keyboard, native renderer.

## 2026-11-12

- ReXGlue v0.10.1 (prebuilt Windows release). `tools/xiso_get.py` pulls `default.xex` out of the ISO,
  `tools/stfs_extract.py` pulls `default.xexp` out of the TU2 package.
- First codegen: "XEX patch applied successfully" (0.0.0.1 -> 0.0.2.1), then 3 UnresolvedCall
  targets reached only by `b` tail calls (0x82DE01CC, 0x826CC0E0, 0x8309C808), added to
  `config/functions.toml`. Second codegen: 591 files written in 97 s.
- First build (Clang 23.1.3, `win-amd64-release`, 300 steps): links cleanly, `wwe2k17.exe` 104 MB.
  `rexglue init` leaves out the GPU plugin: `rexglue_setup_target(wwe2k17 GPU_PLUGINS xenos)`, as in
  2K15, so `rexgpu-xenos.dll` lands next to the exe. The SDK now lives in the repo's ignored `sdk/`.

### First boot (2026-11-12, stock SDK, Intel UHD, D3D12)

The game reaches its menus (One on One match type list). What it took, in order:
- `--game_data_root=<folder>` (not a positional argument) and the whole disc extracted
  (`tools/xiso_get.py --all`). Title Update 2's `default.xexp` must sit beside `default.xex` in the game
  folder: the loader applies the update only from there ("XEX patch applied successfully ... 0.0.2.1").
  The rest of TU2 (pac files) is mounted as `update:` from `userdata/title_update` (`stfs_extract.py --all`).
- Missed entry points: `tools/find_functions.py` (from WWE 2K16 PC) on the decrypted TU2 image
  (`XexTool -p default.xexp -e u -c u`), then `tools/make_fptrs.py`: 206 pointer targets in
  `config/functions_fptrs.toml` (code-built addresses 0x100 bytes or less after their reference are
  dropped as in-function labels), setjmp 0x831BA200 / longjmp 0x831B9EE0 in `config/setjmp.toml`.
  The first FATAL was 0x83278B38, a static initialiser only reached through a pointer.
- Ranged physical allocations (Yuke's engine, 2K15/2K16 patched the SDK): `[[midasm_hook]]` at
  0x82241770 shifts [min, max] up by 0x1000 (`src/memory_hooks.cpp`). The ~458 "BaseHeap::Alloc failed
  to find contiguous range" lines remain after it: they look like the game probing free memory, not a
  blocker. To check later.
- `cache:` drive: not in the stock SDK, mounted from `userdata/xbox_cache` in `OnPostSetup` (as 2K16).
- `gpu_plugin = "xenos"` in `wwe2k17.toml` (copied from `wwe2k17_runtime.toml`): without it the stock SDK
  runs with no GPU and ignores the game's ring buffer. `gpu_backend = "vulkan"` was ignored on this run
  (log shows Direct3D 12 on the Intel UHD): to check.
- `NtQueryInformationFile(XFileXctdCompressionInformation) unimplemented` is expected; 2K15 found that
  answering "not compressed" hangs the first menu load. Leave it.
