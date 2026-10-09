"""ext.py - E20.f1 oracle extension (a COPY of the E20.b / f-weapons attack oracle in tools/, which is left byte-for-byte as copied).

What this adds to the discovery oracle (tools/ + probe_orig.py):

 (a) A real effect pool.  The discovery oracle stubbed the two effect-create entry points (0x8003BDD8 and 0x8003BE74) and handed back ONE shared
     zeroed dummy slot.  Here the stubs are removed and the binary's own code runs: the allocator 0x8003B9C4 (first of 128 slots, 0x80 bytes each, at
     0x80138608, with +0x68 == 0), the initialiser 0x8003BB14 (copies the template slot at 0x8013C608, keeps word 0, sets +4 +0x30 +0x34 +0x68=2
     +0x6C..+0x71 and the position +0x3C +0x40 +0x44) and the two entry points (0x8003BE74 also sets +0x38 anchor, +0x60, +0x48 +0x4C +0x50).
     Observers (not stubs: they do not replace any instruction) journal the arguments at entry and the returned slot at the final jr $ra.
     The template slot is zero (INFERENCE: the only code that refers to it is the copy at 0x8003BB20; nothing writes it; BSS is zero).
     The table is initialised like 0x8003C17C does (word 0 of slot i = i) and +0x68 = 0 everywhere (like 0x8003C1A4).
     A slot is never freed unless the harness says so (the effect pass 0x8003C410 is not run by the oracle): see free_pass().
 (b) Every row records the RNG word 0x80098708 (before the tick, after the passes, after the optional frame advance), the hero's +0x1C (already in
     the row as cnt1c), the aura handle 0x80126FF8 and, for each effect created in the tick, the slot fields at the END of the tick.
 (c) Two optional models, both OFF by default (= the discovery oracle's behaviour):
     frame_rng  : the per-frame advance 0x8002BD0C-0x8002BD34 of the frame function 0x8002BAEC (x = x * 0x7D2B89DD + 0xE06A02E7), applied after the passes
     effect_free: the free path of the effect pass 0x8003C410 (0x8003C444-0x8003C47C): slot +0x68 == 2, ([0x800DC4B8] & 0x48) == 0, +0x7D != 0 -> +0x68 = 0
     effect_life: {effect id: ticks} on top of effect_free: the harness sets +0x7D itself that many ticks after the creation tick (the binary sets it when the animation
                  script reaches its end op; that update path is not run).  The ticks come from the exported bank (probe_ext.life_table()).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import lib
import machine as MM
import attack as A
import world as W
from machine import M32, s32

RNG = 0x80098708
EFFECT_TABLE = 0x80138608
EFFECT_N = 128
EFFECT_STRIDE = 0x80
EFFECT_TEMPLATE = EFFECT_TABLE + EFFECT_N * EFFECT_STRIDE        # 0x8013C608
CREATE, CREATE_ATT = 0x8003BDD8, 0x8003BE74
RETURN_PC = {CREATE: 0x8003BE6C, CREATE_ATT: 0x8003BF3C}          # the jr $ra of each entry point
AURA_HANDLE = 0x80126FF8
FLAGS_800DC4B8 = 0x800DC4B8
HERO = W.ent(0)
HF = dict(x=0x114, y=0x118, z=0x11C, fx=0xC4, fy=0xC8, fz=0xB8)
LCG_A, LCG_C = 0x7D2B89DD, 0xE06A02E7
IMAGE_SEED = 0xB017C93D                                            # the word 0x80098708 holds in the EXE image itself


def lcg(s):
    return (s * LCG_A + LCG_C) & M32


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def init_pool(m):
    m.zero(EFFECT_TABLE, (EFFECT_N + 1) * EFFECT_STRIDE)          # 128 slots and the template slot (zero)
    for i in range(EFFECT_N):
        m.poke(EFFECT_TABLE + i * EFFECT_STRIDE, i)                # 0x8003C17C: word 0 of slot i = i


def slot_index(addr):
    return (addr - EFFECT_TABLE) // EFFECT_STRIDE


class ExtMachine(MM.Machine):
    """MM.Machine with the real effect-create code and observers instead of the two effect stubs."""

    def __init__(self):
        self.observers = {}
        self.created = {}           # slot address -> tick of its creation (for the optional lifetime model)
        self.slot_log = []          # effect calls of the current tick, in order
        self._open = []
        super().__init__()
        init_pool(self)

    def install_stubs(self):
        super().install_stubs()
        del self.hooks[CREATE]
        del self.hooks[CREATE_ATT]
        for name, fn in (('effect_3BDD8', CREATE), ('effect_3BE74', CREATE_ATT)):
            self.observers[fn] = self._entry(name, fn)
            self.observers[RETURN_PC[fn]] = self._exit(fn)

    def _entry(self, name, fn):
        def f(m):
            sp = m.r[29]
            args = [m.r[4], m.r[5], m.r[6], m.r[7]] + [m.peek(sp + 0x10 + 4 * i) for i in range(6)]
            args = [x & M32 for x in args]
            m.journal.append((m.tick_no, name, args))            # same record as the discovery stub: events text is unchanged
            c = dict(tick=m.tick_no, fn=fn, name=name, args=args, rng_entry=m.peek(RNG), handle_entry=m.peek(AURA_HANDLE), slot=0, idx=-1,
                     hero={k: m.peeks(HERO + o) for k, o in HF.items()})
            m._open.append(c)
            m.slot_log.append(c)
        return f

    def _exit(self, fn):
        def f(m):
            c = m._open.pop()
            assert c['fn'] == fn
            c['slot'] = m.r[2] & M32
            c['idx'] = slot_index(c['slot']) if c['slot'] else -1
            if c['slot']:
                m.created[c['slot']] = m.tick_no
        return f

    def exec1(self, pc, w):
        ob = self.observers.get(pc)
        if ob is not None:
            ob(self)
        return super().exec1(pc, w)


def free_pass(m, life=None):
    """Model of the effect pass 0x8003C410 (0x8003C444-0x8003C47C), run after the eight passes of the tick like 0x8002E058 does (0x8002E110):
    an active slot (+0x68 == 2) whose destroy flag +0x7D is non-zero is freed (+0x68 = 0); unless ([0x800DC4B8] & 0x48) the whole pass is skipped.
    The update path of the binary (0x8003BBDC: animation script advance, which SETS +0x7D when the script reaches its end op, 0x8003BD58-0x8003BD68) is NOT run;
    `life` = {effect id: number of ticks after the creation tick at which the script's end op is reached} lets the harness set +0x7D itself at that pass.
    Without `life` nothing but the harness ever sets +0x7D."""
    if m.peek(FLAGS_800DC4B8) & 0x48:
        return
    for i in range(EFFECT_N):
        a = EFFECT_TABLE + i * EFFECT_STRIDE
        if m.peek(a + 0x68) != 2:
            continue
        if peek8(m, a + 0x7D) != 0:
            m.poke(a + 0x68, 0)
            m.created.pop(a, None)
            continue
        if life:
            eid = peek8(m, a + 0x6E)
            if eid in life and m.tick_no - m.created.get(a, m.tick_no) >= life[eid]:
                m.poke8(a + 0x7D, 1)


SLOT_FIELDS = dict(x=0x3C, y=0x40, z=0x44, fx=0x54, fy=0x58, s68=0x68, s7d=0x7D, s34=0x34, s38=0x38, s60=0x60, s48=0x48, s4c=0x4C, s50=0x50, eid=0x6E)


def peek8(m, a):
    return m.ram[a & 0x1FFFFFFF]


def read_slot(m, a):
    d = {}
    for k, o in SLOT_FIELDS.items():
        if k in ('s7d', 'eid'):
            d[k] = peek8(m, a + o)
        else:
            d[k] = m.peeks(a + o)
    return d


def run_ext(spec, frame_rng=False, effect_free=False, seed=None, after_tick=None, effect_life=None):
    """spec as attack.run (name, holds, n, after, item, before, cells, warm...).  Returns dict(rows, machine, meta).
    after_tick: optional fn(m, t) called after tick t has been recorded (harness actions between ticks)."""
    A.Machine = ExtMachine
    kw = dict(item=spec.get('item'), cells=spec.get('cells'), start=spec.get('start', (A.START_X, A.START_Y)), mutate=spec.get('mutate'),
              no_weapon=spec.get('no_weapon', False))
    if seed is not None:
        kw['seed'] = seed
    m, info = A.setup(**kw)
    setup_slots = len(m.slot_log)
    init_pool(m)                                       # a clean pool at tick 1 whatever setup did
    m.created.clear()
    m.slot_log.clear()
    m._open.clear()
    pad = W.Pad()
    if spec.get('warm') == 'east':
        raise NotImplementedError('warm-up not needed here')
    hold_after = spec.get('after', 0)
    rows = []
    holds = spec['holds']
    before = spec.get('before', {})
    state = {}

    def ticker(mm, pd, hold, skip=()):
        W.tick(mm, pd, hold, skip=skip)
        state['rng_passes'] = mm.peek(RNG)
        if effect_free or effect_life:
            free_pass(mm, effect_life)
        if frame_rng:
            mm.poke(RNG, lcg(mm.peek(RNG)))

    for t in range(1, spec['n'] + 1):
        if t in before:
            before[t](m)
        m.slot_log.clear()
        rng0 = m.peek(RNG)
        fc_in = m.peek(HERO + 0x1C)
        A.step(m, pad, holds.get(t, hold_after), rows, t, ticker, ())
        r = rows[-1]
        r['rng_before'] = rng0
        r['fc_in'] = fc_in
        r['rng_passes'] = state['rng_passes']
        r['rng_after'] = m.peek(RNG)
        h = m.peek(AURA_HANDLE)
        r['aura_handle'] = h
        r['aura_idx'] = slot_index(h) if h else -1
        r['aura_68'] = m.peeks(h + 0x68) if h else None
        r['aura_7d'] = peek8(m, h + 0x7D) if h else None
        calls = []
        for c in m.slot_log:
            c = dict(c)
            c['f'] = read_slot(m, c['slot']) if c['slot'] else None
            calls.append(c)
        r['calls'] = calls
        if after_tick:
            after_tick(m, t)
    A.COV.update(m.cov)
    return dict(rows=rows, machine=m, meta=info, setup_slots=setup_slots, spec=spec)


# ---- formatting -----------------------------------------------------------------------------------------------------------------------
def collapse(events):
    out = []
    for e in events:
        k = e.split('(')[0] + '(' + (e.split('id=')[1].split(',')[0] if 'id=' in e else '')
        if out and out[-1][0] == k:
            out[-1][1] += 1
        else:
            out.append([k, 1, e])
    return ['%s x%d' % (o[2][:60], o[1]) if o[1] > 1 else o[2][:140] for o in out]


def brief_ext(r):
    """the discovery columns exactly as the STORED outputs have them (hc=.. b0=.. then the events), plus fc, rng, aura between b0 and the events"""
    aura = '%08X(%s,%s)' % (r['aura_handle'], r['aura_68'], r['aura_7d']) if r['aura_handle'] else '--------'
    return ('t%3d hold=%04X anim_t=%02X anim_c=%02X fx=%7d fy=%7d fz=%7d pz=%7d gnd=%d chg=%2d ptr=%08X cnt=%2d hc=%d b0=%d fc=%d>%d rng=%08X>%08X>%08X aura=%s  %s' % (
        r['tick'], r['hold'], r['anim_target'], r['anim_cur'], r['force_x'], r['force_y'], r['force_z'], r['pos_z_dll'], r['on_ground'], r['charge_timer'], r['rec_ptr'],
        r['rec_cnt'], r['hit_counter_220'], r['b0'], r['fc_in'], r['cnt1c'], r['rng_before'], r['rng_passes'], r['rng_after'], aura, ' | '.join(collapse(r['events']))))


def call_line(c):
    f = c['f']
    kind = 'att' if c['fn'] == CREATE_ATT else 'new'
    if not c['slot']:
        return '      call %-3s id=0x%02X -> NULL (pool full)' % (kind, c['args'][1])
    s = '      call %-3s id=0x%02X slot#%03d @%08X rng_in=%08X  x=%d y=%d z=%d fx=%d fy=%d  +68=%d +7D=%d' % (
        kind, c['args'][1], c['idx'], c['slot'], c['rng_entry'], f['x'], f['y'], f['z'], f['fx'], f['fy'], f['s68'], f['s7d'])
    if c['fn'] == CREATE_ATT:
        s += '  [+34=%d +38=%08X +60=%d +48=%d +4C=%d +50=0x%X]' % (f['s34'], f['s38'] & M32, f['s60'], f['s48'], f['s4c'], f['s50'])
    return s


def write_run(path, name, res, extra_header=''):
    with open(path, 'w') as fh:
        fh.write('# %s item=%s  setup_effect_calls=%d  %s\n' % (name, res['spec'].get('item'), res['setup_slots'], extra_header))
        fh.write('# columns = discovery columns (probe.py + b0) then fc=hero +0x1C at the start>end of the tick (MovePlayer sees the START value), rng=before>afterPasses>afterFrameAdvance, aura=handle(+68,+7D)\n')
        for r in res['rows']:
            fh.write(brief_ext(r) + '\n')
            for c in r['calls']:
                fh.write(call_line(c) + '\n')


# ---- independent recomputation of the burst and of the hold spark (formulas read in the disassembly, tables read from the image) ------------------
def image_s16(addr):
    return s16(lib.DATA[lib.OFF + addr - lib.BASE] | lib.DATA[lib.OFF + addr - lib.BASE + 1] << 8)


def burst_expected(hero):
    """0x8002EB6C-0x8002EC48: i = 0..15; X += tab1[2i] * 3 << 10, Y += tab2[2i] * 3 << 10, Z = z + 0x100000; forces tab[(2i + 0x18) & 0x1F] * 448"""
    T1, T2 = 0x80023654, 0x80023694
    out = []
    for i in range(16):
        tx, ty = image_s16(T1 + 4 * i), image_s16(T2 + 4 * i)
        k = ((2 * i + 0x18) & 0x1F) * 2
        out.append(dict(x=s32(hero['x'] + ((tx * 3) << 10)), y=s32(hero['y'] + ((ty * 3) << 10)), z=s32(hero['z'] + 0x100000),
                        fx=s32(image_s16(T1 + k) * 448), fy=s32(image_s16(T2 + k) * 448), tx=tx, ty=ty))
    return out


def spark_expected(rng_entry, hero):
    """0x8002F5EC-0x8002F71C: two LCG steps; jx = hi(s1 * 0x60001) - 0x30000 ; jy = hi(s2 * 0x40001) - 0x20000 ; pos += 20 * j ; force = heroForce - j"""
    s1 = lcg(rng_entry)
    s2 = lcg(s1)
    jx = ((s1 * 0x60001) >> 32) - 0x30000
    jy = ((s2 * 0x40001) >> 32) - 0x20000
    return dict(s1=s1, s2=s2, jx=jx, jy=jy, x=s32(hero['x'] + 20 * jx), y=s32(hero['y'] + 20 * jy), z=s32(hero['z'] + 0x100000),
                fx=s32(hero['fx'] - jx), fy=s32(hero['fy'] - jy))
