# WWE 2K17 native renderer: WWE 2K16 PC's (itself from WWE '12 PC, iMrBurn/wwe12-pc, GPL-3.0, adapted from re:Blue,
# BSD-3-Clause, see src/reblue/LICENSE.reblue, through nefariousjosiah's SvR 2008-2010 ports, GPL-3.0), with WWE '12
# PC's D3D12 and D3D11 builds (its commits f1b5864..8d97c15; the D3D11 backend of plume comes from the SvR 2011 PC
# port by KaikoClanworth1, adapted in WWE '12 PC with their agreement).
#
# Draws through plume by observing the game's XDK D3D functions, instead of emulating the Xenos GPU. Three executables
# from the same recompiled code (wwe2k17_add_native_renderer):
#   wwe2k17_native.exe  Vulkan. The game's shaders are converted on the player's PC (XenosRecomp built in, DXC's
#                       dxcompiler.dll beside the exe -> SPIR-V, cache\native_shaders.bin), nothing game-derived ships.
#   wwe2k17_d3d12.exe   Direct3D 12. The game's shaders converted at build time (generated/native/shader_cache.cpp,
#                       tools/dump_shaders.ps1 + XenosRecomp) and every spec-constant variant pre-linked to DXIL by
#                       wwe2k17_prelink: no DXC at run time.
#   wwe2k17_d3d11.exe   Direct3D 11. The same HLSL compiled to DXBC with the Windows SDK's fxc at build time
#                       (tools/convert_shaders_d3d11.py, from work/shaders/d3d11/hlsl).
# The D3D12 / D3D11 exes carry the game's converted shaders (user's decision of 2026-11-13, like WWE '12 PC).
#
# The renderer's sources go straight into the executables: their REX_HOOK overrides are strong definitions nothing else
# references, so a static library would drop them at link time.

enable_language(C)  # zstd, xxHash and miniz are C

set(WWE2K17_NATIVE_ROOT "${CMAKE_CURRENT_SOURCE_DIR}")
# plume (WWE '12 PC's copy, D3D11 backend included, plus WWE 2K15 PC's Vulkan pipeline cache) and reblue-XenosRecomp.
set(WWE2K17_NATIVE_DEPS "${CMAKE_CURRENT_SOURCE_DIR}/native-deps" CACHE PATH
    "Folder holding plume and reblue-XenosRecomp for the native renderer")
set(WWE2K17_XENOS_DIR "${WWE2K17_NATIVE_DEPS}/reblue-XenosRecomp")
set(WWE2K17_PLUME_DIR "${WWE2K17_NATIVE_DEPS}/plume")
set(WWE2K17_NATIVE_GEN "${CMAKE_BINARY_DIR}/native_gen")
set(WWE2K17_SHADER_CACHE "${WWE2K17_NATIVE_ROOT}/generated/native/shader_cache.cpp")
set(WWE2K17_DXC_DIR "${WWE2K17_XENOS_DIR}/thirdparty/dxc-bin")
set(WWE2K17_MINIZ_DIR "${WWE2K17_XENOS_DIR}/thirdparty/smol-v/testing/external/miniz")

# The D3D builds need the game's shaders converted at build time (tools\dump_shaders.ps1).
if(WIN32 AND EXISTS "${WWE2K17_SHADER_CACHE}")
    set(WWE2K17_D3D_DEFAULT ON)
else()
    set(WWE2K17_D3D_DEFAULT OFF)
endif()
option(WWE2K17_D3D12_EXE "Also build wwe2k17_d3d12.exe (Direct3D 12 renderer)" ${WWE2K17_D3D_DEFAULT})
option(WWE2K17_D3D11_EXE "Also build wwe2k17_d3d11.exe (Direct3D 11 renderer)" ${WWE2K17_D3D_DEFAULT})
if(WWE2K17_D3D12_EXE OR WWE2K17_D3D11_EXE)
    set(PLUME_D3D12_ENABLED ON CACHE BOOL "" FORCE)
else()
    set(PLUME_D3D12_ENABLED OFF CACHE BOOL "" FORCE)
endif()
add_subdirectory("${WWE2K17_PLUME_DIR}" "${CMAKE_BINARY_DIR}/plume" EXCLUDE_FROM_ALL)

# Small third-party pieces the renderer uses.
add_library(wwe2k17_native_thirdparty STATIC
    "${WWE2K17_NATIVE_ROOT}/third_party/reblue_thirdparty/zstd/zstddeclib.c"
    "${WWE2K17_XENOS_DIR}/thirdparty/xxHash/xxhash.c"
    "${WWE2K17_XENOS_DIR}/thirdparty/smol-v/source/smolv.cpp")
