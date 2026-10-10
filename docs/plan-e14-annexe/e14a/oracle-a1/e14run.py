"""e14run.py - E14.a1 oracle: the runner (one scenario -> rows, per-call records, probes), the event helpers, and the readers.

A scenario spec holds SETUP and INPUTS only (no expected value):
    name, desc, map (None = flat field, else a map number whose cells are loaded), kind ('muruta' | 'beannoide'), recs (see e14world.spawn), hero_px, direction, seed, n,
    holds {tick: pad word}, after (pad word for the other ticks), before {tick: [event, ...]} (harness writes made between two ticks), ghost (keep the hero untouchable: +0x1CC := 0x78
    before every tick, so the hit pass never lets a claw reach him; the hero's own state is then independent of the enemy), probes (record ids whose 0x2C count is read before every tick),
    frame_advance (apply the binary's per-frame RNG advance after every tick).
Row t = state AFTER the passes of tick t (tick 0 = start).  `counts` of row t = the 0x2C [N] search made by the map events pass of tick t, i.e. BEFORE the entity block of tick t.
"""
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import e14world as E
import c1world as C
import world as W
import hworld as H
import interp2
from machine import M32, s32

ENT0, STRIDE = E.ENT0, E.STRIDE
SQ, RIGHT, UP, DOWN, LEFT = W.SQUARE, W.RIGHT, W.UP, W.DOWN, W.LEFT


def ent(slot):
    return ENT0 + STRIDE * slot


# ---- events (harness writes between two ticks; every one is data of the spec) --------------------------------------------------------------
def ev_poke(slot, off, val):
    return lambda m, slots: m.poke(ent(slot) + off, val)


def ev_anim(slot, anim):
    """TargetAnimationId := anim (stands for an animation the enemy's own program would have chosen)"""
    return ev_poke(slot, 0x88, anim)


def ev_state(slot, anim=None, direction=None, ai1=None, b0=None, b1=None, b3=None):
    """write the C-handler state of an enemy: TargetAnimationId (+0x88), TargetDirection (+0x8C), AIValues[1] (u16 +0x282), Bytes[0] (+0x274), Bytes[1] (+0x275), Bytes[3] (+0x277)"""
    def f(m, slots):
        e = ent(slot)
        if anim is not None: m.poke(e + 0x88, anim)
        if direction is not None: m.poke(e + 0x8C, direction)
        if ai1 is not None: m.poke16(e + 0x282, ai1)
        if b0 is not None: m.poke8(e + 0x274, b0)
        if b1 is not None: m.poke8(e + 0x275, b1)
        if b3 is not None: m.poke8(e + 0x277, b3)
    return f


