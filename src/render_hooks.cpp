// wwe2k17 - render thread ring wait and present pacing (ported from WWE 2K15 PC, render_wait_hook.cpp and
// framerate_hook.cpp).
//
// The XDK Direct3D library has the same layout in 2K15 TU4, 2K16 TU3 and 2K17 TU 1.04, shifted by a constant:
// D3DDevice_Swap (the only VdSwap caller) is sub_82BADF20 in 2K15 TU4 and sub_82BB2488 here, so 2K17 =
// 2K15 + 0x4568. The predicted functions are function starts in the 2K17 code and call each other as in 2K15:
//   sub_82BB0C18  ring poll (2K15 sub_82BAC6B0): r3 == 1 while the GPU read pointer has not passed the target;
//                 on the console it burns cycles with db16cyc, which are no-ops here.
//   sub_82BB46E8, sub_82BB40E0  ring / fence waits that call the poll in a loop.
//   sub_82BB2488  D3DDevice_Swap: once per guest present.
//
// Without the yield the render thread spins a whole core while it waits for the host command processor
// (2K15: ~92 % of a core, the whole game 200 % -> 115 % with it, same pacing). With it, after a 150 us spin
// the thread waits in 250 us slices. Every 5 s the log gets "Guest pacing" (presents per second, worst gap,
// share of time spent waiting for the GPU ring).

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#endif

#include <intrin.h>

#include <algorithm>
#include <atomic>
#include <cstdint>

REXCVAR_DEFINE_BOOL(wwe2k17_render_wait_yield, true, "WWE2K17",
                    "Let the render thread sleep briefly while waiting for the GPU ring");

REX_EXTERN(__imp__sub_82BB0C18);
#if !defined(SVR_NATIVE_RENDERER)
REX_EXTERN(__imp__sub_82BB2488);
#endif

namespace {

constexpr double kSpinUs = 150.0;
constexpr double kSleepUs = 250.0;
constexpr double kNewWaitGapUs = 2000.0;

uint64_t NowUs() {
  static const double us_per_tick = [] {
    LARGE_INTEGER f;
    QueryPerformanceFrequency(&f);
    return 1e6 / static_cast<double>(f.QuadPart);
  }();
  LARGE_INTEGER c;
  QueryPerformanceCounter(&c);
  return static_cast<uint64_t>(c.QuadPart * us_per_tick);
}

struct WaitState {
  uint64_t last_call = 0;
  uint64_t wait_start = 0;
  HANDLE timer = nullptr;
};
thread_local WaitState t_state;
thread_local bool t_in_wait = false;
thread_local uint64_t t_wait_begin = 0;
std::atomic<uint64_t> g_gpu_wait_us{0};

void SleepSlice(WaitState& s) {
  if (!s.timer) {
    s.timer = CreateWaitableTimerExW(nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
  }
  if (s.timer) {
    LARGE_INTEGER due;
    due.QuadPart = -static_cast<LONGLONG>(kSleepUs * 10.0);  // 100 ns units, relative
    if (SetWaitableTimer(s.timer, &due, 0, nullptr, nullptr, FALSE)) {
      WaitForSingleObject(s.timer, 5);
      return;
    }
  }
  SwitchToThread();
}

struct PaceStats {
  uint64_t window_start = 0, last = 0, max_gap = 0, wait_at_start = 0;
  uint32_t frames = 0;
} g_pace;

}  // namespace

// Ring poll.
extern "C" REX_FUNC(sub_82BB0C18) {
  __imp__sub_82BB0C18(ctx, base);
  const bool waiting = ctx.r3.u32 == 1;
  if (waiting && !t_in_wait) {
    t_in_wait = true;
    t_wait_begin = NowUs();
  } else if (!waiting && t_in_wait) {
    t_in_wait = false;
    g_gpu_wait_us.fetch_add(NowUs() - t_wait_begin, std::memory_order_relaxed);
  }
  if (!waiting || !REXCVAR_GET(wwe2k17_render_wait_yield)) return;
  WaitState& s = t_state;
  const uint64_t now = NowUs();
  if (now - s.last_call > kNewWaitGapUs) s.wait_start = now;  // a new wait began
  s.last_call = now;
  if (now - s.wait_start > kSpinUs) {
    SleepSlice(s);
  } else {
    _mm_pause();
  }
}

// Once per guest present: pacing log every 5 s. Called by the Swap hook below (wwe2k17.exe) or by the native renderer
// for each presented frame (wwe2k17_native.exe, src/native/wwe2k17_native_stubs.cpp: re:Blue owns Swap there).
void Wwe2k17PacingOnPresent() {
  const uint64_t now = NowUs();
  PaceStats& p = g_pace;
  if (!p.window_start) {
    p.window_start = p.last = now;
    p.wait_at_start = g_gpu_wait_us.load();
  } else {
    p.max_gap = std::max(p.max_gap, now - p.last);
    p.last = now;
    ++p.frames;
    const uint64_t span = now - p.window_start;
    if (span >= 5000000) {
      const uint64_t wait = g_gpu_wait_us.load() - p.wait_at_start;
      REXLOG_INFO("Guest pacing: {} frames in {:.1f} s ({:.1f} fps), max gap {:.1f} ms, GPU-ring wait {:.0f}%",
                  p.frames, span / 1e6, p.frames * 1e6 / span, p.max_gap / 1000.0, 100.0 * wait / span);
      p = PaceStats{};
      p.window_start = p.last = now;
      p.wait_at_start = g_gpu_wait_us.load();
    }
  }
}

#if !defined(SVR_NATIVE_RENDERER)
// D3DDevice_Swap.
extern "C" REX_FUNC(sub_82BB2488) {
  Wwe2k17PacingOnPresent();
  __imp__sub_82BB2488(ctx, base);
}
#endif
