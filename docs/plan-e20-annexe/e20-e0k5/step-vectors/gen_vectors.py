"""gen_vectors.py - E20.e0k5 T1: the vectors of the pure halving search, made from the hand model of the binary (own code, from the disassembly of
ALUN_CD.EXE (France)): docs/plan-e20-annexe/e20-step-search/work/xymodel.py (compute_xy, replayed against the real code on 55 000 vectors, 0 gap).

For each case the model runs on a random world (the generator of xyfuzz.py: cells with the 0x40/0x41/0x1000 attributes, heights, entity boxes, fractions of pixel)
for a NON-HERO subject with NoObstacleSlide (0x2000, the projectile), no ground snapping (0x100 cleared) and no collidable: no entity candidate, no slide, so the
only thing left is the halving loop. The attempts of the model (trace) give, in order, the offset (dx, dy) of the tried position from the start (16.16 raw units)
and the verdict of the wall test there. A case is one TSV line:
    cardinal(1 if TargetDirection & 7 == 0) \t forceX \t forceY \t resultDx \t resultDy \t accepted(1 = some sub-step accepted) \t "dx,dy,b;dx,dy,b;..."
The C# test answers its predicate from the recorded queries and fails on any query that is not recorded, or that arrives in another order.
Usage: python -I gen_vectors.py <out.tsv.gz> [n=20000] [seed=2025]
"""
import sys, os, random, gzip

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.normpath(os.path.join(HERE, "..", "..", "e20-step-search", "work"))
sys.path.insert(0, WORK)
import xymodel as XM          # noqa: E402
import xyfuzz as XF           # noqa: E402


def make_case(rnd):
    while True:
        w, a = XF.gen_world(rnd)
        if a == XM.HERO:
            continue
        break
    S = w.ents[a]
    S[0x6c] = (S[0x6c] | 0x2000) & ~0x100
    S[0x13c] = 0
    S[0x28] = 0
    S[0xb4] = 0
    w.collidables = []
    # wider spread of the vectors: the halving depth depends on the magnitude
    if rnd.random() < 0.5:
        mag = rnd.choice([3, 7, 100, 4096, 65535, 98304, 196608, 393216, 1 << 19, (1 << 19) - 5])
        S[0xe4] = rnd.choice([-1, 0, 1, 1]) * rnd.choice([mag, mag // 3 + 1, rnd.randint(0, mag)])
        S[0xe8] = rnd.choice([-1, 0, 1, 1]) * rnd.choice([mag, mag // 3 + 1, rnd.randint(0, mag)])
    if rnd.random() < 0.5:
        wall_world(rnd, w, a)
    return w, a


def wall_world(rnd, w, a):
    """a flat field with ONE wall (a column or a row of 0x40 cells) ahead of the subject: the cases where the halving converges on a partial step"""
    S = w.ents[a]
    px, py = S[0x114] >> 16, S[0x118] >> 16
    c0, r0 = min(max(px // 24, 0), 51), min(max(py // 16, 0), 59)
    for c in range(max(c0 - 6, 0), min(c0 + 7, 52)):
        for r in range(max(r0 - 6, 0), min(r0 + 7, 60)):
            w.cell[(c, r)] = (0, 0, 0)
    for ad in w.ents:
        w.ents[ad][0x1ec] = 0
    S[0x11c] = (rnd.choice([0, 1, 2]) << 20) + 1
    XM.set_mod(S)
    fx, fy = S.get(0xe4, 0), S.get(0xe8, 0)
    left = (S[0x114] + S[0x1e4]) >> 16
    right = (S[0x114] + S[0x1e4] + S[0x1f0]) >> 16
    top = (S[0x118] + S[0x1e8]) >> 16
    bottom = (S[0x118] + S[0x1e8] + S[0x1f4]) >> 16
    if rnd.random() < 0.6:
        c = (right // 24 + 1) if fx >= 0 else (left // 24 - 1)
        c = min(max(c, 0), 51)
        for r in range(max(r0 - 6, 0), min(r0 + 7, 60)):
            w.cell[(c, r)] = (rnd.choice([0x40, 0x41, 0x1040]), 0, 0)
        if rnd.random() < 0.7:           # a step that reaches into the wall by a few pixels
            gap = (c * 24 - right) if fx >= 0 else (left - (c * 24 + 24))
            S[0xe4] = (1 if fx >= 0 else -1) * (max(gap, 0) + rnd.randint(0, 9)) * 65536 + rnd.choice([0, rnd.randint(0, 65535)])
    else:
        r = (bottom // 16 + 1) if fy >= 0 else (top // 16 - 1)
        r = min(max(r, 0), 59)
        for c in range(max(c0 - 6, 0), min(c0 + 7, 52)):
            w.cell[(c, r)] = (rnd.choice([0x40, 0x41, 0x1040]), 0, 0)
        if rnd.random() < 0.7:
            gap = (r * 16 - bottom) if fy >= 0 else (top - (r * 16 + 16))
            S[0xe8] = (1 if fy >= 0 else -1) * (max(gap, 0) + rnd.randint(0, 9)) * 65536 + rnd.choice([0, rnd.randint(0, 65535)])


def run_case(w, a):
    E = w.ents[a]
    fx, fy = E.get(0xe4, 0), E.get(0xe8, 0)
    cardinal = 1 if (E.get(0x8c, 0) & 7) == 0 else 0
    x0, y0 = E[0x114], E[0x118]
    trace = []
    XM.compute_xy(w, a, trace)
    # the model leaves the position at the accepted sub-steps (it is restored after a refusal)
    rx, ry = XM.s32(E[0x114] - x0), XM.s32(E[0x118] - y0)
    queries = []
    accepted = 0
    for rec in trace:
        px, py, _ = rec["pos"]
        queries.append((XM.s32(px - x0), XM.s32(py - y0), 0 if not rec["blocked"] else 1))
        if not rec["blocked"]:
            accepted = 1
    return cardinal, fx, fy, rx, ry, accepted, queries


def main():
    out = sys.argv[1]
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 20000
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else 2025
    rnd = random.Random(seed)
    # the stairs table of the exe is the model default; xyfuzz.main loads it only for the real-code replay, which this script does not need
    stats = dict(accepted=0, refused=0, zero=0, queries=0, blocked=0, free=0, partial=0)
    lines = []
    for _ in range(n):
        w, a = make_case(rnd)
        c, fx, fy, rx, ry, acc, q = run_case(w, a)
        stats["queries"] += len(q)
        stats["blocked"] += sum(1 for t in q if t[2])
        stats["free"] += sum(1 for t in q if not t[2])
        if not q:
            stats["zero"] += 1
        elif acc:
            stats["accepted"] += 1
            if (rx, ry) != (fx, fy):
                stats["partial"] += 1
        else:
            stats["refused"] += 1
        lines.append("\t".join([str(c), str(fx), str(fy), str(rx), str(ry), str(acc), ";".join(f"{dx},{dy},{b}" for dx, dy, b in q)]))
    with open(out, "wb") as raw, gzip.GzipFile(filename="", fileobj=raw, mode="wb", mtime=0) as f:   # no name, no date: the same bytes at each run
        f.write(("\n".join(lines) + "\n").encode("ascii"))
    print(f"cases={n} seed={seed} {stats}")


if __name__ == "__main__":
    main()
