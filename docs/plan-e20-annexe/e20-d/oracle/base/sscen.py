"""sscen.py - E20.i discovery ("swim-oracle"): the swim scenarios on the REAL code of ALUN_CD.EXE (France) in the MIPS emulator of E20.0 / E20.c1 / c1b / c2 / E20.d.

NEW FILE.  Nothing in tools/, hworld.py, hscen.py, rworld.py, rscen.py is edited: this module builds specs for rworld.run_r (the run-oracle runner), runs them and writes
the traces with rscen.write into ../out-swim/ (rscen.OUT is redirected, so ../out/ is never touched).  The specs hold SETUP and INPUTS only; every number printed comes from
the emulated binary.  Nothing here knows an expected value (the analysis helpers only look up ticks and columns).

usage:  python -B sscen.py [W1 .. W9] [--cross]      (no argument: every group; --cross repeats each run through the second interpreter and compares tick by tick)

Groups
  W1  entering deep water: walking and running by boots level 0..3, a jump over the shore, a fall from a ledge, a higher bank
  W2  swimming: idle, the eight directions, turning, release, boots level 3, a level-0 hero already inside
  W3  the dive: each button edge, held buttons, repeated presses, direction at the dive tick, the end of the dive (surfacing), the first tick after the entry
  W4  leaving the water: onto equal-height land, on a dive, with Triangle held, diagonal, onto a bank one unit higher
  W5  walls in the water: wall, diagonal against a wall, dive against a wall, the map edge, level 0/1 walls (information)
  W6  weapon state in the water: the charge counter, the pending-shot pointer, the aura pointer (UpdatePlayerWeaponEffect is skipped on slope 4)
  W7  hurt in the water (information for E20.c2b): idle, swimming, diving, at the shore, pad held, a second contact
  W8  the slope-4 state table: every TargetAnimationId at the start of a water tick (what the tick writes), pad idle / direction / dive edge
  W9  the Circle button in the water (TryUseItem 0x8002ED64 with the kit) and the land-side handling of the swim states
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R
import rscen as RS

OUT = os.path.join(HERE, '..', 'out-swim')
RS.OUT = OUT

UP, RIGHT, DOWN, LEFT = R.UP, R.RIGHT, R.DOWN, R.LEFT
TRI, CIR, CRO, SQU = R.TRI, R.CIR, R.CRO, R.SQU
DIRS8 = [('U', UP), ('R', RIGHT), ('D', DOWN), ('L', LEFT), ('UR', UP | RIGHT), ('DR', DOWN | RIGHT), ('DL', DOWN | LEFT), ('UL', UP | LEFT)]
BTN = [('T', TRI), ('X', CRO), ('S', SQU), ('O', CIR)]
LV = {0: 'kit', 1: 'b1a', 2: 'b1b', 3: 'b1c'}          # boots level -> inventory of the oracle (0x1A level 1, 0x1B level 2, 0x1C level 3)
CENTER = (624, 480)
LOG = []

# extra inventories of this module (in-memory entries of hworld.INV, nothing on disk is edited): the holy sword and the swim boots together, and equipped Circle items
import hworld as _HW
_HW.INV['holy_b1b'] = dict(name='holy_b1b', owned=(1, 17, 25, 4, 0x1B), weapon=1, item=None, desc='Holy Sword Elnard + boots 0x1B (level 2), kit otherwise')
for _it in range(0x1F, 0x33):
    _HW.INV['item%02X' % _it] = dict(name='item%02X' % _it, owned=(1, 17, 25, 0x1B, _it), weapon=1, item=_it, desc='boots 0x1B + item 0x%02X owned and set as the equipped Circle item' % _it)


def say(s=''):
    LOG.append(s)
    print(s)


H = RS.H
first = RS.first
first_not = RS.first_not
tl = RS.tl
sounds = RS.sounds
effects = RS.effects

CROSS = False


def run(spec, sub=''):
    res = R.run_r(spec)
    if CROSS:
        RS.CROSS = True
        RS.cross_check(res, R.run_r(spec, 'mips'))
    RS.write(res, sub)
    return res


def lake(c0=14, hb=None, **kw):
    """deep water (word 0x0800: slope 4) from column c0 to the east edge, at the height of the rest of the field (0)"""
    return R.Layout().rect(c0, 0, 51, 59, word=0x800, hb=hb, **kw)


def allwater():
    return R.Layout().rect(0, 0, 51, 59, word=0x800)


def tap(base, btn, t, n=1):
    """pad pieces: `base` held from tick 1, `btn` added for n ticks from tick t"""
    return [(1, base), (t, base | btn), (t + n, base)]


def hz(res, t, key):
    return H(res, t, key)


def row(res, t):
    r = (res['pre'] if t == 0 else res['rows'][t - 1])
    h = r['hero']
    return 't%-3d %02X>%02X dir %02X/%02X f%d F(%d,%d,%d) pos(%.2f,%.2f,%.2f) g%d adj%d sl%d b0=%d ch%d snd%s fx%s' % (
        t, h['anim_target'] & 0xFF, h['anim_cur'] & 0xFF, h['dir_target'] & 0xFF, h['dir_cur'] & 0xFF, h['facing'], h['force_x'], h['force_y'], h['force_z'],
        h['pos_x'] / 65536.0, h['pos_y'] / 65536.0, h['pos_z'] / 65536.0, h['on_ground'], h['force_adj'], h['slope'], h['b0_complete'], h['charge_cnt'],
        [v for k, v in [(k, v[0]) for k, v in r.get('journal', []) if k == 'sound']], [v['id'] for k, v in r.get('journal', []) if k == 'effect'])


def show(res, a, b, step=1):
    for t in range(a, b + 1, step):
        say('   ' + row(res, t))


def tls(res, key='anim_cur', a=0, b=None):
    return tl(res, key, a, b)


# ---- W1 ----------------------------------------------------------------------------------------------------------------------------------------------
def w1():
    say('== W1 entering deep water (word 0x0800 from col 14 = x 336, same height as the shore).  Hero box = pos +- 10 px, so slope 4 needs pos_x >= 346 ==')
    out = {}
    for lv in (0, 1, 2, 3):
        out['walk%d' % lv] = run(dict(name='W1-walk-L%d' % lv, desc='boots level %d; walk east (Right only) from (300,100) into the lake, 100 ticks' % lv, hero_px=(300, 100), n=100, pad=[(1, RIGHT)],
                                      layout=lake(), inv=LV[lv]), 'w1')
        out['run%d' % lv] = run(dict(name='W1-run-L%d' % lv, desc='boots level %d; Triangle + Right from (100,100) into the lake, 130 ticks' % lv, hero_px=(100, 100), n=130, pad=[(1, TRI | RIGHT)],
                                     layout=lake(), inv=LV[lv]), 'w1')
    say('walk / run into the lake, per boots level (timelines of anim_cur; slope timeline):')
    for lv in (0, 1, 2, 3):
        r = out['walk%d' % lv]
        say('  walk L%d  anim %s | slope %s | ForceAdjusted first %s | x@100 %.2f' % (lv, tls(r), tls(r, 'slope'), first(r, 'force_adj', 1), H(r, 100, 'pos_x') / 65536.0))
        r = out['run%d' % lv]
        say('  run  L%d  anim %s | slope %s | ForceAdjusted first %s | x@130 %.2f' % (lv, tls(r), tls(r, 'slope'), first(r, 'force_adj', 1), H(r, 130, 'pos_x') / 65536.0))
    # jump over the shore: Right held, Cross pressed at tick s for one tick, from (300,100)
    say('jump over the shore (boots level 2): Right held from 1, Cross pressed for 1 tick at tick s, hero from (300,100), 90 ticks')
    for s in (3, 6, 9, 12, 15):
        r = run(dict(name='W1-jump-s%02d' % s, desc='boots level 2; Right held, Cross at tick %d, from (300,100)' % s, hero_px=(300, 100), n=90, pad=tap(RIGHT, CRO, s), layout=lake(), inv='b1b'), 'w1')
        out['jump%d' % s] = r
        say('  s=%-2d anim %s | slope %s | ground %s | z max %.2f | x@90 %.2f' % (s, tls(r), tls(r, 'slope'), tls(r, 'on_ground'), max(x['hero']['pos_z'] for x in r['rows']) / 65536.0, H(r, 90, 'pos_x') / 65536.0))
    # fall from a ledge into the water: land 32 px high on cols 0-13, the lake at height 0 from col 14
    ledge = R.Layout().rect(0, 0, 13, 59, hb=2).rect(14, 0, 51, 59, word=0x800)
    out['fall-walk'] = run(dict(name='W1-fall-walk', desc='boots level 2; land 32 px high (cols 0-13), lake at height 0 (cols 14-51); walk east off the ledge from (300,100), 120 ticks', hero_px=(300, 100), n=120,
                                pad=[(1, RIGHT)], layout=ledge, inv='b1b'), 'w1')
    out['fall-run'] = run(dict(name='W1-fall-run', desc='as above, Triangle + Right from (100,100): the run leaves the ledge, 160 ticks', hero_px=(100, 100), n=160, pad=[(1, TRI | RIGHT)], layout=ledge, inv='b1b'), 'w1')
    ledge1 = R.Layout().rect(0, 0, 13, 59, hb=1).rect(14, 0, 51, 59, word=0x800)
    out['fall1-walk'] = run(dict(name='W1-fall16-walk', desc='as W1-fall-walk with a 16 px ledge', hero_px=(300, 100), n=120, pad=[(1, RIGHT)], layout=ledge1, inv='b1b'), 'w1')
    # the same entries at boots level 0 and 1 (information: is the wall rule a horizontal-move rule only?)
    for lv in (0, 1):
        out['fall-walk-L%d' % lv] = run(dict(name='W1-fall-walk-L%d' % lv, desc='boots level %d (information); land 32 px high (cols 0-13), lake at height 0; walk east off the ledge from (300,100), 120 ticks' % lv, hero_px=(300, 100), n=120,
                                             pad=[(1, RIGHT)], layout=ledge, inv=LV[lv]), 'w1')
        out['jump-L%d' % lv] = run(dict(name='W1-jump-L%d' % lv, desc='boots level %d (information); Right held, Cross at tick 6 from (300,100): a jump over the shore' % lv, hero_px=(300, 100), n=90, pad=tap(RIGHT, CRO, 6), layout=lake(), inv=LV[lv]), 'w1')
        for k in ('fall-walk-L%d' % lv, 'jump-L%d' % lv):
            r = out[k]
            say('  %-12s anim %s | slope %s | ground %s | x@end %.2f | ForceAdjusted first %s' % (k, tls(r), tls(r, 'slope'), tls(r, 'on_ground'), r['rows'][-1]['hero']['pos_x'] / 65536.0, first(r, 'force_adj', 1)))
    for k in ('fall-walk', 'fall-run', 'fall1-walk'):
        r = out[k]
        say('  %-10s anim %s | slope %s | ground %s | z %.2f -> %.2f' % (k, tls(r), tls(r, 'slope'), tls(r, 'on_ground'), H(r, 1, 'pos_z') / 65536.0, r['rows'][-1]['hero']['pos_z'] / 65536.0))
    say()
    return out


# ---- W2 ----------------------------------------------------------------------------------------------------------------------------------------------
def w2():
    say('== W2 swimming (the whole field is deep water, boots level 2 unless said; the hero starts in swim idle 0x1D after 4 idle ticks) ==')
    out = {}
    pre = 5
    out['idle'] = run(dict(name='W2-idle', desc='all water, no pad, 120 ticks, hero starts in Idle (0)', hero_px=CENTER, n=120, pad=[], layout=allwater(), inv='b1b'), 'w2')
    r = out['idle']
    say('swim idle from Idle: anim %s | b0 at 40/80/120 %s | sounds %s | effects %s' % (tls(r), [H(r, t, 'b0_complete') for t in (40, 80, 120)], sounds(r), effects(r)))
    for n, d in DIRS8:
        r = run(dict(name='W2-dir-%s' % n, desc='all water, boots level 2; %s held from tick %d to 70, released from 71 to 130' % (n, pre), hero_px=CENTER, n=130, pad=[(pre, d), (71, 0)], layout=allwater(), inv='b1b', hero_anim=0x1D), 'w2')
        out['dir-' + n] = r
        say('  dir %-2s anim %s | dir_target@10 %02X facing@10 %d | Force@%d/%d/%d/%d %s | speed %.4f px/tick @60 | after release F@72/80/90/129 %s' % (
            n, tls(r), H(r, 10, 'dir_target') & 0xFF, H(r, 10, 'facing'), 6, 10, 30, 60, [(H(r, t, 'force_x'), H(r, t, 'force_y')) for t in (6, 10, 30, 60)],
            ((H(r, 61, 'pos_x') - H(r, 60, 'pos_x')) ** 2 + (H(r, 61, 'pos_y') - H(r, 60, 'pos_y')) ** 2) ** 0.5 / 65536.0, [(H(r, t, 'force_x'), H(r, t, 'force_y')) for t in (72, 80, 90, 129)]))
    r = run(dict(name='W2-turn', desc='all water; Right 5-40, Left 41-90, Up 91-130, Down 131-170', hero_px=CENTER, n=170, pad=[(pre, RIGHT), (41, LEFT), (91, UP), (131, DOWN)], layout=allwater(), inv='b1b', hero_anim=0x1D), 'w2')
    out['turn'] = r
    say('turn: anim %s | facing %s | dir_target %s' % (tls(r), tls(r, 'facing'), tls(r, 'dir_target')))
    show(r, 39, 48)
    r = run(dict(name='W2-L3-R', desc='all water, boots level 3, Right 5-70', hero_px=CENTER, n=90, pad=[(pre, RIGHT), (71, 0)], layout=allwater(), inv='b1c', hero_anim=0x1D), 'w2')
    out['L3-R'] = r
    r2 = out['dir-R']
    same = all(a['hero'] == b['hero'] for a, b in zip(r['rows'], r2['rows'][:90]))
    say('boots level 3 vs 2 (Right 5-70): hero columns identical over 90 ticks: %s' % same)
    for lv in (0, 1):
        r = run(dict(name='W2-L%d-inside-R' % lv, desc='all water, boots level %d (information): the hero is already inside; Right from tick 5, 60 ticks' % lv, hero_px=CENTER, n=60, pad=[(5, RIGHT)], layout=allwater(), inv=LV[lv], hero_anim=0x1D), 'w2')
        out['inside%d' % lv] = r
        say('  level %d inside the lake, Right: anim %s | x 5 -> 60: %.2f -> %.2f | ForceAdjusted first %s' % (lv, tls(r), H(r, 5, 'pos_x') / 65536.0, H(r, 60, 'pos_x') / 65536.0, first(r, 'force_adj', 1)))
    # long swim: the 0x0F animation chains to itself every 42 ticks: sound?
    r = run(dict(name='W2-long-R', desc='all water, Right from tick 5 for 200 ticks: the 0x0F animation (42 ticks, chain to itself) and its sounds / effects', hero_px=(100, 480), n=200, pad=[(pre, RIGHT)], layout=allwater(), inv='b1b', hero_anim=0x1D), 'w2')
    out['long'] = r
    say('long swim: anim %s | b0 at 50/100/150/200 %s | sounds %s | effects %s' % (tls(r), [H(r, t, 'b0_complete') for t in (50, 100, 150, 200)], sounds(r), effects(r)))
    say()
    return out


# ---- W3 ----------------------------------------------------------------------------------------------------------------------------------------------
def w3():
    say('== W3 the dive (pressed & 0xD0 = Triangle 0x10, Cross 0x40, Square 0x80; Circle 0x20 is not in the mask).  All water, boots level 2, hero in 0x1D at tick 0 ==')
    out = {}
    base = dict(hero_px=CENTER, layout=allwater(), inv='b1b', hero_anim=0x1D)
    say('(a) one-tick press of each button, from swim idle (press at tick 10) and while swimming Right (held from 5, press at tick 20), 120 ticks')
    for n, b in BTN:
        r = run(dict(name='W3-%s-idle' % n, desc='button %s pressed 1 tick at tick 10, no direction, 120 ticks' % n, n=120, pad=[(10, b), (11, 0)], **base), 'w3')
        out['%s-idle' % n] = r
        say('  %s from idle : anim %s | b0@50 %s | dir %s | pos x %.2f | snd %s' % (n, tls(r), H(r, 50, 'b0_complete'), tls(r, 'dir_target'), H(r, 120, 'pos_x') / 65536.0, sounds(r, 1, 120)))
        r = run(dict(name='W3-%s-swim' % n, desc='Right held from tick 5; button %s pressed 1 tick at tick 20, 120 ticks' % n, n=120, pad=[(5, RIGHT), (20, RIGHT | b), (21, RIGHT)], **base), 'w3')
        out['%s-swim' % n] = r
        say('  %s from swim : anim %s | F(x) 18/20/21/22/30/60 %s | pos x %.2f' % (n, tls(r), [H(r, t, 'force_x') for t in (18, 20, 21, 22, 30, 60)], H(r, 120, 'pos_x') / 65536.0))
    say('(b) Triangle HELD from tick 10 to the end (single edge), swim idle: does the dive re-trigger when it ends?')
    r = run(dict(name='W3-T-held', desc='Triangle held from tick 10 to 150, no direction', n=150, pad=[(10, TRI)], **base), 'w3')
    out['T-held'] = r
    say('  anim %s' % tls(r))
    say('(c) taps every 5 ticks from tick 10 (T pressed 1 tick each) for 120 ticks: re-trigger when the dive ends? presses during the dive are ignored?')
    pieces = [(1, 0)]
    for t in range(10, 130, 5):
        pieces += [(t, TRI), (t + 1, 0)]
    r = run(dict(name='W3-T-taps5', desc='Triangle tapped every 5 ticks from 10', n=140, pad=pieces, **base), 'w3')
    out['T-taps5'] = r
    say('  anim %s' % tls(r))
    say('(d) the direction at the dive tick: dive pressed with each direction held (pad = dir | T at tick 10, dir kept 10-14, then 0): dir_target and facing at 11, and the force built during the dive')
    for n, d in DIRS8 + [('none', 0)]:
        r = run(dict(name='W3-dive-dir-%s' % n, desc='direction %s with Triangle pressed at tick 10 (direction pressed at the same tick), then released at 12; 100 ticks' % n, n=100, pad=[(10, d | TRI), (11, d), (12, 0)], **base), 'w3')
        out['dive-dir-' + n] = r
        say('  dir %-4s anim %s | dir_target@10/11/12/40 %s facing@40 %d | F@11/12/20/40 %s | pos(40) (%.2f,%.2f)' % (
            n, tls(r), [H(r, t, 'dir_target') & 0xFF for t in (10, 11, 12, 40)], H(r, 40, 'facing'), [(H(r, t, 'force_x'), H(r, t, 'force_y')) for t in (11, 12, 20, 40)],
            (H(r, 40, 'pos_x') - H(r, 0, 'pos_x')) / 65536.0, (H(r, 40, 'pos_y') - H(r, 0, 'pos_y')) / 65536.0))
    say('(e) direction held through the dive, changed during it (Right at the press, Left from tick 20, Up from 30): TargetDirection is not written during 0x28')
    r = run(dict(name='W3-dive-steer', desc='Right + T at 10, Right to 19, Left 20-29, Up 30-60; 100 ticks', n=100, pad=[(10, RIGHT | TRI), (11, RIGHT), (20, LEFT), (30, UP), (61, 0)], **base), 'w3')
    out['dive-steer'] = r
    say('  anim %s | dir_target %s | facing %s' % (tls(r), tls(r, 'dir_target'), tls(r, 'facing')))
    show(r, 9, 14)
    show(r, 49, 56)
    say('(f) the end of the dive (surfacing): dive at 10 with Right held through; with the pad released after the dive; with the pad released at 30 (mid-dive)')
    r = run(dict(name='W3-dive-end-held', desc='Right held from 5 to the end, T pressed at 10: surfacing at the end of the 42-tick dive', n=90, pad=[(5, RIGHT), (10, RIGHT | TRI), (11, RIGHT)], **base), 'w3')
    out['dive-end-held'] = r
    say('  held     : anim %s' % tls(r))
    show(r, 50, 55)
    r = run(dict(name='W3-dive-end-release', desc='Right held 5-30 (released mid-dive), T pressed at 10: surfacing at the end of the dive with nothing held', n=100, pad=[(5, RIGHT), (10, RIGHT | TRI), (11, RIGHT), (31, 0)], **base), 'w3')
    out['dive-end-release'] = r
    say('  released : anim %s | F(x) at 30/31/40/51/52/53/60 %s' % (tls(r), [H(r, t, 'force_x') for t in (30, 31, 40, 51, 52, 53, 60)]))
    show(r, 50, 55)
    say('(g) two buttons: T and X pressed the same tick (one edge word) / X pressed during the dive / S pressed on the last dive tick / T pressed on the tick after the dive ends')
    r = run(dict(name='W3-TX-same-tick', desc='T + X pressed together at 10', n=80, pad=[(10, TRI | CRO), (11, 0)], **base), 'w3')
    out['TX'] = r
    say('  T+X same tick : %s' % tls(r))
    r = run(dict(name='W3-X-during-dive', desc='T at 10, X at 25 (during the dive)', n=100, pad=[(10, TRI), (11, 0), (25, CRO), (26, 0)], **base), 'w3')
    out['X-during'] = r
    say('  X during dive : %s' % tls(r))
    t_end = None
    rr = out['T-idle']
    t_end = first(rr, 'anim_cur', 0x1D, 12)
    say('  (dive from idle ends: first tick back to 0x1D = %s)' % t_end)
    for dt in (-1, 0, 1, 2):
        r = run(dict(name='W3-T-near-end%+d' % dt, desc='T at 10; T pressed again at tick %d (the dive ends at tick %d)' % (t_end + dt, t_end), n=120, pad=[(10, TRI), (11, 0), (t_end + dt, TRI), (t_end + dt + 1, 0)], **base), 'w3')
        out['near-end%+d' % dt] = r
        say('  second T at end%+d (tick %d): %s' % (dt, t_end + dt, tls(r)))
    say('(h) the first ticks after entering the water (walk east from (320,100), lake at x 336, boots level 2): a dive pressed around the first tick with slope 4')
    r0 = run(dict(name='W3-enter-ref', desc='reference: walk east from (320,100), no dive', hero_px=(320, 100), n=40, pad=[(1, RIGHT)], layout=lake(), inv='b1b'), 'w3')
    ts = first(r0, 'slope', 4)
    say('  reference: slope first 4 at the end of tick %s; anim %s' % (ts, tls(r0)))
    out['enter-ref'] = r0
    for dt in range(-1, 5):
        r = run(dict(name='W3-enter-T%+d' % dt, desc='walk east from (320,100); T pressed 1 tick at tick %d (slope-4 tick %d %+d)' % (ts + dt, ts, dt), hero_px=(320, 100), n=60, pad=tap(RIGHT, TRI, ts + dt), layout=lake(), inv='b1b'), 'w3')
        out['enter-T%+d' % dt] = r
        say('  T at ts%+d (tick %d): anim %s' % (dt, ts + dt, tls(r, 'anim_cur', 0, 60)))
    say()
    return out


# ---- W4 ----------------------------------------------------------------------------------------------------------------------------------------------
def w4():
    say('== W4 leaving the water (lake from col 14 = x 336; the land is col < 14).  Boots level 2.  The hero box leaves slope 4 when the west edge (pos-10) passes x 336 ==')
    out = {}
    L = lake()
    out['leave'] = run(dict(name='W4-leave-L', desc='start (500,100) in the lake (0x1D), Left from tick 5, 140 ticks', hero_px=(500, 100), n=140, pad=[(5, LEFT)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['leave']
    ts = first(r, 'slope', 4, 20)
    t_land = None
    for x in r['rows']:
        if x['tick'] > 20 and x['hero']['slope'] != 4:
            t_land = x['tick']
            break
    say('swim out (Left): anim %s | slope %s | first non-4 slope tick %s' % (tls(r), tls(r, 'slope'), t_land))
    show(r, t_land - 2, t_land + 8)
    out['leave-release'] = run(dict(name='W4-leave-L-release', desc='as above, pad released the tick the slope drops (momentum on land)', hero_px=(500, 100), n=140, pad=[(5, LEFT), (t_land, 0)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['leave-release']
    say('release at the shore: anim %s | F(x) %s' % (tls(r, 'anim_cur', t_land - 2, t_land + 30), [H(r, t, 'force_x') for t in range(t_land - 1, t_land + 8)]))
    out['leave-T'] = run(dict(name='W4-leave-L-T', desc='Triangle + Left held from tick 5 (Triangle at tick 5 is a dive edge), the dive ends, the hero swims on with Triangle held, leaves the water', hero_px=(560, 100), n=200,
                              pad=[(5, LEFT | TRI)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['leave-T']
    say('Triangle held when leaving: anim %s | slope %s' % (tls(r), tls(r, 'slope')))
    out['leave-T2'] = run(dict(name='W4-leave-L-T-late', desc='Left from tick 5; Triangle pressed at tick 30 and held (a dive, then Triangle held until the shore)', hero_px=(560, 100), n=200,
                               pad=[(5, LEFT), (30, LEFT | TRI)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['leave-T2']
    say('Triangle pressed at 30, held: anim %s | slope %s' % (tls(r), tls(r, 'slope')))
    # dive into the shore: the dive must be in progress when the slope drops
    base_x = 400
    for s in (5, 15, 25):
        r = run(dict(name='W4-leave-dive-s%02d' % s, desc='Left held from 5, dive (T) pressed at tick %d, from (%d,100)' % (s, base_x), hero_px=(base_x, 100), n=120, pad=[(5, LEFT), (s, LEFT | TRI), (s + 1, LEFT)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
        out['leave-dive%d' % s] = r
        say('  dive at %d: anim %s | slope %s' % (s, tls(r), tls(r, 'slope')))
    out['leave-diag'] = run(dict(name='W4-leave-UL', desc='Up+Left from (400,300)', hero_px=(400, 300), n=140, pad=[(5, UP | LEFT)], layout=L, inv='b1b', hero_anim=0x1D), 'w4')
    say('leave diagonal (Up+Left): anim %s | slope %s' % (tls(out['leave-diag']), tls(out['leave-diag'], 'slope')))
    # bank one unit higher: the land west of x 336 is 16 px higher
    bank = R.Layout().rect(0, 0, 13, 59, hb=1).rect(14, 0, 51, 59, word=0x800)
    out['bank16'] = run(dict(name='W4-bank16', desc='land 16 px higher than the water (cols 0-13); swim Left from (500,100), 140 ticks', hero_px=(500, 100), n=140, pad=[(5, LEFT)], layout=bank, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['bank16']
    say('bank 16 px: anim %s | x@140 %.2f | ForceAdjusted first %s | slope %s' % (tls(r), H(r, 140, 'pos_x') / 65536.0, first(r, 'force_adj', 1), tls(r, 'slope')))
    bank8 = R.Layout().rect(0, 0, 13, 59, hb=2).rect(14, 0, 51, 59, word=0x800)
    out['bank32'] = run(dict(name='W4-bank32', desc='land 32 px higher; swim Left, also Cross pressed (dive) at tick 60', hero_px=(500, 100), n=140, pad=[(5, LEFT), (60, LEFT | CRO), (61, LEFT)], layout=bank8, inv='b1b', hero_anim=0x1D), 'w4')
    r = out['bank32']
    say('bank 32 px: anim %s | x@140 %.2f | slope %s' % (tls(r), H(r, 140, 'pos_x') / 65536.0, tls(r, 'slope')))
    say()
    return out


# ---- W5 ----------------------------------------------------------------------------------------------------------------------------------------------
def w5():
    say('== W5 walls and edges in the water (all water, boots level 2, wall word 0x0040 at cols 30-31 (x 720..767) overlaid on the water) ==')
    out = {}
    wall = allwater().rect(30, 0, 31, 59, word=0x40)
    out['wall-R'] = run(dict(name='W5-wall-R', desc='swim east into a wall from (600,480), Right from 5, 140 ticks', hero_px=(600, 480), n=140, pad=[(5, RIGHT)], layout=wall, inv='b1b', hero_anim=0x1D), 'w5')
    r = out['wall-R']
    say('wall: anim %s | ForceAdjusted first %s | x final %.2f | F(x) at end %d' % (tls(r), first(r, 'force_adj', 1), H(r, 120, 'pos_x') / 65536.0, H(r, 120, 'force_x')))
    out['wall-UR'] = run(dict(name='W5-wall-UR', desc='swim Up+Right into the wall (slide?) from (500,480)', hero_px=(600, 480), n=200, pad=[(5, UP | RIGHT)], layout=wall, inv='b1b', hero_anim=0x1D), 'w5')
    r = out['wall-UR']
    say('wall diagonal: anim %s | pos at 60/100/140 %s | ForceAdjusted first %s' % (tls(r), [(round(H(r, t, 'pos_x') / 65536.0, 2), round(H(r, t, 'pos_y') / 65536.0, 2)) for t in (60, 100, 140, 200)], first(r, 'force_adj', 1)))
    out['wall-dive'] = run(dict(name='W5-wall-dive', desc='Right from 5, dive (T) at tick 20 into the wall (the dive is 42 ticks long)', hero_px=(560, 480), n=120, pad=[(5, RIGHT), (20, RIGHT | TRI), (21, RIGHT)], layout=wall, inv='b1b', hero_anim=0x1D), 'w5')
    r = out['wall-dive']
    say('wall dive: anim %s | ForceAdjusted first %s | x@120 %.2f' % (tls(r), first(r, 'force_adj', 1), H(r, 120, 'pos_x') / 65536.0))
    out['edge-E'] = run(dict(name='W5-edge-E', desc='swim east to the map edge from (1150,480)', hero_px=(1150, 480), n=100, pad=[(5, RIGHT)], layout=allwater(), inv='b1b', hero_anim=0x1D), 'w5')
    r = out['edge-E']
    say('east map edge: anim %s | x final %.2f | ForceAdjusted first %s' % (tls(r), H(r, 100, 'pos_x') / 65536.0, first(r, 'force_adj', 1)))
    out['edge-N'] = run(dict(name='W5-edge-N', desc='swim north to the map edge from (624,60)', hero_px=(624, 60), n=100, pad=[(5, UP)], layout=allwater(), inv='b1b', hero_anim=0x1D), 'w5')
    r = out['edge-N']
    say('north map edge: anim %s | y final %.2f | ForceAdjusted first %s' % (tls(r), H(r, 100, 'pos_y') / 65536.0, first(r, 'force_adj', 1)))
    # level 0 and 1 walls (information): walking into a lake from the shore
    for lv in (0, 1):
        r = run(dict(name='W5-L%d-wall-walk' % lv, desc='information: boots level %d, walk east into the lake from (300,100): the water is a wall' % lv, hero_px=(300, 100), n=100, pad=[(1, RIGHT)], layout=lake(), inv=LV[lv]), 'w5')
        out['L%d-walk' % lv] = r
        say('level %d walk into the lake: anim %s | x final %.2f | ForceAdjusted first %s | slope %s' % (lv, tls(r), H(r, 100, 'pos_x') / 65536.0, first(r, 'force_adj', 1), tls(r, 'slope')))
    # diagonal approach at level 0: slide along the water edge?
    r = run(dict(name='W5-L0-wall-diag', desc='information: boots level 0, walk Down+Right toward the lake edge from (250,60)', hero_px=(250, 60), n=140, pad=[(1, DOWN | RIGHT)], layout=lake(), inv='kit'), 'w5')
    out['L0-diag'] = r
    say('level 0 diagonal into the lake edge: anim %s | pos at 60/100/140 %s' % (tls(r), [(round(H(r, t, 'pos_x') / 65536.0, 2), round(H(r, t, 'pos_y') / 65536.0, 2)) for t in (60, 100, 140)]))
    # a one-cell pond: swim to its far edge and out
    say()
    return out


# ---- W6 ----------------------------------------------------------------------------------------------------------------------------------------------
def w6():
    say('== W6 weapon state in the water: charge counter [0x80126FFC], pending-shot pointer [0x80127110], aura pointer [0x80126FF8] (inventory "holy": sacred sword, charges) ==')
    out = {}
    L = lake()
    # walk into the lake with Square held from tick 1
    out['walk-sq'] = run(dict(name='W6-walk-square-held', desc='holy sword; Right + Square held from tick 1, from (200,100) toward the lake (reaches the water near tick 50), 160 ticks', hero_px=(200, 100), n=160,
                              pad=[(1, RIGHT | SQU)], layout=L, inv='holy_b1b'), 'w6')
    r = out['walk-sq']
    ts = first(r, 'slope', 4)
    say('walk into the water with Square held: slope-4 first tick %s; charge counter at ts-2..ts+6 %s; aura at ts-1/ts/ts+1 %s' % (ts, [H(r, t, 'charge_cnt') for t in range(ts - 2, ts + 7)], [hex(H(r, t, 'aura')) for t in (ts - 1, ts, ts + 1)]))
    say('  charge counter every 10 ticks %s' % [H(r, t, 'charge_cnt') for t in range(0, 161, 10)])
    say('  anim %s' % tls(r))
    # all-water start, Square held: the counter stays 0; stays 0 on release
    out['aw-sq'] = run(dict(name='W6-allwater-square-held', desc='holy sword; all water; Square held from tick 5 to 100 (a dive edge at 5), 130 ticks', hero_px=CENTER, n=130, pad=[(5, SQU), (101, 0)], layout=allwater(), inv='holy_b1b', hero_anim=0x1D), 'w6')
    r = out['aw-sq']
    say('all-water Square held: anim %s | charge counter every 10 ticks %s' % (tls(r), [H(r, t, 'charge_cnt') for t in range(0, 131, 10)]))
    out['leave-sq'] = run(dict(name='W6-leave-water-square-held', desc='holy sword + boots; start (500,100) in the lake (0x1D), Left held from 5, Square pressed at 5 and held to the end (a dive edge, then Square held): swim out and onto land, 220 ticks', hero_px=(500, 100), n=220,
                               pad=[(5, LEFT | SQU)], layout=L, inv='holy_b1b', hero_anim=0x1D), 'w6')
    r = out['leave-sq']
    tl4 = None
    for x in r['rows']:
        if x['tick'] > 60 and x['hero']['slope'] != 4:
            tl4 = x['tick']
            break
    say('swim out with Square held: first land tick %s; anim %s; charge counter every 10 ticks from 100 %s; sounds %s' % (tl4, tls(r), [H(r, t, 'charge_cnt') for t in range(100, 221, 10)], sounds(r)))
    # counter pre-loaded on land by a poke, then swim: zeroed on the first slope-4 tick?
    out['poke'] = run(dict(name='W6-poke-state', desc='all water, holy sword, no pad; before tick 3 the harness writes charge counter 45, pending-shot pointer 0x80127000, aura pointer 0x80127D30 (a pointer to the hero itself)', hero_px=CENTER, n=12,
                           pad=[], layout=allwater(), inv='holy_b1b', hero_anim=0x1D,
                           events={3: lambda m, slots: (m.poke(R.CHARGE_CNT, 45), m.poke(R.WPTR, 0x80127000), m.poke(R.AURA, 0x80127D30))}), 'w6')
    r = out['poke']
    say('poked state in the water: charge %s | wptr %s | aura %s (ticks 1..12)' % ([H(r, t, 'charge_cnt') for t in range(1, 13)], [hex(H(r, t, 'wptr')) for t in range(1, 13)], [hex(H(r, t, 'aura')) for t in range(1, 13)]))
    # the same poke on land for contrast
    out['poke-land'] = run(dict(name='W6-poke-state-land', desc='contrast: flat dry ground, holy sword, no pad; same pokes before tick 3', hero_px=CENTER, n=12, pad=[], inv='holy_b1b',
                                events={3: lambda m, slots: (m.poke(R.CHARGE_CNT, 45), m.poke(R.WPTR, 0x80127000), m.poke(R.AURA, 0x80127D30))}), 'w6')
    r = out['poke-land']
    say('poked state on land: charge %s | wptr %s | aura %s' % ([H(r, t, 'charge_cnt') for t in range(1, 13)], [hex(H(r, t, 'wptr')) for t in range(1, 13)], [hex(H(r, t, 'aura')) for t in range(1, 13)]))
    # charge on land to >= 60 then walk into the water: the counter and the release
    out['charge-then-water'] = run(dict(name='W6-charge-then-water', desc='holy sword; Square held from tick 1 standing at (300,100) (charge reaches 60 near tick 61), Right added at tick 70 (the lake is 36 px away); Square released at 120', hero_px=(300, 100), n=170,
                                        pad=[(1, SQU), (70, SQU | RIGHT), (120, RIGHT)], layout=L, inv='holy_b1b'), 'w6')
    r = out['charge-then-water']
    ts = first(r, 'slope', 4)
    say('charged (60) then into the water: ts %s; charge %s; anim %s' % (ts, [H(r, t, 'charge_cnt') for t in (60, 69, 70, ts - 1, ts, ts + 1, ts + 2, 120, 121, 170)], tls(r)))
    sc = [(x['tick'], v) for x in r['rows'] for k, v in x['journal'] if k == 'sound']
    say('  sounds %s' % sc)
    say()
    return out


# ---- W7 ----------------------------------------------------------------------------------------------------------------------------------------------
def w7():
    say('== W7 hurt in the water (information for E20.c2b; the entry 0x80031A68 is the real code).  The Beannoid of map 14 (inert) is at (252,96) = target spec (20,11,0) ==')
    out = {}
    tg = [(20, 11, 0)]
    W = allwater()
    shore = lake()
    out['idle'] = run(dict(name='W7-hurt-idle-L2', desc='all water, boots level 2, hero (232,96) at rest 20 px west of the Beannoid, 80 ticks', hero_px=(232, 96), n=80, pad=[], layout=W, inv='b1b', hero_anim=0x1D, targets=tg), 'w7')
    out['idle3'] = run(dict(name='W7-hurt-idle-L3', desc='as above, boots level 3', hero_px=(232, 96), n=60, pad=[], layout=W, inv='b1c', hero_anim=0x1D, targets=tg), 'w7')
    for k, r in (('idle', out['idle']), ('idle3', out['idle3'])):
        t = first(r, 'anim_cur', 0x3A)
        say('hurt at rest %-5s: anim %s | hurt tick %s | dir %s | F(x) from hurt %s | pos x %s -> %.2f | hp %s' % (k, tls(r), t, tls(r, 'dir_target'), [H(r, x, 'force_x') for x in range(t, t + 6)], H(r, 0, 'pos_x') / 65536.0, H(r, 80 if k == 'idle' else 60, 'pos_x') / 65536.0, tls(r, 'hp')))
    show(out['idle'], 1, 4)
    # swim toward the Beannoid: Right from tick 3 starting at (150,96)
    r = run(dict(name='W7-hurt-swim', desc='all water; swim Right from (150,96) toward the Beannoid, 100 ticks', hero_px=(150, 96), n=100, pad=[(3, RIGHT)], layout=W, inv='b1b', hero_anim=0x1D, targets=tg), 'w7')
    out['swim'] = r
    t = first(r, 'anim_cur', 0x3A)
    say('hurt while swimming Right: anim %s | hurt tick %s' % (tls(r), t))
    show(r, t - 2, t + 6)
    # the pad keeps being held (Right) through the hurt
    say('  pad Right still held through 0x3A/0x3B: F(x) from the hurt %s; dir_target %s' % ([H(r, x, 'force_x') for x in range(t, t + 20, 2)], tls(r, 'dir_target')))
    # dive timed to be active at contact: sweep the dive press tick
    best = []
    for s in (35, 45, 55, 60, 65, 70, 75):
        r = run(dict(name='W7-hurt-dive-s%02d' % s, desc='Right from 3, dive (T) pressed at %d, Beannoid ahead' % s, hero_px=(100, 96), n=110, pad=[(3, RIGHT), (s, RIGHT | TRI), (s + 1, RIGHT)], layout=W, inv='b1b', hero_anim=0x1D, targets=tg), 'w7')
        out['dive%d' % s] = r
        t = first(r, 'anim_cur', 0x3A)
        td = first(r, 'anim_cur', 0x28)
        prev = H(r, t - 1, 'anim_cur') if t else None
        best.append((s, td, t, prev))
        say('  dive at %-2d: dive starts %s, hurt tick %s, state just before the hurt %s | anim %s' % (s, td, t, ('%02X' % (prev & 0xFF)) if prev is not None else '-', tls(r)))
    # hurt at the shore: the hero half in the water (slope 0): 0x31 expected from the table
    r = run(dict(name='W7-hurt-shore', desc='lake from x 336; hero at (335,96) (box 325..345: half in the water), Beannoid 20 px east inside the water at (355,96)', hero_px=(335, 96), n=60, pad=[], layout=shore, inv='b1b', targets=[(28, 11, 0)]), 'w7')
    out['shore'] = r
    say('hurt at the shore (slope %s): anim %s | slope %s' % (tls(r, 'slope'), tls(r), tls(r, 'slope')))
    # the knockback carries the hero out of the water during 0x3A / 0x3B (the swim hurt states run on land: 0x80031DB4)
    for hx in (352, 346):
        r = run(dict(name='W7-hurt-leaving-x%d' % hx, desc='lake from x 336; hero at (%d,96) in the water (box %d..%d), Beannoid 20 px east at (%d,96); the knockback (west, 3 px/tick) crosses the shore during 0x3A' % (hx, hx - 10, hx + 10, hx + 20),
                     hero_px=(hx, 96), n=60, pad=[], layout=shore, inv='b1b', hero_anim=0x1D, targets=[((hx + 20 - 12) // 12, 11, 0)]), 'w7')
        out['leaving%d' % hx] = r
        t = first(r, 'anim_cur', 0x3A)
        say('hurt then knocked out of the water (start x %d): anim %s | slope %s | dir_target %s | F(x) from the hurt %s | x end %.2f' % (hx, tls(r), tls(r, 'slope'), tls(r, 'dir_target'), [H(r, x, 'force_x') for x in range(t, t + 8)] if t else None, H(r, 60, 'pos_x') / 65536.0))
        if t:
            show(r, t - 1, t + 8)
    # second contact: the invulnerability is 120 ticks; keep the contact for 300 ticks by teleporting back
    ev = {t: (lambda m, slots, _t=t: m.poke(0x80127D30 + 0x114, 232 << 16)) for t in range(3, 300)}
    r = run(dict(name='W7-hurt-keep', desc='all water; the contact is kept (PosX := 232 px before every tick from 3), 300 ticks: the second and third hurt', hero_px=(232, 96), n=300, pad=[], layout=W, inv='b1b', hero_anim=0x1D, targets=tg, events=ev), 'w7')
    out['keep'] = r
    hurts = [x['tick'] for x in r['rows'] if x['hero']['anim_target'] == 0x3A and r['rows'][x['tick'] - 2]['hero']['anim_target'] != 0x3A] if True else []
    say('contact kept: hp %s | anim %s' % (tls(r, 'hp'), tls(r)))
    # buttons pressed during 0x3A / 0x3B (dive edge, Circle)
    for n, b in (('T', TRI), ('X', CRO)):
        r = run(dict(name='W7-hurt-press-%s' % n, desc='all water, resting hero hurt at tick 2; button %s pressed 1 tick at tick 5 (in 0x3A) and at tick 9 (in 0x3B) and 20 (0x1D)' % n, hero_px=(232, 96), n=60,
                     pad=[(5, b), (6, 0), (9, b), (10, 0), (20, b), (21, 0)], layout=W, inv='b1b', hero_anim=0x1D, targets=tg), 'w7')
        out['press-' + n] = r
        say('  %s pressed during the hurt: anim %s' % (n, tls(r)))
    say()
    return out


# ---- W8 ----------------------------------------------------------------------------------------------------------------------------------------------
def w8():
    say('== W8 the slope-4 state table: hero in the water starts at TargetAnimationId s (setup), 3 ticks; pad idle, Right held, dive edge (T pressed at tick 1) ==')
    out = {}
    W = allwater()
    rows = []
    for s in range(0x50):
        cols = []
        for lab, pad in (('idle', []), ('R', [(1, RIGHT)]), ('T', [(1, TRI)])):
            r = run(dict(name='W8-s%02X-%s' % (s, lab), desc='start target animation 0x%02X, pad %s, all water, boots level 2, 3 ticks' % (s, lab), hero_px=CENTER, n=3, pad=pad, layout=W, inv='b1b', hero_anim=s), 'w8')
            cols.append((lab, [H(r, t, 'anim_target') & 0xFF for t in (0, 1, 2, 3)], [H(r, t, 'dir_target') & 0xFF for t in (0, 1, 2, 3)], H(r, 0, 'slope')))
            out[(s, lab)] = r
        rows.append((s, cols))
    say('start state -> anim_target at ticks 0,1,2,3 (idle | Right | T edge at tick 1); dir_target in brackets when it changed')
    for s, cols in rows:
        parts = []
        for lab, a, d, sl in cols:
            parts.append('%s: %s%s' % (lab, '>'.join('%02X' % x for x in a), '' if len(set(d)) == 1 else ' d' + '/'.join('%02X' % x for x in d)))
        say('  s=%02X (slope0=%d)  %s' % (s, cols[0][3], ' | '.join(parts)))
    say()
    return out


# ---- W9 ----------------------------------------------------------------------------------------------------------------------------------------------
def w9():
    say('== W9 Circle in the water, and the land-side handling of the swim states ==')
    out = {}
    base = dict(hero_px=CENTER, layout=allwater(), inv='b1b', hero_anim=0x1D)
    r = run(dict(name='W9-circle-idle', desc='Circle pressed at tick 10 from swim idle (kit: no item)', n=60, pad=[(10, CIR), (11, 0)], **base), 'w9')
    out['circle-idle'] = r
    say('Circle with the kit: anim %s | sounds %s' % (tls(r), sounds(r)))
    r = run(dict(name='W9-circle-swim', desc='Circle pressed at tick 20 while swimming Right', n=60, pad=[(5, RIGHT), (20, RIGHT | CIR), (21, RIGHT)], **base), 'w9')
    out['circle-swim'] = r
    say('Circle while swimming: anim %s | sounds %s' % (tls(r), sounds(r)))
    # the Circle items in the water (information for E20.h): every item 0x1F..0x32 equipped, Circle pressed at tick 10, 60 ticks
    say('Circle with each equipped item 0x1F..0x32 (TryUseItem -> 0x8003499C jump table 0x8002339C), pressed at tick 10 in swim idle: first tick the animation or a spawn/effect changes, journal')
    for it in range(0x1F, 0x33):
        r = run(dict(name='W9-item%02X' % it, desc='item 0x%02X equipped, Circle pressed at tick 10 in swim idle, 60 ticks' % it, n=60, pad=[(10, CIR), (11, 0)], hero_px=CENTER, layout=allwater(), inv='item%02X' % it, hero_anim=0x1D), 'w9')
        out['item%02X' % it] = r
        jr = [(x['tick'], k, (v[0] if k == 'sound' else v)) for x in r['rows'] for k, v in x['journal'] if k in ('sound', 'effect', 'effect_on', 'spawn')]
        say('  item %02X: anim %s | dir %s | journal %s' % (it, tls(r), tls(r, 'dir_target'), jr[:6]))
    # land-side: a swim state started on dry land (information, as a hero carried out of the water)
    for s in (0x0F, 0x1D, 0x28, 0x3A, 0x3B):
        r = run(dict(name='W9-land-s%02X' % s, desc='dry ground, target animation 0x%02X at the start, no pad, 6 ticks' % s, hero_px=CENTER, n=6, pad=[], hero_anim=s, inv='b1b'), 'w9')
        out['land%02X' % s] = r
        say('  land, start %02X: anim_target %s dir_target %s' % (s, [H(r, t, 'anim_target') & 0xFF for t in range(0, 7)], [H(r, t, 'dir_target') & 0xFF for t in range(0, 7)]))
    say()
    return out


# ---- W10 ---------------------------------------------------------------------------------------------------------------------------------------------
def w10():
    say('== W10 extras: invalid direction nibbles in the water, the first tick after a land state, the hero on a one-cell-wide channel, shallow water next to deep water ==')
    out = {}
    base = dict(hero_px=CENTER, layout=allwater(), inv='b1b', hero_anim=0x1D)
    r = run(dict(name='W10-LR-UD', desc='Right 5-40, Left+Right 41-80 (nibble 0xA: no 8-way direction), Up+Down 81-120 (0x5), released 121-140', n=140,
                 pad=[(5, RIGHT), (41, LEFT | RIGHT), (81, UP | DOWN), (121, 0)], **base), 'w10')
    out['LR-UD'] = r
    say('invalid nibbles: anim %s | dir_target %s | F(x) at 40/41/60/80 %s | F(y) at 81/100/120 %s' % (tls(r), tls(r, 'dir_target'), [H(r, t, 'force_x') for t in (40, 41, 60, 80)], [H(r, t, 'force_y') for t in (81, 100, 120)]))
    r = run(dict(name='W10-dive-LR', desc='Left+Right held with T pressed at tick 10 from a hero that faces Right: the dive takes the kept direction', n=80, pad=[(10, LEFT | RIGHT | TRI), (11, LEFT | RIGHT), (12, 0)], **base), 'w10')
    out['dive-LR'] = r
    say('dive with an invalid nibble: anim %s | dir_target %s | F(x) at 10/20 %s' % (tls(r), tls(r, 'dir_target'), [H(r, t, 'force_x') for t in (10, 20)]))
    # a channel one cell wide (24 px) of deep water, the hero box is 20 px: all four corners are in the channel only if the box sits inside one column
    chan = R.Layout().rect(20, 0, 20, 59, word=0x800)
    r = run(dict(name='W10-channel-down', desc='a 24 px wide channel of deep water at col 20 (x 480..503), hero at (492,100) swimming Down: does slope stay 4 (box 482..502)?', hero_px=(492, 100), n=80, pad=[(5, DOWN)], layout=chan, inv='b1b', hero_anim=0x1D), 'w10')
    out['channel'] = r
    say('channel: anim %s | slope %s | pos y end %.2f' % (tls(r), tls(r, 'slope'), H(r, 80, 'pos_y') / 65536.0))
    r = run(dict(name='W10-channel-edge', desc='the same channel, hero at (480,100): box 470..490 straddles two columns: slope 0 (land logic) at the start', hero_px=(480, 100), n=40, pad=[(5, DOWN)], layout=chan, inv='b1b'), 'w10')
    out['channel-edge'] = r
    say('channel straddle: anim %s | slope %s' % (tls(r), tls(r, 'slope')))
    # shallow water (wk 0x08) next to deep water
    sh = R.Layout().rect(0, 0, 51, 59, wk=0x08).rect(14, 0, 51, 59, word=0x800)
    r = run(dict(name='W10-shallow-to-deep', desc='shallow water (0x08) on cols 0-13, deep water from col 14; boots level 2; walk east from (300,100), 100 ticks', hero_px=(300, 100), n=100, pad=[(1, RIGHT)], layout=sh, inv='b1b'), 'w10')
    out['shallow-deep'] = r
    say('shallow -> deep: anim %s | slope %s | F(x) %s' % (tls(r), tls(r, 'slope'), [H(r, t, 'force_x') for t in (10, 18, 20, 22, 30, 60)]))
    r = run(dict(name='W10-shallow-to-deep-L0', desc='as above with boots level 0 (information): the deep water is a wall, the shallow water halves the speed', hero_px=(300, 100), n=100, pad=[(1, RIGHT)], layout=sh, inv='kit'), 'w10')
    out['shallow-deep-L0'] = r
    say('shallow -> deep, level 0: anim %s | x end %.2f | ForceAdjusted first %s' % (tls(r), H(r, 100, 'pos_x') / 65536.0, first(r, 'force_adj', 1)))
    say()
    return out


GROUPS = dict(W1=w1, W2=w2, W3=w3, W4=w4, W5=w5, W6=w6, W7=w7, W8=w8, W9=w9, W10=w10)


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
            f.write('group %s: cross-check %s; runs compared %d, ticks compared %d, mismatches %d %s, uninitialised-read sites over both engines %d\n' % (
                g, 'ON' if CROSS else 'off', RS.XC['runs'], RS.XC['ticks'], len(RS.XC['mismatches']), RS.XC['mismatches'][:5], RS.XC['uninit_sites']))
        for k in ('runs', 'ticks', 'uninit_sites', 'dash_runs'):
            RS.XC[k] = 0
        RS.XC['mismatches'] = []


if __name__ == '__main__':
    main(sys.argv[1:])
