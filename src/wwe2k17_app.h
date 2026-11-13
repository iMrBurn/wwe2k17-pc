// wwe2k17 - WWE 2K17 (Xbox 360) static recompilation with the stock ReXGlue SDK v0.10.1.

#pragma once

#include <filesystem>
#include <memory>
#include <string>
#include <system_error>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <dxgi.h>
#pragma comment(lib, "dxgi.lib")
#endif

#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/filesystem/devices/host_path_device.h>
#include <rex/filesystem/vfs.h>
#include <rex/logging/macros.h>
#include <rex/rex_app.h>
#include <rex/system/gpu_plugin.h>
#include <rex/system/kernel_state.h>

#if defined(_WIN32)
#include <timeapi.h>
#pragma comment(lib, "winmm.lib")
#endif

#if defined(SVR_NATIVE_RENDERER)
// Native renderer (wwe2k17_native.exe only, cmake/native_renderer.cmake): re:Blue draws through plume (Vulkan) from the
// game's own D3D calls instead of emulating the Xenos GPU.
#include "gpu/device.h"
#include "gpu/imgui_overlay_drawer.h"
#include "gpu/shaders/shader_cache.h"
#endif

REXCVAR_DEFINE_STRING(wwe2k17_gpu_backend, "vulkan", "WWE2K17",
                      "GPU emulation backend of wwe2k17.exe: vulkan, d3d12 or any (D3D12 hangs in the first 3D scene)");

class Wwe2k17App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(
      rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<Wwe2k17App>(new Wwe2k17App(ctx, "wwe2k17",
        PPCImageConfig));
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
#if defined(_WIN32)
    // The GPU command processor waits with Sleep(1 ms); at the default ~15.6 ms timer tick every wait costs a whole tick.
    timeBeginPeriod(1);
#endif
    PickDiscreteD3D12Adapter();
    if (config.graphics) return;
#if defined(SVR_NATIVE_RENDERER)
    // The Xenos plugin's "null" backend (patches/sdk/0001-null-gpu.patch) keeps the game's D3D command stream moving
    // (fences, interrupts, vblank) without drawing; the re:Blue hooks draw instead.
    config.graphics = rex::system::LoadGpuPlugin("xenos", "null");
    REXLOG_INFO("Native renderer: guest GPU {}", config.graphics ? "null (sync only)" : "MISSING");
#else
    // Vulkan first: the D3D12 backend hangs the GPU (DEVICE_HUNG) when the first 3D scene loads, as in WWE 2K15/2K16.
    const std::string backend = REXCVAR_GET(wwe2k17_gpu_backend);
    config.graphics = rex::system::LoadGpuPlugin("xenos", backend);
    if (!config.graphics && backend != "any") {
      REXLOG_WARN("GPU backend '{}' unavailable, trying any", backend);
      config.graphics = rex::system::LoadGpuPlugin("xenos", "any");
    }
#endif
  }