target_include_directories(wwe2k17_native_thirdparty PUBLIC
    "${WWE2K17_NATIVE_ROOT}/third_party/reblue_thirdparty/zstd"
    "${WWE2K17_XENOS_DIR}/thirdparty/xxHash"
    "${WWE2K17_XENOS_DIR}/thirdparty/smol-v/source")

# Vulkan exe: XenosRecomp's translator (MIT) built in; DXC's dxcompiler.dll (beside the exe) turns its HLSL into SPIR-V
# on the player's PC (src/native/wwe2k17_shader_cache.cpp).
add_library(wwe2k17_xenosrecomp STATIC
    "${WWE2K17_XENOS_DIR}/XenosRecomp/shader_recompiler.cpp"
    "${WWE2K17_NATIVE_ROOT}/src/native_xr/xenos_translate.cpp")
target_include_directories(wwe2k17_xenosrecomp PRIVATE
    "${WWE2K17_XENOS_DIR}/XenosRecomp"
    "${WWE2K17_XENOS_DIR}/thirdparty/fmt/include"
    "${WWE2K17_XENOS_DIR}/thirdparty/zstd/lib"
    "${WWE2K17_XENOS_DIR}/thirdparty/xxHash")
target_include_directories(wwe2k17_xenosrecomp PUBLIC
    "${WWE2K17_NATIVE_ROOT}/src/native_xr"
    "${WWE2K17_DXC_DIR}/inc"
    "${WWE2K17_XENOS_DIR}/thirdparty/smol-v/source")
target_compile_definitions(wwe2k17_xenosrecomp PRIVATE REBLUE_RECOMP FMT_HEADER_ONLY _CRT_SECURE_NO_WARNINGS)
if(MSVC)
    target_compile_options(wwe2k17_xenosrecomp PRIVATE "/FI${WWE2K17_XENOS_DIR}/XenosRecomp/pch.h"
        /clang:-Wno-switch /clang:-Wno-unused-variable /clang:-Wno-null-arithmetic)
else()
    target_compile_options(wwe2k17_xenosrecomp PRIVATE -include "${WWE2K17_XENOS_DIR}/XenosRecomp/pch.h"
        -Wno-switch -Wno-unused-variable -Wno-null-arithmetic -fms-extensions)
