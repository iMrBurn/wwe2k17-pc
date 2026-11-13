// wwe2k17 - Universe mode: the Story Collection shows every story as played, with its clues revealed.
//
// How the game stores it (Title Update 1.04, found from the Story Collection's own texts, see
// tools/list_strings.py): the Universe save holds one 16-byte record per story, indexed by story id
// (sub_82A59478 = records + id * 16, up to 1024). Byte +0 is "played" (sub_82A59590) and byte +3 is
// set beside it (sub_82A596D8); the game's own "mark as played" (sub_82A59738) writes both.
//
// sub_82A609E0 builds one Story Collection row from a story definition and those records:
//   row +0  story id        +4/+8 definition fields   +36 the story has clues
//   row +12 played          (sub_82A59590, record byte +0)
//   row +16 record byte +2  +24 in list table 8   +28 in table 9
//   row +32 record byte +3  (sub_82A596D8)
// The clue text comes from sub_82A60AE8, which answers "???" (string 51760) unless row +12 is set.
//
// So this hook lets the game build the row and then sets +12 and +32, exactly the pair the game's own
// "mark as played" sets. It touches the row only, never the save: Universe still picks and schedules
// stories from the real records (sub_82A7CC20 looks for a story that has NOT been played), so nothing
// about how Universe runs changes and the save is left untouched.
// Off with wwe2k17_universe_stories_unlocked = false.

#include <atomic>
#include <cstdint>
#include <cstring>

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

REXCVAR_DEFINE_BOOL(wwe2k17_universe_stories_unlocked, true, "WWE2K17",
                    "Universe: every story in the Story Collection counts as played, clues revealed");

REX_EXTERN(__imp__sub_82A609E0);  // BuildStoryCollectionRow(row r3, definition r4)

namespace {

constexpr uint32_t kRowStoryId = 0;
constexpr uint32_t kRowPlayed = 12;
constexpr uint32_t kRowPlayedMark = 32;

std::atomic<uint32_t> g_rows_unlocked{0};

inline uint32_t Ld32(const uint8_t* base, uint32_t addr) {
  uint32_t v;
  std::memcpy(&v, base + addr, 4);
  return __builtin_bswap32(v);
}

inline void St32(uint8_t* base, uint32_t addr, uint32_t value) {
  value = __builtin_bswap32(value);
  std::memcpy(base + addr, &value, 4);
}

}  // namespace

extern "C" REX_FUNC(sub_82A609E0) {
  const uint32_t row = ctx.r3.u32;
  __imp__sub_82A609E0(ctx, base);
  if (!REXCVAR_GET(wwe2k17_universe_stories_unlocked) || row == 0) return;
  if (Ld32(base, row + kRowStoryId) == 0) return;  // no story in this row: leave it alone
  if (Ld32(base, row + kRowPlayed) != 0) return;   // already played: nothing to do
  St32(base, row + kRowPlayed, 1);
  St32(base, row + kRowPlayedMark, 1);
  const uint32_t n = g_rows_unlocked.fetch_add(1) + 1;
  if (n == 1 || n % 50 == 0) REXLOG_INFO("[universe] Story Collection: {} stories shown as played", n);
}
