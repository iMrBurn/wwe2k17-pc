// wwe2k17 - development aid: which big single-channel textures a frame binds, in the Xenos build.
//
// A Bink movie is drawn from planes (Y, two chroma, alpha) held in two sets used in turn, and what the picture shows
// is which set a draw binds. wwe2k17_native.exe logs that from its renderer ("[texbind] f<frame> va 0x<texture>",
// native/svr_resources.cpp). In wwe2k17.exe the renderer is the Xbox GPU emulation and does not see the draws, so the
// same line is written from the game's own D3DDevice_SetTexture, once per texture per frame. Both logs then read
// with tools/flip_cadence.py, which says how long each movie frame was held (2 render frames is right for a 30 Hz
// movie at 60 fps; 1 or 3 is the judder).
//
// Only k_8 textures of 128x128 and more are logged: the planes of a movie, and little else. Each line also names the
// sampler and the guest code that bound it (the return address), so two players feeding the same screen can be told
// apart.
// Off by default (wwe2k17_log_movie_binds). Compiled into every exe but only active in the Xenos one: in the native
// builds the same function is already wrapped by the renderer's hook (src/reblue/gpu/hooks/state.cpp).

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

#include <atomic>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <unordered_map>

REXCVAR_DEFINE_BOOL(wwe2k17_log_movie_binds, false, "WWE2K17",
                    "Development aid (wwe2k17.exe): log the big single-channel textures each frame binds, to read "
                    "a movie's cadence with tools/flip_cadence.py");

uint64_t Wwe2k17PresentCount();  // render_hooks.cpp

#if !defined(SVR_NATIVE_RENDERER)

REX_EXTERN(__imp__sub_82BA8E58);  // D3DDevice_SetTexture(device r3, sampler r4, texture r5)

namespace {

inline uint32_t LoadGuestU32(const uint8_t* base, uint32_t addr) {
  uint32_t v;
  std::memcpy(&v, base + addr, 4);
  return __builtin_bswap32(v);
}

std::mutex g_mutex;
std::unordered_map<uint64_t, uint64_t> g_last_frame;  // (texture, sampler, caller) -> frame it was last logged in
uint32_t g_logged = 0;

}  // namespace

extern "C" REX_FUNC(sub_82BA8E58) {
  if (REXCVAR_GET(wwe2k17_log_movie_binds) && ctx.r5.u32) {
    const uint32_t tex = ctx.r5.u32;
    // D3DTexture: 24 bytes of D3DResource, MipFlush, then the 6-dword fetch constant at +0x1C. Dword 1 low 6 bits
    // are the format (2 = k_8), dword 2 holds width-1 in bits 0-12 and height-1 in bits 13-25.
    const uint32_t d1 = LoadGuestU32(base, tex + 0x1C + 4);
    const uint32_t d2 = LoadGuestU32(base, tex + 0x1C + 8);
    const uint32_t width = (d2 & 0x1FFF) + 1, height = ((d2 >> 13) & 0x1FFF) + 1;
    if ((d1 & 0x3F) == 2 && width >= 128 && height >= 128) {
      const uint64_t frame = Wwe2k17PresentCount();
      const uint32_t sampler = ctx.r4.u32, caller = static_cast<uint32_t>(ctx.lr);
      const uint64_t key = (uint64_t(tex) << 32) ^ (uint64_t(sampler) << 24) ^ caller;
      std::lock_guard lock(g_mutex);
      auto [it, fresh] = g_last_frame.try_emplace(key, frame);
      if (fresh || it->second != frame) {
        it->second = frame;
        REXLOG_INFO("[texbind] f{} va 0x{:08X} {}x{} base 0x{:08X} s{} lr 0x{:08X}", frame, tex, width, height,
                    d1 & 0xFFFFF000u, sampler, caller);
        ++g_logged;
      }
    }
  }
  __imp__sub_82BA8E58(ctx, base);
}

#endif
