"""probe_ext.py - E20.f1 oracle extension: re-runs the five charge probes with ext.py, checks them against the discovery outputs and writes the
excerpts.  Writes only into this scratch folder (out/, out_baseline/, summary.txt)."""
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import ext
import attack as A
import world as W
import machine as MM
import probe_orig as P            # the STORED discovery probe.py (unchanged copy)

OUT = os.path.join(HERE, 'out')
BASE = os.path.join(HERE, 'out_baseline')
STORED = os.path.join(HERE, 'out_orig')
os.makedirs(OUT, exist_ok=True)
os.makedirs(BASE, exist_ok=True)
SQ = W.SQUARE
SUMMARY = io.StringIO()


def say(s=''):
    print(s)
    SUMMARY.write(s + '\n')


def hold_range(a, b, word=SQ):
    return {t: word for t in range(a, b + 1)}


SCEN = {          # same table as the discovery probe.py main block: name -> (item, H); Square held 1..H, run H + 60 ticks
    'C2-steel': (2, 90),
    'C6-william': (6, 90),
    'C10-crusher': (10, 100),
    'C14-ice': (14, 100),
    'C16-fire': (16, 100),
}


def spec_of(name):
    item, H = SCEN[name]
    return dict(name=name, holds=hold_range(1, H), n=H + 60, item=item, before={}, after=0)


# ---- 1. the discovery probe, unchanged, regenerated here ----------------------------------------------------------------------------------------
def baseline():
    A.Machine = MM.Machine
    P.OUT = BASE
    res = {}
    for nm, (item, H) in SCEN.items():
        res[nm] = P.run(nm, item, hold_range(1, H), H + 60)
    return res


STRIP_EXT = re.compile(r' fc=\S+ rng=\S+ aura=\S+')
STRIP_B0 = re.compile(r' b0=-?\d+')
STRIP_PZ = re.compile(r' pz=\s*-?\d+')
DISCOVERY = os.path.join(os.path.dirname(HERE), '..', 'disc-weapons', 'f-weapons')


def norm(l):
    return STRIP_PZ.sub('', STRIP_B0.sub('', l))


def read_lines(path):
    with open(path) as f:
        return f.read().split('\n')


def compare_files(base_res, ext_res):
    say('## 1. Shared columns: stored discovery output vs regenerated stored probe vs extended oracle')
    import time
    if os.path.isdir(DISCOVERY):  # E20.f1a T1: the discovery folder (scratchpad) is not part of the repository; the section is skipped when it is missing.
        say('Columns present in the STORED outputs (discovery folder, mtime in parentheses) and in the stored probe.py brief():')
        for nm in list(SCEN) + ['probe.py']:
            path = os.path.join(DISCOVERY, 'probe.py') if nm == 'probe.py' else os.path.join(DISCOVERY, 'out', nm + '.txt')
            txt = open(path).read()
            say('  %-12s %s  b0= %s  pz= %s' % (nm, time.strftime('%H:%M:%S', time.localtime(os.path.getmtime(path))),
                                             ('lines: %d' % txt.count(' b0=')) if nm != 'probe.py' else ("'b0=' in brief(): %s" % ("b0=" in txt)),
                                             ('lines: %d' % txt.count(' pz=')) if nm != 'probe.py' else ("'pz=' in brief(): %s" % ("pz=" in txt))))
    say('Comparison after removing the b0= and pz= columns from every side:')
    say('Row fields compared at row level (every key of attack.ROW_FIELDS + chg/ptr/cnt/weapon_rec + events text) between the stored probe.py run and the extension:')
    keys = [n for n, _, _ in A.ROW_FIELDS] + ['charge_timer', 'rec_ptr', 'rec_cnt', 'weapon_rec', 'events', 'pos_z_dll']
    for nm in SCEN:
        a, b = base_res[nm]['rows'], ext_res[nm]['rows']
        bad = []
        for ra, rb in zip(a, b):
            for k in keys:
                if ra[k] != rb[k]:
                    bad.append((ra['tick'], k, ra[k], rb[k]))
        # text level 1: stored output (with b0) vs regenerated stored probe (without b0)
        st = [norm(l) for l in read_lines(os.path.join(STORED, nm + '.txt'))]
        rg = [norm(l) for l in read_lines(os.path.join(BASE, nm + '.txt'))]
        d1 = [i for i, (x, y) in enumerate(zip(st, rg)) if x != y]
        # text level 2: stored output (with b0) vs extended output with the new columns removed
        ex = [norm(STRIP_EXT.sub('', l)) for l in read_lines(os.path.join(OUT, nm + '.txt')) if l.startswith('t')]
        stl = [norm(l) for l in read_lines(os.path.join(STORED, nm + '.txt')) if l.startswith('t')]
        d2 = [i for i, (x, y) in enumerate(zip(stl, ex)) if x != y]
        say('  %-12s rows %d/%d | row-level differences: %d | stored(-b0) vs regenerated: %d line diffs (%d vs %d lines) | stored vs extended(-new cols): %d line diffs (%d vs %d lines)' % (
            nm, len(a), len(b), len(bad), len(d1), len(st), len(rg), len(d2), len(stl), len(ex)))
        if bad[:3]:
            say('     first row-level differences: %s' % bad[:3])
        if d1[:2]:
            say('     d1 sample: %r vs %r' % (st[d1[0]][:200], rg[d1[0]][:200]))
        if d2[:2]:
            say('     d2 sample: %r vs %r' % (stl[d2[0]][:200], ex[d2[0]][:200]))
        # the old brief() did not collapse repeated events: a line differing only by that is explained when the stored events equal the row's event list
        for i in d2:
            tick = int(stl[i].split()[1])
            row = [r for r in b if r['tick'] == tick][0]
            sfx = stl[i].split('hc=0  ', 1)[1] if 'hc=0  ' in stl[i] else stl[i].split('hc=1  ', 1)[1]
            say('     explained line t%d: stored events (uncollapsed) == the row event list joined by " | " : %s (%d events)' % (tick, sfx == ' | '.join(row['events']), len(row['events'])))


