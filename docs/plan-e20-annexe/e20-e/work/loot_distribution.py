"""Exact distribution of ChooseRandomlyAnItem(0xFF) (0x80032968) from the table at 0x80028E0C of ALUN_CD.EXE. Read-only."""
import collections, exelib as e
T = 0x80028E0C
def row(code): return [e.u8(T + (code & 0x7F) * 16 + i) for i in range(16)]
dist = collections.Counter(); draws = collections.Counter()
for n1 in range(16):
    c = row(0xFF)[n1]
    if c & 0x80 and c < 0x100:
        for n2 in range(16):
            c2 = row(c)[n2]
            item = c2 if (c2 < 0x62 and not (c2 & 0x80)) else 0
            dist[item] += 1; draws[2] += 1
    else:
        dist[c if c < 0x62 else 0] += 16; draws[1] += 16
tot = 256
for it, n in sorted(dist.items()):
    print(f"item {it:#04x}: {n}/256 = {100*n/tot:.3f}%")
print("draws: 1 draw in", draws[1], "/256 (", 100*draws[1]/256, "%), 2 draws in", draws[2], "/256 (", 100*draws[2]/256, "%)")