#if defined(SVR_NATIVE_RENDERER)
  // The plume device draws the ImGui overlays at Present time.
  std::unique_ptr<rex::ui::ImmediateDrawer> OnCreateImmediateDrawer() override {
    return std::make_unique<bd::gpu::ImGuiOverlayDrawer>();
  }

  void OnPreLaunchModule() override {
    if (!bd::gpu::Video::CreateHostDevice(window())) {
      // GPU or driver without the Vulkan 1.2 features re:Blue needs (bufferDeviceAddress, descriptor indexing...):
      // device.cpp has logged which. Start the GPU-emulation build beside this exe instead (same command line).
      REXLOG_ERROR("Native renderer: could not create the host device (Vulkan unavailable or too old)");
      rex::FlushLogging();
      RestartAs(L"wwe2k17.exe");
      MessageBoxW(nullptr,
                  L"The native renderer could not start: this graphics card or its driver lacks the Vulkan 1.2 "
                  L"features it needs.\n\nUpdate the graphics driver.\n\nLe rendu natif n’a pas pu démarrer : "
                  L"cette carte graphique ou son pilote n’a pas les fonctions Vulkan 1.2 nécessaires.\n\n"
                  L"Mets à jour le pilote graphique.",
                  L"WWE 2K17 PC", MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
      std::_Exit(1);
    }
#if defined(WWE2K17_RUNTIME_SHADERS)
    // Without a usable dxcompiler.dll no shader can be converted and the window stays black: fail cleanly.
    if (!Wwe2k17DxcUsable()) {
      REXLOG_ERROR("Native renderer: shader compiler (dxcompiler.dll) missing or unusable");
      rex::FlushLogging();
      MessageBoxW(nullptr,
                  L"The native renderer could not start: dxcompiler.dll is missing or unusable (it must sit next to the "
                  L"program).\n\nLe rendu natif n’a pas pu démarrer : dxcompiler.dll est absent ou "
                  L"inutilisable (il doit être à côté du programme).",
                  L"WWE 2K17 PC", MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
      std::_Exit(1);
    }
#endif
    // Present runs on the game thread and ImGui on the UI thread: draw the overlay synchronously on the UI thread into
    // the presented image.
    bd::gpu::Video::SetOverlayDrawHook([this](plume::RenderCommandList* cmd, plume::RenderFramebuffer* fb, uint32_t w,
                                              uint32_t h) {
      if (!imgui_drawer() || !imgui_drawer()->HasDialogs()) return;
      app_context().CallInUIThreadSynchronous([this, cmd, fb, w, h] {
        bd::gpu::ReblueUIDrawContext ctx(w, h, cmd, fb);
        imgui_drawer()->Draw(ctx);
      });
    });
  }

  void OnWindowPixelSizeChanged(uint32_t, uint32_t) override { bd::gpu::Video::RequestResize(); }
#endif

 private:
#if defined(_WIN32) && defined(SVR_NATIVE_RENDERER)
  // Start another exe of this folder with this process's command line, then end this process (no return on success).
  static void RestartAs(const wchar_t* exe_name) {
    const std::filesystem::path other = rex::filesystem::GetExecutableFolder() / exe_name;
    std::error_code ec;
    wchar_t self[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, self, MAX_PATH);
    if (!std::filesystem::exists(other, ec) || std::filesystem::equivalent(other, self, ec)) return;
    std::wstring args = GetCommandLineW();
    size_t cut = 0;
    if (!args.empty() && args[0] == L'"') {
      cut = args.find(L'"', 1);
      cut = cut == std::wstring::npos ? args.size() : cut + 1;
    } else {
      cut = args.find(L' ');
      if (cut == std::wstring::npos) cut = args.size();
    }
    std::wstring command = L"\"" + other.wstring() + L"\"" + args.substr(cut);
    STARTUPINFOW si{sizeof(si)};
    PROCESS_INFORMATION pi{};
    if (CreateProcessW(other.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr,
                       rex::filesystem::GetExecutableFolder().c_str(), &si, &pi)) {
      REXLOG_WARN("Native renderer unavailable: started {} instead", other.filename().string());
      rex::FlushLogging();
      CloseHandle(pi.hThread);
      CloseHandle(pi.hProcess);
      std::_Exit(0);
    }
  }
#endif

  // The SDK's d3d12_adapter = -1 takes the first hardware DXGI adapter, which on a laptop is the integrated GPU
  // (a first test ran on the Intel UHD next to an RTX 5070). Unless the player chose an adapter, use the one
  // with the most dedicated video memory.
  static void PickDiscreteD3D12Adapter() {
#if defined(_WIN32)
    if (rex::cvar::HasNonDefaultValue("d3d12_adapter")) return;
    IDXGIFactory1* factory = nullptr;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), reinterpret_cast<void**>(&factory)))) return;
    int best = -1, index = 0;
    SIZE_T best_vram = 0;
    std::string best_name;
    IDXGIAdapter1* adapter = nullptr;
    for (UINT i = 0; factory->EnumAdapters1(i, &adapter) != DXGI_ERROR_NOT_FOUND; ++i) {
      DXGI_ADAPTER_DESC1 desc{};
      adapter->GetDesc1(&desc);
      adapter->Release();
      if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) continue;
      if (desc.DedicatedVideoMemory > best_vram) {
        best_vram = desc.DedicatedVideoMemory;
        best = static_cast<int>(i);
        best_name.clear();
        for (const wchar_t* c = desc.Description; *c; ++c) best_name += *c < 0x80 ? static_cast<char>(*c) : '?';
      }
      ++index;
    }
    factory->Release();
    if (best > 0) {  // 0 is what the SDK picks anyway
      rex::cvar::SetFlagByName("d3d12_adapter", std::to_string(best));
      REXLOG_INFO("[gpu] using adapter {} ({}, {} MB video memory)", best, best_name, best_vram >> 20);
    }
#endif
  }

 public:
  void OnConfigurePaths(rex::PathConfig& paths) override {
    user_data_root_ = paths.user_data_root;
  }

  // The title opens its Xbox HDD cache (cache:\) at boot and waits on it. The stock SDK mounts no cache
  // drive (WWE 2K16 PC added one to its SDK), so mount userdata\xbox_cache there, as 2K16 does.
  void OnPostSetup() override {
    if (user_data_root_.empty()) return;
    const auto cache_root = user_data_root_ / "xbox_cache";
    std::error_code ec;
    std::filesystem::create_directories(cache_root, ec);
    if (ec) {
      REXLOG_ERROR("Unable to create Xbox cache folder {}: {}", cache_root.string(), ec.message());
      return;
    }
    auto* fs = REX_KERNEL_FS();
    auto device = std::make_unique<rex::filesystem::HostPathDevice>("\\CACHE", cache_root, false);
    if (!fs || !device->Initialize() || !fs->RegisterDevice(std::move(device))) {
      REXLOG_ERROR("Unable to mount Xbox cache folder {}", cache_root.string());
      return;
    }
    fs->RegisterSymbolicLink("cache:", "\\CACHE");
    fs->RegisterSymbolicLink("cache0:", "\\CACHE");
    fs->RegisterSymbolicLink("cache1:", "\\CACHE");
    REXLOG_INFO("Xbox cache mounted at {}", cache_root.string());
    InstallDlcInbox();
  }

 private:
  // DLC: copy your own packages (STFS, content type 00000002) into userdata\dlc_inbox\. Each one is installed
  // once by the SDK's own installer (extracted to userdata\0000000000000000\545408BA\00000002\<name>\ with
  // its .header), then the copy in the inbox is deleted. A hand-made header (2K16 launcher layout) was listed
  // but crashed the game ("deferred completion threw 'Invalid UTF-8'"), so the SDK writes them itself.
  void InstallDlcInbox() {
    const auto inbox = user_data_root_ / "dlc_inbox";
    std::error_code ec;
    if (!std::filesystem::is_directory(inbox, ec)) return;
    auto* content = REX_KERNEL_STATE()->content_manager();
    if (!content) return;
    for (const auto& entry : std::filesystem::directory_iterator(inbox, ec)) {
      if (!entry.is_regular_file(ec)) continue;
      const auto result = content->InstallContent(entry.path());
      if (result == 0) {
        REXLOG_INFO("[dlc] installed {}", entry.path().filename().string());
        std::filesystem::remove(entry.path(), ec);
      } else {
        REXLOG_ERROR("[dlc] could not install {} (result {:#x})", entry.path().filename().string(), result);
      }
    }
  }

  std::filesystem::path user_data_root_;
};