# ---- 2. burst and spark checks against the formulas ---------------------------------------------------------------------------------------------
def check_effects(nm, res):
    """per tick: the id-0x10 creates are split into hold sparks (the 2 LCG draws) and the 16-effect burst (the LAST 16 of the tick: UpdatePlayerWeaponEffect runs before
    PlayerTryAttack in MovePlayer, so a release tick whose fc_in & 3 == 0 holds one spark THEN the 16 burst effects)"""
    out = []
    for r in res['rows']:
        c10 = [c for c in r['calls'] if c['fn'] == ext.CREATE and c['args'][1] == 0x10]
        sparks, burst = (c10[:-16], c10[-16:]) if len(c10) >= 16 else (c10, [])
        for c in sparks:
            e = ext.spark_expected(c['rng_entry'], c['hero'])
            ok = c['slot'] and all(c['f'][k] == e[k] for k in ('x', 'y', 'z', 'fx', 'fy'))
            out.append('spark t%d slot#%d: rng %08X > %08X (2 LCG steps, matches formula: %s) ; jx=%d jy=%d ; slot fields equal the formula: %s ; fc_in=%d (&3=%d &7=%d) sound %s' % (
                r['tick'], c['idx'], c['rng_entry'], r['rng_passes'], r['rng_passes'] == e['s2'], e['jx'], e['jy'], bool(ok), r['fc_in'], r['fc_in'] & 3, r['fc_in'] & 7,
                [x for x in r['events'] if x in ('sound(428)', 'sound(429)')]))
        if burst:
            exp = ext.burst_expected(burst[0]['hero'])
            ok = sum(1 for c, e in zip(burst, exp) if c['slot'] and all(c['f'][k] == e[k] for k in ('x', 'y', 'z', 'fx', 'fy')))
            distinct = len(set(c['slot'] for c in burst))
            draws = r['rng_before'] != r['rng_passes']
            out.append('burst t%d: %d/16 tuples equal the formula, %d distinct slots (#%d..#%d), preceded by %d spark(s) in the same tick, rng %08X>%08X (changed by the spark only: %s)' % (
                r['tick'], ok, distinct, burst[0]['idx'], burst[-1]['idx'], len(sparks), r['rng_before'], r['rng_passes'], draws == bool(sparks)))
    return out


