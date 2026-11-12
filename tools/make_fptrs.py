"""Turn tools/find_functions.py output into config/functions_fptrs.toml and config/setjmp.toml.
Drops code-built addresses referenced 0x100 bytes or less before them: those are labels inside the
function that builds them (jump targets, local data), and declaring them would split it.
usage: make_fptrs.py <candidates.toml> <config dir>"""
import os, re, sys

src, cfg = sys.argv[1], sys.argv[2]
lines = open(src, encoding="utf-8").read().splitlines()
out = ["# Pointer-referenced functions the codegen did not generate (tools/find_functions.py work/image_tu.bin",
       "# --registered generated/default/wwe2k17_init.cpp, then tools/make_fptrs.py).", "", "[functions]"]
setjmp, kept, dropped, section, seen = [], 0, 0, "", set()
for line in lines:
    m = re.match(r"# (setjmp|longjmp)_address = (0x[0-9A-F]+)", line)
    if m:
        setjmp.append(f"{m.group(1)}_address = {m.group(2)}")
        continue
    if line.startswith("# ---"):
        section = line
        out.append(line)
        continue
    m = re.match(r"(0x[0-9A-F]+) = \{\}\s+# .*e\.g\. (0x[0-9A-F]+)", line)
    if not m:
        continue
    target, ref = int(m.group(1), 16), int(m.group(2), 16)
    if target in seen:
        continue
    seen.add(target)
    if "data pointers" not in section and 0 <= target - ref <= 0x100:
        dropped += 1
        continue
    out.append(line)
    kept += 1
open(os.path.join(cfg, "functions_fptrs.toml"), "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
open(os.path.join(cfg, "setjmp.toml"), "w", encoding="utf-8", newline="\n").write(
    "# The CRT's setjmp / longjmp of WWE 2K17 + Title Update 2 (jmp_buf in r3), found by tools/find_functions.py.\n"
    "# Recompiled as plain code they break error paths (libjpeg decodes, as in WWE 2K16); the codegen replaces calls.\n"
    + "\n".join(setjmp) + "\n")
print(f"kept {kept}, dropped {dropped} in-function labels, {len(setjmp)} setjmp/longjmp lines")
