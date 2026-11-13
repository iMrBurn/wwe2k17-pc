# wwe2k17 - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake. The SDK is ReXGlue v0.10.1 built from source with patches\sdk
# (tools\build-sdk.ps1, installed to sdk\rexglue-install): Vulkan in the Xenos plugin and a "null" GPU backend.

cmake_minimum_required(VERSION 3.25)
project(wwe2k17 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(generated/rexglue.cmake)

# Sources
set(WWE2K17_SOURCES
    src/main.cpp
    src/memory_hooks.cpp
    src/content_hooks.cpp
    src/render_hooks.cpp
)

if(WIN32)
    add_executable(wwe2k17 WIN32 ${WWE2K17_SOURCES})
else()
    add_executable(wwe2k17 ${WWE2K17_SOURCES})
endif()

# Native renderer (docs/PORTING_PLAN.md): a second executable, wwe2k17_native.exe, that draws the game's D3D calls
# through re:Blue + plume (Vulkan), taken from WWE 2K16 PC (itself from WWE 2K15 PC / WWE '12 PC). It links the same
# recompiled objects; wwe2k17.exe (Xenos GPU emulation) is unchanged. Needs plume and reblue-XenosRecomp in
# native-deps/ (WWE2K17_NATIVE_DEPS). Added before the SDK libraries so plume's VMA wins over rexruntime's.
option(WWE2K17_NATIVE_RENDERER "Also build wwe2k17_native.exe (native Vulkan renderer)" ON)
if(WWE2K17_NATIVE_RENDERER)
    include(cmake/native_renderer.cmake)
    add_executable(wwe2k17_native WIN32 ${WWE2K17_SOURCES})
    wwe2k17_add_native_renderer(wwe2k17_native)
endif()

rexglue_setup_target(wwe2k17 GPU_PLUGINS xenos)

if(WWE2K17_NATIVE_RENDERER)
    # wwe2k17_recomp only exists once codegen has written generated/ (a fresh checkout's first configure doesn't).
    if(TARGET wwe2k17_recomp)
        target_sources(wwe2k17_native PRIVATE $<TARGET_OBJECTS:wwe2k17_recomp>)
    endif()
    target_include_directories(wwe2k17_native PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${REXGLUE_ENTRYPOINT_INCLUDE_DIR})
    target_link_libraries(wwe2k17_native PRIVATE rex::runtime)
    add_dependencies(wwe2k17_native wwe2k17_codegen)
    rexglue_apply_target_settings(wwe2k17_native)
    rexglue_configure_target(wwe2k17_native GPU_PLUGINS xenos)
endif()
