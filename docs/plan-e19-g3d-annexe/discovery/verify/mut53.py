"""Mutation check of my own model against the REAL handler: each mutant must be caught by the random cases."""
import sys, os, random
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import probe53 as P
M32 = P.M32
s32 = P.s32
lcg = P.lcg
OX, OY = P.OX, P.OY


def make_model(mut):
    def model(hp, hpmax, hx, hy, hz, seed, occupied):
        occ = set(occupied)
        slots = {}

        def create():
            for i in range(128):
                if i not in occ:
                    occ.add(i)
                    zadd = 0x10000 if mut == 'z_plus_1px' else 0x100000
                    s = dict(x=hx, y=hy, z=s32(hz + zadd), fx=0, fy=0, fz=0, eff=14, anim=0, bank=0, mode=0, rec=-1,
                             status=2, recidx=0)
                    slots[i] = s
                    return s
            return None
        for j in range(8):
            s = create()
            if s:
                sh = 12 if mut == 'shift12' else 13
                s['x'] = s32(s['x'] + (OX[j] << sh))
                s['y'] = s32(s['y'] + (OY[j] << sh))
                sg = -1 if mut != 'ring_sign' else 1
                s['fx'] = s32(sg * (OX[j] << 9))
                s['fy'] = s32(sg * (OY[j] << 9))
        for _ in range(4):
            s = create()
            if s is None and mut == 'draw_on_fail':
                r1 = lcg(seed); r2 = lcg(r1); seed = r2
            if s:
                r1 = lcg(seed)
                r2 = lcg(r1)
                s['fx'] = s32(((r1 * 0x20001) >> 32) - 0x10000)
                s['fy'] = s32(((r2 * 0x30001) >> 32) - 0x18000)
                if mut == 'swap_mult':
                    s['fx'] = s32(((r1 * 0x30001) >> 32) - 0x10000)
                s['fz'] = 0x40000
                seed = r1 if mut == 'seed_r1' else r2
                if mut == 'one_draw':
                    seed = r1
                    s['fy'] = s32(((r1 * 0x30001) >> 32) - 0x18000)
        hm = s32(hpmax + 1)
        hpout = hm if mut != 'hp_unchanged' else hp
        return dict(size=2, result=1, hp=hpout, hpmax=hm, seed=seed, sounds=[0x31] if mut != 'no_sound' else [], slots=slots)
    return model


def cases(n, rseed):
    rnd = random.Random(rseed)
    for t in range(n):
        hp = rnd.randrange(0, 60); hpmax = rnd.randrange(1, 52)
        hx = rnd.randrange(0, 0x10000000); hy = rnd.randrange(0, 0x10000000)
        hz = rnd.choice([0, 0x100000, 0x7FF00000, rnd.randrange(0, 0x4000000)])
        seed = rnd.randrange(0, 2**32)
        k = rnd.choice([0, 0, 0, 1, 118, 119, 120, 127, 128])
        occupied = list(range(k))
        yield hp, hpmax, hx, hy, hz, seed, occupied


def main():
    reals = []
    for c in cases(60, 99):
        hp, hpmax, hx, hy, hz, seed, occ = c
        reals.append((c, P.run_real(0x53, hp, hpmax, hx & M32, hy & M32, hz & M32, seed, occ)))
    for mut in ['none', 'z_plus_1px', 'shift12', 'ring_sign', 'draw_on_fail', 'swap_mult', 'seed_r1', 'one_draw', 'hp_unchanged', 'no_sound']:
        mdl = make_model(mut)
        bad = 0
        for (c, real) in reals:
            hp, hpmax, hx, hy, hz, seed, occ = c
            if mdl(hp, hpmax, s32(hx), s32(hy), s32(hz), seed, occ) != real:
                bad += 1
        print("%-14s mismatching cases: %d / %d" % (mut, bad, len(reals)))


main()
