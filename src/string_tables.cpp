#include "string_tables.h"

#include <atomic>
#include <cstdio>
#include <filesystem>
#include <string>

#include <rex/cvar.h>
#include <rex/logging/macros.h>

REXCVAR_DEFINE_BOOL(wwe2k17_dump_strings, false, "WWE2K17",
                    "Development aid: write each decompressed string table to userdata/strings");

namespace wwe2k17 {
namespace {
std::atomic<uint32_t> g_next_index{0};
}  // namespace

void MaybeDumpStringTable(const uint8_t* data, uint32_t size) {
  if (!REXCVAR_GET(wwe2k17_dump_strings) || !data || size == 0) return;
  std::error_code ec;
  const std::filesystem::path dir = std::filesystem::path("userdata") / "strings";
  std::filesystem::create_directories(dir, ec);
  const uint32_t index = g_next_index.fetch_add(1);
  const std::filesystem::path path = dir / (std::to_string(index) + ".bin");
  if (std::FILE* f = std::fopen(path.string().c_str(), "wb")) {
    std::fwrite(data, 1, size, f);
    std::fclose(f);
    REXLOG_INFO("[strings] table {} written ({} bytes)", index, size);
  }
}

}  // namespace wwe2k17
