"""Find functions of another recompiled WWE port in this one by instruction similarity.

Reads the generated code of both projects (one `// mnemonic operands` comment per PowerPC instruction),
keeps each function's mnemonic sequence, and for every reference function asked for, ranks this project's
functions in an address window by difflib similarity. Used to move the native renderer's D3D hook table
(src/native/wwe2k17_d3d_names.h) from WWE 2K16 to 2K17.

usage: match_functions.py <ref generated dir> <this generated dir> <lo hex> <hi hex> <names.h of the ref | NAME=ADDR,...>
"""
import difflib
import glob
import os
import re
import sys

FUNC = re.compile(r"^(?:DEFINE_REX_FUNC|PPC_FUNC_IMPL)\((?:__imp__)?sub_([0-9A-F]{8})\)")
INSN = re.compile(r"^\s*//\s*([a-z][a-z0-9.+-]*)")


def load(gen_dir, lo, hi):
    funcs = {}
    for path in glob.glob(os.path.join(gen_dir, "*recomp*.cpp")):
        cur = None
        with open(path, encoding="utf-8", errors="replace") as f:
            for line in f:
                m = FUNC.match(line)
                if m:
                    addr = int(m.group(1), 16)
                    cur = funcs.setdefault(addr, []) if lo <= addr < hi else None
                    continue
                if cur is not None:
                    m = INSN.match(line)
                    if m:
                        cur.append(m.group(1))
    return funcs


def main():
    ref_dir, this_dir, lo, hi, names_h = sys.argv[1], sys.argv[2], int(sys.argv[3], 16), int(sys.argv[4], 16), sys.argv[5]
    names = []
    if "=" in names_h:  # NAME=ADDR,NAME=ADDR on the command line
        for item in names_h.split(","):
            n, a = item.split("=")
            names.append((n, int(a.replace("sub_", ""), 16)))
    else:
        for line in open(names_h, encoding="utf-8", errors="replace"):
            m = re.match(r"#define\s+(D3D\w+)\s+sub_([0-9A-F]{8})", line)
            if m:
                names.append((m.group(1), int(m.group(2), 16)))
    ref_lo = min(a for _, a in names) - 0x10000
    ref_hi = max(a for _, a in names) + 0x10000
    ref = load(ref_dir, ref_lo, ref_hi)
    mine = load(this_dir, lo, hi)
    print(f"reference functions {len(ref)}, candidates {len(mine)}")
    for name, addr in names:
        seq = ref.get(addr)
        if not seq:
            print(f"{name:45s} ref sub_{addr:08X} not found in reference code")
            continue
        scored = []
        for a, s in mine.items():
            if abs(len(s) - len(seq)) > max(8, len(seq) // 2):
                continue
            sm = difflib.SequenceMatcher(None, seq, s, autojunk=False)
            if sm.real_quick_ratio() < 0.6 or sm.quick_ratio() < 0.6:
                continue
            scored.append((sm.ratio(), a, len(s)))
        scored.sort(reverse=True)
        best = scored[:2]
        txt = "  ".join(f"sub_{a:08X} {r:.2f} ({n})" for r, a, n in best) or "no match"
        print(f"{name:45s} ref sub_{addr:08X} ({len(seq)})  ->  {txt}")


main()
