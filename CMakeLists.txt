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
    src/framerate_hooks.cpp
    src/movie_bind_log.cpp
    src/text_hooks.cpp
    src/shadows.cpp
    src/test_input.cpp
    src/exit_menu.cpp
    src/string_tables.cpp
    src/universe_stories.cpp
)

# The default runtime settings, built into the exes: the game writes them as wwe2k17.toml next to itself at the first
# start when the file is missing, so the release zip does not ship that file and an update can never overwrite the
# player's settings and key bindings (the F4 menu saves them there).
file(READ "${CMAKE_CURRENT_SOURCE_DIR}/wwe2k17_runtime.toml" WWE2K17_DEFAULT_TOML)
configure_file(src/default_config.h.in "${CMAKE_CURRENT_BINARY_DIR}/wwe2k17_generated/default_config.h" @ONLY)
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/wwe2k17_runtime.toml")
include_directories("${CMAKE_CURRENT_BINARY_DIR}/wwe2k17_generated")

if(WIN32)
    add_executable(wwe2k17 WIN32 ${WWE2K17_SOURCES})
else()
    add_executable(wwe2k17 ${WWE2K17_SOURCES})
endif()

# Native renderer (docs/PORTING_PLAN.md, cmake/native_renderer.cmake): executables that draw the game's D3D calls
# through re:Blue + plume instead of emulating the Xenos GPU, from the same recompiled objects as wwe2k17.exe:
# wwe2k17_native.exe (Vulkan), wwe2k17_d3d12.exe and wwe2k17_d3d11.exe (once the game's shaders were converted at
# build time, tools\dump_shaders.ps1). Added before the SDK libraries so plume's VMA wins over rexruntime's.
option(WWE2K17_NATIVE_RENDERER "Also build the native renderer executables" ON)
set(WWE2K17_NATIVE_TARGETS "")
if(WWE2K17_NATIVE_RENDERER)
    include(cmake/native_renderer.cmake)
    add_executable(wwe2k17_native WIN32 ${WWE2K17_SOURCES})
    wwe2k17_add_native_renderer(wwe2k17_native)
    list(APPEND WWE2K17_NATIVE_TARGETS wwe2k17_native)
    if(WWE2K17_D3D12_EXE)
        add_executable(wwe2k17_d3d12 WIN32 ${WWE2K17_SOURCES})
        wwe2k17_add_native_renderer(wwe2k17_d3d12 D3D12)
        list(APPEND WWE2K17_NATIVE_TARGETS wwe2k17_d3d12)
    endif()
    if(WWE2K17_D3D11_EXE)
        add_executable(wwe2k17_d3d11 WIN32 ${WWE2K17_SOURCES})
        wwe2k17_add_native_renderer(wwe2k17_d3d11 D3D11)
        list(APPEND WWE2K17_NATIVE_TARGETS wwe2k17_d3d11)
    endif()
endif()

rexglue_setup_target(wwe2k17 GPU_PLUGINS xenos)

foreach(native_target IN LISTS WWE2K17_NATIVE_TARGETS)
    # wwe2k17_recomp only exists once codegen has written generated/ (a fresh checkout's first configure doesn't).
    if(TARGET wwe2k17_recomp)
        target_sources(${native_target} PRIVATE $<TARGET_OBJECTS:wwe2k17_recomp>)
    endif()
    target_include_directories(${native_target} PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${REXGLUE_ENTRYPOINT_INCLUDE_DIR})
    target_link_libraries(${native_target} PRIVATE rex::runtime)
    add_dependencies(${native_target} wwe2k17_codegen)
    rexglue_apply_target_settings(${native_target})
    rexglue_configure_target(${native_target} GPU_PLUGINS xenos)
endforeach()
