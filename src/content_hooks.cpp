// Keep XamContentCreateEx's inputs alive until the SDK's deferred completion reads them.
//
// The title opens each DLC package with XamContentCreateEx("dlc0", &content_data, ..., overlapped). The
// root name is a buffer on the caller's stack. The stock SDK (v0.10.1) runs the overlapped call about
// 100 ms later on its dispatch thread and only then reads the root name and the XCONTENT_DATA from guest
// memory; by then the stack buffer has been reused, the name is garbage, the UTF-8 path helpers throw,
// and the game closes: "[FATAL] Dispatch thread: deferred completion threw 'Invalid UTF-8'" about
// 30 s after boot, with DLC installed only. (WWE 2K15 PC caught that exception in its patched SDK.)
//
// The hook copies both inputs into a ring of guest heap slots that stay valid, points r4 / r5 at the
// copies, and cleans the copied 42-byte file name (bytes after the first NUL become NUL, bytes that are
// not valid UTF-8 become '_'), so the SDK always sees the values the title passed.

#include <rex/logging.h>
#include <rex/ppc.h>
#include <rex/system/kernel_state.h>

#include <atomic>
#include <cstdint>
#include <cstring>
#include <mutex>

namespace {
constexpr uint32_t kRootSize = 0x40;          // root names are short ("dlc0", "save")
constexpr uint32_t kDataSize = 0x148;         // XCONTENT_AGGREGATE_DATA, what the SDK may read
constexpr uint32_t kSlotSize = kRootSize + kDataSize;
constexpr uint32_t kSlots = 64;               // far more than the calls in flight at once
constexpr uint32_t kFileNameOffset = 0x108;   // XCONTENT_DATA: device_id, content_type, display_name[128]
constexpr uint32_t kFileNameSize = 42;

std::mutex g_mutex;
uint32_t g_pool = 0;
uint32_t g_next = 0;
std::atomic<int> g_logged{0};

void CleanFileName(uint8_t* name) {
  size_t i = 0;
  while (i < kFileNameSize && name[i] != 0) {
    const uint8_t c = name[i];
    const size_t n = c < 0x80 ? 1 : (c & 0xE0) == 0xC0 ? 2 : (c & 0xF0) == 0xE0 ? 3 : (c & 0xF8) == 0xF0 ? 4 : 0;
    bool ok = n != 0 && i + n <= kFileNameSize && (n != 2 || c >= 0xC2);
    for (size_t k = 1; ok && k < n; ++k) ok = (name[i + k] & 0xC0) == 0x80;
    if (!ok) {
      name[i++] = '_';
      continue;
    }
    i += n;
  }
  for (; i < kFileNameSize; ++i) name[i] = 0;
}
}  // namespace

// [[midasm_hook]] at 0x82B8E734 (TU 1.04; 0x82B6084C in TU2) (bl XamContentCreateEx): r4 = root name, r5 = XCONTENT_DATA*.
void Wwe2k17ContentNameHook(PPCRegister& r4, PPCRegister& r5) {
  auto* memory = REX_KERNEL_MEMORY();
  uint32_t slot;
  {
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_pool == 0) {
      g_pool = memory->SystemHeapAlloc(kSlotSize * kSlots);
      if (g_pool == 0) {
        REXLOG_ERROR("[content] could not allocate the XamContentCreateEx copy buffers");
        return;
      }
    }
    slot = g_pool + (g_next++ % kSlots) * kSlotSize;
  }
  auto* root_copy = memory->TranslateVirtual<uint8_t*>(slot);
  auto* data_copy = memory->TranslateVirtual<uint8_t*>(slot + kRootSize);
  std::memset(root_copy, 0, kSlotSize);
  if (r4.u32) {
    const auto* root = memory->TranslateVirtual<const uint8_t*>(r4.u32);
    for (uint32_t i = 0; i + 1 < kRootSize && root[i]; ++i) root_copy[i] = root[i];
    r4.u64 = slot;
  }
  if (r5.u32) {
    // Only the 0x134-byte XCONTENT_DATA belongs to the title; the aggregate tail (xuid, title id) stays 0.
    std::memcpy(data_copy, memory->TranslateVirtual<const uint8_t*>(r5.u32), 0x134);
    CleanFileName(data_copy + kFileNameOffset);
    r5.u64 = slot + kRootSize;
  }
  if (g_logged.fetch_add(1) < 8) {
    REXLOG_INFO("[content] XamContentCreateEx '{}' '{}'", reinterpret_cast<const char*>(root_copy),
                reinterpret_cast<const char*>(data_copy + kFileNameOffset));
  }
}
