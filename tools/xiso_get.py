"""Minimal XDVDFS reader: list the root of an Xbox 360 ISO and extract chosen root files.
usage: xiso_get.py <iso> <outdir> [name ...]   (no names = list only)"""
import os, struct, sys

MAGIC = b"MICROSOFT*XBOX*MEDIA"
SECTOR = 2048

def find_partition(f):
    for off in (0, 0x2080000, 0xFD90000, 0x18300000):
        f.seek(off + 32 * SECTOR)
        if f.read(20) == MAGIC:
            return off
    raise SystemExit("no XDVDFS volume found")

def walk_dir(f, base, sector, size):
    f.seek(base + sector * SECTOR)
    data = f.read(size)
    out, stack = [], [0]
    while stack:
        o = stack.pop() * 4
        if o + 14 > len(data):
            continue
        left, right, s, sz, attr, nlen = struct.unpack_from("<HHIIBB", data, o)
        if left == 0xFFFF:
            continue
        name = data[o + 14:o + 14 + nlen].decode("latin-1")
        out.append((name, s, sz, attr))
        if left:
            stack.append(left)
        if right:
            stack.append(right)
    return out

def main():
    iso, outdir, names = sys.argv[1], sys.argv[2], [n.lower() for n in sys.argv[3:]]
    with open(iso, "rb") as f:
        base = find_partition(f)
        f.seek(base + 32 * SECTOR + 20)
        root_sector, root_size = struct.unpack("<II", f.read(8))
        print(f"partition offset 0x{base:X}")
        for name, s, sz, attr in sorted(walk_dir(f, base, root_sector, root_size)):
            kind = "<DIR>" if attr & 0x10 else f"{sz:>12}"
            print(f"{kind}  {name}")
            if name.lower() in names and not attr & 0x10:
                os.makedirs(outdir, exist_ok=True)
                f.seek(base + s * SECTOR)
                with open(os.path.join(outdir, name), "wb") as o:
                    left = sz
                    while left:
                        chunk = f.read(min(left, 1 << 20))
                        o.write(chunk)
                        left -= len(chunk)
                print(f"  -> extracted {name}")

main()