endif()
# The HLSL prelude XenosRecomp prepends to every shader (shader_common.h), embedded as bytes.
file(READ "${WWE2K17_XENOS_DIR}/XenosRecomp/shader_common.h" _common_hex HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1," _common_bytes "${_common_hex}")
file(WRITE "${WWE2K17_NATIVE_GEN}/shader_common_text.inc" "${_common_bytes}")
set(WWE2K17_DXCOMPILER_DLL "${WWE2K17_DXC_DIR}/bin/x64/dxcompiler.dll")

# Host shaders (copy, ImGui overlay, MSAA resolve, gamma): SPIR-V (Vulkan) and DXIL (D3D12) with the DXC shipped in
# reblue-XenosRecomp, DXBC (D3D11) with the Windows SDK's fxc after tools/host_shader_d3d11.py.
set(WWE2K17_DXC "${WWE2K17_DXC_DIR}/bin/x64/dxc.exe")
set(WWE2K17_HLSL_DIR "${WWE2K17_NATIVE_ROOT}/src/reblue/gpu/shaders/hlsl")
set(WWE2K17_HOST_SHADER_HEADERS "")
set(WWE2K17_HOST_SHADER_HEADERS_DXIL "")
set(WWE2K17_HOST_SHADER_HEADERS_DXBC "")
if(WIN32)
    file(GLOB WWE2K17_FXC_CANDIDATES "C:/Program Files (x86)/Windows Kits/10/bin/10.*/x64/fxc.exe")
    list(SORT WWE2K17_FXC_CANDIDATES)
    list(POP_BACK WWE2K17_FXC_CANDIDATES WWE2K17_FXC)
    find_package(Python3 COMPONENTS Interpreter QUIET)
endif()
function(wwe2k17_host_shader STEM PROFILE)
    file(GLOB hlsl_includes "${WWE2K17_HLSL_DIR}/*.hlsli")
    set(args -spirv -fvk-use-dx-layout)
    if(PROFILE MATCHES "^vs")
        list(APPEND args -fvk-invert-y)
    endif()
    set(out "${WWE2K17_NATIVE_GEN}/src/gpu/shaders/hlsl/${STEM}.hlsl.spirv.h")
    add_custom_command(
        OUTPUT "${out}"
        COMMAND "${WWE2K17_DXC}" -T ${PROFILE} -HV 2021 -all-resources-bound -Wno-ignored-attributes
                ${args} -I "${WWE2K17_NATIVE_ROOT}" -Fh "${out}" "${WWE2K17_HLSL_DIR}/${STEM}.hlsl"
                -Vn g_${STEM}_spirv
        DEPENDS "${WWE2K17_HLSL_DIR}/${STEM}.hlsl" ${hlsl_includes}
        COMMENT "Compiling ${STEM}.hlsl (${PROFILE}, SPIR-V)"
        VERBATIM)
    set(WWE2K17_HOST_SHADER_HEADERS ${WWE2K17_HOST_SHADER_HEADERS} "${out}" PARENT_SCOPE)
    if(PLUME_D3D12_ENABLED)
        set(out_dxil "${WWE2K17_NATIVE_GEN}/src/gpu/shaders/hlsl/${STEM}.hlsl.dxil.h")
        add_custom_command(
            OUTPUT "${out_dxil}"
            COMMAND "${WWE2K17_DXC}" -T ${PROFILE} -HV 2021 -all-resources-bound
                    -Wno-ignored-attributes -I "${WWE2K17_NATIVE_ROOT}" -Fh "${out_dxil}"
                    "${WWE2K17_HLSL_DIR}/${STEM}.hlsl" -Vn g_${STEM}_dxil
            DEPENDS "${WWE2K17_HLSL_DIR}/${STEM}.hlsl" ${hlsl_includes}
            COMMENT "Compiling ${STEM}.hlsl (${PROFILE}, DXIL)"
            VERBATIM)
        set(WWE2K17_HOST_SHADER_HEADERS_DXIL ${WWE2K17_HOST_SHADER_HEADERS_DXIL} "${out_dxil}" PARENT_SCOPE)
    endif()
    if(WWE2K17_D3D11_EXE AND WWE2K17_FXC AND Python3_FOUND)
        string(REPLACE "_6_0" "_5_0" profile_sm5 "${PROFILE}")
        set(out_dxbc "${WWE2K17_NATIVE_GEN}/src/gpu/shaders/hlsl/${STEM}.hlsl.dxbc.h")
        add_custom_command(
            OUTPUT "${out_dxbc}"
            COMMAND "${Python3_EXECUTABLE}" -I "${WWE2K17_NATIVE_ROOT}/tools/host_shader_d3d11.py"
                    "${WWE2K17_FXC}" ${profile_sm5} "${WWE2K17_HLSL_DIR}/${STEM}.hlsl" "${out_dxbc}"
                    g_${STEM}_dxbc -I "${WWE2K17_NATIVE_ROOT}"
            DEPENDS "${WWE2K17_HLSL_DIR}/${STEM}.hlsl" ${hlsl_includes}
                    "${WWE2K17_NATIVE_ROOT}/tools/host_shader_d3d11.py"
            COMMENT "Compiling ${STEM}.hlsl (${profile_sm5}, DXBC)"
            VERBATIM)
        set(WWE2K17_HOST_SHADER_HEADERS_DXBC ${WWE2K17_HOST_SHADER_HEADERS_DXBC} "${out_dxbc}" PARENT_SCOPE)
    endif()
endfunction()
foreach(shader IN ITEMS copy_vs imgui_vs)
    wwe2k17_host_shader(${shader} vs_6_0)
endforeach()
foreach(shader IN ITEMS copy_color_ps copy_depth_ps gamma_correction_ps pfx_occlusion_count_ps
        imgui_ps resolve_msaa_color_2x resolve_msaa_color_4x resolve_msaa_color_8x
        resolve_msaa_depth_2x resolve_msaa_depth_4x resolve_msaa_depth_8x)
    wwe2k17_host_shader(${shader} ps_6_0)
endforeach()

# D3D12: every spec-constant variant of the game's DXIL shaders linked at build time by wwe2k17_prelink
# (src/prelink, WWE '12 PC's wwe12_prelink), compiled into wwe2k17_d3d12.exe as linked_shader_cache.cpp, so the exe
# needs no DXC at run time (dxcompiler.dll / dxil.dll are never shipped: Microsoft's dxil.dll licence). The tool runs
# DXC on the build machine; the two DLLs land next to it in the build folder.
if(WWE2K17_D3D12_EXE)
    add_executable(wwe2k17_prelink
        "${WWE2K17_NATIVE_ROOT}/src/prelink/wwe12_prelink.cpp"
        "${WWE2K17_NATIVE_ROOT}/src/reblue/gpu/shaders/dxc_link.cpp"
        "${WWE2K17_SHADER_CACHE}"
        "${WWE2K17_NATIVE_ROOT}/third_party/reblue_thirdparty/zstd/zstddeclib.c"
        "${WWE2K17_MINIZ_DIR}/miniz.c")
    target_include_directories(wwe2k17_prelink PRIVATE
        "${WWE2K17_NATIVE_ROOT}/src/reblue"
        "${WWE2K17_NATIVE_ROOT}/third_party/reblue_thirdparty/zstd"
        "${WWE2K17_MINIZ_DIR}"
        "${WWE2K17_DXC_DIR}/inc")
    target_link_libraries(wwe2k17_prelink PRIVATE "${WWE2K17_DXC_DIR}/lib/x64/dxcompiler.lib")
    add_custom_command(TARGET wwe2k17_prelink POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${WWE2K17_DXC_DIR}/bin/x64/dxcompiler.dll" "${WWE2K17_DXC_DIR}/bin/x64/dxil.dll"
            $<TARGET_FILE_DIR:wwe2k17_prelink>
        VERBATIM)
    set(WWE2K17_LINKED_SHADER_CACHE "${WWE2K17_NATIVE_GEN}/linked_shader_cache.cpp")
    add_custom_command(
        OUTPUT "${WWE2K17_LINKED_SHADER_CACHE}"
        COMMAND wwe2k17_prelink "${WWE2K17_LINKED_SHADER_CACHE}"
        DEPENDS wwe2k17_prelink "${WWE2K17_SHADER_CACHE}"
        COMMENT "Pre-linking the D3D12 spec-constant shader variants"
        VERBATIM)
endif()

file(GLOB_RECURSE WWE2K17_NATIVE_SOURCES CONFIGURE_DEPENDS
    "${WWE2K17_NATIVE_ROOT}/src/reblue/*.cpp" "${WWE2K17_NATIVE_ROOT}/src/reblue/*.h"
    "${WWE2K17_NATIVE_ROOT}/src/native/*.cpp" "${WWE2K17_NATIVE_ROOT}/src/native/*.h")

# wwe2k17_add_native_renderer(<target> [D3D12|D3D11]): D3D12 builds re:Blue with REBLUE_D3D12 (DXIL shaders); D3D11
# adds REBLUE_D3D11 (plume_d3d11, DXBC shaders); without either, Vulkan with run-time shader conversion.
function(wwe2k17_add_native_renderer target)
    cmake_parse_arguments(ARG "D3D12;D3D11" "" "" ${ARGN})
    set(sources ${WWE2K17_NATIVE_SOURCES})
    if(ARG_D3D11 OR ARG_D3D12)
        if(NOT EXISTS "${WWE2K17_SHADER_CACHE}")
            message(FATAL_ERROR "generated/native/shader_cache.cpp missing: run tools\\dump_shaders.ps1 first")
        endif()
        # Build-time shaders: the run-time converter of the Vulkan exe stays out.
        list(FILTER sources EXCLUDE REGEX "/native/wwe2k17_shader_cache\\.cpp$")
        target_sources(${target} PRIVATE "${WWE2K17_SHADER_CACHE}")
        target_include_directories(${target} PRIVATE "${WWE2K17_MINIZ_DIR}")
    endif()
    if(ARG_D3D11)
        set(dxbc_hlsl "${WWE2K17_NATIVE_ROOT}/work/shaders/d3d11/hlsl")
        if(NOT (EXISTS "${dxbc_hlsl}" AND WWE2K17_FXC AND Python3_FOUND))
            message(FATAL_ERROR "wwe2k17_d3d11: needs work/shaders/d3d11/hlsl (tools\\dump_shaders.ps1), the Windows "
                                "SDK's fxc and Python 3")
        endif()
        set(dxbc_cache "${WWE2K17_NATIVE_GEN}/dxbc_shader_cache.cpp")
        file(GLOB dxbc_inputs CONFIGURE_DEPENDS "${dxbc_hlsl}/*.hlsl")
        add_custom_command(
            OUTPUT "${dxbc_cache}"
            COMMAND "${Python3_EXECUTABLE}" -I "${WWE2K17_NATIVE_ROOT}/tools/convert_shaders_d3d11.py"
                    "${dxbc_hlsl}" "${dxbc_cache}"
            DEPENDS ${dxbc_inputs} "${WWE2K17_NATIVE_ROOT}/tools/convert_shaders_d3d11.py"
            COMMENT "Converting the game's shaders to DXBC (fxc)"
            VERBATIM)
        set(host_shaders ${WWE2K17_HOST_SHADER_HEADERS_DXBC})
        target_compile_definitions(${target} PRIVATE REBLUE_D3D12=1 REBLUE_D3D11=1)
        target_link_libraries(${target} PRIVATE d3d11 dxgi dxguid)
        list(FILTER sources EXCLUDE REGEX "/gpu/shaders/(shader_linker|dxc_link)\\.(cpp|h)$")
        # plume's D3D11 backend (from the SvR 2011 PC port, through WWE '12 PC).
        target_sources(${target} PRIVATE "${WWE2K17_MINIZ_DIR}/miniz.c" "${WWE2K17_PLUME_DIR}/plume_d3d11.cpp"
            "${dxbc_cache}")
    elseif(ARG_D3D12)
        set(host_shaders ${WWE2K17_HOST_SHADER_HEADERS_DXIL})
        target_compile_definitions(${target} PRIVATE REBLUE_D3D12=1)
        target_include_directories(${target} PRIVATE "${WWE2K17_DXC_DIR}/inc")
        # dxcompiler.dll is loaded only when a variant is missing from the pre-linked cache (dev builds).
        target_link_libraries(${target} PRIVATE d3d12 dxgi dxguid delayimp "${WWE2K17_DXC_DIR}/lib/x64/dxcompiler.lib")
        target_link_options(${target} PRIVATE "-Wl,/DELAYLOAD:dxcompiler.dll")
        target_sources(${target} PRIVATE "${WWE2K17_MINIZ_DIR}/miniz.c" "${WWE2K17_LINKED_SHADER_CACHE}")
    else()
        set(host_shaders ${WWE2K17_HOST_SHADER_HEADERS})
        # re:Blue's DXIL linker serves the D3D12 exe only.
        list(FILTER sources EXCLUDE REGEX "/gpu/shaders/(shader_linker|dxc_link)\\.(cpp|h)$")
        list(FILTER sources EXCLUDE REGEX "/gpu/d3d11_slots\\.cpp$")
        target_compile_definitions(${target} PRIVATE WWE2K17_RUNTIME_SHADERS=1)
        target_link_libraries(${target} PRIVATE wwe2k17_xenosrecomp)
        add_custom_command(TARGET ${target} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${WWE2K17_DXCOMPILER_DLL}"
                "$<TARGET_FILE_DIR:${target}>/dxcompiler.dll"
            VERBATIM)
    endif()
    target_sources(${target} PRIVATE ${sources} ${host_shaders})
    target_include_directories(${target} PRIVATE
        "${WWE2K17_NATIVE_ROOT}/src/reblue"
        "${WWE2K17_NATIVE_ROOT}/src/native"
        "${WWE2K17_NATIVE_GEN}"
        "${WWE2K17_PLUME_DIR}"
        "${WWE2K17_XENOS_DIR}/XenosRecomp")
    target_compile_definitions(${target} PRIVATE SVR_NATIVE_RENDERER=1)
    # WWE 2K17's addresses for the D3D functions re:Blue's hooks name (D3DDevice_Clear -> sub_...).
    if(MSVC)
        set(_names_include "/FI${WWE2K17_NATIVE_ROOT}/src/native/wwe2k17_d3d_names.h")
    else()
        set(_names_include "-include;${WWE2K17_NATIVE_ROOT}/src/native/wwe2k17_d3d_names.h")
    endif()
    set_source_files_properties(${WWE2K17_NATIVE_SOURCES} PROPERTIES COMPILE_OPTIONS "${_names_include}")
    target_link_libraries(${target} PRIVATE plume wwe2k17_native_thirdparty rex::o1heap)
    # Linker map: names recompiled functions in crash stacks.
    if(MSVC)
        target_link_options(${target} PRIVATE "/MAP:${CMAKE_BINARY_DIR}/${target}.map")
    elseif(WIN32)
        target_link_options(${target} PRIVATE "-Wl,/MAP:${CMAKE_BINARY_DIR}/${target}.map")
    else()
        target_link_options(${target} PRIVATE "-Wl,-Map=${CMAKE_BINARY_DIR}/${target}.map")
    endif()
endfunction()
