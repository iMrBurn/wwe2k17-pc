"""List entries of the game's string tables dumped by wwe2k17_dump_strings (userdata/strings/*.bin).

Each table is: header (0, count), then count entries of (offset, length, id), then the UTF-8 text.

    python -I tools/list_strings.py userdata/strings "STORY|COLLECTION"
"""
from __future__ import annotations

import os
import re
import struct
import sys


def entries(data: bytes):
    if len(data) < 12 or struct.unpack_from("<I", data, 0)[0] != 0:
        return
    count = struct.unpack_from("<I", data, 4)[0]
    if not 0 < count < 200000 or 8 + count * 12 > len(data):
        return
    for i in range(count):
        off, ln, sid = struct.unpack_from("<III", data, 8 + i * 12)
        if off + ln > len(data) or ln == 0:
            continue
        yield sid, data[off:off + ln].split(b"\0")[0].decode("utf-8", "replace")


def main() -> None:
    root, pattern = sys.argv[1], sys.argv[2]
    max_len = int(sys.argv[3]) if len(sys.argv) > 3 else 80
    pat = re.compile(pattern, re.IGNORECASE)
    paths = [root] if os.path.isfile(root) else [os.path.join(root, f) for f in sorted(os.listdir(root))]
    seen = set()
    for path in paths:
        with open(path, "rb") as f:
            data = f.read()
        for sid, text in entries(data):
            if len(text) <= max_len and pat.search(text) and (sid, text) not in seen:
                seen.add((sid, text))
                print(f"{os.path.basename(path):>10}  {sid:6d}  {text!r}")


if __name__ == "__main__":
    main()
