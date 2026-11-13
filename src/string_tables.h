// wwe2k17 - Dumping the game's string tables, to look up the IDs a menu uses.
//
// The tables arrive inflated in guest memory (exit_menu.cpp's ZlibDecompress hook). With
// wwe2k17_dump_strings = true each one is written to userdata/strings/<n>.bin, in the same layout the
// game uses (header 0, count; then count entries of offset, length, id; then the UTF-8 text), so
// tools/list_strings.py can list them. Off by default: this is a development aid, not a game feature.
#pragma once

#include <cstdint>

namespace wwe2k17 {

// Writes the string table at `data` (its whole decompressed buffer, `size` bytes) to userdata/strings/
// when the cvar is on. Does nothing otherwise, and never throws.
void MaybeDumpStringTable(const uint8_t* data, uint32_t size);

}  // namespace wwe2k17