def life_lines(res):
    """aura life and call census of a run"""
    out = []
    prev = 0
    for r in res['rows']:
        if r['aura_handle'] != prev:
            out.append('aura handle t%d: %08X -> %08X (slot#%d)' % (r['tick'], prev, r['aura_handle'], r['aura_idx']))
            prev = r['aura_handle']
    m = res['machine']
    out.append('end of run: pool slot#000 +68=%d +7D=%d' % (m.peeks(ext.EFFECT_TABLE + 0x68), ext.peek8(m, ext.EFFECT_TABLE + 0x7D)))
    return out


def burst_table(res, tick):
    r = [x for x in res['rows'] if x['tick'] == tick][0]
    sixteen = [c for c in r['calls'] if c['fn'] == ext.CREATE and c['args'][1] == 0x10]
    hero = sixteen[0]['hero']
    exp = ext.burst_expected(hero)
    say('hero at the calls: x=%d y=%d z=%d (raw 16.16 / binary convention)' % (hero['x'], hero['y'], hero['z']))
    say(' i  slot  addr      X          Y          Z         ForceX   ForceY   | formula check (tab1[2i]=%d tab2[2i]=%d ...)' % (exp[0]['tx'], exp[0]['ty']))
    for i, (c, e) in enumerate(zip(sixteen, exp)):
        f = c['f']
        say('%2d  #%03d  %08X %10d %10d %10d %8d %8d   %s  (tab1[2i]=%d tab2[2i]=%d)' % (
            i, c['idx'], c['slot'], f['x'], f['y'], f['z'], f['fx'], f['fy'], 'OK' if all(f[k] == e[k] for k in ('x', 'y', 'z', 'fx', 'fy')) else 'DIFF', e['tx'], e['ty']))


# ---- 3. the aura scenarios ------------------------------------------------------------------------------------------------------------------
def poke_flag(m):
    h = m.peek(ext.AURA_HANDLE)
    assert h, 'no aura handle at the poke'
    m.poke8(h + 0x7D, 1)


def eff3_ticks(res):
    return [(r['tick'], c['idx']) for r in res['rows'] for c in r['calls'] if c['args'][1] == 3 and c['fn'] == ext.CREATE_ATT]


