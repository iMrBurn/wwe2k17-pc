// wwe2k17 - Main Menu EXIT option with a quit confirmation, and the retired Xbox Live entry and notice removed.
// Port of WWE 2K16 PC's exit_menu.cpp (title update 3) to WWE 2K17 Title Update 1.04.
//
// Main Menu:
//   - "WWE SHOP" (string 35178, the same ID in 2K17's tables) becomes "EXIT" in the string tables (ZlibDecompress), and
//     its descriptor swaps places with the next one (OPTIONS), so EXIT is the last item.
//   - The "Xbox Live" item (string 35243) is removed: GetMenuItemDesc returns the menu's invalid sentinel descriptor
//     (menu_obj + 11480 in 2K17, +11472 in 2K16) and CheckMenuItemVisible answers -1.
//   - The "not connected to Xbox Live" notice (string 30041, or the ID whose text says so) is not shown.
// EXIT opens the game's own Yes / No dialog ("Are you sure you want to quit the game?"); Yes closes the window cleanly.
//
// 2K17 functions, found by instruction similarity with 2K16 TU3's (tools/match_functions.py; similarity, instructions):
//   sub_82B4A208  ZlibDecompress(dst r3, cap r4)              2K16 sub_82BBD158  1.00
//   sub_827ADE28  GetMenuItemDesc(menu r3, group r4, item r5) 2K16 sub_8281B388  1.00 (sentinel +11480 read from it)
//   sub_827AF0A0  CheckMenuItemVisible(menu, group, item)     2K16 sub_8281C6B0  0.95 (same arguments, checked)
//   sub_827AF8A0  ResolveMenuAction(menu, group, item)        2K16 sub_8281CE98  0.79 (same arguments, checked)
//   sub_827AFB80  ConfirmCheckAndPopup(menu, group, item)     2K16 sub_8281D090  1.00
//   sub_8287C738  InitDialogReq(req)                          2K16 sub_828F3568  0.98 (same fields, 60 bytes now)
//   sub_8287CFC8  ShowDialog(mgr r3, req r4)                  2K16 sub_828F3DA0  0.99
//   sub_8287ED20  EnsureDialogManager                         2K16 sub_828F5A38  1.00 (global 0x8379A62C read from it)
//   sub_8287E1D0  PollDialogResult                            2K16 sub_828F4EF8  1.00
//   sub_8287E2B8  DialogUpdate                                2K16 sub_828F4FE0  1.00
//   sub_8287DB48  GetDialogResult                             2K16 sub_828F48E8  1.00
//   sub_8287C790  ResetDialog                                 2K16 sub_828F35C0  1.00
// Left out: 2K16's SetupMenuSlot brightness override for the EXIT row (its slot layout changed in 2K17, 0.85).
// The "Xbox 360" -> "PC" texts are done by text_hooks.cpp. Off with wwe2k17_exit_menu = false.

#include <atomic>
#include <cstdint>
#include <cstring>
#include <string_view>

#include <rex/cvar.h>
#include <rex/logging/macros.h>
#include <rex/memory.h>
#include <rex/ppc.h>
#include <rex/runtime.h>
#include <rex/ui/window.h>
#include <rex/ui/windowed_app_context.h>

#include "string_tables.h"

REXCVAR_DEFINE_BOOL(wwe2k17_exit_menu, true, "WWE2K17",
                    "Main Menu: EXIT instead of WWE SHOP (with a quit confirmation), no Xbox Live entry or notice");

REX_EXTERN(__imp__sub_82B4A208);  // ZlibDecompress
REX_EXTERN(__imp__sub_827ADE28);  // GetMenuItemDesc
REX_EXTERN(__imp__sub_827AF0A0);  // CheckMenuItemVisible
REX_EXTERN(__imp__sub_827AF8A0);  // ResolveMenuAction
REX_EXTERN(__imp__sub_827AFB80);  // ConfirmCheckAndPopup
REX_EXTERN(sub_8287C738);         // InitDialogReq
REX_EXTERN(__imp__sub_8287CFC8);  // ShowDialog
REX_EXTERN(sub_8287ED20);         // EnsureDialogManager
REX_EXTERN(sub_8287E1D0);         // PollDialogResult
REX_EXTERN(__imp__sub_8287E2B8);  // DialogUpdate
REX_EXTERN(sub_8287DB48);         // GetDialogResult
REX_EXTERN(__imp__sub_8287C790);  // ResetDialog

