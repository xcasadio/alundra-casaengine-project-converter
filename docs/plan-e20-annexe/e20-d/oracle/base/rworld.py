"""rworld.py - E20.d discovery ("run-oracle"): the run scenarios on the REAL code of ALUN_CD.EXE (France) in the MIPS emulator of E20.0 / E20.c1 / c1b / c2.

New file.  NOTHING in tools/ (28 copied .py), hworld.py, hscen.py is edited: this module adds, by pokes after the c2 setup and by a monkey patch of world.build_world during the
machine build, what the run scenarios need:

  * a CELL LAYOUT painted BEFORE the hero is placed (the real InitializeEntity 0x80039D04 then raises the hero to terrain + 1 on the painted ground; hworld.paint paints after
    the placement, which is fine for a uniform fill but leaves a hero below a raised ground);
  * a pad driver: piecewise-constant hold word per tick (the Python port of AlundraTickPad, cross-checked every tick with the real routine 0x8002E250, as hworld.htick does);
  * a DASH-NIBBLE OVERRIDE (what-if of D-E20-15): at the two sites where the binary looks up g_dashDirections[hold >> 12] (0x80031FD0 in PrepareSprint 0x29, 0x800321A0 in
    StopSprint 0x2A) register s3 is replaced by a nibble chosen by the harness.  s3 is dead after the lookup (both paths jump to the epilogue), so the override reaches nothing else;
  * a per-tick trace with the columns of the plan (state, animation target / current, direction, forces, position, ground, counters, sounds, effects and stub calls).

Nothing here knows an expected value.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
sys.path.insert(0, HERE)
import c1world as C
import world as W
import kworld as K
import hworld as H
from machine import M32, s32

HERO = W.ENT0
SPRINT_CNT = 0x80126FF4          # StopSprint resume counter (MovePlayer only)
CHARGE_CNT = 0x80126FFC          # charge counter (0x8002F49C and MovePlayer)
WPTR = 0x80127110                # pending delayed-shot sheet pointer
AURA = 0x80126FF8                # charge aura entity pointer

PX = 65536
DASH_SITES = (0x80031FD0, 0x800321A0)          # sll v1,s3,2 : the two g_dashDirections lookups (PrepareSprint, StopSprint)

# pad bits
UP, RIGHT, DOWN, LEFT = W.UP, W.RIGHT, W.DOWN, W.LEFT
TRI, CIR, CRO, SQU = W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE


# ---- cell layout ------------------------------------------------------------------------------------------------------------------------
class Layout:
    """a 52 x 60 field of 24 x 16 px cells (col = x // 24, row = y // 16).  rect() paints an inclusive cell rectangle; later rectangles win.
       A cell is (height byte, slope byte, walkability byte, ground-property byte); `word` sets walkability | ground_property << 8 at once (the u16 the binary reads at the cell)."""
    def __init__(self):
        self.rects = []

    def rect(self, c0, r0, c1, r1, hb=None, sl=None, wk=None, gp=None, word=None):
        if word is not None:
            wk, gp = word & 0xFF, (word >> 8) & 0xFF
        self.rects.append((c0, r0, c1, r1, hb, sl, wk, gp))
        return self

    def cell(self, col, row):
        hb, sl, wk, gp = 0, 0, 0, 0
        for c0, r0, c1, r1, h, s, w, g in self.rects:
            if c0 <= col <= c1 and r0 <= row <= r1:
                if h is not None: hb = h
                if s is not None: sl = s
                if w is not None: wk = w
                if g is not None: gp = g
        return (hb, sl, wk, gp)

    def describe(self):
        return [dict(c0=c0, r0=r0, c1=c1, r1=r1, height_byte=h, slope_byte=s, walk=w, gprop=g) for c0, r0, c1, r1, h, s, w, g in self.rects]


# ---- machine build ------------------------------------------------------------------------------------------------------------------------
class _Cells:
    """patches world.build_world so that the field is painted at the world build (before the hero's InitializeEntity)"""
    def __init__(self, layout):
        self.layout = layout

    def __enter__(self):
        self.orig = W.build_world
        lay = self.layout

        def bw(m, gravity=128, visc=4096, cells=None):
            return self.orig(m, gravity=gravity, visc=visc, cells=(lay.cell if lay is not None else cells))
        W.build_world = bw
        return self

    def __exit__(self, *a):
        W.build_world = self.orig


BOX = {'val': None, 'hits': []}      # the override nibble shared by both interpreters (None = no override)
COV = set()                          # every PC executed by the emu engine over all runs of this process (load-delay hazard check)
import interp2 as _interp2
_orig_cpu2_step = _interp2.Cpu2.step


def _cpu2_step(self):
    """the same override for the second interpreter (mips.Cpu through interp2.Cpu2); dormant while BOX['val'] is None"""
    if BOX['val'] is not None and self.pc in DASH_SITES:
        self.r[19] = BOX['val'] & 0xF
        BOX['hits'].append((self.pc, BOX['val']))
    return _orig_cpu2_step(self)


_interp2.Cpu2.step = _cpu2_step


def install_dash_override(m, box):
    """box = {'val': None | nibble}; while box['val'] is not None the two lookups read the nibble `val` instead of hold >> 12"""
    orig = m.exec1

    def ex(pc, w):
        if pc in DASH_SITES and box['val'] is not None:
            m.r[19] = box['val'] & 0xF
            box.setdefault('hits', []).append((m.tick_no, pc, box['val']))
        return orig(pc, w)
    m.exec1 = ex


def setup_r(spec):
    """c2 setup (hworld.setup_h) with the layout painted first.  spec: name, hero_px, direction, targets, kind, layout (Layout or None), inv, hp, hpmax, seed, setup (fn(m) at the end),
       hero_anim (initial TargetAnimationId, default 0)"""
    inv = H.INV[spec.get('inv', 'kit')]
    kw = dict(hero_px=spec['hero_px'], targets=spec.get('targets', ()), direction=spec.get('direction', 24), seed=spec.get('seed', 0x12345678), kind=spec.get('kind'),
              hero_anim=spec.get('hero_anim', 0))
    exp = [] if spec.get('experiments') else None
    if exp is not None:
        kw['experiments'] = exp
    with _Cells(spec.get('layout')):
        m, meta = K.setup_inv(inv, **kw)
    meta['_experiments'] = exp
    hp, hpmax = spec.get('hp', 10), spec.get('hpmax', 10)
    m.call(0x8004DC68, hpmax & M32)
    m.call(0x8004DD30, hp & M32)
    got_max = H.s16(m.call(0x8004DC50))
    got_hp = H.s16(m.call(0x8004DD18))
    m.poke(HERO + 0x18, got_max)
    m.poke(HERO + 0x14, got_hp)
    W.write_pad(m, W.Pad())
    m.call(0x800307E8)
    for a in (H.REGEN, H.REGEN + 4, H.REGEN + 8, H.ITEM_TICKS, H.ITEM_STATE):
        m.poke(a, 0)
    m.poke(0x80134360, max([0] + list(meta['target_slots'])))
    if spec.get('setup'):
        spec['setup'](m)
    m.uninit_reads.clear()
    m.journal.clear()
    meta['inventory'] = dict(name=inv['name'], owned=list(inv['owned']), item=inv['item'])
    if spec.get('layout') is not None:
        meta['layout'] = spec['layout'].describe()
    return m, meta


# ---- readers ------------------------------------------------------------------------------------------------------------------------------
HERO_COLS = [   # (name, offset, signed)
    ('anim_target', 0x88, 1), ('dir_target', 0x8C, 1), ('anim_cur', 0x90, 1), ('dir_cur', 0x94, 1), ('facing', 0x98, 1),
    ('force_x', 0xC4, 1), ('force_y', 0xC8, 1), ('force_z', 0xB8, 1),
    ('pos_x', 0x114, 1), ('pos_y', 0x118, 1), ('pos_z', 0x11C, 1),
    ('on_ground', 0x144, 1), ('coll_z', 0x140, 1), ('force_adj', 0x13C, 1), ('coll_ent', 0x130, 0), ('terrain', 0x138, 1), ('floor', 0x134, 1),
    ('vram_or', 0x180, 0), ('slope', 0x18C, 1), ('b0_complete', 0xB0, 1), ('anim_end_AC', 0xAC, 1), ('anim_flags_B4', 0xB4, 0),
    ('cnt1c', 0x1C, 1), ('inv_1cc', 0x1CC, 1), ('hp', 0x14, 1), ('carried_2c', 0x2C, 0), ('cur_attack', 0x1C8, 0), ('frame_box', 0x1D4, 0),
    ('hit_counter', 0x220, 1), ('touching', 0x224, 0), ('izf_F8', 0xF8, 1),
]


def snap_hero(m):
    d = {}
    for name, off, sg in HERO_COLS:
        d[name] = m.peeks(HERO + off) if sg else m.peek(HERO + off)
    d['sprint_cnt'] = m.peeks(SPRINT_CNT)
    d['charge_cnt'] = m.peeks(CHARGE_CNT)
    d['wptr'] = m.peek(WPTR)
    d['aura'] = m.peek(AURA)
    d['pos_z_dll'] = d['pos_z'] - 1
    return d


def snap_target(m, slot):
    d = C.snap_entity(m, slot)
    return {k: d[k] for k in ('hp', 'anim_cur', 'pos_x', 'pos_y', 'pos_z', 'c1cc', 'c1d0', 'hit_counter', 'touching_slot', 'frame_box_1D4', 'atk_attr', 'atk_power', 'force_x', 'force_y')}


def journal_items(m, j0):
    out = []
    for j in m.journal[j0:]:
        kind = j[1]
        if kind.startswith('pass_') or kind == 'handler':
            continue
        a = C.trim_args(kind, j[2]) if kind != 'sound' else j[2]
        if kind == 'sound':
            out.append(('sound', [a if not isinstance(a, list) else a[0]]))
        elif kind == 'effect_3BDD8':
            out.append(('effect', dict(id=a[1], cx=s32(a[3]) / PX, cy=s32(a[4]) / PX, cz=s32(a[5]) / PX)))
        elif kind == 'effect_3BE74':
            t = a[3]
            out.append(('effect_on', dict(id=a[1], target=(('slot %d' % ((t - HERO) // W.STRIDE)) if HERO <= t < HERO + 64 * W.STRIDE else hex(t)))))
        elif kind == 'spawn_39F58':
            out.append(('spawn', [x for x in a]))
        else:
            out.append((kind, a))
    return out


def fmt_items(items):
    parts = []
    for k, v in items:
        if k == 'sound':
            parts.append('snd%d' % v[0])
        elif k == 'effect':
            parts.append('fx%d@(%.1f,%.1f,%.1f)' % (v['id'], v['cx'], v['cy'], v['cz']))
        elif k == 'effect_on':
            parts.append('fx%d->%s' % (v['id'], v['target']))
        elif k == 'spawn':
            parts.append('spawn(%s)' % ','.join(str(s32(x)) for x in v[:4]))
        else:
            parts.append(k)
    return ' '.join(parts)


# ---- pad driver -----------------------------------------------------------------------------------------------------------------------------------
def piecewise(pieces, default=0):
    """pieces: [(from_tick, value), ...] ascending; value holds from from_tick until the next piece (tick 1 is the first tick)"""
    ps = sorted(pieces)

    def f(t):
        v = default
        for a, b in ps:
            if t >= a:
                v = b
            else:
                break
        return v
    return f


def run_r(spec, engine='emu'):
    """spec: name, desc, n, pad (list of pieces or callable t -> hold), dash (list of pieces for the nibble override, value None = no override; default none), events {tick: fn(m, slots)},
       plus the setup_r keys.  Returns dict(meta, pre, rows, machine)."""
    K.use_engine(engine)
    m, meta = setup_r(spec)
    slots = meta['target_slots']
    for sl, fn in (spec.get('target_init') or {}).items():
        fn(m, slots[sl])
    box = BOX
    box['val'] = None
    box['hits'] = []
    if spec.get('dash') is not None and engine == 'emu':
        install_dash_override(m, box)
    pad = W.Pad()
    padf = spec['pad'] if callable(spec['pad']) else piecewise(spec['pad'])
    dashf = None
    if spec.get('dash') is not None:
        dashf = spec['dash'] if callable(spec['dash']) else piecewise(spec['dash'], None)
    events = spec.get('events', {})

    def snap(tick, hold):
        return dict(tick=tick, hold=hold, hero=snap_hero(m), targets=[snap_target(m, s) for s in slots], rng=m.peek(C.RNG))
    pre = snap(0, 0)
    rows = []
    for t in range(1, spec['n'] + 1):
        if t in events:
            events[t](m, slots)
        m.tick_no = t
        j0 = len(m.journal)
        hold = padf(t)
        box['val'] = dashf(t) if dashf else None
        H.htick(m, pad, hold, engine, True)
        r = snap(t, hold)
        r['dash_override'] = box['val']
        r['journal'] = journal_items(m, j0)
        rows.append(r)
    meta['uninit_reads'] = sorted((hex(pc), hex(a), n, c) for (pc, a, n), c in m.uninit_reads.items())
    meta['dash_hits'] = list(box.get('hits', []))
    box['val'] = None
    COV.update(m.cov)
    K.use_engine('emu')
    exps = meta.pop('_experiments', None) or []
    return dict(spec=spec, meta=meta, pre=pre, rows=rows, machine=m, engine=engine, experiments=exps)


# ---- text trace ---------------------------------------------------------------------------------------------------------------------------------
HOLD_NAMES = [(UP, 'U'), (RIGHT, 'R'), (DOWN, 'D'), (LEFT, 'L'), (TRI, 'T'), (CIR, 'O'), (CRO, 'X'), (SQU, 'S')]


def hold_str(h):
    s = ''.join(n for b, n in HOLD_NAMES if h & b)
    return s or '-'


def fmt_px(v):
    return '%.2f' % (v / PX)


def trace_text(res, with_targets=True):
    spec = res['spec']
    lines = ['# %s: %s' % (spec['name'], spec.get('desc', '')),
             '# columns: t hold | anim target>current (b0 = AnimCompleteCounter) | dir target/cur facing | ForceX ForceY ForceZ | pos x y z (px; z = binary convention, rest = terrain + 1) | '
             'ground adj(+0x13C) collZ | slope vorOR | sprintCnt chargeCnt | journal (sounds snd<id>, effects fx<id>@(x,y,z px), spawns)']
    if with_targets and res['rows'] and res['rows'][0]['targets']:
        lines.append('# targets: tN[hp anim c1d0 hits touch]')
    cols = ('%4s %-5s | %-9s b0=%-2s | %3s/%-3s f%-2s | %8s %8s %8s | %8s %8s %6s | g%d adj%-2s cz%-2s | sl%d v%04X | sp%-2s ch%-2s | %s')

    def line(r):
        h = r['hero']
        it = fmt_items(r.get('journal', []))
        s = cols % (r['tick'], hold_str(r['hold']), '%02X>%02X' % (h['anim_target'] & 0xFF, h['anim_cur'] & 0xFF), h['b0_complete'], '%02X' % (h['dir_target'] & 0xFF), '%02X' % (h['dir_cur'] & 0xFF),
                    h['facing'], h['force_x'], h['force_y'], h['force_z'], fmt_px(h['pos_x']), fmt_px(h['pos_y']), fmt_px(h['pos_z']),
                    h['on_ground'], h['force_adj'], h['coll_z'], h['slope'], h['vram_or'] & 0xFFFF, h['sprint_cnt'], h['charge_cnt'], it)
        if with_targets:
            for i, t in enumerate(r['targets']):
                s += ' | t%d[hp%d a%X d%d h%d tc%s]' % (i, t['hp'], t['anim_cur'] & 0xFF, t['c1d0'], t['hit_counter'], t['touching_slot'])
        return s
    lines.append(line(dict(res['pre'], journal=[])))
    for r in res['rows']:
        lines.append(line(r))
    return '\n'.join(lines) + '\n'


def timeline(res, key='anim_cur'):
    """run-length list of (first tick, last tick, value) for a hero column"""
    out = []
    prev = None
    for r in [res['pre']] + res['rows']:
        v = r['hero'][key]
        if prev is None or v != prev[2]:
            if prev:
                out[-1] = (prev[0], r['tick'] - 1, prev[2])
            out.append((r['tick'], r['tick'], v))
            prev = out[-1]
    if out:
        out[-1] = (out[-1][0], res['rows'][-1]['tick'] if res['rows'] else 0, out[-1][2])
    return out


def timeline_str(res, key='anim_cur', fmt='%02X'):
    return ' '.join('%d-%d:%s' % (a, b, fmt % (v & 0xFF)) for a, b, v in timeline(res, key))