def aura_scenarios():
    say('## 4. Aura re-creation path (effect 3, handle 0x80126FF8, destroy flag +0x7D)')
    base = dict(name='A', holds=hold_range(1, 90), n=90, item=2, after=0)
    # A0: nothing touches +0x7D
    r0 = ext.run_ext(dict(base, name='A0-aura-persist', before={}))
    ext.write_run(os.path.join(OUT, 'A0-aura-persist.txt'), 'A0-aura-persist', r0, 'C2-steel, +0x7D never touched')
    e0 = eff3_ticks(r0)
    window = [r for r in r0['rows'] if 33 <= r['tick'] <= 82]
    say('A0 (C2-steel, +0x7D left at 0): effect-3 creates at (tick, slot) = %s ; charge ticks t33..t82 = %d ticks with timer >= 11 (timers %d..%d) ; effect-3 creates inside them: %d' % (
        e0, len(window), window[0]['charge_timer'], window[-1]['charge_timer'], sum(1 for t, _ in e0 if 33 <= t <= 82)))
    # A1: +0x7D := 1 on the live aura slot just before tick 50 (the effect update of tick 49 would have set it), free pass modelled
    log = []

    def watch(m, t):
        h = m.peek(ext.AURA_HANDLE)
        s0 = ext.EFFECT_TABLE
        log.append('   after t%-3d handle=%08X  slot#000 +68=%d +7D=%d | slot#h +68=%s +7D=%s' % (
            t, h, m.peeks(s0 + 0x68), ext.peek8(m, s0 + 0x7D), m.peeks(h + 0x68) if h else '-', ext.peek8(m, h + 0x7D) if h else '-'))
    r1 = ext.run_ext(dict(base, name='A1', before={50: poke_flag}), effect_free=True, after_tick=watch)
    ext.write_run(os.path.join(OUT, 'A1-aura-recreate.txt'), 'A1-aura-recreate', r1, 'C2-steel, +0x7D := 1 on the aura slot before tick 50, free pass modelled')
    e1 = eff3_ticks(r1)
    say('A1 (+0x7D := 1 on the live aura slot just before t50; free pass modelled): effect-3 creates at (tick, slot) = %s' % e1)
    for r in r1['rows']:
        if r['tick'] in (48, 49, 50, 51, 52):
            b = ext.brief_ext(r)
            say('t%3d chg=%2d' % (r['tick'], r['charge_timer']) + b[b.index(' fc='):][:150])
            for c in r['calls']:
                say(ext.call_line(c))
    for l in log[47:52]:
        say(l)
    # A1b: same without the free pass
    r1b = ext.run_ext(dict(base, name='A1b', before={50: poke_flag}), effect_free=False)
    say('A1b (same, free pass NOT modelled): effect-3 creates at %s ; old slot#000 stays allocated (+68=%d +7D=%d), handle -> slot#%d' % (
        eff3_ticks(r1b), r1b['machine'].peeks(ext.EFFECT_TABLE + 0x68), ext.peek8(r1b['machine'], ext.EFFECT_TABLE + 0x7D), r1b['rows'][-1]['aura_idx']))
    # A2: two flags (t60 and t70) and the flag raised twice on the same slot
    r2 = ext.run_ext(dict(base, name='A2', before={60: poke_flag, 70: poke_flag}), effect_free=True)
    ext.write_run(os.path.join(OUT, 'A2-aura-recreate-twice.txt'), 'A2-aura-recreate-twice', r2, 'C2-steel, +0x7D := 1 before t60 and before t70, free pass modelled')
    say('A2 (+0x7D := 1 before t60 and before t70): effect-3 creates at %s' % eff3_ticks(r2))
    # A3: the flag is set on a tick where the timer is < 11 (no charge): nothing is created, the handle is cleared at the next release
    r3 = ext.run_ext(dict(base, name='A3', holds=hold_range(1, 25), n=45, before={40: lambda m: None}), effect_free=True)
    say('A3 (control: charge dropped at t26, timer < 11 at the poke): effect-3 creates %s ; aura handle at t45 = %08X' % (eff3_ticks(r3), r3['rows'][-1]['aura_handle']))
    return r0, r1, r2


# ---- 4. rng / frame advance -----------------------------------------------------------------------------------------------------------------
def rng_section(ext_res):
    say('## 3. The per-frame RNG advance 0x8002BD0C-0x8002BD34 and the seed')
    m = ext_res['C2-steel']['machine']
    say('FACT (tools/world.py:187) the oracle ticks with PASSES = %s ; tick() = pad update + these 8 passes (world.py:198+)' % ', '.join('%08X' % a for a in W.PASSES))
    say('FACT (xref.py) 0x8002BD0C-0x8002BD34 lies in the function 0x8002BAEC (start 0x8002BAEC, epilogue 0x8002BD58), called only from 0x8002C3E4 and 0x8002C404; that function calls the tick wrapper 0x8002E058 (jal at 0x8002BC4C), which calls 0x8003B388 (jal 0x8002E108) then the effect pass 0x8003C410')
    say('FACT coverage of the C2-steel run (m.cov, %d distinct PCs): 0x8002BAEC executed=%s, 0x8002BD0C executed=%s, 0x8002BD34 executed=%s, 0x8002E058 executed=%s, 0x8003C410 executed=%s' % (
        len(m.cov), 0x8002BAEC in m.cov, 0x8002BD0C in m.cov, 0x8002BD34 in m.cov, 0x8002E058 in m.cov, 0x8003C410 in m.cov))
    rows = ext_res['C2-steel']['rows']
    changes = [(r['tick'], '%08X>%08X' % (r['rng_before'], r['rng_passes'])) for r in rows if r['rng_before'] != r['rng_passes']]
    say('FACT rng word changes only at spark ticks: %s' % changes)
    say('FACT seed: attack.py:51 setup(seed=0x12345678) and attack.py:77 m.poke(0x80098708, seed); row 0 of every run holds %08X ; the EXE image itself holds %08X at 0x80098708' % (
        rows[0]['rng_before'], ext.IMAGE_SEED))
    # frame advance model on, C2-steel
    fr = ext.run_ext(spec_of('C2-steel'), frame_rng=True)
    ext.write_run(os.path.join(OUT, 'C2-steel.frame-rng.txt'), 'C2-steel', fr, 'frame_rng=True: x = x*0x7D2B89DD + 0xE06A02E7 after every tick (0x8002BD0C-0x8002BD34)')
    say('Model frame_rng=True (C2-steel): first hold spark (t85) enters with rng %08X (no-advance run: %08X)' % (
        [r for r in fr['rows'] if r['tick'] == 85][0]['calls'][0]['rng_entry'], [r for r in ext_res['C2-steel']['rows'] if r['tick'] == 85][0]['calls'][0]['rng_entry']))
    for nm_, res_ in (('frame_rng', fr),):
        for l in check_effects('C2-steel/' + nm_, res_):
            say('   ' + l)
    # same release with the image seed
    im = ext.run_ext(spec_of('C2-steel'), seed=ext.IMAGE_SEED)
    c0 = [r for r in im['rows'] if r['tick'] == 85][0]['calls'][0]
    say('Model seed=image (0xB017C93D), no frame advance: first hold spark enters with rng %08X, jitter-derived slot x=%d y=%d fx=%d fy=%d' % (c0['rng_entry'], c0['f']['x'], c0['f']['y'], c0['f']['fx'], c0['f']['fy']))
    return fr


