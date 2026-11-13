// wwe2k17 - Shadows option: the ring mat plane shadow pass can be turned off (WWE 2K16 PC's shadows.cpp).
//
// sub_82ACF100 is the ring mat plane shadow composite pass ('yplaneshadowmap'; WWE 2K16 TU3 sub_82B5F7F0, found in 2K17
// by instruction similarity: 1.00 with the same 2,764 instructions, runner-up 0.98 with 2,783). With wwe2k17_shadows
// off it is skipped (returns 0). The character depth shadow map passes stay: their buffer stays cleanly initialised,
// so no striped shadow acne on the wrestlers (2K16's finding). Saves GPU time on weak graphics chips.

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

#include <atomic>

REXCVAR_DEFINE_BOOL(wwe2k17_shadows, true, "WWE2K17", "Ring mat shadows (off = faster on weak graphics chips)");

REX_EXTERN(__imp__sub_82ACF100);

extern "C" REX_FUNC(sub_82ACF100) {
  if (!REXCVAR_GET(wwe2k17_shadows)) {
    static std::atomic_flag logged = ATOMIC_FLAG_INIT;
    if (!logged.test_and_set(std::memory_order_relaxed)) {
      REXLOG_INFO("[shadows] ring plane shadow pass off (wwe2k17_shadows = false)");
    }
    ctx.r3.s64 = 0;
    return;
  }
  __imp__sub_82ACF100(ctx, base);
}
