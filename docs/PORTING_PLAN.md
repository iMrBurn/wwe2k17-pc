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
- [x] TU2 `pac/` (update:) and DLC (dlc_inbox, all 6 open).
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

### Graphics, memory, DLC, first match on screen (2026-11-12 evening)

- GPU: the prebuilt `rexgpu-xenos.dll` of v0.10.1 has the D3D12 backend only (no Vulkan cvars at all), so
  `gpu_backend = "vulkan"` cannot work; D3D12 it is. The run used the Intel UHD because the laptop was on
  battery (its RTX disappears from Windows then): keep it plugged in for tests.
- The ~458 "BaseHeap::Alloc failed to find contiguous range" lines all come in the same 0.1 s at boot, on one
  thread, never again: the engine probing for its largest free block. Harmless, left alone.
- FATAL 0x82371598 after ~1.5 min: a virtual-call thunk (lwz r12,0(r3) / lwz r11,N(r12) / mtctr / bctr)
  reached only through a computed address. `tools/scan_thunks.py` (from 2K16, import stub start passed as
  argument 4: 0x8334CD0C) adds 95 of them in `config/functions_thunks.toml`. Run it on a generation made
  without that file.
- DLC: copy the packages into `userdata\dlc_inbox\`; `Wwe2k17App::InstallDlcInbox` installs each one with the
  SDK's own `ContentManager::InstallContent` and deletes the copy. The game lists all 6 and opens them as
  `dlc0`..`dlc5`.
- FATAL "Dispatch thread: deferred completion threw 'Invalid UTF-8'" ~30 s after boot, only with DLC: the
  title calls XamContentCreateEx("dlc0" on its stack, &content_data, ..., overlapped); the SDK runs the call
  100 ms later and only then reads the root name, by which time the stack buffer is garbage. Midasm hook at
  0x82B6084C (`src/content_hooks.cpp`) copies root name + XCONTENT_DATA into a ring of guest heap slots and
  points r4/r5 at them. The content data itself was fine (logged hex).
- Result: 2+ minutes without a crash, the attract-mode demo match renders correctly (Kevin Owens vs Sami Zayn,
  referee, crowd), title screen shows version 1.02, then the save warning screen. A played match from the
  menus is still to test (stopped: WWE '12 was open at the same time and the key script sends global keys).

### Title Update 1.04 (2026-11-12, night)

- In game, the Future Stars and Hall of Fame DLC said "requires a more recent version of the game" on TU2
  (1.02). The Xbox 360 got a later update, 1.04 (prepares Hall of Fame Showcase): package
  `tu00000001_00000000`, 109,420,544 bytes, image 0.0.4.1. Aurora's cached TU list only knew TU1/TU2.
- Same procedure as for TU2: 3 tail-call targets (0x826E3558, 0x830CA7D0, 0x82E0E38C), 209 pointer-only
  functions, setjmp 0x831ED470 / longjmp 0x831ED150, 237 thunks (import stubs from 0x83380E2C).
  Hooks moved: ranged physical alloc 0x822516F0, XamContentCreateEx 0x82B8E734.
- Result: 2+ minutes, all 6 DLC opened, RAW arena entrance renders. DLC message not re-checked yet (needs
  the menus).

### Discrete GPU (2026-11-12, night)

- The user's first real test ran on the Intel UHD although the laptop was plugged in and the RTX 5070 visible:
  the SDK's `d3d12_adapter = -1` takes the first DXGI adapter. `Wwe2k17App::OnPreSetup` now sets
  `d3d12_adapter` to the adapter with the most dedicated video memory (unless the player set it), and
  `main.cpp` exports NvOptimusEnablement / AmdPowerXpressRequestHighPerformance as 2K15 does.
- The 4,278 "page count too big" + ~4,800 parent-heap lines at match load are the same allocator probes 2K15
  measured (4,281) and found harmless.
- Not ported yet from 2K15 (TU4 addresses there, each needs its 2K17 function found by shape): render-thread
  ring-wait sleep (CPU), SetFrameRate 60 fps, job hand-off order (freezes on 2-4 thread CPUs), NULL-pointer
  crash guards, camera cuts on finishers, "Xbox 360" -> "PC" texts, crash reports, launcher.

### Render wait, pacing, and the D3D12 hang (2026-11-12, night)

- The XDK D3D library sits at a constant offset from 2K15 TU4 here too: D3DDevice_Swap (only VdSwap caller)
  is sub_82BB2488 = 2K15 sub_82BADF20 + 0x4568. Predicted ring poll sub_82BB0C18 (has the db16cyc delays),
  ring/fence waits sub_82BB46E8 / sub_82BB40E0 (both call the poll): all are function starts.
  `src/render_hooks.cpp`: 2K15's ring-wait yield + a "Guest pacing" log line every 5 s from Swap.
- Before the yield, on D3D12: game 221 % of a core, one thread 87 %.
- With the RTX 5070: 60.0 fps steady in the attract demo, then
  `D3D12 device removed: HRESULT 0x887A0006 - DEVICE_HUNG` about 100 s after launch, when the demo match
  loads. Same as 2K15 (TDR in the first 3D scene, NVIDIA and AMD; readback_resolve=none avoids it but draws
  nothing). 2K15/2K16 use the Vulkan backend; the prebuilt v0.10.1 plugin has none, so the SDK has to be
  built from source with REXGLUE_USE_VULKAN=ON (2K16: tools/setup.ps1 / build-sdk10.ps1).

## 2026-11-13: SDK from source, Vulkan, native renderer

- SDK: ReXGlue v0.10.1 source (tag clone with submodules, `sdk/rexglue-src`), WWE 2K15 PC's null GPU patch
  (`patches/sdk/0001-null-gpu.patch`, applies cleanly), built with clang-cl, REXGLUE_USE_VULKAN=ON, D3D12 ON,
  Tracy off (`tools/build-sdk.ps1`, ~10 min on 20 jobs), installed to `sdk/rexglue-install`. `tools/build.ps1` uses
  it when present. The Xenos plugin now has Vulkan and a "null" backend.
- wwe2k17.exe: `Wwe2k17App::OnPreSetup` loads the Xenos plugin with `wwe2k17_gpu_backend` (default "vulkan", then
  "any").
- wwe2k17_native.exe: WWE 2K16 PC's native renderer (re:Blue + plume, Vulkan; `src/reblue`, `src/native`,
  `src/native_xr`, `cmake/native_renderer.cmake`, deps copied to `native-deps/`), Xenos plugin in "null" mode for the
  guest command stream. The D3D library of 2K17 is reordered relative to 2K16: only Present/Swap/Sync are 2K16 -
  0x80088; all 22 hooked functions were found by instruction similarity (`tools/match_functions.py`, every one 1.00)
  in `src/native/wwe2k17_d3d_names.h`. Only link gap: `GetFileIoStats` (a 2K15 SDK patch), stubbed.
- First native run (RTX 5070, Vulkan 1.4): 611 game shaders converted on first start (~20 s, stutters meanwhile),
  then the NXT attract demo match renders correctly at 60.0 fps, whole game ~92 % of one core (Xenos D3D12: 221 %).
  No device hang (the D3D12 emulation hung at that point).
- Next: D3D12 and D3D11 builds of the native renderer from WWE '12 PC (it has wwe12_d3d12 / wwe12_d3d11 exes; 2K16's
  copy is Vulkan only), a played match from the menus, the remaining 2K15 ports (60 fps SetFrameRate, job hand-off,
  crash guards, texts), launcher integration.
- wwe2k17.exe on the Vulkan backend (same session): 140 s, past the attract demo match load, 58-60 fps, no hang.

## 2026-11-13: D3D12 and D3D11 native builds (from WWE '12 PC)

- User's decision: like WWE '12 PC, the D3D12 / D3D11 exes carry the game's shaders converted at build time
  (CLAUDE.md hard rules: exception). The Vulkan exe still converts them on the player's PC.
- Shaders: 2K17's .pac files are XCTD-compressed (0x0FF512ED), so WWE '12's find_shaders.py finds nothing in them.
  The game creates all 611 shaders at boot instead: `--wwe2k17_dump_shaders=<folder>` in the Vulkan exe writes each
  container; `tools/dump_shaders.ps1` runs that, builds reblue-XenosRecomp's CLI (work/xenosrecomp-build) and writes
  generated/native/shader_cache.cpp + work/shaders/d3d11/hlsl. (A first try passed the folder through `powershell
  -File` and its spaces cut it: 611 files landed in Desktop\WWE, moved back and the folder removed.)
- Renderer: a full 3-way merge of WWE '12's re:Blue (HEAD f06b344, base 5601b0a = the copy 2K15/2K16 took) gave 9
  conflicts, but the glue (svr_resources.cpp) had 12 more where 2K16 and WWE '12 both reworked attire read-backs:
  dropped. Instead WWE '12's D3D commits only (f1b5864 d106d11 71bd3f3 f220536 9d96e9e de87fc5 eaa5863 9bcd772 b47cf9d
  c987d89 8d97c15) replayed file by file on src/reblue: one conflict (device.cpp, kept 2K16's GPU fallback loop and
  made its Vulkan feature check Vulkan-only). Added `Wwe12DxcAvailable` (src/native/wwe2k17_dxc.cpp).
- plume: WWE '12's copy (D3D11 backend from the SvR 2011 PC port, D3D12 fixes) with 2K15/2K16's Vulkan pipeline cache
  merged in (two conflicts, both sides had a pipeline cache: kept ours). The old copy stays as native-deps/plume.2k16.
- XenosRecomp: WWE '12's shader_common.h / shader_recompiler.cpp / dxc_compiler.cpp (SVR_DXIL_DEPTH_FLIP: z' = w - z
  in vertex shaders, D3D12 cannot take MinZ > MaxZ). Without them the D3D12 picture was a blurred empty arena (no
  wrestlers, no ring): exactly WWE '12's "keep the reversed depth" trap. Our older files kept as *.2k16.
- cmake/native_renderer.cmake: three variants (Vulkan run-time shaders; D3D12 pre-linked DXIL from wwe2k17_prelink =
  WWE '12's wwe12_prelink; D3D11 DXBC by tools/convert_shaders_d3d11.py with the Windows SDK's fxc). D3D exes are
  built when generated/native/shader_cache.cpp exists.
- Results on the RTX 5070 (attire demo matches, no keys sent): D3D12 12_2, Cesaro vs The Miz, 60.0 fps, 100 s; D3D11
  11_1, Balor vs Joe, 59.4-59.9 fps, 100 s; wwe2k17_native.exe (Vulkan) rechecked after the converter change.

## 2026-11-13: fixes from WWE 2K15 / 2K16 PC

- 60 FPS scenes (`src/framerate_hooks.cpp`, from 2K16 PC's framerate_hook.cpp + 2K15 PC's automatic fallback): 2K16's
  60->30 / 30->60 switches found by instruction similarity, both 1.00 (sub_82A94AA8, sub_82A94B48; 2K16 sub_82B23E80 /
  sub_82B23F20), timing block 0x837C4710 (lis/addi of both). Skipping the switch to 30 keeps the engine's time step
  at 60; a scene that cannot hold 60 (2 x 90 frames over 26 ms) goes back to 30 for the session. Cvars
  wwe2k17_60fps, wwe2k17_60fps_auto, wwe2k17_60fps_auto_ms. Not seen firing yet: the attract demos have no entrance.
- PC texts (`src/text_hooks.cpp`, 2K15 PC's text_hook.cpp): "incorrect header check" at 0x82076718 is used by
  sub_82B5D9B8 (zlib 1.2 inflate shape, hooked) and sub_82BEB2B0 (a library's zlib 1.1 inflate, left alone).
  First run: 27 "Xbox 360" mentions changed at load. Cvar wwe2k17_pc_texts.
- `REX_LOAD_U32` is gone in SDK v0.10.1: `LoadGuestU32(base, addr)` helpers.
- Not ported: 2K15's job hand-off order fix (no function with its shape in 2K17, even with any offsets: written
  differently or fixed; it only matters on 2-4 thread CPUs), 2K16's NULL memset guard (best match 0.86 only, no crash
  seen), 2K15's crash guards and finisher camera cuts (need 2K15's generated code, not on this PC), 2K16's shadow
  toggle and EXIT menu.

### Shadows option, EXIT menu, scripted input (2026-11-13)

- `src/shadows.cpp`: the ring mat plane shadow pass (sub_82ACF100, 2K16 TU3 sub_82B5F7F0, similarity 1.00 with the
  same 2,764 instructions) can be skipped with `wwe2k17_shadows = false` (weak graphics chips).
- `src/exit_menu.cpp`: WWE 2K16 PC's EXIT menu ported. String 35178 ("WWE SHOP", same ID in 2K17) becomes EXIT and
  swaps with OPTIONS so it is last; the Xbox Live entry (35243) and its offline notice (30041) are removed; EXIT opens
  the game's Yes/No dialog and closes the window. The 12 functions were matched by instruction similarity (1.00 for
  most; CheckMenuItemVisible 0.95 and ResolveMenuAction 0.79, arguments checked by hand); the sentinel descriptor is
  menu_obj + 11480 (2K16: 11472) and the DialogManager global 0x8379A62C, both read from the matched code. 2K16's
  SetupMenuSlot brightness override is left out (its slot layout differs, 0.85). Seen in the log: string 35178
  patched. Off with `wwe2k17_exit_menu = false`. The menu itself is not navigated yet.
- `src/test_input.cpp`, `src/script_input.h`: WWE 2K16 PC's scripted controller (WWE2K17_TEST_INPUT=1, commands in
  logs\test_input.txt), plus XInputGetKeystroke events, which 2K17's "Press Start" screen reads instead of the button
  state. `tools/send_keys.ps1` posts keys to the game window only (safe while another game is in use).

### Movies: judder and ghost images (user report 2026-11-13, not fixed yet)

Measured on the user's screen recording of the intro (`game/movies/menu/WWE17_OPEN.bk2`) and the matching log:
- Presentation is perfect: 60.0 fps, max gap 19.5 ms, 0 hitches, GPU wait 0 %.
- The renderer uploads ~100 texture mirrors a second during the movie, costing 135-147 ms of render-thread CPU each
  second; render-thread work is 11.8-13.7 ms per 16.7 ms frame. A Bink frame is 3 k_8 planes (Y/cR/cB, see
  native_texture_mirror.cpp), so ~100/s = ~33 movie frames a second: the decode rate is right.
- In the recording the picture changes only ~20 times a second, in an irregular rhythm (runs of 3 and 7 changed
  frames separated by repeats): that is the judder.
- Root cause found in `GetOrCreateNativeMirror`: a mirror whose source changed is **evicted and recreated** (the old
  GPU texture goes to the pending-destroy list). For a movie that is 90 GPU texture creations and destructions a
  second instead of 90 content updates into the same textures.
- Ruled out: the 60 FPS hook (never fired in that run), the sampled hash (`svr_texture_full_hash`, added as a
  diagnostic, shows the sampling is not missing plane updates), presentation pacing.
- The doubled face on some shots is at least partly the video's own double-exposure style: the Xenos emulation build
  shows the same look on the Cena shot. To confirm once the judder is fixed.

### Movies fixed: textures refilled instead of recreated (2026-11-13)

`GetOrCreateNativeMirror` used to evict a mirror whose bytes changed and build a new host texture (with its view
and a new bindless slot), retiring the old one. A Bink movie frame is 3 k_8 planes, so the intro ran that 90 times a
second. Now, on the per-frame rebuild path only (`name_va == 0`, from `SvrResolveTexture`), the existing mirror is
offered to the builder: when it describes exactly the same texture (format, size, mips, view dimension, and it owns a
descriptor), `BuildBCMirrorCore` uploads into it and returns it, so the map entry, the view and the slot are kept and
nothing is retired. Anything else evicts and rebuilds as before. The fetch swizzle is only re-applied to a refilled
mirror when the game changed it (applying it rebuilds the view), tracked in `g_native_swizzles`.

Measured on the intro (`game/movies/menu/WWE17_OPEN.bk2`), native Vulkan, RTX 5070:

| | before | after |
|---|---|---|
| host textures created per second | ~100 | ~36 |
| render-thread CPU for them | 135-147 ms/s | 11-13 ms/s |
| render-thread work per frame | 11.8-13.7 ms (of 16.7) | 0.9-2.3 ms |
| frames over 25 ms | 1-2 per 600 | 0 |

The user confirmed the movie is smooth now. Vulkan, D3D12 and D3D11 all boot and play the intro with no FATAL; the
picture changes 32-38 times a second in all three (the measuring script tops out around 33-40 captures a second, so
that is its ceiling, not the game's). `svr_texture_refill = false` restores the old behaviour if a texture is ever
seen holding stale contents.

### CPU: dynamic geometry (2026-11-13)

WWE '12 PC's geometry work since our base was never taken: commits 3b6048c (dynamic geometry copied without a full
hash, hashed every 8 frames to catch it settling, settled after 60 frames instead of 300, and the game's own D3D
bodies timed inside our hooks) and 20676db (`svr_geometry_sample_min`, and the [perf] line now says how much of the
hashing is in buffers under 32 KiB) replayed on `src/reblue` and `src/native`. One conflict, `svr_hook.h`: kept our
`svr_hooks_off` switch and added their WWE12_CPU_ZONE(kGuestD3D) around the game's D3D body.

Measured on the attract demo match (two wrestlers, referee, crowd), same scene before and after:

| | before | after |
|---|---|---|
| render-thread work per frame (RTX 5070) | 8.0-12.5 ms | 5.0-5.3 ms |
| frames over 25 ms, per 600 | 1-4 | 0 |
| geometry hashed per frame | 2.33 MB | 1.77 MB (1.74 of it in buffers under 32 KiB) |
| Intel iGPU + 2 CPU threads | 30.0 fps | 46-52 fps |

The 2-thread case is now render-thread bound (work 18-21 ms per frame). Hashing itself is not the cost left: 1.74 MB
at xxHash speed is about 0.2 ms, so the win came from the copies and compares 3b6048c removes, not from hashing less.
Lowering `svr_geometry_sample_min` would buy ~0.2 ms and risks missing a change: left at 32 KiB.
The picture was checked on the demo match and the menus: no geometry or texture corruption.

Not taken yet from WWE '12 PC (they touch `svr_resources.cpp`, where 2K16 and WWE '12 diverged and a full merge gave
12 conflicts): e73b139 (remembered pipelines built at startup, shader stutters), c615578 (read-backs of a streak
become one copy at frame end, match start on weak GPUs), d1084cc / f8a431d / 6b9cb26 (turbo loading and fast boot),
6bef73b (adaptive V-sync). The render-thread split in the [perf] line is still Android-only here (WWE '12 enabled it
on PC in 31e4c93): worth taking before optimising the 2-thread case further.
