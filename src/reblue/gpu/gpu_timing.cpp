// GPU time per render target (see gpu_timing.h).
#include "gpu/gpu_timing.h"

#include <algorithm>
#include <chrono>
#include <map>
#include <memory>
#include <string>
#include <vector>

#include <plume_render_interface.h>
#include <rex/cvar.h>

#include "core/logging.h"
#include "gpu/device.h"
#include "gpu/resources.h"

REXCVAR_DEFINE_BOOL(svr_gpu_timing, false, "SvR",
                    "Measure GPU time per render target with timestamp queries; one [gputime] log "
                    "line every 10 s (a diagnostic, off by default). Trust D3D12's numbers: on the "
                    "Intel Raptor Lake iGPU, Vulkan's came out ~60x too small (2026-11-17)");

namespace bd::gpu {
namespace {

constexpr u32 kMaxQueries = 512;

struct SlotQueries {
  std::unique_ptr<plume::RenderQueryPool> pool;
  std::vector<std::string> labels;  // label i: the work after timestamp i
  bool open = false;                // reset and first timestamp written this frame
};

struct Timing {
  bool tried = false;
  bool usable = false;
  SlotQueries slots[kNumFrames];
  std::map<std::string, double> ms;  // label -> GPU ms in the window
  double total_ms = 0;
  u32 frames = 0;
  u32 overflows = 0;
  std::chrono::steady_clock::time_point window_start{};
} g_timing;

bool Ready(VideoState &s) {
  auto &t = g_timing;
  if (!REXCVAR_GET(svr_gpu_timing) || !s.device || !s.command_list)
    return false;
  if (!t.tried) {
    t.tried = true;
    t.usable = true;
    for (auto &slot : t.slots) {
      slot.pool = s.device->createQueryPool(kMaxQueries);
      if (!slot.pool)
        t.usable = false;
    }
    BD_INFO("[gputime] timestamp queries {}", t.usable ? "on" : "not available on this backend");
    t.window_start = std::chrono::steady_clock::now();
  }
  return t.usable;
}

void Write(VideoState &s, std::string label) {
  auto &slot = g_timing.slots[s.frame.load(std::memory_order_relaxed)];
  if (!slot.open)
    return;
  if (slot.labels.size() >= kMaxQueries) {
    ++g_timing.overflows;
    return;
  }
  s.command_list->writeTimestamp(slot.pool.get(), u32(slot.labels.size()));
  slot.labels.push_back(std::move(label));
}

std::string FormatName(plume::RenderFormat f) {
  switch (f) {
  case plume::RenderFormat::R8G8B8A8_UNORM: return "rgba8";
  case plume::RenderFormat::B8G8R8A8_UNORM: return "bgra8";
  case plume::RenderFormat::R16G16B16A16_FLOAT: return "rgba16f";
  case plume::RenderFormat::R11G11B10_FLOAT: return "r11g11b10f";
  case plume::RenderFormat::R32_FLOAT: return "r32f";
  case plume::RenderFormat::R16G16_FLOAT: return "rg16f";
  case plume::RenderFormat::D32_FLOAT: return "d32";
  case plume::RenderFormat::D32_FLOAT_S8_UINT: return "d32s8";
  case plume::RenderFormat::D24_UNORM_S8_UINT: return "d24s8";
  case plume::RenderFormat::D16_UNORM: return "d16";
  default: return "fmt" + std::to_string(int(f));
  }
}

}  // namespace

void GpuTimingFrameBegin(VideoState &s) {
  if (!Ready(s))
    return;
  auto &slot = g_timing.slots[s.frame.load(std::memory_order_relaxed)];
  if (slot.open) {  // the frame's list reopened after a mid-frame submit: carry on
    Write(s, "(after mid-frame submit)");
    return;
  }
  s.command_list->resetQueryPool(slot.pool.get(), 0, kMaxQueries);
  slot.labels.clear();
  slot.open = true;
  Write(s, "(frame start, no target yet)");
}

void GpuTimingTarget(VideoState &s, const GuestTexture *rt, const GuestTexture *ds) {
  if (!g_timing.usable || !REXCVAR_GET(svr_gpu_timing))
    return;
  std::string label;
  if (rt)
    label = std::to_string(rt->width) + "x" + std::to_string(rt->height) + " " + FormatName(rt->format) +
            (rt->sampleCount != plume::RenderSampleCount::COUNT_1 ? " msaa" : "");
  else
    label = "(no colour)";
  if (ds)
    label += " + " + std::to_string(ds->width) + "x" + std::to_string(ds->height) + " " + FormatName(ds->format);
  Write(s, std::move(label));
}

void GpuTimingMark(VideoState &s, const char *label) {
  if (!g_timing.usable || !REXCVAR_GET(svr_gpu_timing))
    return;
  Write(s, label);
}

void GpuTimingCollect(VideoState &s, u32 slot_index) {
  auto &t = g_timing;
  if (!t.usable)
    return;
  auto &slot = t.slots[slot_index];
  if (!slot.open)
    return;
  slot.open = false;
  const size_t n = slot.labels.size();
  if (n >= 2) {
    slot.pool->queryResults();
    const u64 *ts = slot.pool->getResults();  // nanoseconds
    for (size_t i = 0; i + 1 < n; ++i) {
      if (ts[i + 1] >= ts[i])
        t.ms[slot.labels[i]] += double(ts[i + 1] - ts[i]) / 1e6;
    }
    if (ts[n - 1] >= ts[0])
      t.total_ms += double(ts[n - 1] - ts[0]) / 1e6;
    ++t.frames;
  }
  const auto now = std::chrono::steady_clock::now();
  if (now - t.window_start < std::chrono::seconds(10) || !t.frames)
    return;
  std::vector<std::pair<double, std::string>> top;
  for (auto &[label, ms] : t.ms)
    top.emplace_back(ms / t.frames, label);
  std::sort(top.begin(), top.end(), std::greater<>());
  std::string line;
  for (size_t i = 0; i < top.size() && i < 10; ++i)
    line += std::format(" | {:.2f} {}", top[i].first, top[i].second);
  BD_INFO("[gputime] {} frames: GPU {:.2f} ms per frame{}{}", t.frames, t.total_ms / t.frames, line,
          t.overflows ? std::format(" | {} timestamps dropped", t.overflows) : std::string());
  t.ms.clear();
  t.total_ms = 0;
  t.frames = 0;
  t.overflows = 0;
  t.window_start = now;
  (void)s;
}

}  // namespace bd::gpu
