"""Minimal XDVDFS reader for Xbox 360 disc images.
usage: xiso_get.py <iso> <outdir> [name ...]   list the root, extract the named root files
       xiso_get.py <iso> <outdir> --all       extract the whole disc"""
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
    if size == 0:
        return []
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

def copy_out(f, base, sector, size, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    f.seek(base + sector * SECTOR)
    with open(path, "wb") as o:
        left = size
        while left:
            chunk = f.read(min(left, 1 << 22))
            if not chunk:
                raise SystemExit(f"truncated image while reading {path}")
            o.write(chunk)
            left -= len(chunk)

def extract_all(f, base, sector, size, outdir):
    count = 0
    for name, s, sz, attr in walk_dir(f, base, sector, size):
        path = os.path.join(outdir, name)
        if attr & 0x10:
            os.makedirs(path, exist_ok=True)
            count += extract_all(f, base, s, sz, path)
        else:
            copy_out(f, base, s, sz, path)
            count += 1
    return count

def main():
    iso, outdir, rest = sys.argv[1], sys.argv[2], sys.argv[3:]
    with open(iso, "rb") as f:
        base = find_partition(f)
        f.seek(base + 32 * SECTOR + 20)
        root_sector, root_size = struct.unpack("<II", f.read(8))
        if rest == ["--all"]:
            print(f"extracted {extract_all(f, base, root_sector, root_size, outdir)} files to {outdir}")
            return
        names = [n.lower() for n in rest]
        print(f"partition offset 0x{base:X}")
        for name, s, sz, attr in sorted(walk_dir(f, base, root_sector, root_size)):
            print(f"{'<DIR>' if attr & 0x10 else sz:>12}  {name}")
            if name.lower() in names and not attr & 0x10:
                copy_out(f, base, s, sz, os.path.join(outdir, name))
                print(f"  -> extracted {name}")

main()
