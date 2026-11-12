# wwe2k17 - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(wwe2k17 LANGUAGES CXX)

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

rexglue_setup_target(wwe2k17 GPU_PLUGINS xenos)