namespace {

constexpr uint32_t kMainMenuXboxLiveStringId = 35243;      // "Xbox Live" on the Main Menu: removed
constexpr uint32_t kWweShopStringId = 35178;               // "WWE SHOP" on the Main Menu: now "EXIT"
constexpr uint32_t kXboxLiveOfflineNoticeStringId = 30041;  // "You are currently not connected to Xbox Live..."
constexpr uint32_t kSentinelOffset = 11480;                // menu_obj + this = the invalid descriptor (2K16: 11472)
constexpr uint32_t kDialogMgr = 0x8379A62Cu;               // DialogManager singleton pointer (2K16: 0x837797EC)
constexpr char kExitConfirmationPrompt[] = "Are you sure you want to quit the game?";

std::atomic<bool> g_exit_dialog_pending{false};
std::atomic<uint32_t> g_xbox_live_alert_string_id{0};

inline uint32_t Ld32(const uint8_t* base, uint32_t addr) {
  uint32_t v;
  std::memcpy(&v, base + addr, 4);
  return __builtin_bswap32(v);
}
inline void St32(uint8_t* base, uint32_t addr, uint32_t value) {
  value = __builtin_bswap32(value);
  std::memcpy(base + addr, &value, 4);
}
inline uint32_t LoadLe32(const uint8_t* p) {
  return uint32_t(p[0]) | (uint32_t(p[1]) << 8) | (uint32_t(p[2]) << 16) | (uint32_t(p[3]) << 24);
}
inline void StoreLe32(uint8_t* p, uint32_t v) {
  p[0] = uint8_t(v), p[1] = uint8_t(v >> 8), p[2] = uint8_t(v >> 16), p[3] = uint8_t(v >> 24);
}

bool On() { return REXCVAR_GET(wwe2k17_exit_menu); }

bool IsXboxLiveOfflineMessage(const uint8_t* text, uint32_t length) {
  if (length && text[length - 1] == 0) --length;
  const std::string_view value(reinterpret_cast<const char*>(text), length);
  return value.find("not connected to Xbox Live") != std::string_view::npos ||
         value.find("related features won't be available") != std::string_view::npos;
}

uint32_t RawMenuItemDesc(PPCContext& ctx, uint8_t* base, uint32_t menu_obj, uint32_t group_id, uint32_t item_id) {
  const PPCContext saved = ctx;
  ctx.r3.u64 = menu_obj;
  ctx.r4.s64 = int32_t(group_id);
  ctx.r5.s64 = int32_t(item_id);
  __imp__sub_827ADE28(ctx, base);
  const uint32_t desc = ctx.r3.u32;
  ctx = saved;
  return desc;
}

uint32_t RawMenuItemStringId(uint8_t* base, uint32_t desc) {
  if (!desc || Ld32(base, desc) == 0xFFFFFFFFu) return 0;
  return Ld32(base, desc + 24);
}

// WWE SHOP's descriptor (renamed EXIT) goes last: it trades places with the next one (OPTIONS).
uint32_t GetSwappedMenuItemDesc(PPCContext& ctx, uint8_t* base, uint32_t menu_obj, uint32_t group_id,
                                uint32_t item_id) {
  const uint32_t desc = RawMenuItemDesc(ctx, base, menu_obj, group_id, item_id);
  const uint32_t string_id = RawMenuItemStringId(base, desc);
  if (string_id == kWweShopStringId && item_id < 32) {
    const uint32_t candidate = RawMenuItemDesc(ctx, base, menu_obj, group_id, item_id + 1);
    const uint32_t candidate_id = RawMenuItemStringId(base, candidate);
    if (candidate_id && candidate_id != kWweShopStringId && candidate_id != kMainMenuXboxLiveStringId) return candidate;
  }
  if (item_id > 0) {
    const uint32_t previous = RawMenuItemDesc(ctx, base, menu_obj, group_id, item_id - 1);
    if (RawMenuItemStringId(base, previous) == kWweShopStringId) return previous;
  }
  return desc;
}

uint32_t GetMenuItemStringId(PPCContext& ctx, uint8_t* base, uint32_t menu_obj, uint32_t group_id, uint32_t item_id) {
  return RawMenuItemStringId(base, GetSwappedMenuItemDesc(ctx, base, menu_obj, group_id, item_id));
}

// The row's action is resolved apart from its descriptor: apply the same swap there.
uint32_t GetActionItemIndex(PPCContext& ctx, uint8_t* base, uint32_t menu_obj, uint32_t group_id, uint32_t item_id) {
  if (group_id != 1) return item_id;
  const uint32_t id = RawMenuItemStringId(base, RawMenuItemDesc(ctx, base, menu_obj, group_id, item_id));
  if (id == kWweShopStringId && item_id < 32) return item_id + 1;
  if (item_id > 0) {
    const uint32_t previous = RawMenuItemStringId(base, RawMenuItemDesc(ctx, base, menu_obj, group_id, item_id - 1));
    if (previous == kWweShopStringId) return item_id - 1;
  }
  return item_id;
}

struct ExitDialogGuestStorage {
  uint32_t req_ea = 0;
  uint32_t text_ea = 0;
};

ExitDialogGuestStorage GetExitDialogGuestStorage() {
  static ExitDialogGuestStorage s{};
  if (!s.req_ea) {
    auto* rt = rex::Runtime::instance();
    if (rt && rt->memory()) {
      s.req_ea = rt->memory()->SystemHeapAlloc(64, 16);
      s.text_ea = rt->memory()->SystemHeapAlloc(sizeof(kExitConfirmationPrompt), 16);
      if (s.text_ea && rt->virtual_membase())
        std::memcpy(rt->virtual_membase() + s.text_ea, kExitConfirmationPrompt, sizeof(kExitConfirmationPrompt));
    }
  }
  return s;
}

void RequestCleanClose() {
  REXLOG_INFO("[menu] EXIT confirmed from the Main Menu: closing the window");
  if (auto* rt = rex::Runtime::instance(); rt && rt->app_context()) {
    rt->app_context()->CallInUIThreadDeferred([] {
      if (auto* runtime = rex::Runtime::instance(); runtime && runtime->display_window())
        runtime->display_window()->RequestClose();
    });
  }
}

}  // namespace

