// Physical memory workaround for the Yuke's engine (WWE '13, 2K15, 2K16 and 2K17).
//
// The game allocates physical memory with an explicit range and checks that the result is not below
// the range's lower bound. The SDK's 4 KB physical heap starts 0x1000 above its base, so the kernel
// turns a [min, max] request into [min - 0x1000, max - 0x1000] and the game rejects the result:
// hundreds of "BaseHeap::Alloc failed to find contiguous range" at boot, then the game stalls.
// xenia-canary fixes it with ignore_offset_for_ranged_allocations; WWE 2K15 / 2K16 PC patched the SDK.
// The stock ReXGlue v0.10.1 has no such option, so this hook shifts the range up by the same offset
// before the call, which gives the kernel the bounds it would use with the offset ignored.

#include <rex/logging.h>
#include <rex/ppc.h>

#include <atomic>
#include <cstdint>

namespace {
std::atomic<int> g_logged{0};
}

// [[midasm_hook]] at 0x822516F0 (TU 1.04; 0x82241770 in TU2) (bl MmAllocatePhysicalMemoryEx in the game's physical allocator):
// r3 flags, r4 size, r5 protect, r6 min_addr_range, r7 max_addr_range, r8 alignment.
void Wwe2k17RangedPhysicalAllocHook(PPCRegister& r6, PPCRegister& r7) {
  const uint32_t min = r6.u32, max = r7.u32;
  if (min == 0 || max == 0) return;
  r6.u64 = min + 0x1000u;
  r7.u64 = max >= 0xFFFFF000u ? 0xFFFFFFFFu : max + 0x1000u;
  if (g_logged.fetch_add(1) < 4) {
    REXLOG_INFO("[mem] ranged physical alloc [{:#x}, {:#x}] -> [{:#x}, {:#x}]", min, max, r6.u32, r7.u32);
  }
}
