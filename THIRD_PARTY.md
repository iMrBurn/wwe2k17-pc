# Third-party components

This project's own code is under the **GPL-3.0** (see `LICENSE`), like WWE '12 PC, WWE 2K15 PC and WWE 2K16 PC, whose
native renderer it reuses. The components below keep their own licenses. No game data (XEX, disc files, extracted or
converted shaders, generated code) is included in this repository or in its releases.

| Component | Use | License |
|---|---|---|
| [ReXGlue SDK](https://github.com/rexglue/rexglue-sdk) v0.10.1 | static recompilation, runtime, Xenos GPU emulation (built from source by `tools/build-sdk.ps1` with `patches/sdk`) | BSD 3-Clause (derived from [Xenia](https://xenia.jp)) |
| [re:Blue](https://github.com/zolaware/reblue) | core of the native renderer (`src/reblue`) | BSD 3-Clause (`src/reblue/LICENSE.reblue`) |
| [plume](https://github.com/zolaware/plume) | Vulkan layer of the native renderer (built from `native-deps`, not vendored) | MIT |
| [XenosRecomp](https://github.com/hedge-dev/XenosRecomp) (re:Blue fork) | Xbox 360 shader to HLSL translator, built into `wwe2k17_native.exe` (converts the player's own shaders) | MIT (`third_party/licenses/XenosRecomp-LICENSE.md`) |
| [DirectX Shader Compiler](https://github.com/microsoft/DirectXShaderCompiler) | `dxcompiler.dll`, shipped beside `wwe2k17_native.exe`: HLSL to SPIR-V on the player's PC | University of Illinois/NCSA (`third_party/licenses/DXC-LICENSE.txt`, `DXC-ThirdPartyNotices.txt`) |
| smol-v, xxHash, fmt | SPIR-V packing, shader hashes, text formatting (XenosRecomp's copies) | MIT, BSD 2-Clause, MIT (`third_party/licenses`) |
| zstd (`third_party/reblue_thirdparty/zstd`) | shader cache decompression | BSD 3-Clause / GPL-2.0 dual |
| Licenses in `third_party/licenses` | disruptorplus, renderdoc_app (re:Blue dependencies) | see the files |

Projects this port takes code from (all by iMrBurn unless noted, GPL-3.0):

- WWE 2K16 PC: the native renderer as adapted to the WWE 2K engine (`src/reblue`, `src/native`, `src/native_xr`,
  `cmake/native_renderer.cmake`), `tools/find_functions.py`, `tools/scan_thunks.py`, `tools/check_labels.py`, the
  window capture and input scripts, the cache drive mount.
- WWE 2K15 PC: the render-thread ring-wait yield and pacing log (`src/render_hooks.cpp`), the null GPU SDK patch
  (`patches/sdk/0001-null-gpu.patch`), the content-name UTF-8 idea (`src/content_hooks.cpp`).
- WWE '12 PC and nefariousjosiah's SvR 2008/2009/2010 ports (GPL-3.0): where the native renderer comes from.
