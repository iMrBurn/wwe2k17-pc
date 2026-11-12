// wwe2k17 - ReXGlue Recompiled Project

#include "generated/default/wwe2k17_init.h"

#include "wwe2k17_app.h"

// Ask NVIDIA Optimus / AMD switchable graphics to power and prefer the discrete GPU for this title
// (same as WWE 2K15 PC). Wwe2k17App::OnPreSetup also picks the D3D12 adapter with the most video memory,
// because the SDK otherwise takes the first DXGI adapter, the integrated one on a laptop.
extern "C" __declspec(dllexport) unsigned long NvOptimusEnablement = 0x00000001;
extern "C" __declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;

REX_DEFINE_APP(wwe2k17, Wwe2k17App::Create)
