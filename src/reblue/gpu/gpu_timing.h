// GPU time per render target, from timestamp queries (svr_gpu_timing, off by default): where a weak GPU's
// frame goes. Every 10 s one [gputime] line: GPU time per frame from the first to the last timestamp, and
// the targets (size and format) that took most of it. D3D12 (Vulkan's readings were wrong on an Intel iGPU; D3D11 has no query pool).
#pragma once

#include <rex/types.h>

namespace bd::gpu {
struct VideoState;
struct GuestTexture;

void GpuTimingFrameBegin(VideoState &s);  // command list (re)opened
void GpuTimingTarget(VideoState &s, const GuestTexture *rt, const GuestTexture *ds);  // new framebuffer
void GpuTimingMark(VideoState &s, const char *label);  // anything else worth its own line (present)
void GpuTimingCollect(VideoState &s, u32 slot);  // slot's fence waited: read and accumulate
}  // namespace bd::gpu
