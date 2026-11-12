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
#include <rex/system/kernel_state.h>

class Wwe2k17App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(
      rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<Wwe2k17App>(new Wwe2k17App(ctx, "wwe2k17",
        PPCImageConfig));
  }

  // The SDK's d3d12_adapter = -1 takes the first hardware DXGI adapter, which on a laptop is the integrated GPU
  // (a first test ran on the Intel UHD next to an RTX 5070). Unless the player chose an adapter, use the one
  // with the most dedicated video memory.
  void OnPreSetup(rex::RuntimeConfig& config) override {
    (void)config;
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
