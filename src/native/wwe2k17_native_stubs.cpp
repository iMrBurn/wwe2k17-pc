// WWE 2K16 native renderer: stand-ins for the WWE '12 PC features that re:Blue and the svr_* glue
// call (texture packs, FPS counter, quit entry, loading turbo, belt probe). They do nothing yet;
// each can be ported from C:\Dev\wwe12-pc\port\src\native when 2K15 needs it.

#include <cstdint>
#include <memory>
#include <string>

#include <rex/cvar.h>
#include <rex/logging.h>

#include "wwe12_display.h"
#include "wwe12_texture_packs.h"

namespace wwe12::texture_packs {

bool Active() { return false; }

uint64_t HashBlocks(const uint8_t*, uint32_t, uint32_t, uint32_t, uint32_t) { return 0; }

void Dump(uint64_t, uint32_t, uint32_t, Format, const uint8_t*, uint32_t, uint32_t) {}

std::shared_ptr<const Replacement> Find(uint64_t) { return nullptr; }

}  // namespace wwe12::texture_packs

void Wwe12DisplayInit(rex::ui::ImGuiDrawer*) {}

// The "Guest pacing" log line of render_hooks.cpp (the Xenos build gets it from its D3DDevice_Swap hook; here re:Blue
// owns Swap, so the renderer reports each presented frame instead).
void Wwe2k17PacingOnPresent();
void Wwe2k17OnGuestPresent(uint8_t*) { Wwe2k17PacingOnPresent(); }

// Called by the renderer once per presented guest frame (svr_resources.cpp).
void Wwe12DisplayOnFrame() { Wwe2k17OnGuestPresent(nullptr); }

namespace bd::gpu {
void Wwe12QuitEntryTick(uint64_t) {}
}  // namespace bd::gpu

// WWE '12 PC's hang watchdog (native/crash_trace.cpp there); 2K15 has its own crash_report.cpp.
void SvrWatchdogHeartbeat() {}


bool Wwe12LoadTurboSkipPresent() { return false; }

bool Wwe12BeltProbe(uint32_t, uint64_t, uint32_t, uint32_t) { return false; }

REXCVAR_DEFINE_STRING(svr_hooks_off, "", "SvR",
                      "Native renderer bring-up: comma list of D3D hooks whose renderer side is "
                      "skipped (D3DDevice_Swap, D3DDevice_Resolve, ..., or all)");

namespace svr {
bool HookEnabled(const char* name) {
  const std::string list = "," + REXCVAR_GET(svr_hooks_off) + ",";
  const bool off = list.find("," + std::string(name) + ",") != std::string::npos ||
                   list.find(",all,") != std::string::npos;
  if (off) {
    REXLOG_INFO("Native renderer: hook {} observed by nobody (svr_hooks_off)", name);
  }
  return !off;
}
}  // namespace svr

// WWE 2K15's SDK patch rexglue-sdk-iostats.patch adds file read statistics to the kernel; the v0.10.1 SDK built here
// has none, so the loading diagnostics of svr_resources.cpp read zeros.
namespace rex::kernel::xboxkrnl {
void GetFileIoStats(uint64_t* bytes, uint64_t* calls, uint64_t* nanoseconds) {
  *bytes = 0;
  *calls = 0;
  *nanoseconds = 0;
}
}  // namespace rex::kernel::xboxkrnl
