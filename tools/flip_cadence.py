"""How evenly a full-screen movie advances, read off a run log.

    tools\\run.ps1 --svr_log_texture_cadence=true     (then bring a full-screen movie on screen)
    python -I tools\flip_cadence.py logs\run-<time>.log

A movie player double buffers its planes, so what the picture shows is which set a draw binds, not
when the decoder's write was noticed. The log gives both: [texcad] for rebuilds, [texbind] for binds.
This script picks out the plane textures (rebuilt often, and every few frames rather than every
frame, which is what a render target the scene redraws looks like), then reports how long each movie
frame was held. At 60 fps a 30 Hz movie should hold every frame for exactly 2 render frames; a 1 or a
3 is one frame shown too short or too long, and that is what reads as judder.

Only meaningful on a screen where a full-screen movie plays and the 3D scene is not being redrawn -
an arena preview, a transition. On the attract loop the match itself redraws everything every frame
and the number is noise (it comes out at 3% correct whatever the renderer does).
"""
import re
import statistics
import sys
from collections import Counter, defaultdict


def main(path: str) -> int:
    rebuilt: dict[str, list[int]] = defaultdict(list)
    size: dict[str, int] = {}
    bound: dict[int, set[str]] = defaultdict(set)
    for line in open(path, encoding="utf-8", errors="replace"):
        m = re.search(r"\[texcad\] f(\d+) va (0x[0-9A-F]+) bytes (\d+)", line)
        if m:
            rebuilt[m.group(2)].append(int(m.group(1)))
            size[m.group(2)] = int(m.group(3))
            continue
        m = re.search(r"\[texbind\] f(\d+) va (0x[0-9A-F]+)", line)
        if m:
            bound[int(m.group(1))].add(m.group(2))
    if not bound:
        print("no [texbind] lines: run the game with --svr_log_texture_cadence=true")
        return 1

    planes = set()
    for va, frames in rebuilt.items():
        if len(frames) < 20:
            continue
        gaps = [b - a for a, b in zip(frames, frames[1:]) if b - a < 12]
        if gaps and 2 <= statistics.median(gaps) <= 6:
            planes.add(va)
    print(f"{len(planes)} plane-like textures, sizes {sorted({size[v] for v in planes})}")

    previous, changes = None, []
    for frame in sorted(bound):
        shown = tuple(sorted(bound[frame] & planes))
        if not shown:
            continue
        if shown != previous:
            changes.append(frame)
            previous = shown
    held = [b - a for a, b in zip(changes, changes[1:]) if b - a < 10]
    if not held:
        print("no full-screen movie in this capture")
        return 1
    even = sum(1 for h in held if h == 2)
    print(f"{len(held)} movie frames, {even} held the expected 2 render frames "
          f"= {100 * even / len(held):.0f}% even, {len(held) - even} held 1 or 3")
    print(f"held-for counts: {Counter(held).most_common(6)}")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        raise SystemExit(2)
    raise SystemExit(main(sys.argv[1]))
