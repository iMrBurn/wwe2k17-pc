// wwe2k17 - "Xbox 360" in the game's texts becomes "PC" (WWE 2K15 PC's text_hook.cpp).
//
// The game's texts are UTF-8 in zlib blocks the game inflates itself. sub_82B5D9B8 is the title's zlib inflate (Title
// Update 1.04 address; it references "incorrect header check" at 0x82076718 and has zlib 1.2's inflate() shape: strm
// checks, state at +28, mode TYPE = 11; 2K15 TU4: sub_82B529A0). sub_82BEB2B0, the other user of that string, is an
// older zlib 1.1 inflate of a library and is left alone. After each call the bytes it wrote (z_stream next_out at +12,
// before/after) are scanned for the console warnings of the five languages and rewritten in place. Replacements are
// shorter than what they replace: the rest of the string moves left and the freed bytes before its terminating NUL
// become NULs, so string offsets and table sizes stay valid. Xbox Live / Xbox Store texts are left as they are.

#include <rex/ppc.h>

#include <atomic>
#include <cstdint>
#include <cstring>
#include <string_view>

#include <rex/cvar.h>
#include <rex/logging/macros.h>

REXCVAR_DEFINE_BOOL(wwe2k17_pc_texts, true, "WWE2K17",
                    "Say PC instead of Xbox 360 console in the game's texts");

namespace {
// Big-endian 32-bit read of guest memory (REX_LOAD_U32 of older SDKs).
inline uint32_t LoadGuestU32(const uint8_t* base, uint32_t addr) {
  uint32_t v;
  std::memcpy(&v, base + addr, 4);
  return __builtin_bswap32(v);
}
}  // namespace
REX_EXTERN(__imp__sub_82B5D9B8);

namespace {

struct Rule {
  std::string_view from, to;
};

// Longest first: each language's whole phrase, then the bare names.
constexpr Rule kRules[] = {
    {"el sistema Consola Xbox 360", "tu PC"},     // es
    {"your Xbox 360 console", "your PC"},         // en
    {"votre Console Xbox 360", "votre PC"},       // fr
    {"votre console Xbox 360", "votre PC"},       // fr
    {"deine Xbox 360 Konsole", "deinen PC"},      // de
    {"tu Consola Xbox 360", "tu PC"},             // es
    {"la console Xbox 360", "il PC"},             // it
    {"Xbox 360 console", "PC"},
    {"Xbox 360 Konsole", "PC"},
    {"Consola Xbox 360", "PC"},
    {"Console Xbox 360", "PC"},
    {"console Xbox 360", "PC"},
    {"Xbox 360", "PC"},
};
constexpr std::string_view kKey = "Xbox 360";

// Rewrites the text strings in [p, end). Only touches a match whose string ends (NUL) in range.
// Returns the number of strings changed.
int Rewrite(char* p, char* end) {
  int changed = 0;
  char* hit = p;
  while (end - hit >= static_cast<ptrdiff_t>(kKey.size())) {
    hit = static_cast<char*>(std::memchr(hit, 'X', end - hit));
    if (!hit || end - hit < static_cast<ptrdiff_t>(kKey.size())) return changed;
    if (std::memcmp(hit, kKey.data(), kKey.size()) != 0) { ++hit; continue; }
    char* nul = static_cast<char*>(std::memchr(hit, 0, end - hit));
    if (!nul) return changed;
    // Try every rule whose "from" contains this occurrence of the key.
    bool done = false;
    for (const Rule& r : kRules) {
      const size_t key_at = r.from.find(kKey);
      if (hit - p < static_cast<ptrdiff_t>(key_at)) continue;
      char* start = hit - key_at;
      if (nul - start < static_cast<ptrdiff_t>(r.from.size()) ||
          std::memcmp(start, r.from.data(), r.from.size()) != 0)
        continue;
      char* tail = start + r.from.size();
      std::memcpy(start, r.to.data(), r.to.size());
      std::memmove(start + r.to.size(), tail, nul - tail);
      char* new_nul = start + r.to.size() + (nul - tail);
      std::memset(new_nul, 0, nul - new_nul);
      hit = start + r.to.size();
      done = true;
      ++changed;
      break;
    }
    if (!done) hit += kKey.size();
  }
  return changed;
}

}  // namespace

extern "C" REX_FUNC(sub_82B5D9B8) {
  const uint32_t strm = ctx.r3.u32;
  const uint32_t out_before = strm ? LoadGuestU32(base, strm + 12) : 0;  // z_stream.next_out
  __imp__sub_82B5D9B8(ctx, base);
  if (!strm || !REXCVAR_GET(wwe2k17_pc_texts)) return;
  const uint32_t out_after = LoadGuestU32(base, strm + 12);
  if (out_after <= out_before || out_after - out_before < kKey.size()) return;
  const int changed =
      Rewrite(reinterpret_cast<char*>(base + out_before), reinterpret_cast<char*>(base + out_after));
  static std::atomic<int> s_total{0};
  if (!changed) return;
  const int before = s_total.fetch_add(changed);
  if (before == 0 || before / 100 != (before + changed) / 100)
    REXLOG_INFO("Game texts: {} Xbox 360 mention(s) changed to PC so far", before + changed);
}
