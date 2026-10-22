"""edges.py - the spawn-time support edges of chosen maps (validation of census2/3 against the known stacks of the h1b1 guards), and a tolerance sweep for 3-level piles."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import census2 as C
m = C.m
sys.stdout.reconfigure(encoding="utf-8")


def edges(mid, tol=0):
    mp = m.Map(mid)
    ents = C.make_entities(mp)
    sp = [e for e in ents if e.spawned and e.collidable]
    out = []
    for s in sp:
        if not s.subject:
            continue
        for o in sp:
            if o is s:
                continue
            gap = s.modz - (o.modz + o.d + 1)
            if abs(gap) > tol:
                continue
            save = (o.modz,)
            # emulate the exact rule when the gap is within the tolerance by temporarily shifting
            o_modz = o.modz
            o.modz = o.modz + gap
            ok = C.rests_on(s, o)
            o.modz = o_modz
            if ok:
                out.append((s.idx, s.name, o.idx, o.name, gap))
                break
    return out


if __name__ == "__main__":
    for mid in (390, 163, 179, 67):
        print("map", mid)
        for x in edges(mid):
            print("  rec %d (%s) rests on rec %d (%s)" % x[:4])
    # tolerance sweep: 3-level chains with up to 8 px of slack between the authored heights
    for tol in (0, 8 << 16, 16 << 16):
        n = 0
        for mid in C.census_z.all_maps():
            ed = {a: b for a, _, b, _, _ in edges(mid, tol)}
            for a, b in ed.items():
                if b in ed:
                    n += 1
        print("tolerance", tol >> 16, "px: chains of 3 entities or more:", n)
