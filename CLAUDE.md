# WWE 2K17 PC: instructions for Claude

Native Windows x64 port of WWE 2K17 (Xbox 360) by static recompilation with ReXGlue v0.10.1 (stock
release, in `sdk/`). Public repo: https://github.com/iMrBurn/wwe2k17-pc. Work log: `docs/PORTING_PLAN.md`
(dated sections, newest at the bottom). Sister projects to read before solving a problem:
WWE 2K16 PC (`Desktop\WWE 2K16 Recomp\wwe2k16-pc-src`, same SDK generation), WWE 2K15 PC
(`Documents\wwe2k15-pc`), WWE '12 PC (`Documents\WWE12-PC\wwe12-pc`).

The user (iMrBurn) is French-speaking and not a developer: answer in French, simply, no jargon.
Code, commits and docs in English. Commit as `iMrBurn <267004884+iMrBurn@users.noreply.github.com>`.

## Hard rules
- No game data in Git or in any package: no xex/xexp, iso, .pac, sound, textures, `generated/`,
  Title Update or DLC, and no download links for them.
- Ask before any download. Never touch Windows security settings.
- Never modify the user's original files (`Desktop\WWE ISOS\...`, `Desktop\tu00000001_00000000` = TU 1.04, the TU2 zip in Downloads).
  Work copies: `game_tu/` (default.xex + default.xexp), `..\tu4\` (default.xexp of TU 1.04), `..\tu2\` (old TU2, unused).
- Ask before launching the game when the user may be using the PC.

## Build and run
- `tools\setup.ps1 -Iso <iso> -TitleUpdate <tu00000001_00000000>` then `tools\build.ps1`
  (`-NoCodegen` after a source-only change), then `tools\run.ps1` (window + `logs\run-*.log`).
  Close `wwe2k17.exe` before building: the link fails while it runs.
- Codegen input: `wwe2k17_manifest.toml` -> `generated/default` (ignored). Missing entry points go in
  `config/functions.toml` (UnresolvedCall lines of the codegen log); pointer-only functions come from
  `tools/find_functions.py` + `tools/make_fptrs.py` (`config/functions_fptrs.toml`, `config/setjmp.toml`).
- The stock SDK cannot be patched here: game-specific fixes go through `[[midasm_hook]]` (`config/hooks.toml`,
  `src/*_hooks.cpp`) and `Wwe2k17App` overrides (`src/wwe2k17_app.h`). Runtime settings:
  `wwe2k17_runtime.toml` (copied as `wwe2k17.toml` next to the exe; `gpu_plugin = "xenos"` is required).
- `tools\capture_window.ps1 -Out logs\shot.png` saves an image of the game window.
- Switching title update: redo setup, codegen with only config/functions.toml, then find_functions + make_fptrs,
  scan_thunks (import stub start = lowest __imp__ in wwe2k17_init.cpp), and move the [[midasm_hook]] addresses
  (find the call sites in generated code). See docs/PORTING_PLAN.md 'Title Update 1.04'.