def ev_hero_to(x_px, y_px):
    """put the hero at (x_px, y_px) exactly (16.16 positions), tile fields consistent (TileX = x // 24, TileY = y // 16 as the physics pass writes them), forces zeroed"""
    def f(m, slots):
        h = ent(0)
        m.poke(h + 0x114, int(round(x_px * 65536)))
        m.poke(h + 0x118, int(round(y_px * 65536)))
        m.poke(h + 0x120, int(x_px) // 24)
        m.poke(h + 0x124, int(y_px) // 16)
        for off in (0xBC, 0xC0, 0xC4, 0xC8, 0xD4, 0xD8):
            m.poke(h + off, 0)
    return f


def ev_hero_beside(slot, dx, dy=0):
    """put the hero dx px WEST and dy px SOUTH of the enemy in slot `slot` (its position at this moment), forces zeroed, tile fields consistent"""
    def f(m, slots):
        t = ent(slot)
        x = s32(m.peek(t + 0x114)) / 65536.0 - dx
        y = s32(m.peek(t + 0x118)) / 65536.0 + dy
        ev_hero_to(x, y)(m, slots)
    return f


def ev_hp(slot, hp):
    return ev_poke(slot, 0x14, hp)



class Swordsman:
    """a player policy, deterministic (reads the machine, no randomness): kill the enemies in `order` (slots) one after another with the sword.  From tick `start` on, each time the oracle's hero is free
       (cooldown over) and the current target is hittable now (status 2, on the ground, +0x1D0 lock expired, animation without the immunity bit 0x40) and the hero can stand 40 / 34 / 28 / 22 px (the first of
       `dxs` that fits) west of it (the four corners of the hero's footprint, offset (-10, -7) size (21, 15), lie on walkable cells of the target's floor height; always true on the flat field), the hero is put there (forces zeroed, tile fields consistent) and
       Square is pressed; the next press is `cooldown` ticks later.  `log` keeps (tick, slot, dx).  The hero faces east all along (place_hero direction 24)."""
    def __init__(self, order, dxs=(40,), cooldown=25, start=1, mapnum=None):
        self.order = list(order)
        self.dxs = tuple(dxs)
        self.cool = 0
        self.start = start
        self.log = []
        self.cells = E.cells_fn(mapnum) if mapnum else None

    def _ok(self, x, y, floor_units):
        if self.cells is None:
            return True
        for ex in (-11, 12):                     # the hero's footprint: offset (-10, -7), size (21, 15) px (0x800370C4 samples these corners)
            for ey in (-8, 9):
                col = int((x + ex) // 24)
                row = int((y + ey) // 16)
                if not (0 <= col < 52 and 0 <= row < 60):
                    return False
                hb, sl, wk, gp = self.cells(col, row)
                if wk != 0 or hb != floor_units:
                    return False
        return True

    def step(self, m, slots, t):
        if t < self.start:
            return None
        if self.cool:
            self.cool -= 1
            return None
        for s in self.order:
            e = ent(s)
            if m.peek(e + 0x10) in (1, 2, 3):
                break
        else:
            return None
        if m.peek(e + 0x10) != 2 or not m.peek(e + 0x144) or m.peeks(e + 0x1D0) != 0 or (m.peek(e + 0xB4) & 0x40):
            return None
        x = s32(m.peek(e + 0x114)) / 65536.0
        y = s32(m.peek(e + 0x118)) / 65536.0
        fl_units = (s32(m.peek(e + 0x138)) >> 20)           # terrain height of the target in cell units (16 px)
        for dx in self.dxs:
            if self._ok(x - dx, y, fl_units):
                ev_hero_beside(s, dx)(m, slots)
                self.cool = 25
                self.log.append((t, s, dx))
                return SQ
        return None


# ---- running -------------------------------------------------------------------------------------------------------------------------
def snap_row(m, tick, hold, slots, probes_counts, j0, c0, hero_pre=None):
    row = {'tick': tick, 'hold': hold, 'rng': m.peek(E.RNG), 'latch': s32(m.peek(E.LATCH))}
    raw_h = m._raw(ENT0)
    row['hero'] = E.decode(raw_h)
    row['targets'] = [E.decode(m._raw(ent(s))) for _, s in slots]
    row['journal'] = [(j[1], C.trim_args(j[1], j[2])) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] != 'handler']
    row['letters'] = [(j[2][0], j[2][1], j[2][2]) for j in m.journal[j0:] if j[1] == 'handler']      # stub handlers called (slot, letter, address)
    row['calls'] = [(c['slot'], c['handler'], c['letter']) for c in m.calls_log[c0:]]
    row['counts'] = probes_counts
    return row


def run(spec, engine='emu', keep_machine=False):
    E_ = E
    H_ = H
    m, meta = E.setup(spec)
    slots = meta['slots']
    pad = W.Pad()
    probes = spec.get('probes') or []
    m.watching = (engine == 'emu')
    m.tick_no = 0
    pre = snap_row(m, 0, 0, slots, {}, 0, 0)
    rows = []
    before = spec.get('before', {})
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    ghost = spec.get('ghost', True)
    for t in range(1, spec['n'] + 1):
        for fn in before.get(t, ()):
            fn(m, slots)
        if ghost:
            m.poke(ENT0 + 0x1CC, 0x78)
        m.tick_no = t
        counts = {}
        for rid in probes:                       # map events pass: BEFORE the entity block of this tick (UpdateWorld: 0x8003C67C, then 0x8003B388)
            res, _v0 = E.count_op2c(m, rid, engine=engine)
            counts[rid] = (E.count_search(m, rid, engine=engine), res)
        j0 = len(m.journal)
        c0 = len(m.calls_log)
        hold = holds.get(t, after)
        ctrl = spec.get('controller')
        if ctrl is not None:
            pw = ctrl.step(m, slots, t)
            if pw is not None:
                hold = pw
        H.htick(m, pad, hold, engine, True)
        if spec.get('frame_advance'):
            m.poke(E.RNG, E.lcg(m.peek(E.RNG)))
        rows.append(snap_row(m, t, hold, slots, counts, j0, c0))
    out = dict(ctrl_log=list(spec['controller'].log) if spec.get('controller') is not None else [], spec=spec, meta=meta, pre=pre, rows=rows, calls=list(m.calls_log), cov=set(m.cov), uninit=sorted((hex(pc), hex(a), n, c) for (pc, a, n), c in m.uninit_reads.items()),
               engine=engine)
    out['final_md5'] = hashlib.md5(bytes(m.ram[ENT0 & 0x1FFFFFFF:(ENT0 & 0x1FFFFFFF) + STRIDE * 8])).hexdigest()
    if keep_machine:
        out['machine'] = m
    return out


# ---- readers ---------------------------------------------------------------------------------------------------------------------------
def px(v):
    return '%.3f' % (v / 65536.0) if v % 65536 else str(v // 65536)


def tgt_series(res, i, name):
    return [res['pre']['targets'][i][name]] + [r['targets'][i][name] for r in res['rows']]


def changes_of(res, i, name, who='targets'):
    """[(tick, old, new)] of a decoded field of target i (or of the hero when who == 'hero')"""
    out = []
    prev = None
    rows = [res['pre']] + res['rows']
    for r in rows:
        v = r['hero'][name] if who == 'hero' else r['targets'][i][name]
        if prev is not None and v != prev:
            out.append((r['tick'], prev, v))
        prev = v
    return out


def call_list(res, slot=None, handler=None):
    return [c for c in res['calls'] if (slot is None or c['slot'] == slot) and (handler is None or c['handler'] == handler)]


def call_summary(c):
    b = E.decode(c['ent_before'])
    a = E.decode(c['ent_after'])
    diff = {k: (b[k], a[k]) for k in b if a[k] != b[k]}
    return b, a, diff
