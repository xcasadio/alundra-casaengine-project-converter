"""Critic: static reachability of `jal 0x8002ED64` from each handler of the main jump table 0x80022D3C (80 entries) and of the swim table 0x80022E7C."""
import struct
from c_exe import load
pc0, gp0, tbase, tsize, img = load()
def w(a): return struct.unpack_from("<I", img, a - tbase)[0]
TARGET = 0x8002ED64
END = 0x80032914
def reach(entry):
    seen = set(); stack = [entry]; hits = set()
    while stack:
        pc = stack.pop()
        while True:
            if pc in seen or pc == END or not (0x80031B70 <= pc < 0x80033000):
                break
            seen.add(pc)
            ins = w(pc); op = ins >> 26
            if op == 3:
                if (ins & 0x3FFFFFF) << 2 | 0x80000000 == TARGET:
                    hits.add(pc)
                pc += 4; continue           # a call returns (delay slot is executed: ignore it, it is next word)
            if op == 2:                      # j
                t = ((ins & 0x3FFFFFF) << 2) | 0x80000000
                # delay slot executes; follow target
                seen.add(pc + 4)
                pc = t; continue
            if op in (4, 5, 6, 7) or (op == 1):
                off = struct.unpack("<h", struct.pack("<H", ins & 0xFFFF))[0]
                stack.append(pc + 4 + (off << 2))
                pc += 4; continue
            if op == 0 and (ins & 63) == 8:  # jr
                break
            pc += 4
    return hits
main = [w(0x80022D3C + 4 * i) for i in range(80)]
swim = [w(0x80022E7C + 4 * i) for i in range(80)]
print("distinct handlers main:", sorted(set(hex(x) for x in main)))
res = {}
for s in range(80):
    res[s] = reach(main[s])
none = [s for s in range(80) if not res[s]]
print("main table: states whose handler can reach TryUseItem: %d ; none: %d" % (80 - len(none), len(none)))
print("none:", ["%02X" % s for s in none])
print("swim table handlers:", sorted(set(hex(x) for x in swim)))
resw = {s: reach(swim[s]) for s in range(80)}
nonew = [s for s in range(80) if not resw[s]]
print("swim table: no reachable TryUseItem for", ["%02X" % s for s in nonew])
