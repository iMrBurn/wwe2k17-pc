// wwe2k17 - WWE 2K17 (Xbox 360) static recompilation with the stock ReXGlue SDK v0.10.1.

#pragma once

#include <filesystem>
#include <memory>
#include <system_error>

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
  }

 private:
  std::filesystem::path user_data_root_;
};
