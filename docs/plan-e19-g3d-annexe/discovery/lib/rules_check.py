"""rules_check.py: an independent transcription of the rules of the plan (what the DLL will do) checked against g3d-golden.tsv (made by the real code).
Pure python, no interpreter: pool of 128 slots (lowest free wins), the ring, the sparks with the shared generator, the tick of mode 0."""
import os, sys, collections
H = os.path.dirname(os.path.abspath(__file__))
M32 = 0xFFFFFFFF
OFFX = [0, -543, -768, -543, 0, 543, 768, 543]
OFFY = [512, 362, 0, -362, -512, -362, 0, 362]


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


class Pool:
    def __init__(self, active):
        self.active = set(active)
        self.slots = {}

    def create(self, x, y, z):
        for i in range(128):
            if i not in self.active:
                self.active.add(i)
                self.slots[i] = [s32(x), s32(y), s32(z), 0, 0, 0]
                return i
        return None


def lcg(seed):
    return (seed * 0x7D2B89DD + 0xE06A02E7) & M32


def vessel(pool, hero, seed):
    hx, hy, hz = hero
    created = []
    for j in range(8):
        i = pool.create(hx, hy, hz + 0x100000)
        if i is not None:
            s = pool.slots[i]
            s[0] = s32(s[0] + (OFFX[j] << 13))
            s[1] = s32(s[1] + (OFFY[j] << 13))
            s[3] = s32(-(OFFX[j] << 9))
            s[4] = s32(-(OFFY[j] << 9))
            created.append(i)
    sparks = 0
    for _ in range(4):
        i = pool.create(hx, hy, hz + 0x100000)
        if i is not None:
            r1 = lcg(seed)
            r2 = lcg(r1)
            s = pool.slots[i]
            s[3] = s32(((r1 * 0x20001) >> 32) - 0x10000)
            s[4] = s32(((r2 * 0x30001) >> 32) - 0x18000)
            s[5] = 0x40000
            seed = r2
            created.append(i)
            sparks += 1
    return created, sparks, seed


def tick(pool, ids):
    for i in ids:
        s = pool.slots[i]
        s[0] = s32(s[0] + s[3]); s[2] = s32(s[2] + s[5]); s[1] = s32(s[1] + s[4])


def main():
    rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(H, "g3d-golden.tsv"), encoding="utf-8")]
    V = {r[1]: r for r in rows if r[0] == "V"}
    S = collections.defaultdict(dict)
    T = collections.defaultdict(lambda: collections.defaultdict(dict))
    for r in rows:
        if r[0] == "S":
            S[r[1]][int(r[2])] = tuple(int(v) for v in r[3:9])
        elif r[0] == "T":
            T[r[1]][int(r[2])][int(r[3])] = None if r[4] == "free" else tuple(int(v) for v in r[4:7])
    bad = 0
    for case, v in V.items():
        seed = int(v[2], 16)
        hero = tuple(int(x) for x in v[3:6])
        prefill = int(v[6])
        free = [] if v[7] == "-" else [int(x) for x in v[7].split(",")]
        active = set(range(prefill)) - set(free)
        # the prefill creates slots 0..prefill-1 lowest first; freed holes come back to the lowest-free rule
        pool = Pool(active)
        created, sparks, seed2 = vessel(pool, hero, seed)
        exp_n, exp_sparks, exp_seed = int(v[17]), int(v[18]), int(v[16], 16)
        got = {i: tuple(pool.slots[i]) for i in created}
        if len(created) != exp_n or sparks != exp_sparks or seed2 != exp_seed or got != S[case]:
            bad += 1
            print("MISMATCH", case, len(created), exp_n, sparks, exp_sparks, hex(seed2), hex(exp_seed), got == S[case])
            continue
        # ticks: after N updates (the sparks only move in mode 0; the global effect 14 anim 0 lives 28 updates, freed on the 29th)
        if not T.get(case):
            continue
        pool2 = Pool(active)
        created2, _, _ = vessel(pool2, hero, int(v[2], 16))
        for n in range(1, 30):
            tick(pool2, created2)
            if n in (1, 2, 16, 27, 28):
                for i in created2:
                    e = T[case][n][i]
                    g = (pool2.slots[i][0], pool2.slots[i][1], pool2.slots[i][2])
                    if e != g:
                        bad += 1
                        print("TICK MISMATCH", case, n, i, e, g)
            if n == 29:
                for i in created2:
                    if T[case][n][i] is not None:
                        bad += 1
                        print("NOT FREED", case, n, i)
    print("cases", len(V), "bad", bad)


if __name__ == "__main__":
    main()