// --- String tables: "WWE SHOP" -> "EXIT" (and the ID of the Xbox Live offline notice) ------------------------------

extern "C" REX_FUNC(sub_82B4A208) {
  const uint32_t dst = ctx.r3.u32;
  const uint32_t dst_cap = ctx.r4.u32;
  __imp__sub_82B4A208(ctx, base);
  if (ctx.r3.u32 != 0 && dst != 0 && dst_cap >= 20) wwe2k17::MaybeDumpStringTable(base + dst, dst_cap);
  if (!On() || ctx.r3.u32 == 0 || dst == 0 || dst_cap < 20) return;
  uint8_t* const p = base + dst;
  if (LoadLe32(p) != 0) return;
  const uint32_t count = LoadLe32(p + 4);
  if (count < 1 || count > 50000) return;
  const uint64_t table_end = 8ull + uint64_t(count) * 12ull;
  if (table_end > dst_cap || LoadLe32(p + 8) != uint32_t(table_end)) return;
  for (uint32_t i = 0; i < count; ++i) {
    uint8_t* const entry = p + 8 + i * 12;
    const uint32_t offset = LoadLe32(entry + 0), length = LoadLe32(entry + 4), id = LoadLe32(entry + 8);
    if (offset < table_end || uint64_t(offset) + length > dst_cap || length == 0) continue;
    if (IsXboxLiveOfflineMessage(p + offset, length)) {
      uint32_t expected = 0;
      g_xbox_live_alert_string_id.compare_exchange_strong(expected, id);
    }
    if (id == kWweShopStringId && length >= 5) {
      std::memcpy(p + offset, "EXIT", 5);
      if (length > 5) std::memset(p + offset + 5, 0, length - 5);
      StoreLe32(entry + 4, 5);
      REXLOG_INFO("[menu] string {} (WWE SHOP) is now EXIT", id);
    }
  }
}

// --- The retired Xbox Live notice is not shown ----------------------------------------------------------------------

extern "C" REX_FUNC(sub_8287CFC8) {
  const uint32_t req_ea = ctx.r4.u32;
  const uint32_t message_id = req_ea ? Ld32(base, req_ea + 4) : 0;
  if (On() && message_id &&
      (message_id == kXboxLiveOfflineNoticeStringId || message_id == g_xbox_live_alert_string_id.load())) {
    REXLOG_INFO("[menu] Xbox Live offline notice not shown (string {})", message_id);
    ctx.r3.u64 = 0;
    return;
  }
  __imp__sub_8287CFC8(ctx, base);
}

// --- Main Menu entries -----------------------------------------------------------------------------------------------

extern "C" REX_FUNC(sub_827ADE28) {
  if (!On()) {
    __imp__sub_827ADE28(ctx, base);
    return;
  }
  const uint32_t menu_obj = ctx.r3.u32, group_id = ctx.r4.u32, item_id = ctx.r5.u32;
  const uint32_t desc = GetSwappedMenuItemDesc(ctx, base, menu_obj, group_id, item_id);
  if (RawMenuItemStringId(base, desc) == kMainMenuXboxLiveStringId) {
    ctx.r3.u64 = menu_obj + kSentinelOffset;  // no button, no cursor stop, no gap
    return;
  }
  ctx.r3.u64 = desc;
}

extern "C" REX_FUNC(sub_827AF0A0) {
  if (On()) {
    const uint32_t str_id = GetMenuItemStringId(ctx, base, ctx.r3.u32, ctx.r4.u32, ctx.r5.u32);
    if (str_id == kMainMenuXboxLiveStringId) {
      ctx.r3.s64 = -1;
      return;
    }
    if (str_id == kWweShopStringId) {  // EXIT always visible
      ctx.r3.u64 = kWweShopStringId;
      return;
    }
  }
  __imp__sub_827AF0A0(ctx, base);
}

