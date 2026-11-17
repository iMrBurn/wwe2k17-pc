# WWE 2K17 PC on Linux / Steam Deck (Proton): guide and status

**Status: not tested on Linux.** Nobody on the project has a Linux machine, and the same is true of the sister
projects (WWE 2K15 PC has the same kind of page). This is a checklist, not a promise. Reports are welcome: open an
issue with the log file from `logs\`, the Proton version and the GPU. No game files, please.

You need your own copy of the game and Title Update 1.04; nothing here links to game files.

## How to try it

1. Prepare the game folder on a Windows PC with `WWE2K17 Launcher.exe` (it is a Windows program and is unlikely to run
   under Proton), then copy the whole folder (game files included) to the Deck, for example `~/Games/WWE2K17-PC/`.
2. Steam, Desktop Mode: *Add a Non-Steam Game*, pick `wwe2k17_native.exe`. In its Properties > Compatibility, force
   *Proton Experimental* or a recent GE-Proton.
3. Keep `dxcompiler.dll` and `dxil.dll` in the same folder as the exe (the zip has them; the very first upload of 0.1
   did not, so download it again if you got that one). Without them the Vulkan renderers say "dxcompiler.dll is
   missing".
4. Launch options (optional): `--fullscreen=true`. If frames drop, `--svr_render_scale=540`.
5. Controller: set the game's Steam Input layout to a **Gamepad** template, not the default Keyboard (WASD) and Mouse
   one, otherwise the game sees no controller. Without a pad, `--mnk_mode=true` turns the keyboard on.
6. If `wwe2k17_native.exe` fails, try `wwe2k17_d3d11.exe` (Direct3D 11, translated by DXVK).
7. First start only: the game prepares its shaders and the very first entrance may stutter once; the result is kept in
   `userdata\cache\`. Do not copy a cache from another PC.

## What may break under Proton (from reading the code, nothing was run)

| Area | Expectation |
|---|---|
| Shader conversion | `dxcompiler.dll` and `dxil.dll` are Windows DLLs next to the exe, loaded as usual. If they fail to load the picture stays black or the game stops with the "dxcompiler.dll" message. First thing to check. |
| GPU | The Vulkan renderers need Vulkan 1.2 (buffer device address, descriptor indexing); Mesa RADV on the Deck has both. AMD GPUs are untested here. `wwe2k17.exe` (Xbox GPU emulation) is the one most likely to struggle on AMD. |
| Paths | `game\` and `userdata\` are beside the exe, so they stay inside the folder you copied. Prefer a path without accents or spaces. |
| CPU | A match needs about 4 threads to hold 60 fps; the Deck has 4 cores / 8 threads at lower clocks than a desktop, so it is the unknown. |
| Launcher | Windows-only (WPF): do not rely on it under Proton. |

## Reporting

Send `logs\run-*.log`, the Proton version, the Steam OS version and the output of `vulkaninfo --summary`.