# ---- 5. pool full: create returns NULL (0x8003B9C4 finds no slot with +0x68 == 0) ---------------------------------------------------------
class Filler:
    """occupies every free slot of the pool (+0x68 := 2, +0x7D := 0) except `keep` of them; release() frees exactly the slots it took"""

    def __init__(self):
        self.taken = []

    def fill(self, keep=0):
        def f(m):
            free = [i for i in range(ext.EFFECT_N) if m.peek(ext.EFFECT_TABLE + i * ext.EFFECT_STRIDE + 0x68) == 0]
            take = free[:len(free) - keep] if keep else free
            for i in take:
                m.poke(ext.EFFECT_TABLE + i * ext.EFFECT_STRIDE + 0x68, 2)
            self.taken = take
        return f

    def release(self):
        def f(m):
            for i in self.taken:
                m.poke(ext.EFFECT_TABLE + i * ext.EFFECT_STRIDE + 0x68, 0)
            self.taken = []
        return f


def pool_scenarios():
    say('## 5. Pool full (create returns NULL): what the binary does with a NULL')
    fl = Filler()
    base = dict(name='P1', holds=hold_range(1, 90), n=95, item=2, after=0)
    res = ext.run_ext(dict(base, before={30: fl.fill(), 60: fl.release(), 84: fl.fill(), 88: fl.release()}))
    ext.write_run(os.path.join(OUT, 'P1-pool-full.txt'), 'P1-pool-full', res, 'C2-steel, pool full t30..t59 and t84..t87 (harness fills/frees the free slots)')
    nulls = [(r['tick'], c['args'][1]) for r in res['rows'] for c in r['calls'] if not c['slot']]
    say('P1 (C2-steel, pool filled before t30, freed before t60; filled before t84, freed before t88): NULL returns (tick, effect id) = %s' % nulls)
    for r in res['rows']:
        if r['tick'] in (33, 34, 37, 59, 60, 61, 85, 89):
            b = ext.brief_ext(r)
            say('t%3d chg=%2d' % (r['tick'], r['charge_timer']) + b[b.index(' fc='):][:230])
            for c in r['calls']:
                say(ext.call_line(c))
    say('   => with a NULL from the aura create the handle stays 0 and the create is retried at EVERY charge tick; the sound 0x2A (42) does not depend on the result; the spark with a NULL plays NO 0x1AC/0x1AD and draws NO random word (rng t85 %08X>%08X, t89 enters with %08X)' % (
        [r for r in res['rows'] if r['tick'] == 85][0]['rng_before'], [r for r in res['rows'] if r['tick'] == 85][0]['rng_passes'], [r for r in res['rows'] if r['tick'] == 89][0]['rng_before']))
    # burst with only 5 free slots
    fl2 = Filler()
    base2 = dict(name='P2', holds=hold_range(1, 90), n=95, item=2, after=0)
    res2 = ext.run_ext(dict(base2, before={91: fl2.fill(keep=5)}))
    r91 = [r for r in res2['rows'] if r['tick'] == 91][0]
    say('P2 (C2-steel, only 5 free slots at the release tick t91): burst creates -> %s' % ['%s' % ('#%d' % c['idx'] if c['slot'] else 'NULL') for c in r91['calls']])
    ev = r91['events']
    say('   events of t91: %s ; pointer %08X, step counter %d, anim %02X (the release still completes: sound 43 plays, pointer set)' % ([e[:40] for e in ev if not e.startswith('effect')], r91['rec_ptr'], r91['rec_cnt'], r91['anim_cur']))
    say('   the 5 created tuples are the ones of i = 0..4 (the NULL ones are skipped without shifting the others): %s' % [(c['f']['x'], c['f']['y'], c['f']['fx'], c['f']['fy']) for c in r91['calls'] if c['slot']][:5])
    ext.write_run(os.path.join(OUT, 'P2-burst-5-free.txt'), 'P2-burst-5-free', res2, 'C2-steel, 5 free slots at t91')

