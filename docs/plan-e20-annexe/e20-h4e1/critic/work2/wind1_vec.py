"""wind1_vec.py - h4e1 discovery (read-only): replayable vectors of the wind-1 driver 0x80035EB0 over the REAL MIPS code of ALUN_CD.EXE (France), with the REAL query 0x8003AF70 + sort 0x8003AE04, the REAL
guard 0x800351B8 and the REAL cancel dispatcher 0x800350C0, SpawnWarpEntity 0x80039F58 stubbed (journalled) and DestroyEntityNoFx 0x8003A774 stubbed (journalled).  Uses the vector framework of
docs/plan-e20-annexe/e20-h4b/bin/h/ (vtrace.py, machine.py, emu.py ... : a COPY is in ./h4b_h/; this script is run from a copy of this folder).  Compared, vector by vector, with my own model (wind1_model.py).

usage: python -B wind1_vec.py N SEED OUTFILE      -> writes OUTFILE (format of e20-h4b/bin/vec/README.txt) and prints the mismatch / coverage counts
Every data word the code reads is listed ("reads"), every final written word ("writes"); the stubbed callees are journalled in "spawn_calls" / "destroy_calls" (replay hooks: see replay()).
"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4b_h'))
sys.path.insert(0, HERE)
import vtrace as V
from machine import M32
import wind1_model as WM

HERO = WM.HERO
STRIDE = WM.STRIDE
PHASE, COUNTER, ITEM, LASTT, RNG = 0x80127160, 0x8012715C, 0x80127164, 0x8012712C, 0x80098708
COUNTW = 0x80134360
PTRS, KEYS = 0x800CC058, 0x800CC158
SPAWNED_SLOT = 40                      # the entity the spawn stub returns lives in a free slot beyond the count
FICHE0 = 0x80190000                    # synthetic balance sheets (+0x1C4 points to them): the driver's query reads the byte at fiche + 2 + 11
FUNC = 0x80035EB0


def ent(i):
    return HERO + STRIDE * i


def pick_counter(rnd):
    c = rnd.random()
    if c < 0.55:
        base = rnd.choice(WM.TABLE[:8])
        return base + rnd.choice([0, 0, 0, 0, -1, 1])
    if c < 0.7:
        return rnd.choice([0, 1, 2, 29, 101, 102, 150, 255, 1000, 65534])
    return rnd.randrange(0, 200)


def rnd_state(rnd):
    st = {}
    heavy = rnd.random() < 0.7                       # most vectors reach a strike with several valid candidates: phase 1, hero in 0x33, a counter of the table
    st['phase'] = 1 if heavy and rnd.random() < 0.9 else rnd.choice([-1, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 3, 100])
    st['counter'] = rnd.choice(WM.TABLE[:8]) if heavy and rnd.random() < 0.9 else pick_counter(rnd)
    st['item'] = rnd.choice([0x31] * 6 + [0, 0x2B])
    st['rng'] = rnd.getrandbits(32)
    hx = (rnd.randrange(60, 1000) << 16) + rnd.choice([0, 0, rnd.getrandbits(16)])
    hy = (rnd.randrange(60, 800) << 16) + rnd.choice([0, 0, rnd.getrandbits(16)])
    hz = rnd.choice([1, 1, 0, rnd.randrange(0, 1 << 24), (-rnd.randrange(1, 1 << 20)) & M32])
    anim = 0x33 if heavy and rnd.random() < 0.9 else rnd.choice([0x32, 0x33, 0x33, 0x33, 0x33, 0x34, 0x31, 0, 0x2D])
    slope = 0 if heavy and rnd.random() < 0.9 else rnd.choice([0, 0, 0, 0, 0, 0, 4, 1])
    st['hero'] = dict(anim=anim, slope=slope, x=hx, y=hy, z=hz)
    n = rnd.choice([0, 1, 2, 3, 3, 4, 5, 6, 8]) if not heavy else rnd.choice([1, 2, 2, 3, 3, 4, 5, 6, 7, 8])
    ents = {}
    pbad = 0.08 if heavy else 0.5                    # chance that a given filter of the query rejects the entity
    for k in range(1, n + 1):
        big = rnd.random() < 0.04
        if big:
            dx = rnd.randrange(30000, 50000) * rnd.choice([-1, 1])
            dy = rnd.randrange(30000, 50000) * rnd.choice([-1, 1])
        else:
            dx = rnd.choice([0, 160, -160, 161, -161, 159, rnd.randrange(-200, 200), rnd.randrange(-200, 200), rnd.randrange(-60, 60), rnd.randrange(-130, 130), rnd.randrange(-300, 300)])
            dy = rnd.choice([0, 160, -160, 161, -161, rnd.randrange(-200, 200), rnd.randrange(-60, 60), rnd.randrange(-100, 100), rnd.randrange(-300, 300)])
        if not big and rnd.random() < 0.15:                # exact threshold pairs: key 0x6400 (passes) / 0x6401 (stops): 160^2, 160^2+1, 113^2+113^2 = 25538, 114^2+113^2 = 25765, 1^2+160^2
            dx, dy = rnd.choice([(160, 0), (0, 160), (-160, 0), (0, -160), (160, 1), (-160, 1), (1, 160), (1, -160), (113, 113), (114, 113), (-113, -113), (161, 0), (0, 161), (127, 98), (-32768, -32768), (-32768, 32767), (32767, 32767), (-32768, 0)])
        fracx = rnd.choice([0, 0, 0xFFFF, rnd.getrandbits(16)])
        fracy = rnd.choice([0, 0, 0xFFFF, rnd.getrandbits(16)])
        zbad = rnd.random() < (0.1 if heavy else 0.5)
        z = rnd.choice([hz + 0x500001, hz + 0x1000000, rnd.getrandbits(32)]) if zbad else rnd.choice([hz, hz, hz + 0x500000, hz + 0x4FFFFF, hz - 0x100000, 1])
        bad = lambda: rnd.random() < pbad
        ents[k] = dict(status=rnd.choice([0, 1, 4, 5]) if bad() else rnd.choice([2, 2, 2, 3]),
                       f20=5 if bad() else 0,
                       flags=rnd.choice([0, 0x8, 0x100, 0x80, rnd.getrandbits(16) & ~1]) if bad() else rnd.choice([1, 1, 0x81, 0x101, 0x5, 0x2001, rnd.getrandbits(16) | 1]),
                       c1d0=rnd.choice([25, 1]) if bad() else 0, c1cc=30 if bad() else 0, b4=rnd.choice([0x40, 0x41, 0xC0]) if bad() else rnd.choice([0, 0, 0x10, 0x80, 0x3F]),
                       fiche=0 if bad() else FICHE0 + 0x20 * k,
                       resp10=rnd.getrandbits(8),
                       resp=rnd.choice([0x80, 0x81, 0xBF]) if bad() else rnd.choice([0x00, 0x10, 0x10, 0x10, 0x40, 0xC0, 0x7F, 0x01, rnd.getrandbits(8) & ~0x80]),
                       x=(hx + (dx << 16) + fracx) & M32, y=(hy + (dy << 16) + fracy) & M32, z=z & M32)
    if rnd.random() < 0.04:                          # the corner of the 32-bit key: one valid candidate at (-32768, -32768) px: key 0x80000000, negative for the signed compare of the driver (slti 0x6401)
        st['phase'], st['counter'] = 1, rnd.choice(WM.TABLE[:8])
        st['hero'].update(anim=0x33, slope=0)
        n = 1
        ents = {1: dict(status=2, f20=0, flags=1, c1d0=0, c1cc=0, b4=0, fiche=FICHE0 + 0x20, resp10=0x55, resp=0x10, x=(hx - (32768 << 16)) & M32, y=(hy - (32768 << 16)) & M32, z=hz)}
    # two entities may share a sheet block: the byte at fiche + 13 is the last poked; the model reads the value of the LAST entity poked with that sheet (see poke_state)
    st['ents'] = ents
    st['count'] = rnd.choice([n + 1, n + 1, n + 1, n + 1, n, n, max(0, n - 1), n + 2, 0 if rnd.random() < 0.05 else n + 1])
    st['last'] = rnd.choice([0, 0, 0, ent(rnd.randrange(1, max(2, n + 1))), ent(rnd.randrange(1, max(2, n + 1))), ent(rnd.randrange(0, 63)), rnd.getrandbits(32)])
    st['spawn'] = dict(ret=rnd.choice([ent(SPAWNED_SLOT)] * 7 + [0] * 3), ground=rnd.choice([0] * 8 + [1, rnd.getrandbits(32) | 1]))
    return st


def poke_state(m, st):
    m.poke(PHASE, st['phase'] & M32)
    m.poke(COUNTER, st['counter'] & M32)
    m.poke(ITEM, st['item'])
    m.poke(LASTT, st['last'])
    m.poke(RNG, st['rng'])
    h = st['hero']
    m.poke(HERO + 0x88, h['anim'])
    m.poke(HERO + 0x18C, h['slope'])
    m.poke(HERO + 0x114, h['x'])
    m.poke(HERO + 0x118, h['y'])
    m.poke(HERO + 0x11C, h['z'])
    m.poke(COUNTW, st['count'])
    for k, e in st['ents'].items():
        a = ent(k)
        m.poke(a + 0x10, e['status'])
        m.poke(a + 0x20, e['f20'])
        m.poke(a + 0x6C, e['flags'])
        m.poke(a + 0x1D0, e['c1d0'])
        m.poke(a + 0x1CC, e['c1cc'])
        m.poke(a + 0xB4, e['b4'])
        m.poke(a + 0x1C4, e['fiche'])
        m.poke(a + 0x114, e['x'])
        m.poke(a + 0x118, e['y'])
        m.poke(a + 0x11C, e['z'])
        if e['fiche']:
            m.poke8(e['fiche'] + 2 + 10, e['resp10'])
            m.poke8(e['fiche'] + 2 + 11, e['resp'])
    m.poke(ent(SPAWNED_SLOT) + 0x144, st['spawn']['ground'])


def observe(m):
    return dict(phase=m.peeks(PHASE), counter=m.peeks(COUNTER), item=m.peek(ITEM), last=m.peek(LASTT), rng=m.peek(RNG), anim=m.peek(HERO + 0x88),
                forcez=m.peek(ent(SPAWNED_SLOT) + 0xB8))


def run(N, seed):
    rnd = random.Random(seed)
    m = V.TM()
    mism = 0
    vecs = []
    m.cov.clear()
    stats = dict(ret0=0, ret1=0, strikes_target=0, strikes_random=0, spawn_null=0, destroy=0, end=0, q_ge3=0, q_eq2=0)
    for n in range(N):
        st = rnd_state(rnd)
        # the machine is reused: clear what the previous vector wrote that the next one may read (entities, arrays, spawn slot)
        m.zero(HERO, 64 * STRIDE)
        m.zero(PTRS, 0x200)
        m.zero(FICHE0, 0x20 * 8)
        poke_state(m, st)
        spawn_calls, destroy_calls = [], []

        def spawn_stub(mm, ret=st['spawn']['ret'], calls=spawn_calls):
            sp = mm.r[29]
            calls.append([mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32, mm.peek(sp + 0x10), mm.peek(sp + 0x14), mm.peek(sp + 0x18)])
            mm.r[2] = ret

        def destroy_stub(mm, calls=destroy_calls):
            calls.append(mm.r[4] & M32)
            mm.r[2] = 0
        m.hooks[0x80039F58] = spawn_stub
        m.hooks[0x8003A774] = destroy_stub
        vec = V.run(m, FUNC, ())
        got = observe(m)
        got['ret'] = vec['ret']
        ret, want = WM.model(st)
        exp = dict(phase=want['phase'], counter=st['counter'], item=want['item'], last=want['last'], rng=want['rng'], anim=want['hero']['anim'], ret=ret)
        fz = [v for (p, v) in want['force_z']]
        exp['forcez'] = fz[0] if fz else 0
        sp_exp = [s['args'] for s in want['spawns']]
        bad = {k: (got[k], exp[k]) for k in exp if got[k] != exp[k]}
        if sp_exp != spawn_calls:
            bad['spawns'] = (spawn_calls, sp_exp)
        if destroy_calls != want['destroys']:
            bad['destroys'] = (destroy_calls, want['destroys'])
        if want['query'] is not None:
            P, K = want['query']
            gP = [m.peek(PTRS + 4 * k) for k in range(len(P))]
            gK = [m.peek(KEYS + 4 * k) for k in range(len(K))]
            if gP != P or gK != K:
                bad['query'] = ((gP, gK), (P, K))
        if bad:
            mism += 1
            if mism <= 5:
                print('MISMATCH vector', n, bad, json.dumps({k: v for k, v in st.items() if k != 'ents'}))
        if ret == 0:
            stats['ret0'] += 1
        else:
            stats['ret1'] += 1
        if want['spawns'] and st['spawn']['ret'] == 0:
            stats['spawn_null'] += 1
        if want['destroys']:
            stats['destroy'] += 1
        if want['query'] is not None:
            P, K = want['query']
            if len(P) >= 3:
                stats['q_ge3'] += 1
            if len(P) == 2:
                stats['q_eq2'] += 1
        if want['spawns']:
            stats['strikes_target' if want['last'] else 'strikes_random'] += 1
        if want['phase'] == 2 and st['phase'] == 1:
            stats['end'] += 1
        v = dict(id=n, **vec)
        v['stub_spawn_ret'] = st['spawn']['ret']
        v['spawn_calls'] = spawn_calls
        v['destroy_calls'] = destroy_calls
        vecs.append(v)
    m.hooks.pop(0x80039F58, None)
    m.hooks.pop(0x8003A774, None)

    def cov(a, b):
        return len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4
    cv = dict(driver=cov(0x80035EB0, 0x80036218), guard=cov(0x800351B8, 0x80035204), cancel=cov(0x800350C0, 0x800351B8), query=cov(0x8003AF70, 0x8003B0E8), sort=cov(0x8003AE04, 0x8003AF70))
    return vecs, mism, stats, cv, m


if __name__ == '__main__':
    N = int(sys.argv[1])
    seed = int(sys.argv[2])
    out = sys.argv[3]
    vecs, mism, stats, cv, m = run(N, seed)
    print('vectors %d, mismatches with my model: %d' % (N, mism))
    print('stats', stats)
    print('coverage (instructions executed / total)', cv)
    uncovered = {}
    for name, (a, b) in dict(driver=(0x80035EB0, 0x80036218), guard=(0x800351B8, 0x80035204)).items():
        uncovered[name] = ['%08x' % p for p in range(a, b, 4) if p not in m.cov]
    print('uncovered', uncovered)
    json.dump({"function": "0x80035EB0", "note": "wind-1 driver; real query 0x8003AF70 + sort 0x8003AE04, real guard 0x800351B8, real cancel 0x800350C0; stubs: SpawnWarpEntity 0x80039F58 returns stub_spawn_ret (replay hook), DestroyEntityNoFx 0x8003A774 records its argument (replay hook, no-op)",
               "seed": seed, "vectors": vecs}, open(out, 'w'))
