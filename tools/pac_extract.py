"""Unpack WWE 2K17 (Xbox 360) .pac archives into a folder tree, for the launcher artwork.

Layers handled: SHDC containers (children table), BPE blocks (Yuke's byte-pair coding, decoder after
tools/yukes_bpe.py of WWE12-PC) and nested containers. Leaves are written as-is, named by their id and
a guessed type. Output goes under work/ (ignored by Git): never commit game data.

    python -I tools/pac_extract.py game/pac/_startup.pac work/pac_dump/_startup
"""
from __future__ import annotations

import os
import struct
import sys


def bpe_expand(src: bytes, expected: int, big: bool) -> bytes:
    out = bytearray()
    pos = 0
    n = len(src)
    left = list(range(256))
    right = [0] * 256
    while pos < n and len(out) < expected:
        for c in range(256):
            left[c] = c
        c = 0
        while True:
            count = src[pos]; pos += 1
            if count > 127:
                c += count - 127
                count = 0
            if c == 256:
                break
            for _ in range(count + 1):
                left[c] = src[pos]; pos += 1
                if left[c] != c:
                    right[c] = src[pos]; pos += 1
                c += 1
            if c == 256:
                break
        size = ((src[pos] << 8) | src[pos + 1]) if big else (src[pos] | (src[pos + 1] << 8))
        pos += 2
        stack = []
        for _ in range(size):
            stack.append(src[pos]); pos += 1
            while stack:
                b = stack.pop()
                if left[b] == b:
                    out.append(b)
                    if len(out) > expected:
                        raise ValueError("overflow")
                else:
                    if len(stack) > 512:
                        raise ValueError("cycle")
                    stack.append(right[b])
                    stack.append(left[b])
    return bytes(out)


def bpe_decode(data: bytes) -> bytes | None:
    if data[:4] != b"BPE " or len(data) < 16:
        return None
    _ver, packed, unpacked = struct.unpack_from("<III", data, 4)
    src = data[16:16 + packed]
    for big in (False, True):
        try:
            out = bpe_expand(src, unpacked, big)
        except (IndexError, ValueError):
            continue
        if len(out) == unpacked:
            return out
    return None


def zlib_unwrap(d: bytes) -> bytes | None:
    """'ZLIB' wrapper: u32 0x1230, u32 packed, u32 size, then a zlib stream (header may have 'ZLIB' tag first)."""
    import zlib
    if d[:4] != b"ZLIB":
        return None
    for off in (0x10, 0xC, 0x8):
        try:
            return zlib.decompress(d[off:])
        except zlib.error:
            continue
    return None


def hspc_children(d: bytes):
    """HSPC (2K17): index at 0x800 {u64 hash, u32 0, u32 location offset, i32 -1}, locations {sector, size/0x100, flags}."""
    idx_bytes = struct.unpack_from("<I", d, 0x18)[0]
    loc_bytes = struct.unpack_from("<I", d, 0x20)[0]
    count = struct.unpack_from("<I", d, 0x38)[0]
    loc_base = (0x800 + idx_bytes + 0x7FF) & ~0x7FF
    for i in range(count):
        h, _z, loff, _m = struct.unpack_from("<QIIi", d, 0x800 + 20 * i)
        sector, size256, _fl = struct.unpack_from("<III", d, loc_base + loff)
        off = sector * 0x800
        yield f"{h:016x}", d[off:off + size256 * 0x100]


def kind(d: bytes) -> str:
    if d[:4] == b"HSPC":
        return "hspc"
    if d[:4] == b"SHDC":
        return "shdc"
    if d[:4] == b"BPE ":
        return "bpe"
    if d[:4] == b"DDS ":
        return "dds"
    if d[:8] == b"\x89PNG\r\n\x1a\n":
        return "png"
    return "bin"


def shdc_children(d: bytes):
    ti, tb = struct.unpack_from("<II", d, 0x1C)
    for i in range(tb // 16):
        cid, off, size, _ = struct.unpack_from("<IIII", d, ti + 16 * i)
        yield cid, d[off:off + size]


def walk(d: bytes, out_dir: str, name: str, depth: int = 0) -> None:
    for _ in range(4):  # unwrap BPE / ZLIB layers
        dec = bpe_decode(d) or zlib_unwrap(d)
        if dec is None:
            break
        d = dec
    k = kind(d)
    if k in ("shdc", "hspc") and depth < 6:
        sub = os.path.join(out_dir, name)
        os.makedirs(sub, exist_ok=True)
        kids = shdc_children(d) if k == "shdc" else hspc_children(d)
        for cid, child in kids:
            walk(child, sub, cid if isinstance(cid, str) else f"{cid:08x}", depth + 1)
        return
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, f"{name}.{k}"), "wb") as f:
        f.write(d)


def main() -> None:
    src, dst = sys.argv[1], sys.argv[2]
    with open(src, "rb") as f:
        data = f.read()
    walk(data, dst, os.path.splitext(os.path.basename(src))[0])


if __name__ == "__main__":
    main()