# E20.f1a T1: relative to the root of the repository (this folder is docs/plan-e20-annexe/e20-f1/oracle).
REPO_ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
EFFECTS_GLOBAL = os.path.join(REPO_ROOT, 'alundra-project', 'Data', 'effects', 'effects-global.json')


def life_table():
    """ticks from the creation tick to the tick whose effect pass reaches the End op of animation 0 (End = Destroy), from the exported global bank:
    the sum of the frame delays (frame = [delay, image set]).  Effect 3 (aura) and effect 0x10 (spark) are created with animation 0 (a2 = 0)."""
    E = json.load(open(EFFECTS_GLOBAL, encoding='utf-8-sig'))['Effects']
    life = {}
    for eid in (3, 0x10):
        a = E[eid]['Animations'][0]
        assert a['End'] == 'Destroy', (eid, a['End'])
        life[eid] = sum(f[0] for f in a['Frames'])
    return life, {eid: [(a['End'], len(a['Frames']), sum(f[0] for f in a['Frames'])) for a in E[eid]['Animations']] for eid in (3, 0x10)}


def natural_expiry():
    say('## 6. Natural expiry of the aura (MODEL: the effect update is not run by the oracle; +0x7D is set by the harness at the script end read from the exported bank)')
    life, anims = life_table()
    say('exported bank Data/effects/effects-global.json (alundra-project): effect 3 animations (End, frames, sum of delays) = %s ; effect 16 = %s -> life used: %s' % (anims[3], anims[0x10], life))
    spec = dict(name='A5', holds=hold_range(1, 300), n=300, item=2, after=0, before={})
    peak = []

    def count_alive(m, t):
        peak.append(sum(1 for i in range(ext.EFFECT_N) if m.peek(ext.EFFECT_TABLE + i * ext.EFFECT_STRIDE + 0x68) == 2))
    res = ext.run_ext(spec, effect_life=life, after_tick=count_alive)
    ext.write_run(os.path.join(OUT, 'A5-natural-expiry.txt'), 'A5-natural-expiry', res, 'C2-steel held 300 ticks, lifetime model effect 3 = %d ticks, effect 16 = %d ticks' % (life[3], life[0x10]))
    e3 = eff3_ticks(res)
    sparks = [r['tick'] for r in res['rows'] for c in r['calls'] if c['args'][1] == 0x10 and c['slot']]
    say('A5 (C2-steel held for 300 ticks): effect-3 creates at (tick, slot) = %s ; hold sparks at %d ticks (first %s, last %s) ; max slots alive %d' % (
        e3, len(sparks), sparks[:3], sparks[-1], max(peak)))
    for r in res['rows']:
        if r['tick'] in (128, 129, 130, 131):
            b = ext.brief_ext(r)
            say('t%3d chg=%2d' % (r['tick'], r['charge_timer']) + b[b.index(' fc='):][:200])
            for c in r['calls']:
                if c['args'][1] == 3:
                    say(ext.call_line(c))
    m = res['machine']
    alive = sum(1 for i in range(ext.EFFECT_N) if m.peek(ext.EFFECT_TABLE + i * ext.EFFECT_STRIDE + 0x68) == 2)
    say('   at the end of the run %d slots are alive (aura + sparks younger than their life); the period of the aura is %d ticks (create at N, +0x7D set at the pass of N+%d, re-create at N+%d)' % (alive, life[3] + 1, life[3], life[3] + 1))


