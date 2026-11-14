// wwe2k17 - Universe mode: the Story Collection counts every story as played, with its clues revealed.
//
// How the game stores it (Title Update 1.04, found from the Story Collection's own texts, see
// tools/list_strings.py): the Universe save holds one 16-byte record per story, indexed by story id
// (sub_82A59478 = records + id * 16, up to 1024). Three queries read it, and everything the Story
// Collection shows is counted from them:
//   sub_82A59590  record byte +0  "played"      sub_82A59738 (the game's own "mark as played") sets +0 and +3
//   sub_82A595F0  record byte +2
//   sub_82A596D8  record byte +3
// sub_8281ACD8 builds the category screen (RIVALRIES 0/42, UNLOCKED CLUES 0/793) by calling the three
// for every story; sub_82A609E0 builds one story's row the same way (+12 played, +32 byte +3), and
// sub_82A60AE8 answers "???" (string 51760) for a clue unless that row's +12 is set.
//
// So the three queries answer yes while this is on. Byte +2 and byte +3 are read by the category screen
// and the row builder only, so those two are display and nothing else. "Played" has one caller that is
// not display: sub_82A7CC20 looks for a story that has NOT been played yet, to pick the next one to run;
// it keeps the real answer, otherwise Universe would never find a story to schedule again.
//
// Nothing is written: the save on disk is left exactly as it is, and turning this off brings the real
// progress back. Off with wwe2k17_universe_stories_unlocked = false.

#include <atomic>
#include <cstdint>

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/ppc.h>

REXCVAR_DEFINE_BOOL(wwe2k17_universe_stories_unlocked, true, "WWE2K17",
                    "Universe: every story in the Story Collection counts as played, clues revealed");

REX_EXTERN(__imp__sub_82A59590);  // IsStoryPlayed(records r3, story id r4)   record byte +0
REX_EXTERN(__imp__sub_82A595F0);  // record byte +2
REX_EXTERN(__imp__sub_82A596D8);  // record byte +3
REX_EXTERN(__imp__sub_82A7CC20);  // PickStoryNotPlayedYet(state r3)

namespace {

// Non-zero while the game is looking for a story that has not been played, where the real answer matters.
thread_local int t_in_story_picker = 0;

std::atomic<bool> g_said_it{false};

bool On() { return REXCVAR_GET(wwe2k17_universe_stories_unlocked); }

void SayItOnce() {
  if (!g_said_it.exchange(true))
    REXLOG_INFO("[universe] Story Collection: every story counts as played, clues revealed");
}

}  // namespace

extern "C" REX_FUNC(sub_82A7CC20) {
  ++t_in_story_picker;
  __imp__sub_82A7CC20(ctx, base);
  --t_in_story_picker;
}

extern "C" REX_FUNC(sub_82A59590) {
  __imp__sub_82A59590(ctx, base);
  if (On() && t_in_story_picker == 0 && ctx.r3.u32 == 0) {
    ctx.r3.u64 = 1;
    SayItOnce();
  }
}

extern "C" REX_FUNC(sub_82A595F0) {
  __imp__sub_82A595F0(ctx, base);
  if (On() && ctx.r3.u32 == 0) ctx.r3.u64 = 1;
}

extern "C" REX_FUNC(sub_82A596D8) {
  __imp__sub_82A596D8(ctx, base);
  if (On() && ctx.r3.u32 == 0) ctx.r3.u64 = 1;
}