extern "C" REX_FUNC(sub_827AF8A0) {
  if (On()) ctx.r5.u64 = GetActionItemIndex(ctx, base, ctx.r3.u32, ctx.r4.u32, ctx.r5.u32);
  __imp__sub_827AF8A0(ctx, base);
}

// --- EXIT pressed: the game's own Yes / No dialog --------------------------------------------------------------------

extern "C" REX_FUNC(sub_827AFB80) {
  const uint32_t menu_obj = ctx.r3.u32, group_id = ctx.r4.u32, item_id = ctx.r5.u32;
  if (!On()) {
    __imp__sub_827AFB80(ctx, base);
    return;
  }
  const uint32_t str_id = GetMenuItemStringId(ctx, base, menu_obj, group_id, item_id);
  if (str_id == kWweShopStringId || str_id == kMainMenuXboxLiveStringId) {
    if (g_exit_dialog_pending.load()) {
      ctx.r3.u64 = 1;
      return;
    }
    const PPCContext saved = ctx;
    ctx.r3.s64 = 0;
    ctx.r4.s64 = 64;
    sub_8287ED20(ctx, base);  // EnsureDialogManager
    const uint32_t dialog_mgr = Ld32(base, kDialogMgr);
    if (!dialog_mgr) {
      REXLOG_WARN("[menu] no DialogManager: closing without confirmation");
      RequestCleanClose();
      ctx = saved;
      ctx.r3.u64 = 1;
      return;
    }
    const auto storage = GetExitDialogGuestStorage();
    const uint32_t req_ea = storage.req_ea ? storage.req_ea : ((ctx.r1.u32 - 128) & ~15u);
    const uint32_t text_ea = storage.text_ea ? storage.text_ea : (req_ea + 64);
    std::memcpy(base + text_ea, kExitConfirmationPrompt, sizeof(kExitConfirmationPrompt));
    ctx.r3.u64 = req_ea;
    sub_8287C738(ctx, base);  // InitDialogReq
    St32(base, req_ea + 0, 2);             // Yes / No dialog
    St32(base, req_ea + 4, 30721);         // message ID (the custom text below is shown instead, as in 2K16)
    St32(base, req_ea + 8, text_ea);       // "Are you sure you want to quit the game?"
    St32(base, req_ea + 16, 0);
    St32(base, req_ea + 20, 0xFFFFFFFFu);  // any controller
    St32(base, req_ea + 28, 0);            // default: Yes
    ctx.r3.u64 = dialog_mgr;
    ctx.r4.u64 = req_ea;
    __imp__sub_8287CFC8(ctx, base);  // ShowDialog
    ctx = saved;
    g_exit_dialog_pending.store(true);
    REXLOG_INFO("[menu] quit confirmation shown");
    ctx.r3.u64 = 1;
    return;
  }
  ctx.r5.u64 = GetActionItemIndex(ctx, base, menu_obj, group_id, item_id);
  __imp__sub_827AFB80(ctx, base);
}

// Every frame while a dialog is up: Yes closes the game, No goes back to the Main Menu.
extern "C" REX_FUNC(sub_8287E2B8) {
  __imp__sub_8287E2B8(ctx, base);
  if (!g_exit_dialog_pending.load()) return;
  PPCContext poll = ctx;
  sub_8287E1D0(poll, base);  // PollDialogResult
  const int32_t status = poll.r3.s32;
  if (status == -2) return;  // still waiting
  g_exit_dialog_pending.store(false);
  if (const uint32_t dialog_mgr = Ld32(base, kDialogMgr)) {
    PPCContext reset = ctx;
    reset.r3.u64 = dialog_mgr;
    __imp__sub_8287C790(reset, base);  // ResetDialog
  }
  if (status == 1) {
    RequestCleanClose();
  } else {
    REXLOG_INFO("[menu] quit cancelled");
  }
}

// Dialog dismissed another way (B / back): Yes still closes.
extern "C" REX_FUNC(sub_8287C790) {
  const uint32_t dialog_mgr = ctx.r3.u32;
  if (g_exit_dialog_pending.exchange(false)) {
    PPCContext check = ctx;
    check.r3.u64 = dialog_mgr;
    sub_8287DB48(check, base);  // GetDialogResult
    const uint32_t result = check.r3.u32;
    const uint32_t raw_sel = dialog_mgr ? Ld32(base, dialog_mgr + 64) : 0xFFFFFFFFu;
    if (result == 1 || raw_sel == 0) {
      RequestCleanClose();
    } else {
      REXLOG_INFO("[menu] quit cancelled");
    }
  }
  __imp__sub_8287C790(ctx, base);
}
