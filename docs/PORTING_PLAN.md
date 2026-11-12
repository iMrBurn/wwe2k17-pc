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
- [ ] First build of `wwe2k17.exe` and boot to the logos.
- [ ] setjmp / longjmp addresses (2K16 needed them for libjpeg error paths).
- [ ] TU2 `pac/` overlay and DLC mounting.
- [ ] Port from 2K15 / 2K16: launcher, crash reports, framerate hook, keyboard, native renderer.

## 2026-11-12

- ReXGlue v0.10.1 (prebuilt Windows release). `tools/xiso_get.py` pulls `default.xex` out of the ISO,
  `tools/stfs_extract.py` pulls `default.xexp` out of the TU2 package.
- First codegen: "XEX patch applied successfully" (0.0.0.1 -> 0.0.2.1), then 3 UnresolvedCall
  targets reached only by `b` tail calls (0x82DE01CC, 0x826CC0E0, 0x8309C808), added to
  `config/functions.toml`. Second codegen: 591 files written in 97 s.
