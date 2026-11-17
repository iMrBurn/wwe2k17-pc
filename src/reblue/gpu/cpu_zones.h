// Where the render thread's time goes, per frame, in the [perf] log line (present.cpp).
// The game's draws, the constants, geometry and textures they need, and Present. Testers' logs
// then tell what to speed up on their phones (a profiler is out of their reach). Zones nest
// (constants are inside draws): each one is a total of its own. PC too (as WWE '12 PC 1.2): weak PCs
// are CPU bound and players' logs are all we get from them. Two clock reads per zone, well under 0.1 ms.
#pragma once

#include <atomic>
#include <chrono>
#include <cstdint>

namespace bd::gpu::zones {

// kGuestD3D: the game's own D3D library inside our hooked calls (it still writes its PM4 packets
// for the null guest GPU), the part a native path would remove (docs/couches.md).
enum Id { kDraws, kConstants, kGeometry, kTextures, kPresent, kGuestD3D, kCount };

inline std::atomic<uint64_t> g_ns[kCount];

struct Scope {
  Id id;
  std::chrono::steady_clock::time_point t0;
  explicit Scope(Id i) : id(i), t0(std::chrono::steady_clock::now()) {}
  ~Scope() {
    g_ns[id].fetch_add(uint64_t(std::chrono::duration_cast<std::chrono::nanoseconds>(
                                    std::chrono::steady_clock::now() - t0).count()),
                       std::memory_order_relaxed);
  }
};

}  // namespace bd::gpu::zones

#define WWE12_CPU_ZONE(id) ::bd::gpu::zones::Scope wwe12_cpu_zone_##id(::bd::gpu::zones::id)
