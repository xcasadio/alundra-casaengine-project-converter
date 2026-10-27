"""fire1_lib.py - h4d2 discovery (read-only): the per-tick key bench of the fire-1 cast (item 0x2F) on a COPY of the e20-h4 harness (h4o/), run with `python -B`.

What it is (nothing here knows an expected value; every number comes from the REAL code of ALUN_CD.EXE run by the harness machine):
  * the machine of e20-h4 (spellworld.make): REAL SpawnWarpEntity 0x80039F58 over the global sprite page installed from data-extracted/data/map_alundra.json (records 1, 2, 16, 17, 19..25),
    REAL entity passes (events with the native programs A/C/D/E, animation, physics), REAL hit pass 0x80039300; stubs (journalled): PlaySoundEffect, CreateEffectEntity 0x8003BDD8,
    CreateAttachedEffectCore 0x8003BE74, the effects pass 0x80038E84;
  * the REMAKE required by plan section 1.31 R-EL-1 (the same correction as e20-h4d1/critic/c/h4o/fire2_key_c.py, FAITHFUL=1): the map-load init 0x8003B24C writes the slot index (+0 = i) of the 64
    slots and the template +0x48 = -1 (0x8003B218-0x8003B21C); the e20-h4 harness leaves them 0, which makes every entity born in a never-used slot one tick late;
  * the PER-FRAME STEP of the generator (_pf, plan section 1.20 point 3 rule): after the tick's own generator state is recorded (rng1), the frame-end step 0x8002BD0C-0x8002BD34
    s <- s * 0x7D2B89DD + 0xE06A02E7 is applied, so rng0(t+1) = step(rng1(t));
  * bench R-EL-4: synthetic flat field (52 x 60 cells), hero at (100, 100) facing east (direction 24), seed 0x12345678 before tick 1, no other entity (map entities absent), Circle held at tick 1.

Options of run(): hurt (the hero animation poked to 0x31 right before tick T, R-EL-6, as e20-h4/work/oracle/interrupt_check.py does), targets (static ClassA blocks of the global sprite 25 at pixel
positions, in this order, first free slots), cells (cell function col,row -> (hb, sl, wk, gp)), hero, remove (tick -> list of target indexes whose status is poked to 4 at the head of the tick),
patches (name -> code patch of the binary image in the machine, for the variant keys only, labelled as such).
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import spellworld as S
import world as W
import trace_targets as T
from machine import s32, M32

HERO = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
COUNT = 0x80134360
MULT, ADD = 0x7D2B89DD, 0xE06A02E7
PTR = 0x8012711C
IDENT = 0x80127120
INUSE, PHASE, COUNTER = 0x80127164, 0x80127160, 0x8012715C
TILES = W.TILES
ROW = W.ROW


def ent(i):
    return HERO + STRIDE * i


def lcg(s):
    return (s * MULT + ADD) & M32


def draws(s0, s1, limit=64):
    s = s0
    for k in range(limit + 1):
        if s == s1:
            return k
        s = lcg(s)
    return None


def set_cells(m, cells):
    """the same cell encoding as world.build_world (hb, sl, wk, gp), applied after the machine is built"""
    for row in range(60):
        for col in range(52):
            a = TILES + 0x604 + row * ROW + col * 8
            hb, sl, wk, gp = cells(col, row)
            m.poke16(a, wk | (gp << 8))
            m.poke8(a + 2, sl)
            m.poke8(a + 3, hb)


def cells_cols(c0, c1, val, r0=0, r1=59):
    def f(col, row):
        return val if (c0 <= col <= c1 and r0 <= row <= r1) else (0, 0, 0, 0)
    return f


FIELDS = [('status', 0x10), ('hp', 0x14), ('id', 0x04), ('type', 0x68), ('flags', 0x6C), ('anim_t', 0x88), ('dir_t', 0x8C), ('anim_c', 0x90), ('dir_c', 0x94), ('row', 0x98),
          ('animend', 0xAC), ('b4', 0xB4), ('fz', 0xB8), ('fx', 0xC4), ('fy', 0xC8), ('x', 0x114), ('y', 0x118), ('z', 0x11C), ('adj13c', 0x13C), ('ground', 0x144), ('slope', 0x18C),
          ('cnt1c', 0x1C), ('c20', 0x20), ('c1cc', 0x1CC), ('c1d0', 0x1D0), ('hitc', 0x220), ('b274', 0x274), ('b278', 0x278), ('b27c', 0x27C), ('atk', 0x1C8), ('bal', 0x1C4)]


def snap_entity(m, i):
    e = ent(i)
    d = {}
    for name, off in FIELDS:
        v = m.peeks(e + off)
        d[name] = v
    d['flags'] = m.peek(e + 0x6C)
    d['touch'] = ((m.peek(e + 0x224) - HERO) // STRIDE) if m.peek(e + 0x224) else None
    d['b278_slot'] = ((m.peek(e + 0x278) - HERO) // STRIDE) if HERO <= m.peek(e + 0x278) < HERO + 64 * STRIDE else None
    d['box'] = [m.peeks(e + 0x1D4 + 4 * k) for k in range(6)]
    d['hb'] = [m.peeks(e + 0x1FC + 4 * k) for k in range(3)] + [m.peeks(e + 0x214 + 4 * k) for k in range(3)]
    p = m.peek(e + 0x1C8)
    d['atk_attr'] = (m.peek(p) & 0xFF) if p else None
    d['atk_pow'] = ((m.peek(p) >> 8) & 0xFF) if p else None
    return d


def snap_all(m):
    out = {}
    for i in range(0, 64):
        if m.peeks(ent(i) + 0x10):
            out[i] = snap_entity(m, i)
    return out


def apply_patch(m, name):
    """variant keys only (labelled INFERENCE in the report): a one-instruction change of the image in the machine.
       query_inclusive : 0x8003AF70 loop bound exclusive -> inclusive ; query_sort2 : the sort threshold 3 -> 2 ; see patch_sites() output for the sites"""
    raise NotImplementedError(name)


def install_turn_fix(m):
    """INFERENCE variant (the author's rule: a proven defect of the original is corrected, Q-H4-2): C63 turns through the shortest way of the 32-direction circle.  A one-point hook at 0x8007AD6C (the compare of
    the current direction s0 with the direction v1 of the target): when the corrected law gives another direction than the binary's numeric comparison, the corrected direction is written (+0x8C), +0x274 = 2 as the binary
    does after a turn, and the handler returns (0x8007ADC4); in every other case the REAL code runs.  The law is tools/f1model.py turn_corrected (equal to the binary on the 784 of the 1024 pairs where |target - current| <= 16)."""
    sys.path.insert(0, os.path.join(HERE, '..', 'tools'))
    import f1model as F1
    orig = m.exec1

    def exec1(pc, w):
        if pc == 0x8007AD6C:
            cur, tgt, s1 = m.r[16] & M32, m.r[3] & M32, m.r[17]
            if cur < 32 and tgt < 32 and cur != tgt:
                nd = F1.turn_corrected(cur, tgt)
                if nd != F1.turn_binary(cur, tgt):
                    m.ww(s1 + 0x8C, nd)
                    m.ww(s1 + 0x274, 2)
                    return ('j', 0x8007ADC4)
        return orig(pc, w)
    m.exec1 = exec1


def run(ticks=200, seed=0x12345678, pf=True, hurt=None, targets=(), cells=None, hero=(100, 100), hero_dir=24, remove=None, hp=5000, faithful=True, item=0x2F, mp=4,
        patch=None, hook=None, extra_slots=None, target_flags=1, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25)):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=4, seed=seed, indices=indices)
    if faithful:
        m.poke(0x80134368 + 0x48, 0xFFFFFFFF)
        for i in range(1, 64):
            m.poke(ent(i), i)
            m.poke(ent(i) + 0x48, 0xFFFFFFFF)
    if cells is not None:
        set_cells(m, cells)
    P = ent(0)
    if hero != (100, 100):
        m.poke(P + 0x114, hero[0] << 16)
        m.poke(P + 0x118, hero[1] << 16)
    if hero_dir != 24:
        m.poke(P + 0x8C, hero_dir)
    tptrs = []
    for k, (x, y) in enumerate(targets):
        p = T.spawn_global(m, 25, x, y)
        assert p, 'target spawn failed'
        m.poke(p + 0x6C, m.peek(p + 0x6C) | target_flags)
        m.poke(p + 0x14, hp)
        tptrs.append(p)
    if patch:
        patch(m)
    m.uninit_reads.clear()
    m.journal.clear()
    pad = W.Pad()
    rows = []
    jn = 0
    prev_1d0 = {p: 0 for p in tptrs}
    for t in range(1, ticks + 1):
        if hurt is not None and t == hurt:
            m.poke(P + 0x88, 0x31)
        if remove and t in remove:
            for k in remove[t]:
                m.poke(tptrs[k] + 0x10, 4)
        # head of the tick: the D handler of a target resolves the touch of the previous hit pass (ComputeNewHp REAL, as trace_targets does)
        resolved = []
        for k, p in enumerate(tptrs):
            tc = m.peek(p + 0x224)
            if tc and m.peeks(p + 0x10) in (2, 3):
                typ = m.peeks(tc + 0x68)
                before = m.peeks(p + 0x14)
                m.call(0x8003A374, p)
                after = m.peeks(p + 0x14)
                resolved.append((k, typ, before - after))
        if hook:
            hook(m, t)
        rng0 = m.peek(RNG)
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t == 1 else 0, check_pad=False)
        rng1 = m.peek(RNG)
        rng2 = rng1
        if pf:
            rng2 = lcg(rng1)
            m.poke(RNG, rng2)
        hits = []
        for k, p in enumerate(tptrs):
            c = m.peeks(p + 0x1D0)
            if c == 0x19 and prev_1d0[p] != 0x19:
                hits.append(k)
            prev_1d0[p] = c
        ev = [(j[1], list(j[2]) if isinstance(j[2], (list, tuple)) else j[2]) for j in m.journal[jn:] if j[0] == t and not j[1].startswith('pass_')]
        jn = len(m.journal)
        ents = snap_all(m)
        rows.append(dict(t=t, hero_anim_t=m.peeks(P + 0x88), hero_anim_c=m.peeks(P + 0x90), inuse=m.peeks(INUSE), phase=m.peeks(PHASE), counter=m.peeks(COUNTER),
                         mp=m.peek(STATS + 4) & 0xFFFF, ptr=m.peek(PTR), ptr_slot=((m.peek(PTR) - HERO) // STRIDE) if HERO <= m.peek(PTR) < HERO + 64 * STRIDE else None,
                         ident=m.peeks(IDENT), count=m.peeks(COUNT), rng0=rng0, rng1=rng1, rng2=rng2, draws=draws(rng0, rng1), ents=ents,
                         hits=hits, resolved=resolved, hp=[m.peeks(p + 0x14) for p in tptrs], journal=ev))
    return dict(rows=rows, targets=[((p - HERO) // STRIDE) for p in tptrs], uninit=sorted((pc, a, n, c) for (pc, a, n), c in m.uninit_reads.items()), m=m)


def jfmt(name, a):
    if name == 'sound':
        return 'snd(%d)' % a
    if name.startswith('effect'):
        x = [v if v < 0x80000000 else v - (1 << 32) for v in a]
        kind = 'fx' if name == 'effect_3BDD8' else 'attach'
        if kind == 'fx':
            return 'fx(id=%d,a2=%d,x=%d,y=%d,z=%d)' % (x[1], x[2], x[3], x[4], x[5])
        return 'attach(id=%d,a2=%d,a3=%#x,a4=%d)' % (x[1], x[2], a[3], x[4])
    return '%s%s' % (name, a)


def dump(res, path, only_ents=True):
    """compact reference dump (text) next to the JSON: one line per tick with what changed; used by the report, not by tests"""
    out = []
    for r in res['rows']:
        evs = [jfmt(n, a) for (n, a) in r['journal']]
        es = ' '.join('s%d:%d/%d/t%d/(%d,%d,%d)/an%d,%d/ae%d/b274=%d,b278=%s,b27c=%d/dir%d/fz=%d' % (i, e['status'], e['id'], e['type'], e['x'] >> 16, e['y'] >> 16, e['z'], e['anim_t'], e['anim_c'], e['animend'], e['b274'],
                                                                                                  e['b278_slot'], e['b27c'], e['dir_t'], e['fz']) for i, e in r['ents'].items() if i)
        out.append('t%03d hero(%#x) inuse=%#x ph=%d cnt=%d mp=%d ptr=s%s id=%d count=%d rng0=%08x rng1=%08x draws=%s | %s | %s' % (
            r['t'], r['hero_anim_t'], r['inuse'], r['phase'], r['counter'], r['mp'], r['ptr_slot'], r['ident'], r['count'], r['rng0'], r['rng1'], r['draws'], es, ';'.join(evs)))
    open(path, 'w').write('\n'.join(out) + '\n')
