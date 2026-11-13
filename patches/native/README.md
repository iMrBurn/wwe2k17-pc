# Patches for native-deps (the native renderer's dependencies)

`native-deps/` is not in Git (`.gitignore`). It holds:

- `plume/`: plume at e0c8871 ("add R11G11B10_FLOAT and D24_UNORM_S8_UINT render formats", the commit WWE '12 PC pins)
  with `plume.patch`: WWE '12 PC's plume changes (D3D11 backend from the SvR 2011 PC port, D3D12 fixes, render formats)
  merged with WWE 2K15 / 2K16 PC's Vulkan pipeline cache (`setVulkanPipelineCacheDirectory`).
- `reblue-XenosRecomp/`: re:Blue's XenosRecomp fork at 339af41 with `reblue-xenosrecomp.patch` (WWE '12 PC's
  REBLUE_RECOMP changes, including SVR_DXIL_DEPTH_FLIP for the D3D12 / D3D11 builds). Its `thirdparty/` submodules
  must be complete (zstd's `build/cmake` is needed to build the command-line tool used by `tools/dump_shaders.ps1`).

On this PC they were copied from WWE 2K16 PC's and WWE '12 PC's own `native-deps` / `recomp` folders (2026-11-13).
