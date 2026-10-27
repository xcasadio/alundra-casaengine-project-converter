"""c3_query.py - critic's own model of the target query 0x8003AF70 and the Hoare sort 0x8003AE04, run against the real code (cmips).
Seed 31337.  Models are transcribed from MY disassembly (cdis.py), not from the other agent's files.  Also measures the n=2 / n>=3 first-pick facts."""
import random, sys
from cmips import *

HERO = 0x80127D30
STR = 0x294
COUNT = 0x80134360
OUTP, OUTK = 0x800CC058, 0x800CC158


def sort_model(P, K, lo, hi):
    i, j = lo, hi
    pivot = ((K[lo] + K[hi]) & M) >> 1
    while True:
        while K[i] < pivot:
            i += 1
        while pivot < K[j]:
            j -= 1
        if i < j:
            K[i], K[j] = K[j], K[i]
            P[i], P[j] = P[j], P[i]
            i += 1
            j -= 1
            continue
        break
    if lo < i - 1:
        sort_model(P, K, lo, i - 1)
    if j + 1 < hi:
        sort_model(P, K, j + 1, hi)


def query_model(ram_get, caller, mask, atk, count, variant=None):
    """returns (n, P, K) after sort. ram_get(addr,size)->int"""
    P, K = [], []
    cx = cy = None
    rng = range(1, count) if variant not in ("inclusive", "corrected") else range(1, count + 1)
    for i in rng:
        e = HERO + STR * i
        if e == caller:
            continue
        st = ram_get(e + 0x10, 4)
        if variant == "status2only":
            if st != 2: continue
        elif (st - 2) & M >= 2:
            continue
        if ram_get(e + 0x20, 4) != 0 and variant != "ign20":
            continue
        if (ram_get(e + 0x6C, 4) & mask) == 0:
            continue
        if ram_get(e + 0x1D0, 4) != 0:
            continue
        if ram_get(e + 0x1CC, 4) != 0:
            continue
        if ram_get(e + 0xB4, 4) & 0x40:
            continue
        t = ram_get(e + 0x1C4, 4)
        if t == 0:
            continue
        if (ram_get((t + atk) & M + 2 if False else ((t + atk + 2) & M), 1) & 0xC0) == 0x80:
            continue
        if cx is None:
            cx, cy = s32(ram_get(caller + 0x114, 4)), s32(ram_get(caller + 0x118, 4))
        dx = s32((ram_get(e + 0x114, 4) - cx) & M) >> 16
        dy = s32((ram_get(e + 0x118, 4) - cy) & M) >> 16
        key = (dx * dx + dy * dy) & M
        P.append(e); K.append(key)
    n = len(P)
    thr = 3 if variant not in ("sort_from2", "corrected") else 2
    if n >= thr:
        sort_model(P, K, 0, n - 1)
    return n, P, K


def world(R, tie=True):
    ram = {}

    def p(a, v, n=4):
        for i in range(n):
            ram[a + i] = (v >> (8 * i)) & 0xFF
    count = R.choice([0, 1, 2, 3, 4, 6, 10, 20, 40, 63])
    p(COUNT, count)
    tab = 0x80220000
    for j in range(0, 64):
        ram[tab + j] = R.choice([0x00, 0x40, 0x80, 0xC0, 0x81, 0x3F, 0xBF, 0x80])
    grid = R.choice([1, 16, 200, 0x7000])
    span = R.choice([2, 3, 8, 40])
    for s in range(0, 64):
        e = HERO + STR * s
        pr = R.random()
        p(e + 0x10, R.choice([0, 1, 2, 2, 2, 3, 3, 4]) if pr < 0.9 else R.getrandbits(32))
        p(e + 0x20, R.choice([0, 0, 0, 0, 0, 1]))
        p(e + 0x6C, R.choice([1, 1, 1, 3, 0, 0x81, 0xFFFF]) if True else 0)
        p(e + 0x1D0, R.choice([0, 0, 0, 0, 0, 2]))
        p(e + 0x1CC, R.choice([0, 0, 0, 0, 0, 1]))
        p(e + 0xB4, R.choice([0, 0, 0x40, 0x80, 0xC0]))
        p(e + 0x1C4, tab + R.randint(0, 3) * 4 if R.random() < 0.9 else 0)
        # positions: 16.16 ; small grid => many ties
        p(e + 0x114, (R.randint(0, span) * grid) << 16 if grid < 0x7000 else R.getrandbits(32))
        p(e + 0x118, (R.randint(0, span) * grid) << 16 if grid < 0x7000 else R.getrandbits(32))
    caller = R.choice([HERO, HERO + STR * R.randint(1, 63), HERO + STR * R.randint(1, 63)])
    mask = R.choice([1, 1, 1, 3, 0x80])
    atk = R.randint(0, 11)
    # 5th argument at [0x801FFF10]
    for i in range(4):
        ram[0x801FFF10 + i] = (OUTK >> (8 * i)) & 0xFF
    return ram, caller, mask, atk, count


def real_run(ram, caller, mask, atk):
    m = Mem(ram, strict=False)
    c = Cpu(m)
    n = c.run(0x8003AF70, (caller, mask, atk, OUTP))
    P = [m.b(OUTP + 4 * k) | m.b(OUTP + 4 * k + 1) << 8 | m.b(OUTP + 4 * k + 2) << 16 | m.b(OUTP + 4 * k + 3) << 24 for k in range(n)]
    K = [m.b(OUTK + 4 * k) | m.b(OUTK + 4 * k + 1) << 8 | m.b(OUTK + 4 * k + 2) << 16 | m.b(OUTK + 4 * k + 3) << 24 for k in range(n)]
    return n, P, K, c


if __name__ == "__main__":
    R = random.Random(31337)
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    variants = [None, "inclusive", "status2only", "ign20", "sort_from2", "corrected"]
    res = {v: 0 for v in variants}
    ties = 0
    cov = set()
    for t in range(N):
        ram, caller, mask, atk, count = world(R)
        rn, rP, rK, c = real_run(ram, caller, mask, atk)
        cov |= c.visited
        g = lambda a, n: sum(ram.get(a + i, IMG[a + i - BASE + TEXT_OFF] if BASE <= a + i < IMG_END else 0) << (8 * i) for i in range(n))
        for v in variants:
            mn, mP, mK = query_model(g, caller, mask, atk, count, v)
            if (mn, mP, mK) != (rn, rP, rK):
                res[v] += 1
        if len(set(rK)) < len(rK):
            ties += 1
    print("vectors", N, "with tied keys", ties)
    print("vectors where the model differs from the real code (None = my model must be 0):", res)
    allq = [a for a in range(0x8003AF70, 0x8003B0E8, 4)]
    alls = [a for a in range(0x8003AE04, 0x8003AF70, 4)]
    print("coverage query %d/%d, sort %d/%d" % (len([a for a in allq if a in cov]), len(allq), len([a for a in alls if a in cov]), len(alls)))
    print("uncovered:", [hex(a) for a in allq + alls if a not in cov])
