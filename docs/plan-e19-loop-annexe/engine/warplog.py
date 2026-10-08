"""Read-only scan of the launcher log for world-change bursts (1 s timestamps, so bounds only).

For each "World change" line: the burst is the run of lines that follow it whose category is part of
the synchronous load in GameManager.UpdateWorld (entity destroyed / waiting / added, Load asset,
AlundraWorldProxy install messages) with no later 'gameplay' line in between. We print the first and
last timestamps of the burst, the number of lines and of "Load asset" lines, and the first line after
the burst. Duration bounds: [max(0, last-first-1), last-first+1] seconds.
"""
import re
import sys
from datetime import datetime

path = sys.argv[1]
lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
ts_re = re.compile(r"^(\d\d:\d\d:\d\d) ")


def ts(line):
    m = ts_re.match(line)
    return datetime.strptime(m.group(1), "%H:%M:%S") if m else None


LOAD_MARKERS = ("Entity destroyed", "Entity waiting", "Entity added", "Load asset", "World change")

rows = []
for i, line in enumerate(lines):
    if "World change:" not in line:
        continue
    t0 = ts(line)
    j = i + 1
    last_t = t0
    loads = 0
    while j < len(lines):
        l = lines[j]
        t = ts(l)
        if t is None:
            j += 1
            continue
        body = l[9:]
        is_load = any(m in body for m in LOAD_MARKERS)
        # the install messages of the DLL during LoadContent/BeginPlay are [Debug]/[Info]/[Trace]; keep
        # them in the burst while their timestamp does not jump by more than 1 s from the previous line.
        if not is_load and (t - last_t).total_seconds() > 1:
            break
        if not is_load and "departure" in body:
            break
        if "Load asset" in body:
            loads += 1
        last_t = t
        j += 1
        if j - i > 5000:
            break
    prev = lines[i - 1][:200] if i > 0 else ""
    nxt = lines[j][:160] if j < len(lines) else "<eof>"
    span = (last_t - t0).total_seconds()
    rows.append((i + 1, t0.strftime("%H:%M:%S"), last_t.strftime("%H:%M:%S"), span, j - i, loads, prev[9:120], nxt))

for r in rows:
    print(f"line {r[0]:5d}  {r[1]} -> {r[2]}  span {r[3]:.0f}s (bounds {max(0, r[3]-1):.0f}-{r[3]+1:.0f}s)  "
          f"lines {r[4]:4d}  loads {r[5]:4d}\n    before: {r[6]}\n    after : {r[7]}")
