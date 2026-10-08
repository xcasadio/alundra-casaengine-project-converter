"""Counts: tracks whose periods P1/P2/P3 are not all equal; tracks with any event shifted between iterations 1-2 and
2-3 (events aligned by stream index inside each iteration, offsets relative to the iteration start), both drivers."""
from seqsim import run

for variant in ("bin", "ana"):
    pvar, j12, j23 = [], [], []
    for n in range(1, 47):
        _, s = run(n, variant, njumps=3)
        if not s.jumps:
            continue
        st = [s.loop_first] + s.jumps
        P = [st[k + 1] - st[k] for k in range(3)]
        if len(set(P)) > 1:
            pvar.append(n)
        i0, (i1, i2, i3) = s.entry_evi, s.jump_evi
        its = [s.ev[i0:i1], s.ev[i1:i2], s.ev[i2:i3]]
        def shifted(a, sa, b, sb):
            return any((x[0] - sa) != (y[0] - sb) for x, y in zip(a, b))
        if shifted(its[0], st[0], its[1], st[1]):
            j12.append(n)
        if shifted(its[1], st[1], its[2], st[2]):
            j23.append(n)
    print(variant, "periods vary:", len(pvar), pvar)
    print(variant, "jitter 1-2:", len(j12), "none on", sorted(set(range(1, 47)) - set(j12) - {26, 36, 37, 38, 40, 42, 43, 44}))
    print(variant, "jitter 2-3:", len(j23), "none on", sorted(set(range(1, 47)) - set(j23) - {26, 36, 37, 38, 40, 42, 43, 44}))
