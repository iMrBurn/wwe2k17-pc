// wwe2k17 - 60 FPS entrances, victory scenes, replays and cinematics (Title Update 1.04 addresses).
//
// Ported from WWE 2K16 PC (framerate_hook.cpp, after Banshee's frame_rate.cpp) and WWE 2K15 PC (framerate_hook.cpp:
// the automatic fallback). The 2K16 functions were found in 2K17 by instruction similarity (tools/match_functions.py,
// both 1.00 with the same instruction count):
//   0x837C4710     engine timing block: +8 frame rate (60/30, PAL 50/25), +12 hold flag, +28..+64 time steps, +40 frame
//                  length in ms (2K16: 0x837A3850; read from the lis/addi pair of both functions below)
//   sub_82A94AA8   60 -> 30 (50 -> 25): loads the 30 FPS time steps when an entrance, victory motion, replay or cinematic
//                  camera starts (2K16 sub_82B23E80)
//   sub_82A94B48   30 -> 60 (25 -> 50), skipped by the game while +12 is set (2K16 sub_82B23F20)
//
// The engine animates on these time steps, not on wall-clock time: skipping its switch to 30 keeps both the display
// rate and the simulation step at 60, so scenes play at normal speed and twice as smoothly. If a scene kept at 60
// cannot hold it on this PC (average frame over 26 ms for two windows of 90 frames: it would run in slow motion), the
// game's own switch to 30 is run after all and used for the rest of the session (WWE 2K15 PC's wwe2k15_60fps_auto).

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

#include <atomic>
#include <cstdint>
#include <cstring>

REXCVAR_DEFINE_BOOL(wwe2k17_60fps, true, "WWE2K17",
                    "Entrances, victory scenes, replays and cinematics at 60 FPS (50 on PAL) instead of the console's "
                    "30 (25); false = original 30");
REXCVAR_DEFINE_BOOL(wwe2k17_60fps_auto, true, "WWE2K17",
                    "If a scene kept at 60 FPS cannot hold it (it would play in slow motion), go back to the game's "
                    "30 FPS for scenes for the rest of the session");
REXCVAR_DEFINE_INT32(wwe2k17_60fps_auto_ms, 26, "WWE2K17",
                     "Average frame time (ms) above which a scene at 60 FPS is given up (wwe2k17_60fps_auto)")
    .range(10, 60);

namespace {
// Big-endian 32-bit read of guest memory (REX_LOAD_U32 of older SDKs).
inline uint32_t LoadGuestU32(const uint8_t* base, uint32_t addr) {
  uint32_t v;
  std::memcpy(&v, base + addr, 4);
  return __builtin_bswap32(v);
}
}  // namespace
REX_EXTERN(__imp__sub_82A94AA8);
REX_EXTERN(__imp__sub_82A94B48);

namespace {
constexpr uint32_t kTimingBlock = 0x837C4710;
constexpr uint32_t kFrameRate = kTimingBlock + 8;

std::atomic<bool> g_scene_at60{false};
std::atomic<bool> g_auto_off{false};
std::atomic<uint64_t> g_scene_start_us{0};
PPCContext g_drop_ctx;          // registers at the last skipped "drop to 30" (to run it later)
uint8_t* g_drop_base = nullptr;
}  // namespace

uint64_t Wwe2k17NowUs();  // render_hooks.cpp

// The engine's switch to 30 FPS.
extern "C" REX_FUNC(sub_82A94AA8) {
  const uint32_t rate = LoadGuestU32(base, kFrameRate);
  g_scene_start_us.store(Wwe2k17NowUs(), std::memory_order_relaxed);
  if (REXCVAR_GET(wwe2k17_60fps) && !g_auto_off.load(std::memory_order_relaxed)) {
    g_drop_ctx = ctx;
    g_drop_base = base;
    g_scene_at60.store(true, std::memory_order_relaxed);
    REXLOG_INFO("[fps] game drops to 30 (rate {}): kept at 60", rate);
    return;
  }
  REXLOG_INFO("[fps] game drops to 30 (rate {})", rate);
  __imp__sub_82A94AA8(ctx, base);
}

// The engine's switch back to 60.
extern "C" REX_FUNC(sub_82A94B48) {
  const uint64_t start = g_scene_start_us.exchange(0, std::memory_order_relaxed);
  if (start) REXLOG_INFO("[fps] back to 60 after {:.2f} s", (Wwe2k17NowUs() - start) / 1e6);
  g_scene_at60.store(false, std::memory_order_relaxed);
  __imp__sub_82A94B48(ctx, base);
}

// Once per guest present (render_hooks.cpp): while a scene runs at 60 instead of the game's 30, give it up if this PC
// cannot hold 60 in it.
void Wwe2k17FramerateOnPresent(uint64_t now_us) {
  static uint64_t last = 0, sum = 0;
  static uint32_t frames = 0, slow = 0;
  if (!g_scene_at60.load(std::memory_order_relaxed)) {
    last = sum = 0, frames = slow = 0;
    return;
  }
  if (last) {
    sum += now_us - last;
    ++frames;
  }
  last = now_us;
  if (frames < 90) return;
  const double avg_ms = sum / 1000.0 / frames;
  sum = 0, frames = 0;
  slow = avg_ms > REXCVAR_GET(wwe2k17_60fps_auto_ms) ? slow + 1 : 0;
  if (slow < 2 || !REXCVAR_GET(wwe2k17_60fps_auto) || !g_drop_base) return;
  g_scene_at60.store(false, std::memory_order_relaxed);
  g_auto_off.store(true, std::memory_order_relaxed);
  PPCContext c = g_drop_ctx;
  __imp__sub_82A94AA8(c, g_drop_base);
  REXLOG_INFO("[fps] scene cannot hold 60 fps ({:.1f} ms per frame): the game's 30 fps is used for scenes from now on",
              avg_ms);
}
