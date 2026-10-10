"""rscen.py - E20.d discovery ("run-oracle"): the run scenarios, their analysis and the writers.  The specs hold SETUP and INPUTS only (cells, hero start, targets, pad word per tick,
dash-nibble override per tick); every number printed comes from the emulated binary (rworld.run_r).  usage: python -B rscen.py [G1 G2 ...]   (no argument: every group)

Groups
  G1  start of a run from idle: Triangle + each cardinal, + each diagonal, Triangle alone, late direction, walk then Triangle, jump then Triangle, diagonal then cardinal
  G2  keep running: long straight run, the map edge
  G3  the cone: turn while running, 4 run directions x (8 pad directions, none, Triangle released, both released)
  G4  stop and resume: release the pad for k ticks, press again (same / other direction), release Triangle too
  G5  blocked: wall (E, N, W, S), adjacent wall, steps of 16 and 32 px, the bounce 0x3E and its exit, pad released during the bounce
  G6  ledge: drop of 16 / 32 / 64 px while running, Triangle released in the air, timing of the ground contact (O-E20-12)
  G7  attack: Square / Cross / Circle during the wind-up, the run, the stop; Tackle into a Beannoid (sweep of the press tick); charge counter with a chargeable weapon
  G8  ladder: run up into a slope-6 strip (climb priority), control without the ladder, walking control
  G9  water: deep water with and without the swim boots, all-water start, shallow water, ice
  G10 the stick: dash-nibble override (D-E20-15) on the start and the resume, over the facing directions
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R

OUT = os.path.join(HERE, '..', 'out')
UP, RIGHT, DOWN, LEFT = R.UP, R.RIGHT, R.DOWN, R.LEFT
TRI, CIR, CRO, SQU = R.TRI, R.CIR, R.CRO, R.SQU
DIRS = [('U', UP), ('R', RIGHT), ('D', DOWN), ('L', LEFT)]
DIAG = [('UR', UP | RIGHT), ('DR', DOWN | RIGHT), ('DL', DOWN | LEFT), ('UL', UP | LEFT)]
CENTER = (624, 480)             # far from every clamp (1248 x 960 px field)
LOG = []                        # summary lines (computed)


def say(s=''):
    LOG.append(s)
    print(s)


# ---- writers ----------------------------------------------------------------------------------------------------------------------------------
def ser_spec(spec):
    d = {}
    for k, v in spec.items():
        if k in ('setup', 'target_init', 'events', 'layout', 'pad', 'dash'):
            continue
        d[k] = v
    d['pad_pieces'] = spec['pad'] if not callable(spec['pad']) else 'callable'
    if spec.get('dash') is not None:
        d['dash_pieces'] = spec['dash'] if not callable(spec['dash']) else 'callable'
    if spec.get('layout') is not None:
        d['layout'] = spec['layout'].describe()
    return d


def write(res, sub=''):
    spec = res['spec']
    d = os.path.join(OUT, sub) if sub else OUT
    os.makedirs(d, exist_ok=True)
    name = spec['name']
    with open(os.path.join(d, name + '.txt'), 'w', encoding='utf-8') as f:
        f.write(R.trace_text(res, with_targets=bool(res['rows'] and res['rows'][0]['targets'])))
        if res.get('experiments'):
            f.write('# damage experiments (ComputeNewHp on copies of the live state, at the target\'s D handler)\n')
            for e in res['experiments']:
                f.write('# ' + exp_line(e) + '\n')
    rows = [res['pre']] + res['rows']
    col = {k: [r['hero'][k] for r in rows] for k in rows[0]['hero']}
    out = dict(spec=ser_spec(spec), meta={k: v for k, v in res['meta'].items() if k not in ('kit',)}, tick=[r['tick'] for r in rows], hold=[r['hold'] for r in rows],
               hero=col, targets=[{k: [r['targets'][i][k] for r in rows] for k in rows[0]['targets'][i]} for i in range(len(rows[0]['targets']))],
               dash_override=[r.get('dash_override') for r in rows], journal=[(r['tick'], r['journal']) for r in rows if r.get('journal')],
               experiments=[json_safe(e) for e in res.get('experiments', [])])
    with open(os.path.join(d, name + '.json'), 'w', encoding='utf-8') as f:
        json.dump(out, f, separators=(',', ':'))


def json_safe(o):
    if isinstance(o, dict):
        return {str(k): json_safe(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [json_safe(v) for v in o]
    return o


def exp_line(e):
    c = e['context']
    v = e['variants']
    return 'tick %d slot %d: attack attr %s power %s, target response %s, latched anim %s; damage live/empty/filled = %s/%s/%s, returns %s' % (
        e['tick'], e['slot'], c['attack_attr'], c['attack_power'], c['response'], c['latched_anim'],
        v['live']['damage'], v['empty']['damage'], v['filled']['damage'], v['live']['returns'])


CROSS = False                   # --cross: every run is repeated through the second interpreter (mips.py via interp2) and compared tick by tick
XC = dict(runs=0, ticks=0, mismatches=[], uninit_sites=0, dash_runs=0)


def cross_check(res, res2):
    name = res['spec']['name']
    XC['runs'] += 1
    if res['spec'].get('dash') is not None:
        XC['dash_runs'] += 1
    XC['uninit_sites'] += len(res['meta'].get('uninit_reads', [])) + len(res2['meta'].get('uninit_reads', []))
    for ra, rb in zip([res['pre']] + res['rows'], [res2['pre']] + res2['rows']):
        XC['ticks'] += 1
        for k in ('hero', 'targets', 'rng', 'journal', 'dash_override', 'hold'):
            if ra.get(k) != rb.get(k):
                XC['mismatches'].append((name, ra['tick'], k))
                break


def run(spec, sub='', quiet=False):
    res = R.run_r(spec)
    if CROSS:
        cross_check(res, R.run_r(spec, 'mips'))
    write(res, sub)
    return res


# ---- helpers on a result --------------------------------------------------------------------------------------------------------------------------
def H(res, t, key):
    return (res['pre'] if t == 0 else res['rows'][t - 1])['hero'][key]


def first(res, key, val, start=1):
    for r in res['rows']:
        if r['tick'] >= start and r['hero'][key] == val:
            return r['tick']
    return None


def first_not(res, key, val, start=1):
    for r in res['rows']:
        if r['tick'] >= start and r['hero'][key] != val:
            return r['tick']
    return None


def sounds(res, a=1, b=10 ** 9):
    out = []
    for r in res['rows']:
        if a <= r['tick'] <= b:
            for k, v in r['journal']:
                if k == 'sound':
                    out.append((r['tick'], v[0]))
    return out


def effects(res, a=1, b=10 ** 9):
    out = []
    for r in res['rows']:
        if a <= r['tick'] <= b:
            for k, v in r['journal']:
                if k == 'effect':
                    out.append((r['tick'], v['id']))
    return out


def dpx(res, t0, t1, key='pos_x'):
    return (H(res, t1, key) - H(res, t0, key)) / 65536.0


def tl(res, key='anim_cur', a=0, b=None):
    """timeline string of a hero column between ticks a and b"""
    rows = [res['pre']] + res['rows']
    out = []
    prev = None
    for r in rows:
        if r['tick'] < a or (b is not None and r['tick'] > b):
            continue
        v = r['hero'][key]
        if prev is None or v != prev[2]:
            out.append([r['tick'], r['tick'], v])
            prev = out[-1]
        else:
            prev[1] = r['tick']
    return ' '.join('%d-%d:%02X' % (x, y, v & 0xFF) if x != y else '%d:%02X' % (x, v & 0xFF) for x, y, v in out)


def pad_range(pieces_text):
    return pieces_text


# ---- G1 ----------------------------------------------------------------------------------------------------------------------------------------------
def g1():
    say('== G1 start of a run from idle (hero at rest, facing east at (624,480), flat field, kit inventory) ==')
    res = {}
    for n, d in DIRS:
        res[n] = run(dict(name='R1-T+%s' % n, desc='Triangle + %s held from tick 1, 64 ticks' % n, hero_px=CENTER, n=64, pad=[(1, TRI | d)]), 'g1')
    for n, d in DIAG:
        res[n] = run(dict(name='R1-T+%s' % n, desc='Triangle + diagonal %s held from tick 1, 64 ticks (never a valid dash direction)' % n, hero_px=CENTER, n=64, pad=[(1, TRI | d)]), 'g1')
    res['T'] = run(dict(name='R1-T', desc='Triangle alone held from tick 1, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI)]), 'g1')
    res['LR'] = run(dict(name='R1-T+LR', desc='Triangle + Left + Right (invalid nibble 0xA) held from tick 1, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | LEFT | RIGHT)]), 'g1')
    for k in (10, 24, 25, 30, 40):
        res['late%d' % k] = run(dict(name='R1-T-then-R-at-%d' % k, desc='Triangle from tick 1, Right added at tick %d, 64 ticks' % k, hero_px=CENTER, n=64, pad=[(1, TRI), (k, TRI | RIGHT)]), 'g1')
    res['dir-then-T'] = run(dict(name='R1-walk-then-T', desc='Right from tick 1 (walk), Triangle added at tick 20, 64 ticks', hero_px=CENTER, n=64, pad=[(1, RIGHT), (20, TRI | RIGHT)]), 'g1')
    res['jumpT'] = run(dict(name='R1-cross-with-T', desc='Triangle + Right + Cross at tick 1 (Cross edge), Cross released at tick 2, 80 ticks', hero_px=CENTER, n=80, pad=[(1, TRI | RIGHT | CRO), (2, TRI | RIGHT)]), 'g1')
    res['diagcard'] = run(dict(name='R1-diag-then-card', desc='Triangle + Up + Right ticks 1-29, then Triangle + Right from 30, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | UP | RIGHT), (30, TRI | RIGHT)]), 'g1')
    res['diagcard2'] = run(dict(name='R1-diag-then-card-early', desc='Triangle + Up + Right ticks 1-9, then Triangle + Right from 10, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | UP | RIGHT), (10, TRI | RIGHT)]), 'g1')
    res['rel-windup-dir'] = run(dict(name='R1-T-released-in-windup', desc='Triangle + Right 1-9, Right only from tick 10 (Triangle released in the wind-up), 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | RIGHT), (10, RIGHT)]), 'g1')
    res['rel-windup-none'] = run(dict(name='R1-T-released-in-windup-nodir', desc='Triangle + Right 1-9, pad 0 from tick 10, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | RIGHT), (10, 0)]), 'g1')
    res['turn-windup'] = run(dict(name='R1-T-turn-in-windup', desc='Triangle + Right 1-9, Triangle + Up from tick 10, 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | RIGHT), (10, TRI | UP)]), 'g1')
    res['turn-windup20'] = run(dict(name='R1-T-turn-in-windup-at-20', desc='Triangle + Right 1-19, Triangle + Up from tick 20 (the facing changes before the first loop ends), 80 ticks', hero_px=CENTER, n=80, pad=[(1, TRI | RIGHT), (20, TRI | UP)]), 'g1')
    res['diag-then-up'] = run(dict(name='R1-diag-then-up', desc='Triangle + Up + Right 1-29 (wind-up, facing right), Triangle + Up from tick 30 (facing changes AFTER the first loop), 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | UP | RIGHT), (30, TRI | UP)]), 'g1')
    res['diag-then-left'] = run(dict(name='R1-diag-then-left', desc='Triangle + Up + Right 1-29, Triangle + Left from tick 30 (reverse direction after the first loop), 64 ticks', hero_px=CENTER, n=64, pad=[(1, TRI | UP | RIGHT), (30, TRI | LEFT)]), 'g1')
    res['Tonly-then-up'] = run(dict(name='R1-T-faceW-then-R', desc='hero starts facing west (dir 8); Triangle + Right from tick 1: TargetDirection written during the wind-up', hero_px=CENTER, n=64, pad=[(1, TRI | RIGHT)], direction=8), 'g1')
    say('state timelines (anim_cur, ticks first-last:state hex):')
    for k, r in res.items():
        say('  %-12s %s' % (k, tl(r)))
    say('sprint start (first tick anim_cur == 3), wind-up length, dir_target / facing at tick 30, plateau forces, sounds in 1..64:')
    for k, r in res.items():
        t3 = first(r, 'anim_cur', 3)
        say('  %-12s start=%s  dir_target@30=%02X facing@30=%d  fX/fY@60=%d/%d  snd=%s' % (k, t3, H(r, 30, 'dir_target') & 0xFF, H(r, 30, 'facing'), H(r, 60, 'force_x'), H(r, 60, 'force_y'),
                                                                                        ' '.join('%d:%d' % x for x in sounds(r, 1, 64) if x[1] != 0)))
    say()
    return res


# ---- G2 ----------------------------------------------------------------------------------------------------------------------------------------------
def g2():
    say('== G2 keep running ==')
    a = run(dict(name='R2-long', desc='Triangle + Right held, 120 ticks from (100,100): steady run, step sounds and dust', hero_px=(100, 100), n=120, pad=[(1, TRI | RIGHT)]), 'g2')
    b = run(dict(name='R2-mapedge', desc='Triangle + Right from (1100,100) to the east map edge (x clamp 1248), 80 ticks', hero_px=(1100, 100), n=80, pad=[(1, TRI | RIGHT)]), 'g2')
    say('R2-long: timeline %s; ForceX plateau %d; distance per tick %.4f px; step sounds %s; dust ticks %s' % (tl(a), H(a, 60, 'force_x'), dpx(a, 60, 61),
        ' '.join('%d:%d' % x for x in sounds(a, 26, 120)), [t for t, i in effects(a, 26, 120) if i == 7]))
    say('R2-mapedge: timeline %s; final x %.2f; ForceAdjusted first tick %s' % (tl(b), H(b, 80, 'pos_x') / 65536.0, first(b, 'force_adj', 1)))
    say()
    return dict(a=a, b=b)


# ---- G3 ----------------------------------------------------------------------------------------------------------------------------------------------
CONE_NAMES = {0: 'down', 1: 'up', 2: 'left', 3: 'right'}


def g3():
    say('== G3 the cone: run 40 ticks (sprint from tick 26), then change the pad at tick 41 for 14 ticks (hero at (624,480)) ==')
    sw = [('none+T', TRI)] + [('T+%s' % n, TRI | d) for n, d in DIRS + DIAG] + [('T+LR', TRI | LEFT | RIGHT), ('T+UD', TRI | UP | DOWN), ('dir-only', None), ('nothing', 0)]
    out = {}
    for rn, rd in DIRS:
        rows = []
        for sn, sv in sw:
            if sn == 'dir-only':
                sv = rd                         # Triangle released, the run direction kept
            name = 'R3-%s-%s' % (rn, sn.replace('+', '_').replace('-', '_'))
            r = run(dict(name=name, desc='run %s (Triangle + %s) ticks 1-40, then pad = %s from tick 41, 54 ticks' % (rn, rn, sn), hero_px=CENTER, n=54,
                         pad=[(1, TRI | rd), (41, sv)]), 'g3')
            out[(rn, sn)] = r
            # outcome at tick 54 and the first change after 41
            t_chg = first_not(r, 'anim_cur', 3, 41)
            rows.append((sn, t_chg, H(r, 54, 'anim_cur'), H(r, 41, 'sprint_cnt'), H(r, 54, 'sprint_cnt'), H(r, 41, 'dir_target'), H(r, 54, 'dir_target'), H(r, 54, 'facing')))
        say('run %s (facing %s):' % (rn, CONE_NAMES.get(H(out[(rn, sw[0][0])], 40, 'facing'))))
        for sn, t_chg, a54, c41, c54, d41, d54, f54 in rows:
            say('   switch to %-8s first non-Sprint tick %-4s anim@54 %02X  sprintCnt@41 %d @54 %d  dirT %02X->%02X facing@54 %d' % (sn, t_chg, a54 & 0xFF, c41, c54, d41 & 0xFF, d54 & 0xFF, f54))
    say()
    return out


# ---- G4 ----------------------------------------------------------------------------------------------------------------------------------------------
def g4():
    say('== G4 stop and resume: Triangle + Right ticks 1-40 (Sprint from 26), pad released at 41 for k ticks, then pressed again ==')
    out = {}
    say('(a) release the d-pad only (Triangle kept) for k ticks then Triangle + Right again; "resume" = first tick >= 41 with anim_cur 3 again:')
    for k in list(range(1, 27)) + [30, 40]:
        r = run(dict(name='R4-rel%02d-R' % k, desc='Triangle + Right 1-40, Triangle only 41-%d, Triangle + Right from %d, %d ticks' % (40 + k, 41 + k, 41 + k + 40), hero_px=CENTER, n=41 + k + 40,
                     pad=[(1, TRI | RIGHT), (41, TRI), (41 + k, TRI | RIGHT)]), 'g4')
        out[('R', k)] = r
        t3 = first(r, 'anim_cur', 3, 41)
        say('   k=%-2d resume tick %-4s (=%s after the re-press at %d)  sprintCnt at re-press %d  states %s' % (k, t3, ('re-press+%d' % (t3 - (41 + k))) if t3 else '-', 41 + k, H(r, 40 + k, 'sprint_cnt'), tl(r, 'anim_cur', 41, 41 + k + 14)))
    say('(b) re-press in another direction after k ticks (Up, Left, Up+Right):')
    for k in (5, 10, 11, 12, 20):
        for n, d in (('U', UP), ('L', LEFT), ('UR', UP | RIGHT)):
            r = run(dict(name='R4-rel%02d-%s' % (k, n), desc='Triangle + Right 1-40, Triangle only 41-%d, Triangle + %s from %d' % (40 + k, n, 41 + k), hero_px=CENTER, n=41 + k + 30,
                         pad=[(1, TRI | RIGHT), (41, TRI), (41 + k, TRI | d)]), 'g4')
            out[(n, k)] = r
            t3 = first(r, 'anim_cur', 3, 41)
            say('   k=%-2d then %-2s: resume tick %-4s dir_target %s  facing %d  states %s' % (k, n, t3, ('%02X' % (H(r, t3, 'dir_target') & 0xFF)) if t3 else '-', H(r, 41 + k + 29, 'facing'), tl(r, 'anim_cur', 41, 41 + k + 29)))
    say('(c) release Triangle too (pad 0) for k ticks, then Triangle + Right: no resume possible (sprintCnt 0)')
    for k in (5, 12, 20):
        r = run(dict(name='R4-relboth%02d' % k, desc='Triangle + Right 1-40, pad 0 for %d ticks, Triangle + Right again' % k, hero_px=CENTER, n=41 + k + 40, pad=[(1, TRI | RIGHT), (41, 0), (41 + k, TRI | RIGHT)]), 'g4')
        out[('both', k)] = r
        t3 = first(r, 'anim_cur', 3, 41)
        say('   k=%-2d first Sprint after the release: %s  states %s  sprintCnt@41 %d' % (k, t3, tl(r, 'anim_cur', 41, 41 + k + 40), H(r, 41, 'sprint_cnt')))
    say('(d) release Triangle only (Right kept), re-press Triangle after k ticks')
    for k in (3, 12):
        r = run(dict(name='R4-relT%02d' % k, desc='Triangle + Right 1-40, Right only %d ticks, Triangle + Right again' % k, hero_px=CENTER, n=41 + k + 40, pad=[(1, TRI | RIGHT), (41, RIGHT), (41 + k, TRI | RIGHT)]), 'g4')
        out[('T', k)] = r
        say('   k=%-2d states %s' % (k, tl(r, 'anim_cur', 40, 41 + k + 40)))
    say()
    return out


# ---- G5 ----------------------------------------------------------------------------------------------------------------------------------------------
def g5():
    say('== G5 blocked: wall, steps, map edge, the bounce 0x3E ==')
    out = {}
    wallE = R.Layout().rect(14, 0, 15, 59, word=0x40)
    out['E'] = run(dict(name='R5-wall-E', desc='wall word 0x40 at cols 14-15 (x 336), run east from (100,100), 170 ticks (several bounces)', hero_px=(100, 100), n=170, pad=[(1, TRI | RIGHT)], layout=wallE), 'g5')
    out['E-adj'] = run(dict(name='R5-wall-E-adjacent', desc='hero starts at x=325 (box edge touching the wall), Triangle + Right from tick 1, 80 ticks', hero_px=(325, 100), n=80, pad=[(1, TRI | RIGHT)], layout=wallE), 'g5')
    out['E-walk'] = run(dict(name='R5-wall-E-walk', desc='control: walk east (Right only) into the same wall, 150 ticks', hero_px=(100, 100), n=150, pad=[(1, RIGHT)], layout=wallE), 'g5')
    wallW = R.Layout().rect(2, 0, 3, 59, word=0x40)
    out['W'] = run(dict(name='R5-wall-W', desc='wall word 0x40 at cols 2-3 (x 48..95), run west from (300,100), 100 ticks', hero_px=(300, 100), n=100, pad=[(1, TRI | LEFT)], layout=wallW, direction=8), 'g5')
    wallN = R.Layout().rect(0, 12, 51, 13, word=0x40)
    out['N'] = run(dict(name='R5-wall-N', desc='wall word 0x40 at rows 12-13 (y 192..223), run north from (300,300), 100 ticks', hero_px=(300, 300), n=100, pad=[(1, TRI | UP)], layout=wallN, direction=16), 'g5')
    wallS = R.Layout().rect(0, 30, 51, 31, word=0x40)
    out['S'] = run(dict(name='R5-wall-S', desc='wall word 0x40 at rows 30-31 (y 480..511), run south from (300,300), 100 ticks', hero_px=(300, 300), n=100, pad=[(1, TRI | DOWN)], layout=wallS, direction=0), 'g5')
    out['E-rel'] = run(dict(name='R5-wall-E-release', desc='as R5-wall-E, the whole pad released at tick 88 (the tick after the bounce starts), 130 ticks', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (88, 0)], layout=wallE), 'g5')
    out['E-relT'] = run(dict(name='R5-wall-E-releaseT', desc='as R5-wall-E, Triangle released at tick 88 (Right kept), 130 ticks', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (88, RIGHT)], layout=wallE), 'g5')
    out['E-atk'] = run(dict(name='R5-wall-E-square', desc='as R5-wall-E, Square pressed at tick 90 during the bounce, 130 ticks', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (90, TRI | RIGHT | SQU), (91, TRI | RIGHT)], layout=wallE), 'g5')
    for hb in (1, 2):
        lay = R.Layout().rect(14, 0, 51, 59, hb=hb)
        out['step%d' % hb] = run(dict(name='R5-step-%dpx' % (16 * hb), desc='ground rises by %d px at col 14 (x 336), run east from (100,100), 110 ticks' % (16 * hb), hero_px=(100, 100), n=110, pad=[(1, TRI | RIGHT)], layout=lay), 'g5')
        out['walkstep%d' % hb] = run(dict(name='R5-walk-step-%dpx' % (16 * hb), desc='control: walk east into the same %d px rise, 150 ticks' % (16 * hb), hero_px=(100, 100), n=150, pad=[(1, RIGHT)], layout=lay), 'g5')
    rE = out['E']
    t_stop = next(r['tick'] for r in rE['rows'] if r['tick'] > 26 and r['hero']['pos_x'] == H(rE, r['tick'] - 1, 'pos_x') and r['hero']['force_x'] > 0)
    t_b = first(rE, 'anim_cur', 0x3E)
    say('wall E: last moving tick %d (pos %.2f), first tick without progress %d, ForceAdjusted (+0x13C) first set at the end of tick %s, bounce 0x3E first tick %s, 3E last tick %s' % (
        t_stop - 1, H(rE, t_stop - 1, 'pos_x') / 65536.0, t_stop, first(rE, 'force_adj', 1, 1), t_b, first_not(rE, 'anim_cur', 0x3E, t_b) - 1))
    for k, r in out.items():
        say('  %-10s %s' % (k, tl(r)))
    r = out['E']
    t = first(r, 'anim_cur', 0x3E)
    say('bounce detail (wall E): tick %s: dir_target %02X (was 18), ForceX %d, ForceZ %d, sounds %s, effects %s' % (t, H(r, t, 'dir_target') & 0xFF, H(r, t, 'force_x'), H(r, t, 'force_z'),
        sounds(r, t, t), [(a, b) for a, b in effects(r, t, t)]))
    say('3E length: ticks %s..%s; Z arc %s' % (t, first_not(r, 'anim_cur', 0x3E, t) - 1, [H(r, x, 'pos_z') / 65536.0 for x in range(t, t + 12)]))
    say('3E invulnerability: animFlags B4 during 3E = 0x%X' % H(r, t, 'anim_flags_B4'))
    say()
    return out


# ---- G6 ----------------------------------------------------------------------------------------------------------------------------------------------
def g6():
    say('== G6 ledge: the ground rises by hb*16 px at cols 0-13 (x < 336); run east from (100,100) over the edge ==')
    out = {}
    for hb in (1, 2, 4):
        lay = R.Layout().rect(0, 0, 13, 59, hb=hb)
        out['drop%d' % hb] = run(dict(name='R6-drop-%dpx' % (16 * hb), desc='ground %d px high for x < 336, 0 beyond; Triangle + Right from (100,100), 130 ticks' % (16 * hb), hero_px=(100, 100), n=130,
                                      pad=[(1, TRI | RIGHT)], layout=lay), 'g6')
    lay2 = R.Layout().rect(0, 0, 13, 59, hb=2)
    out['drop2-relT'] = run(dict(name='R6-drop-32px-releaseT-air', desc='as R6-drop-32px, Triangle released at tick 91 (in the air)', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (91, RIGHT)], layout=lay2), 'g6')
    out['drop2-relall'] = run(dict(name='R6-drop-32px-release-all-air', desc='as R6-drop-32px, pad 0 from tick 91 (in the air)', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (91, 0)], layout=lay2), 'g6')
    out['drop2-sq'] = run(dict(name='R6-drop-32px-square-air', desc='as R6-drop-32px, Square pressed at tick 93 in the air', hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT), (93, TRI | RIGHT | SQU), (94, TRI | RIGHT)], layout=lay2), 'g6')
    out['walk-drop2'] = run(dict(name='R6-walk-drop-32px', desc='control: walk east (Right only) off the 32 px ledge', hero_px=(100, 100), n=150, pad=[(1, RIGHT)], layout=lay2), 'g6')
    say('timelines:')
    for k, r in out.items():
        say('  %-12s %s' % (k, tl(r)))
    say('ground contact timing at the ledge (edge x = 336 px; box = pos-10 .. pos+10): per tick pos_x, on_ground, anim_cur')
    r = out['drop2']
    t0 = first(r, 'pos_x', int(325.5 * 65536), 1)
    for t in range(82, 95):
        say('   tick %d pos_x %.2f ground %d terrain %.2f anim %02X ForceX %d ForceZ %d' % (t, H(r, t, 'pos_x') / 65536.0, H(r, t, 'on_ground'), H(r, t, 'terrain') / 65536.0, H(r, t, 'anim_cur') & 0xFF, H(r, t, 'force_x'), H(r, t, 'force_z')))
    tg = first(r, 'on_ground', 0, 1)
    ta = first(r, 'anim_cur', 0x2D)
    say('first tick with on_ground 0: %s; first tick in 0x2D: %s (gap %s tick)' % (tg, ta, (ta - tg) if tg and ta else None))
    say('landing: first tick on_ground 1 after the fall: %s; next tick anim %02X' % (first(r, 'on_ground', 1, ta), H(r, first(r, 'on_ground', 1, ta) + 1, 'anim_cur') & 0xFF))
    say('air ForceX: 258048 at the last ground tick, then %s' % [H(r, t, 'force_x') for t in range(ta - 1, ta + 5)])
    say()
    return out


# ---- G7 ----------------------------------------------------------------------------------------------------------------------------------------------
def g7():
    say('== G7 attack during a run ==')
    out = {}

    def pad_sq(t0, t1=None, base=TRI | RIGHT, btn=SQU):
        return [(1, base), (t0, base | btn), ((t1 if t1 else t0 + 1), base)]
    out['sq-windup'] = run(dict(name='R7-square-windup', desc='Triangle + Right from 1; Square pressed at tick 10 (1 tick) during the wind-up, 90 ticks', hero_px=CENTER, n=90, pad=pad_sq(10)), 'g7')
    out['sq-windup-hold'] = run(dict(name='R7-square-windup-held', desc='as above, Square held from tick 10 to the end', hero_px=CENTER, n=90, pad=[(1, TRI | RIGHT), (10, TRI | RIGHT | SQU)]), 'g7')
    out['sq-sprint'] = run(dict(name='R7-square-sprint', desc='Square pressed at tick 40 (1 tick) while Sprint, 100 ticks', hero_px=(100, 100), n=100, pad=pad_sq(40)), 'g7')
    out['sq-sprint-hold'] = run(dict(name='R7-square-sprint-held', desc='Square held from tick 40 to the end (Triangle + Right kept), 140 ticks', hero_px=(100, 100), n=140, pad=[(1, TRI | RIGHT), (40, TRI | RIGHT | SQU)]), 'g7')
    out['cro-sprint'] = run(dict(name='R7-cross-sprint', desc='Cross pressed at tick 40 (1 tick) while Sprint, 100 ticks', hero_px=(100, 100), n=100, pad=pad_sq(40, btn=CRO)), 'g7')
    out['cir-sprint'] = run(dict(name='R7-circle-sprint', desc='Circle pressed at tick 40 (1 tick) while Sprint, 100 ticks', hero_px=(100, 100), n=100, pad=pad_sq(40, btn=CIR)), 'g7')
    out['cir-windup'] = run(dict(name='R7-circle-windup', desc='Circle pressed at tick 10 (1 tick) during the wind-up, 60 ticks', hero_px=CENTER, n=60, pad=pad_sq(10, btn=CIR)), 'g7')
    out['cro-windup'] = run(dict(name='R7-cross-windup', desc='Cross pressed at tick 10 (1 tick) during the wind-up, 60 ticks', hero_px=CENTER, n=60, pad=pad_sq(10, btn=CRO)), 'g7')
    out['sq-stop'] = run(dict(name='R7-square-stopsprint', desc='Right released at 41 (StopSprint), Square pressed at tick 45, 90 ticks', hero_px=(100, 100), n=90, pad=[(1, TRI | RIGHT), (41, TRI), (45, TRI | SQU), (46, TRI)]), 'g7')
    out['cro-stop'] = run(dict(name='R7-cross-stopsprint', desc='Right released at 41 (StopSprint), Cross pressed at tick 45, 90 ticks', hero_px=(100, 100), n=90, pad=[(1, TRI | RIGHT), (41, TRI), (45, TRI | CRO), (46, TRI)]), 'g7')
    say('timelines:')
    for k, r in out.items():
        say('  %-16s %s' % (k, tl(r)))
    r = out['sq-sprint']
    t4 = first(r, 'anim_cur', 4)
    t4e = first_not(r, 'anim_cur', 4, t4)
    say('Tackle 0x04 (Square at 40, no target): starts tick %s, ends tick %s (length %s), ForceX at start %d, decay %d/tick, distance %.2f px, sounds %s, dust %s; then states %s' % (
        t4, t4e - 1, t4e - t4, H(r, t4, 'force_x'), H(r, t4, 'force_x') - H(r, t4 + 1, 'force_x'), dpx(r, t4 - 1, t4e - 1), sounds(r, t4 - 1, t4e), [x for x in effects(r, t4, t4e) if x[1] != 7][:4], tl(r, 'anim_cur', t4e - 2, t4e + 40)))
    say('Tackle animFlags B4 = 0x%X, cur_attack = 0x%X, frame_box = 0x%X' % (H(r, t4 + 5, 'anim_flags_B4'), H(r, t4 + 5, 'cur_attack'), H(r, t4 + 5, 'frame_box')))
    r = out['sq-windup']
    say('Square in the wind-up (tick 10): anim %02X at tick 10, sword swing length %s ticks, then %s' % (H(r, 10, 'anim_cur') & 0xFF, (first_not(r, 'anim_cur', 0x10, 10) or 0) - 10, tl(r, 'anim_cur', 9, 70)))
    # Tackle into the Beannoid, sweep of the press tick
    say('Tackle into an inert Beannoid of map 14 (target at (252,96), hero from (100,96), Triangle + Right, Square 1 tick at tick s):')
    best = None
    sweep = {}
    for s in range(30, 64, 2):
        r = run(dict(name='R7-tackle-s%02d' % s, desc='hero (100,96) Triangle + Right, Square pressed at tick %d, Beannoid (20,11) inert' % s, hero_px=(100, 96), n=96, pad=pad_sq(s), targets=[(20, 11, 0)], experiments=True), 'g7/tackle')
        sweep[s] = r
        t4 = first(r, 'anim_cur', 4)
        tgt = [x['c1d0'] for x in [rr['targets'][0] for rr in r['rows']]]
        hit_t = next((rr['tick'] for rr in r['rows'] if rr['targets'][0]['c1d0'] == 25), None)
        hurt_t = first(r, 'anim_cur', 0x31)
        sparks = sum(1 for rr in r['rows'] for k, v in rr['journal'] if k == 'effect' and v['id'] == 9 and rr['tick'] == hit_t)
        say('   s=%-2d  tackle tick %s..%s  tackle hit on target at tick %s (3 sparks: %d)  hero hurt (0x31) at tick %s  target hp end %d  experiments %d' % (
            s, t4, (first_not(r, 'anim_cur', 4, t4) - 1) if t4 else None, hit_t, sparks, hurt_t, r['rows'][-1]['targets'][0]['hp'], len(r['experiments'])))
    r0 = sweep[44]
    say('   experiment at s=44: ' + (exp_line(r0['experiments'][0]) if r0['experiments'] else 'none'))
    out['tackle'] = sweep
    # the plain run into the Beannoid (no Square)
    out['bean-plain'] = run(dict(name='R7-run-into-beannoid', desc='no attack: Triangle + Right from (100,96) into the Beannoid, 90 ticks', hero_px=(100, 96), n=90, pad=[(1, TRI | RIGHT)], targets=[(20, 11, 0)], experiments=True), 'g7')
    r = out['bean-plain']
    say('run into the Beannoid without attacking: timeline %s; hero touched at tick %s (target hit counter), hurt 0x31 at %s, hero HP %d -> %d' % (tl(r), next((rr['tick'] for rr in r['rows'] if rr['targets'][0]['hit_counter']), None),
        first(r, 'anim_cur', 0x31), H(r, 0, 'hp'), H(r, 90, 'hp')))
    # charge counter with a chargeable weapon
    out['holy'] = run(dict(name='R7-holy-square-held', desc='Holy Sword Elnard owned and equipped (inv holy); Triangle + Right from 1, Square held from tick 30 to 160', hero_px=(100, 100), n=160, pad=[(1, TRI | RIGHT), (30, TRI | RIGHT | SQU)], inv='holy'), 'g7')
    r = out['holy']
    say('holy sword, Square held from 30: timeline %s; charge counter at 30/45/60/90/120/159: %s; wptr/aura at 159: %x/%x' % (tl(r), [H(r, t, 'charge_cnt') for t in (30, 45, 60, 90, 120, 159)], H(r, 159, 'wptr'), H(r, 159, 'aura')))
    out['holy-windup'] = run(dict(name='R7-holy-square-held-windup', desc='inv holy; Triangle + Right from 1; Square held from tick 5 (wind-up), 100 ticks', hero_px=CENTER, n=100, pad=[(1, TRI | RIGHT), (5, TRI | RIGHT | SQU)], inv='holy'), 'g7')
    r = out['holy-windup']
    say('holy sword, Square held from 5 (wind-up): timeline %s; charge counter at 5/10/30/60/99: %s' % (tl(r), [H(r, t, 'charge_cnt') for t in (5, 10, 30, 60, 99)]))
    say()
    return out


# ---- G8 ----------------------------------------------------------------------------------------------------------------------------------------------
def ladder_layout(ladder=True):
    lay = R.Layout().rect(0, 0, 51, 19, hb=10).rect(0, 20, 51, 59, hb=2)
    if ladder:
        lay.rect(2, 20, 20, 20, word=0x0C00)
    return lay


def g8():
    say('== G8 ladder (slope 6): cliff of 8 units at rows <= 19, platform hb 2 below, strip of slope-6 cells (word 0x0C00) on row 20, cols 2-20 ==')
    out = {}
    out['sprint'] = run(dict(name='R8-ladder-sprint', desc='Triangle + Up from (100,392) facing north, 70 ticks', hero_px=(100, 392), n=70, pad=[(1, TRI | UP)], layout=ladder_layout(True), direction=16), 'g8')
    out['noladder'] = run(dict(name='R8-cliff-no-ladder', desc='control: same without the slope-6 strip (bounce expected)', hero_px=(100, 392), n=90, pad=[(1, TRI | UP)], layout=ladder_layout(False), direction=16), 'g8')
    out['walk'] = run(dict(name='R8-ladder-walk', desc='control: walk (Up only) into the ladder from (100,392), 120 ticks', hero_px=(100, 392), n=120, pad=[(1, UP)], layout=ladder_layout(True), direction=16), 'g8')
    out['sprint-late'] = run(dict(name='R8-ladder-sprint-faceE', desc='hero facing east, Triangle + Up from tick 1 (TargetDirection written by the wind-up), 70 ticks', hero_px=(100, 392), n=70, pad=[(1, TRI | UP)], layout=ladder_layout(True), direction=24), 'g8')
    out['diag'] = run(dict(name='R8-ladder-sprint-UR', desc='Triangle + Up + Right (diagonal, no sprint start): wind-up only', hero_px=(100, 392), n=60, pad=[(1, TRI | UP | RIGHT)], layout=ladder_layout(True), direction=16), 'g8')
    for k, r in out.items():
        say('  %-12s %s' % (k, tl(r)))
    r = out['sprint']
    t = first(r, 'anim_cur', 0x0E)
    say('sprint -> ladder: slope 6 first tick %s, ForceAdjusted first tick %s, climb 0x0E first tick %s (priority over the Sprint bounce 0x3E: %s)' % (first(r, 'slope', 6), first(r, 'force_adj', 1), t, first(r, 'anim_cur', 0x3E)))
    r = out['noladder']
    say('control without ladder: bounce 0x3E first tick %s, ForceAdjusted first tick %s' % (first(r, 'anim_cur', 0x3E), first(r, 'force_adj', 1)))
    say()
    return out


# ---- G9 ----------------------------------------------------------------------------------------------------------------------------------------------
def g9():
    say('== G9 water and ice ==')
    out = {}
    lay = R.Layout().rect(14, 0, 51, 59, word=0x800)
    out['deep-boots'] = run(dict(name='R9-deepwater-boots2', desc='deep water (word 0x0800, slope 4) from col 14 (x 336) at the same height; swim boots 0x1B (level 2); Triangle + Right from (100,100), 130 ticks', hero_px=(100, 100), n=130,
                                 pad=[(1, TRI | RIGHT)], layout=lay, inv='b1b'), 'g9')
    out['deep-noboots'] = run(dict(name='R9-deepwater-noboots', desc='control: same water, kit inventory (boots level 0): the water is a wall for the hero', hero_px=(100, 100), n=110, pad=[(1, TRI | RIGHT)], layout=lay), 'g9')
    allw = R.Layout().rect(0, 0, 51, 59, word=0x800)
    out['allwater-run'] = run(dict(name='R9-allwater-start-T-R', desc='all cells deep water, swim boots; hero starts in the water, Triangle + Right from tick 1, 80 ticks', hero_px=CENTER, n=80, pad=[(1, TRI | RIGHT)], layout=allw, inv='b1b'), 'g9')
    out['allwater-walk'] = run(dict(name='R9-allwater-start-R', desc='all cells deep water, swim boots; Right only (swim), 60 ticks', hero_px=CENTER, n=60, pad=[(1, RIGHT)], layout=allw, inv='b1b'), 'g9')
    out['allwater-T'] = run(dict(name='R9-allwater-start-T', desc='all cells deep water, swim boots; Triangle only, 40 ticks', hero_px=CENTER, n=40, pad=[(1, TRI)], layout=allw, inv='b1b'), 'g9')
    sh = R.Layout().rect(0, 0, 51, 59, wk=0x18)
    out['shallow'] = run(dict(name='R9-shallow-water', desc='whole field walkability 0x18 (shallow water, VramOR 0x18), kit; Triangle + Right, 80 ticks', hero_px=(100, 100), n=80, pad=[(1, TRI | RIGHT)], layout=sh), 'g9')
    out['shallow-boots'] = run(dict(name='R9-shallow-water-boots1', desc='as above with boots 0x1A (level 1)', hero_px=(100, 100), n=80, pad=[(1, TRI | RIGHT)], layout=sh, inv='b1a'), 'g9')
    ice = R.Layout().rect(0, 0, 51, 59, wk=0x20)
    out['ice'] = run(dict(name='R9-ice', desc='whole field walkability 0x20 (ice, VramOR 0x20); Triangle + Right 1-60 then released, 120 ticks', hero_px=(100, 100), n=120, pad=[(1, TRI | RIGHT), (61, 0)], layout=ice), 'g9')
    out['flat-ref'] = run(dict(name='R9-flat-reference', desc='reference: flat dry ground, same pad as R9-shallow-water', hero_px=(100, 100), n=80, pad=[(1, TRI | RIGHT)]), 'g9')
    for k, r in out.items():
        say('  %-14s %s' % (k, tl(r)))
    r = out['deep-boots']
    t = first(r, 'slope', 4)
    say('deep water with boots: first tick slope 4 %s (box fully in water, pos_x %.2f), anim at that tick %02X, next ticks %s; Triangle still held' % (t, H(r, t, 'pos_x') / 65536.0, H(r, t, 'anim_cur') & 0xFF, tl(r, 'anim_cur', t - 1, t + 8)))
    r = out['deep-noboots']
    say('deep water without boots: ForceAdjusted first tick %s, bounce 0x3E first tick %s, x at the wall %.2f' % (first(r, 'force_adj', 1), first(r, 'anim_cur', 0x3E), H(r, first(r, 'force_adj', 1), 'pos_x') / 65536.0))
    for k in ('shallow', 'shallow-boots', 'ice', 'flat-ref'):
        r = out[k]
        say('%-14s ForceX at ticks 30/40/60: %s; step per tick @60 %.4f px; vram_or@60 0x%X' % (k, [H(r, t, 'force_x') for t in (30, 40, 60)], dpx(r, 60, 61), H(r, 60, 'vram_or')))
    r = out['ice']
    say('ice after release (tick 61..): ForceX %s' % [H(r, t, 'force_x') for t in (60, 61, 62, 65, 70, 80, 100, 120)])
    say()
    return out


# ---- G10 ---------------------------------------------------------------------------------------------------------------------------------------------
def stick_rule(x, y, facing_axis, eps=1.0 / 16.0, gate=0.5):
    """D-E20-15 as written in the plan, on a stick value (x right-positive, y up-positive): returns (stick bits nibble, override nibble or None, kind).
       nibble bits: Up 1, Right 2, Down 4, Left 8 (hold >> 12).  facing_axis = 'H' or 'V' (the axis of the hero's current facing)."""
    bits = 0
    if y >= gate: bits |= 1
    if x >= gate: bits |= 2
    if y <= -gate: bits |= 4
    if x <= -gate: bits |= 8
    horiz = bits & 0xA
    vert = bits & 0x5
    if horiz and vert:
        ax, ay = abs(x), abs(y)
        if ax - ay > eps:
            return bits, horiz, 'H'
        if ay - ax > eps:
            return bits, vert, 'V'
        return bits, (horiz if facing_axis == 'H' else vert), 'tie->facing %s' % facing_axis
    return bits, None, 'cardinal-or-none'


def g10():
    import math
    say('== G10 the stick (D-E20-15).  The binary has no analog input: it reads one digital hold word.  What the oracle can model is the rule\'s EFFECT: the dash lookups'
        ' g_dashDirections[nibble] (0x80031FD0 in 0x29, 0x800321A0 in 0x2A) read an override nibble chosen by the harness; every other use of the pad keeps the ORed bits ==')
    out = {}
    nib = {'U': 1, 'R': 2, 'D': 4, 'L': 8}
    facing_dirs = {'E': 24, 'N': 16, 'S': 0, 'W': 8}
    say('(a) start: Triangle + diagonal pad bits (as the ORed stick bits) from tick 1, override = a cardinal bit or none; hero facing the given way; 50 ticks')
    table = []
    for fname, fd in facing_dirs.items():
        for dn, dd in DIAG:
            for on, ov in (('none', None), ('V', 'V'), ('H', 'H')):
                bits = dd >> 12
                v = None
                if ov == 'H': v = bits & 0xA
                if ov == 'V': v = bits & 0x5
                nm = 'R10-start-%s-%s-%s' % (fname, dn, on)
                r = run(dict(name=nm, desc='hero facing %s (dir %d); Triangle + %s (diagonal bits) from tick 1; dash override nibble %s' % (fname, fd, dn, v), hero_px=CENTER, n=50,
                             pad=[(1, TRI | dd)], dash=[(1, v)], direction=fd), 'g10')
                t3 = first(r, 'anim_cur', 3)
                table.append((fname, dn, on, v, t3, H(r, t3, 'dir_target') & 0xFF if t3 else None, tl(r, 'anim_cur', 20, 50), H(r, 50, 'facing')))
                out[nm] = r
    for t in table:
        say('   facing %s  pad %s  override %-4s (nibble %s): sprint start %s, dir_target at start %s, states 20-50: %s, facing@50 %d' % (t[0], t[1], t[2], t[3], t[4], ('%02X' % t[5]) if t[5] is not None else '-', t[6], t[7]))
    say('(b) resume: Triangle + Right 1-40, Triangle only 41..40+k, then the diagonal Up+Right from 41+k with override Right (nibble 2) or none; 60 ticks after')
    for k in (5, 10, 11, 12, 16):
        for on, v in (('none', None), ('H', 2), ('V', 1)):
            nm = 'R10-resume-k%02d-UR-%s' % (k, on)
            r = run(dict(name=nm, desc='Triangle + Right 1-40, Triangle only for %d ticks, then Triangle + Up + Right with dash override %s' % (k, v), hero_px=CENTER, n=41 + k + 30,
                         pad=[(1, TRI | RIGHT), (41, TRI), (41 + k, TRI | UP | RIGHT)], dash=[(1, None), (41 + k, v)]), 'g10')
            t3 = first(r, 'anim_cur', 3, 41)
            say('   k=%-2d override %-4s: resume tick %s (re-press at %d) dir_target %s states %s' % (k, on, t3, 41 + k, ('%02X' % (H(r, t3, 'dir_target') & 0xFF)) if t3 else '-', tl(r, 'anim_cur', 41, 41 + k + 29)))
            out[nm] = r
    say('(c) the rule applied to a stick sweep (pure Python on the plan\'s text; radius 1.0 and 0.75; eps 1/16; hero facing East / North): angle -> bits, override, kind')
    for rad in (1.0, 0.75):
        for fa in ('H', 'V'):
            seg = []
            prev = None
            for a in range(0, 360):
                x, y = rad * math.cos(math.radians(a)), rad * math.sin(math.radians(a))
                bits, ov, kind = stick_rule(x, y, fa)
                key = (bits, ov, kind)
                if key != prev:
                    seg.append([a, a, key])
                    prev = key
                else:
                    seg[-1][1] = a
            say('   radius %.2f facing axis %s: ' % (rad, fa) + '; '.join('%d-%d deg bits %X ov %s %s' % (a, b, k[0], k[1], k[2]) for a, b, k in seg if k[2] != 'cardinal-or-none' or True))
    say()
    return out


GROUPS = dict(G1=g1, G2=g2, G3=g3, G4=g4, G5=g5, G6=g6, G7=g7, G8=g8, G9=g9, G10=g10)


def main(argv):
    global CROSS
    os.makedirs(OUT, exist_ok=True)
    CROSS = '--cross' in argv
    argv = [a for a in argv if not a.startswith('--')]
    names = argv or list(GROUPS)
    for g in names:
        GROUPS[g]()
        with open(os.path.join(OUT, 'summary-%s.txt' % g), 'w', encoding='utf-8') as f:
            f.write('\n'.join(LOG) + '\n')
        del LOG[:]
        os.makedirs(os.path.join(OUT, 'check'), exist_ok=True)
        with open(os.path.join(OUT, 'check', 'xcheck-%s.txt' % g), 'w', encoding='utf-8') as f:
            f.write('group %s: cross-check %s; runs compared %d (of which with a dash override %d), ticks compared %d, mismatches %d %s, uninitialised-read sites over both engines %d\n' % (
                g, 'ON' if CROSS else 'off', XC['runs'], XC['dash_runs'], XC['ticks'], len(XC['mismatches']), XC['mismatches'][:5], XC['uninit_sites']))
        for k in ('runs', 'ticks', 'uninit_sites', 'dash_runs'):
            XC[k] = 0
        XC['mismatches'] = []
        with open(os.path.join(OUT, 'check', 'cov-%s.json' % g), 'w') as f:
            json.dump(sorted(R.COV), f)


if __name__ == '__main__':
    main(sys.argv[1:])