def main():
    base_res = baseline()
    ext_res = {}
    for nm in SCEN:
        res = ext.run_ext(spec_of(nm))
        ext_res[nm] = res
        ext.write_run(os.path.join(OUT, nm + '.txt'), nm, res)
        with open(os.path.join(OUT, nm + '.json'), 'w') as f:
            json.dump([dict(tick=r['tick'], chg=r['charge_timer'], anim=r['anim_cur'], ptr=r['rec_ptr'], cnt=r['rec_cnt'], fc_in=r['fc_in'], rng=[r['rng_before'], r['rng_passes'], r['rng_after']],
                            aura=[r['aura_handle'], r['aura_68'], r['aura_7d']],
                            calls=[dict(fn='%X' % c['fn'], eid=c['args'][1], args=c['args'], idx=c['idx'], slot=c['slot'], rng_entry=c['rng_entry'], hero=c['hero'], f=c['f']) for c in r['calls']])
                       for r in res['rows']], f)
    say('# Extended oracle run (probe_ext.py)  files: out/<name>.txt (+ .json), out_baseline/<name>.txt (stored probe regenerated), out_orig/<name>.txt (stored discovery outputs)')
    say('setup_effect_calls per scenario: %s' % {nm: r['setup_slots'] for nm, r in ext_res.items()})
    say()
    compare_files(base_res, ext_res)
    say()
    say('## 2. Effect checks against the formulas read in the disassembly (burst 0x8002EB6C-0x8002EC48, hold spark 0x8002F5EC-0x8002F71C)')
    for nm, res in ext_res.items():
        say('%s:' % nm)
        for l in check_effects(nm, res):
            say('   ' + l)
        for l in life_lines(res):
            say('   ' + l)
        cs = [c for r in res['rows'] for c in r['calls']]
        say('   effect calls total %d, distinct slots %d, NULL returns %d, max pool use %d of 128' % (len(cs), len(set(c['slot'] for c in cs if c['slot'])), sum(1 for c in cs if not c['slot']), len(set(c['slot'] for c in cs if c['slot']))))
    say()
    say('### C2-steel release tick (t91): the 16 burst tuples')
    burst_table(ext_res['C2-steel'], 91)
    say()
    say('### C2-steel around the first hold spark (t83..t90), rng = before>afterPasses>afterFrameAdvance')
    for r in ext_res['C2-steel']['rows']:
        if 83 <= r['tick'] <= 90:
            b = ext.brief_ext(r)
            say('t%3d chg=%2d' % (r['tick'], r['charge_timer']) + b[b.index(' fc='):][:130])
            for c in r['calls']:
                say(ext.call_line(c))
                if c['args'][1] == 0x10:
                    e = ext.spark_expected(c['rng_entry'], c['hero'])
                    say('        decode: s0=%08X s1=%08X s2=%08X jx=hi(s1*0x60001)-0x30000=%d jy=hi(s2*0x40001)-0x20000=%d ; x=heroX+20*jx=%d y=heroY+20*jy=%d fx=heroFX-jx=%d fy=heroFY-jy=%d (hero x=%d y=%d fx=%d fy=%d)' % (
                        c['rng_entry'], e['s1'], e['s2'], e['jx'], e['jy'], e['x'], e['y'], e['fx'], e['fy'], c['hero']['x'], c['hero']['y'], c['hero']['fx'], c['hero']['fy']))
    say()
    rng_section(ext_res)
    say()
    aura_scenarios()
    say()
    pool_scenarios()
    say()
    natural_expiry()
    with open(os.path.join(HERE, 'summary.txt'), 'w') as f:
        f.write(SUMMARY.getvalue())


if __name__ == '__main__':
    main()
