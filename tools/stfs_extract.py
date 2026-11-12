"""Extract files from an Xbox 360 STFS package (LIVE/PIRS/CON), e.g. a title update.
usage: stfs_extract.py <package> <outdir> [name ...]   (no names = list only)
Only reads files stored in consecutive blocks (what title updates use)."""
import os, struct, sys

BLOCK = 0x1000

def i24le(b, o):
    return b[o] | b[o + 1] << 8 | b[o + 2] << 16

class Stfs:
    def __init__(self, path):
        self.f = open(path, "rb")
        head = self.f.read(0x400)
        if head[:4] not in (b"LIVE", b"PIRS", b"CON "):
            raise SystemExit("not an STFS package")
        header_size = struct.unpack_from(">I", head, 0x340)[0]
        self.aligned = (header_size + 0xFFF) & ~0xFFF
        self.ft_blocks = struct.unpack_from("<H", head, 0x37C)[0]
        self.ft_start = i24le(head, 0x37E)
        flags = head[0x37B]
        # One hash-table block per table for 0xB000-aligned headers or when bit 0 is set, else two.
        self.shift = 0 if (self.aligned == 0xB000 or flags & 1) else 1

    def offset(self, block):
        adj = 0
        if block >= 0xAA:
            adj += ((block // 0xAA) + 1) << self.shift
        if block >= 0x70E4:
            adj += ((block // 0x70E4) + 1) << self.shift
        return self.aligned + ((1 << self.shift) + block + adj) * BLOCK

    def read_blocks(self, start, count, size):
        out = bytearray()
        for b in range(start, start + count):
            self.f.seek(self.offset(b))
            out += self.f.read(BLOCK)
        return bytes(out[:size])

    def entries(self):
        table = self.read_blocks(self.ft_start, self.ft_blocks, self.ft_blocks * BLOCK)
        for o in range(0, len(table), 0x40):
            e = table[o:o + 0x40]
            nlen = e[0x28] & 0x3F
            if nlen == 0:
                continue
            yield {
                "name": e[:nlen].decode("latin-1"),
                "dir": bool(e[0x28] & 0x80),
                "consecutive": bool(e[0x28] & 0x40),
                "blocks": i24le(e, 0x29),
                "start": i24le(e, 0x2F),
                "size": struct.unpack_from(">I", e, 0x34)[0],
                "parent": struct.unpack_from(">H", e, 0x32)[0],
                "index": o // 0x40,
            }

def extract_all(s, outdir):
    entries = {e["index"]: e for e in s.entries()}
    def rel(e):
        parts = [e["name"]]
        while e["parent"] != 0xFFFF:
            e = entries[e["parent"]]
            parts.append(e["name"])
        return os.path.join(*reversed(parts))
    count = 0
    for e in entries.values():
        path = os.path.join(outdir, rel(e))
        if e["dir"]:
            os.makedirs(path, exist_ok=True)
            continue
        if not e["consecutive"]:
            raise SystemExit(f"{rel(e)}: fragmented file, not supported")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as o:
            o.write(s.read_blocks(e["start"], e["blocks"], e["size"]))
        count += 1
    print(f"extracted {count} files to {outdir}")

def main():
    pkg, outdir, names = sys.argv[1], sys.argv[2], [n.lower() for n in sys.argv[3:]]
    s = Stfs(pkg)
    if names == ["--all"]:
        return extract_all(s, outdir)
    for e in s.entries():
        print(f"{'<DIR>' if e['dir'] else e['size']:>10}  {e['name']}")
        if e["dir"] or e["name"].lower() not in names:
            continue
        if not e["consecutive"]:
            raise SystemExit(f"{e['name']}: fragmented file, not supported")
        os.makedirs(outdir, exist_ok=True)
        with open(os.path.join(outdir, e["name"]), "wb") as o:
            o.write(s.read_blocks(e["start"], e["blocks"], e["size"]))
        print(f"  -> extracted {e['name']}")

if __name__ == "__main__":
    main()
