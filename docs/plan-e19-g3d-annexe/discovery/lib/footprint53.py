"""footprint53.py: the REAL handler of opcode 0x82 (0x8003FE7C) -> HandleMapTriggerCommand (0x80034108) -> IncreaseHpMaxAndCreateEffect (0x800333AC)
run in the MIPS interpreter with an empty pool; prints the Result, the size returned, the sounds requested and the full RAM write footprint
(ranges outside the stack), so the plan can state exactly what sub-case 0x53 writes."""
import sys, os, struct
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
import fxm

HERO = fxm.HERO
M32 = 0xFFFFFFFF
PRNG = 0x80098708
PROG = 0x801F0000
PC_WORD = 0x801F0100
CODE = 0x801F0200
STATE = 0x801F0400
STACK_LO, STACK_HI = 0x801FF000, 0x80200000


def run(sub=0x53, hero=(100 << 16, 80 << 16, 3 << 16), hp=5, hpmax=7, seed=0xB017C93D, prefill=0):
    f = fxm.FxMachine(476)
    m = f.m
    sounds = []
    m.hooks[0x800490FC] = lambda mm: sounds.append(mm.r[4])
    for k, v in zip((0x114, 0x118, 0x11C), hero):
        m.ww(HERO + k, v & M32)
    m.ww(HERO + 0x14, hp)
    m.ww(HERO + 0x18, hpmax)
    m.ww(PRNG, seed)
    for _ in range(prefill):
        f.create_free(0, 14, 0, 0, 0, 0)
    # the opcode bytes [0x82, sub] then 0xFF
    code = bytes([0x82, sub, 0xFF])
    for i, c in enumerate(code):
        m.wb(CODE + i, c)
    m.ww(PC_WORD, CODE)
    m.ww(STATE + 0x2C, 7)          # Result before
    before = bytes(m.ram)
    size = m.call(0x8003FE7C, PROG, 0, PC_WORD, STATE) & 0xFFFFFFFF
    after = bytes(m.ram)
    return f, m, before, after, size, sounds


def ranges(before, after):
    out = []
    i = 0
    n = len(before)
    while i < n:
        if before[i] != after[i]:
            j = i
            while j < n and (before[j] != after[j] or (j + 1 < n and before[j + 1] != after[j + 1] and j - i < 64)):
                j += 1
            out.append((i, j))
            i = j
        else:
            i += 1
    return out


if __name__ == "__main__":
    f, m, before, after, size, sounds = run()
    print("size returned:", size, " Result written at STATE+0x2C:", m.rw(STATE + 0x2C), " sounds:", sounds)
    print("hero hp, hpmax:", m.rw(HERO + 0x14), m.rw(HERO + 0x18), " seed after: %08x" % m.rw(PRNG))
    rs = ranges(before, after)
    # classify
    pool_lo = (fxm.POOL & 0x1FFFFFFF)
    pool_hi = pool_lo + 128 * 0x80
    groups = {}
    for a, b in rs:
        addr = 0x80000000 | a
        if pool_lo <= a < pool_hi:
            slot = (a - pool_lo) // 0x80
            groups.setdefault("pool slot %d" % slot, []).append((addr, b - a))
        elif STACK_LO <= addr < STACK_HI:
            groups.setdefault("stack (scratch)", []).append((addr, b - a))
        else:
            groups.setdefault("other", []).append((addr, b - a))
    for k in sorted(groups, key=lambda s: (s.startswith("pool"), s)):
        if k.startswith("pool slot"):
            continue
        print(k, [(hex(a), n) for a, n in groups[k]])
    slots = sorted(int(k.split()[-1]) for k in groups if k.startswith("pool slot"))
    print("pool slots written:", slots)
    # which fields change per slot: show slot 0 and 8 (first spark)
    for sidx in (0, 8, 11):
        a0 = (fxm.POOL & 0x1FFFFFFF) + 0x80 * sidx
        changed = [(hex(o), after[a0 + o:a0 + o + 4].hex()) for o in range(0, 0x80, 4) if before[a0 + o:a0 + o + 4] != after[a0 + o:a0 + o + 4]]
        print("slot", sidx, "changed words (offset, new):", changed)
