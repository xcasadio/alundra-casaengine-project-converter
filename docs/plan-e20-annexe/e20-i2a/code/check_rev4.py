"""Apply the revision 4 bounds of D-I2A-6 (t2) for W5-L0-wall-diag to the wired dump of T0 (c)."""
import re
import sys

path = sys.argv[1]
text = open(path, encoding="utf-8").read()
line_re = re.compile(r"w5/W5-L0-wall-diag tick (\d+) (pos_[xy]): dll (-?\d+), oracle (-?\d+)")
diffs = {}
for m in line_re.finditer(text):
    tick, col, dll, ora = int(m.group(1)), m.group(2), int(m.group(3)), int(m.group(4))
    diffs[(tick, col)] = dll - ora

def bound(tick, col):
    if tick == 45:
        return 1 if col == "pos_x" else 73872
    if tick >= 46:
        return 0 if col == "pos_x" else 149167
    return 0

bad = [(k, v) for k, v in sorted(diffs.items()) if abs(v) > bound(*k)]
ticks = sorted({t for t, _ in diffs})
print("distinct diff entries:", len(diffs), "ticks", ticks[0] if ticks else None, "..", ticks[-1] if ticks else None)
print("pos_x entries:", sorted((k, v) for k, v in diffs.items() if k[1] == "pos_x"))
print("max |dy| from 46:", max(abs(v) for (t, c), v in diffs.items() if c == "pos_y" and t >= 46))
print("out of the revision 4 bounds:", bad)
