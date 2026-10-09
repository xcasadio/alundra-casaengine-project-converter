"""run_attacks.py - E20.b: the attack scenarios AT1-AT10 (+ variants) on the D1 MovePlayer oracle.

usage: python -B run_attacks.py [outdir]        (default ../out)

Writes, per scenario, outdir/<name>.trace.txt, .trace.json, .machine.txt, and outdir/summary.txt, weapons.txt, at6-sweep.txt, at7-cellmap.txt.
Every value written is computed by the emulated binary.  The inputs (pad words), the field and the weapon of each scenario are the only things chosen here;
the search of AT8 picks a start column by an OBSERVED condition (the hero leaves the platform on tick 1), it does not embed an output.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import attack as A
import oracle
import world as W
import interp2

DIFF2 = True
DIFF = []             # (scenario, ticks, verdict) of the two-interpreter comparison

SQ, CR, RT = A.SQUARE, A.CROSS, A.RIGHT

# (item id, label, used by) - labels only; the slot and the weapon record are read from the binary by the oracle
WEAPONS = [(2, 'Steel Edge'), (3, 'Demon Buster'), (4, 'Holy Sword Elnard'), (9, 'Chain Throw'), (10, 'Crusher'), (5, "Hunter's Bow"), (6, "William's Bow"),
           (16, 'Fireball Rod'), (14, 'Drifting Ice Rod'), (7, 'Magic staff'), (8, 'Item 008 (extra)')]


# ---- reading a run ------------------------------------------------------------------------------------------------------------------------------
def anim_changes(res, key):
    rows = res['rows']
    out = []
    prev = res['pre'][key]
    for r in rows:
        if r[key] != prev:
            out.append((r['tick'], prev, r[key]))
        prev = r[key]
    return out


def ptr_writes(res):
    out = []
    prev = res['pre']['rec_ptr']
    pc = res['pre']['rec_cnt']
    for r in res['rows']:
        if r['rec_ptr'] != prev or (r['rec_cnt'] != pc and r['rec_ptr'] != 0):
            out.append((r['tick'], r['rec_ptr'], r['rec_cnt']))
        prev, pc = r['rec_ptr'], r['rec_cnt']
    return out


def events_of(res):
    return [(r['tick'], e) for r in res['rows'] for e in r['events']]


def force_span(res, key):
    ts = [r['tick'] for r in res['rows'] if r[key] != 0]
    return (ts[0], ts[-1]) if ts else None


def h(v):
    return '0x%02X' % (v & 0xFFFFFFFF) if v is not None else 'None'


def describe(res):
    """key ticks of one run as text lines (all computed from the rows)"""
    rows = res['rows']
    pre = res['pre']
    L = []
    L.append('weapon: slot %s, item now %s, weapon record index %s (kit %s, item props %s)' % (
        res['meta']['weapon_slot'], res['meta']['weapon_item_now'], res['meta']['weapon_rec_index'], res['meta']['kit'], res['meta'].get('item_props')))
    L.append('start: anim_cur %s anim_target %s dir_target %d on_ground %d pos_z %d (pos_z_dll %d) terrain %d force_x %d' % (
        h(pre['anim_cur']), h(pre['anim_target']), pre['dir_target'], pre['on_ground'], pre['pos_z'], pre['pos_z_dll'], pre['terrain'], pre['force_x']))
    L.append('anim_target changes (tick, from, to): ' + ', '.join('t%d %s->%s' % (t, h(a), h(b)) for t, a, b in anim_changes(res, 'anim_target')))
    L.append('anim_cur changes (tick, from, to):    ' + ', '.join('t%d %s->%s' % (t, h(a), h(b)) for t, a, b in anim_changes(res, 'anim_cur')))
    L.append('dir_target changes: ' + (', '.join('t%d %d->%d' % c for c in anim_changes(res, 'dir_target')) or 'none'))
    pw = ptr_writes(res)
    L.append('record pointer 0x80127110 / counter 0x80127114 (tick, pointer, counter) when they change: ' + (', '.join('t%d %s/%d' % (t, h(p), c) for t, p, c in pw) or 'never'))
    for k in ('force_x', 'force_y', 'force_z', 'charge_timer'):
        sp = force_span(res, k)
        L.append('%s non-zero: %s' % (k, ('ticks %d..%d' % sp) if sp else 'never'))
    mx = max(r['charge_timer'] for r in rows)
    L.append('charge timer 0x80126FFC max %d' % mx)
    L.append('final on_ground %d, final pos_z %d, pos_x delta (16.16) %d, pos_y delta %d' % (
        rows[-1]['on_ground'], rows[-1]['pos_z'], rows[-1]['pos_x'] - pre['pos_x'], rows[-1]['pos_y'] - pre['pos_y']))
    ev = events_of(res)
    L.append('journal of sound / effect / spawn calls (tick: call; "xN" = N identical calls on that tick):')
    i = 0
    while i < len(ev):
        j = i
        while j + 1 < len(ev) and ev[j + 1] == ev[i]:
            j += 1
        L.append('   t%d: %s%s' % (ev[i][0], ev[i][1], (' x%d' % (j - i + 1)) if j > i else ''))
        i = j + 1
    return L


def write_trace(res, name, outdir):
    os.makedirs(os.path.dirname(os.path.join(outdir, name)), exist_ok=True)
    rows = res['rows']
    warm = res['warm']
    cols = A.COLS
    pre = res['pre']
    with open(os.path.join(outdir, name + '.trace.txt'), 'w', encoding='utf-8') as f:
        f.write('# %s: %s\n' % (name, res['spec'].get('desc', '')))
        f.write('# per-tick state AFTER the tick, computed by the emulated binary (ALUN_CD.EXE France).  PosZ raw: binary convention (rest = terrain + 1); pos_z_dll = raw - 1.\n')
        f.write('# pos_x / pos_y absolute 16.16 (px << 16).  rec_ptr = 0x80127110, rec_cnt = 0x80127114, charge_timer = 0x80126FFC, weapon_rec = 0x8012710C.  tick 0 = start state\n')
        f.write('\t'.join(cols) + '\n')
        for r in warm + [pre] + rows:
            vals = []
            for c in cols:
                v = r[c]
                if c == 'hold':
                    vals.append('0x%04X' % v)
                elif c == 'rec_ptr':
                    vals.append('0x%08X' % v)
                elif c == 'events':
                    vals.append(' | '.join(v) if v else '-')
                else:
                    vals.append(str(v))
            f.write('\t'.join(vals) + '\n')
    json.dump(dict(spec={k: v for k, v in res['spec'].items() if k in ('name', 'desc', 'holds', 'after', 'n', 'warm', 'item', 'no_weapon', 'start')},
                   meta=res['meta'], start=pre, rows=rows, warm=warm), open(os.path.join(outdir, name + '.trace.json'), 'w'), indent=1, default=str)
    m = res['machine']
    with open(os.path.join(outdir, name + '.machine.txt'), 'w', encoding='utf-8') as f:
        f.write('# functions entered by jal/jalr during the scenario (address: count)\n')
        for a, c in sorted(m.calls.items()):
            f.write('%08x: %d\n' % (a, c))
        f.write('# reads of bytes nobody had written during the ticks (pc, address, size: count), must be empty: %s\n' % ('empty' if not m.uninit_reads else ''))
        for (pc, a, n), c in sorted(m.uninit_reads.items()):
            f.write('pc %08x addr %08x size %d: %d\n' % (pc, a, n, c))
        f.write('# MovePlayer 0x80031B50 reach points (R must be reached, N must not)\n')
        for a, want, what in oracle.MP_POINTS:
            f.write('%08x %s %-8s %s\n' % (a, 'R' if want else 'N', 'REACHED' if a in m.cov else 'not hit', what))
        extra = [(0x8002EAF4, 'PlayerTryAttack entered'), (0x8002EB4C, 'release path of PlayerTryAttack (charge release)'), (0x8002EC8C, 'press path of PlayerTryAttack'),
                 (0x8002ED14, 'success tail: pointer, counter, TargetAnimationId written'), (0x8002ED30, 'animation 0 of the record: sound 3'), (0x8002ED3C, 'return 0'),
                 (0x8002F768, 'UpdateWeaponStepProgression entered')]
        f.write('# attack code points\n')
        for a, what in extra:
            f.write('%08x %-8s %s\n' % (a, 'REACHED' if a in m.cov else 'not hit', what))
        f.write('# journal of stubbed calls (tick, kind, args)\n')
        for j in m.journal:
            f.write(repr(j) + '\n')


# ---- scenario definitions ----------------------------------------------------------------------------------------------------------------------
def holds_from(d):
    return dict(d)


def all_floor(hb):
    """machine hook: set the height byte of every cell (the floor drops or rises under the hero)"""
    def f(m):
        for row in range(60):
            for col in range(52):
                m.poke8(W.TILES + 0x604 + row * W.ROW + col * 8 + 3, hb)
    return f


def sc(name, desc, holds, n, **kw):
    d = dict(name=name, desc=desc, holds=holds, n=n)
    d.update(kw)
    return d


def run_named(spec, outdir, store, **kw):
    res = A.run(spec, capture=DIFF2, **kw)
    store[spec['name']] = res
    write_trace(res, spec['name'], outdir)
    if DIFF2:
        r2 = A.run(spec, ticker=interp2.tick2, capture=True, **kw)             # the same scenario on the second, independent interpreter (mips.py)
        parts = dict(rows=r2['rows'] == res['rows'] and r2['warm'] == res['warm'] and r2['pre'] == res['pre'], hero_slot_every_tick=r2['slots'] == res['slots'],
                     globals_end=r2['globals'] == res['globals'], journal=r2['machine'].journal == res['machine'].journal)
        DIFF.append((spec['name'], len(res['rows']), parts))
    return res


def swing_end(res, swing_anim, start_tick):
    """first tick after start_tick at which the current animation is no longer swing_anim (computed from the rows)"""
    for r in res['rows']:
        if r['tick'] > start_tick and r['anim_cur'] != swing_anim:
            return r['tick'], r['anim_cur']
    return None


def second_swing_tick(res, after_tick, swing_anim):
    for r in res['rows']:
        if r['tick'] > after_tick and r['anim_target'] == swing_anim and r['rec_ptr'] != 0:
            return r['tick']
    return None


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    store = {}
    S = []                                                       # summary lines
    S.append('# E20.b attack oracle - summary computed by run_attacks.py (every number below comes from the emulated binary)')
    S.append('# conventions: tick = logic tick, 1-based; the row of tick t is the state AFTER the passes of tick t; PosZ binary raw (rest = terrain + 1, so 1 on the flat field), DLL = raw - 1')
    S.append('# pad words: Up 0x1000, Right 0x2000, Down 0x4000, Left 0x8000, Triangle 0x10, Circle 0x20, Cross 0x40, Square 0x80')
    S.append('')

    # ---------------- AT1
    at1 = run_named(sc('AT1', 'Idle, Square edge at tick 1, then nothing', {1: SQ}, 60), outdir, store)
    sw_anim = at1['rows'][0]['anim_cur']
    end1 = swing_end(at1, sw_anim, 1)
    c = end1[0]
    S.append('== AT1  Idle, Square edge at tick 1, nothing after (60 ticks)')
    S += describe(at1)
    S.append('=> swing animation %s starts on tick 1 (anim_target and anim_cur both set on the press tick); chains to anim %s on tick %d (the swing is on screen for ticks 1..%d, %d ticks)' % (
        h(sw_anim), h(end1[1]), end1[0], end1[0] - 1, end1[0] - 1))
    S.append('')

    # ---------------- AT2
    at2 = run_named(sc('AT2', 'steady Moving east (warm-up as S-B), Square edge at tick 1 with Right still held', {1: RT | SQ}, 60, after=RT, warm='east'), outdir, store)
    S.append('== AT2  steady Moving east, Square edge at tick 1, Right still held (60 ticks; %d warm-up ticks -%d..-1)' % (len(at2['warm']), len(at2['warm'])))
    S += describe(at2)
    S.append('warm-up last row: anim_cur %s force_x %d pos_x %d' % (h(at2['warm'][-1]['anim_cur']), at2['warm'][-1]['force_x'], at2['warm'][-1]['pos_x']))
    S.append('')

    # ---------------- AT3 (+ variants: later Square so that the swing is still running at the landing)
    sa = oracle.scenario_A()[3]                                   # the D1 S-A rows (24 ticks) as the reference arc
    for nm, tick, desc in (('AT3', 5, 'standing jump (Cross edge tick 1), Square edge at tick 5 (air swing ends before the landing)'),
                           ('AT3b', 14, 'standing jump, Square edge at tick 14 (air swing still running when the hero lands)'),
                           ('AT3c', 19, 'standing jump, Square edge at tick 19 (air swing, landing two ticks later)')):
        r = run_named(sc(nm, desc, {1: CR, tick: SQ}, 60), outdir, store)
        S.append('== %s  %s' % (nm, desc))
        S += describe(r)
        diffs = []
        for t in range(1, 25):
            a, b = sa[t - 1], r['rows'][t - 1]
            d = [k for k in ('force_x', 'force_y', 'force_z', 'pos_x', 'pos_y', 'pos_z', 'on_ground') if a[k] != b[k]]
            if d:
                diffs.append('t%d:%s' % (t, '/'.join(d)))
        S.append('vs the S-A jump arc (D1 oracle, same inputs without the Square): ticks 1..24 differing fields: %s' % (', '.join(diffs) if diffs else 'none (identical forces, positions, IsOnGround)'))
        S.append('')

    # ---------------- AT4
    at4 = run_named(sc('AT4', 'Square and Cross edges on the same tick from Idle', {1: SQ | CR}, 40), outdir, store)
    S.append('== AT4  Square and Cross edges on the same tick (tick 1) from Idle')
    S += describe(at4)
    S.append('')

    # ---------------- AT5
    at5 = run_named(sc('AT5', 'Square held for 30 ticks from tick 1, then released', {t: SQ for t in range(1, 31)}, 60), outdir, store)
    S.append('== AT5  Square held ticks 1..30 (released at tick 31)')
    S += describe(at5)
    S.append('swing starts (anim_target set with a record pointer): %s' % [r['tick'] for r in at5['rows'] if r['rec_ptr'] != 0])
    S.append('')

    # ---------------- AT6
    p_during = [4, 10, 16]
    holds6 = {1: SQ, 4: SQ, 10: SQ, 16: SQ, c + 1: SQ}
    at6 = run_named(sc('AT6', 'Square edge at 1, then edges at 4, 10, 16 (during the swing) and at the tick after the chain back (%d)' % (c + 1), holds6, 80), outdir, store)
    S.append('== AT6  Square edges at ticks 1, %s (during the swing) and %d (the tick after the chain back, AT1 chain tick = %d)' % (p_during, c + 1, c))
    S += describe(at6)
    S.append('swing starts (rec pointer set): %s' % [r['tick'] for r in at6['rows'] if r['rec_ptr'] != 0])
    sweep = ['# AT6 sweep: Square edge at tick 1, then ONE more edge at tick p (Square down on p only).  Columns: p | second swing started | tick the animation target became the swing | anim_cur at p']
    for p in range(c - 4, c + 4):
        r = A.run(sc('AT6s', '', {1: SQ, p: SQ}, c + 40))
        started = [x['tick'] for x in r['rows'] if x['tick'] > 1 and x['rec_ptr'] != 0]
        sweep.append('p=%2d | second swing %-3s | at tick %s | anim_target at p-1/p: %s/%s | anim_cur at p-1/p: %s/%s' % (
            p, 'YES' if started else 'no', started[0] if started else '-', h(r['rows'][p - 2]['anim_target']), h(r['rows'][p - 1]['anim_target']),
            h(r['rows'][p - 2]['anim_cur']), h(r['rows'][p - 1]['anim_cur'])))
    open(os.path.join(outdir, 'at6-sweep.txt'), 'w').write('\n'.join(sweep) + '\n')
    S += sweep
    S.append('')

    # ---------------- AT7
    flag_all = lambda col, row: (0, 0, 0, 0x20)
    at7 = run_named(sc('AT7', 'as AT1 on a field whose every cell has ground-property bit 0x20 (cell word bit 0x2000)', {1: SQ}, 30, cells=flag_all), outdir, store)
    S.append('== AT7  as AT1, every cell has ground property 0x20 (cell word & 0x2000), 30 ticks')
    S += describe(at7)
    S.append('hero +0x180 (CombinedVramFlagsOR) after tick 1: 0x%X; PlayerTryAttack entered: %s; success tail reached: %s' % (
        at7['rows'][0]['vram_or_180'], 0x8002EAF4 in at7['machine'].cov, 0x8002ED14 in at7['machine'].cov))
    S.append('')
    cellmap = ['# AT7 cell map: ONE cell flagged (ground property 0x20) at a time; hero at (100,100) px -> cell column 4, row 6.  hero +0x180 after tick 1 | swing (anim_target set) | cell']
    for row in range(4, 9):
        for col in range(2, 7):
            fl = lambda cc, rr, col=col, row=row: (0, 0, 0, 0x20) if (cc, rr) == (col, row) else (0, 0, 0, 0)
            r = A.run(sc('AT7m', '', {1: SQ}, 3, cells=fl))
            cellmap.append('cell col %d row %d | +0x180 = 0x%X | swing %s' % (col, row, r['rows'][0]['vram_or_180'], 'YES' if r['rows'][0]['rec_ptr'] else 'no (blocked)'))
    open(os.path.join(outdir, 'at7-cellmap.txt'), 'w').write('\n'.join(cellmap) + '\n')
    S += cellmap
    fl_far = lambda cc, rr: (0, 0, 0, 0x20) if (cc, rr) == (20, 20) else (0, 0, 0, 0)
    at7c = run_named(sc('AT7c', 'control: bit 0x2000 only on a far cell (col 20, row 20)', {1: SQ}, 30, cells=fl_far), outdir, store)
    S.append('AT7c control (far cell flagged): swing %s, +0x180 = 0x%X' % ('YES' if at7c['rows'][0]['rec_ptr'] else 'no', at7c['rows'][0]['vram_or_180']))
    S.append('')

    at7d = run_named(sc('AT7d', 'gated field (as AT7) with Square and Cross edges on tick 1: the attack returns 0, the jump test is reached', {1: SQ | CR}, 30, cells=flag_all), outdir, store)
    S.append('== AT7d  gated field (every cell 0x2000), Square + Cross edges on tick 1')
    S += describe(at7d)
    S.append('')

    # ---------------- AT11
    LF, UPB = A.LEFT, A.UP
    at11 = run_named(sc('AT11', 'Idle facing east; Square edge with Left held on tick 1, then Up held through the swing and after (the facing at the press and during the swing)',
                        {1: LF | SQ}, 60, after=UPB), outdir, store)
    S.append('== AT11  Idle facing east (dir 24); tick 1 = Left + Square, then Up held on every tick')
    S += describe(at11)
    S.append('anim_dir (+0x98) / dir_cur (+0x94) per tick 1..4,21..24: ' + ' '.join('t%d:%d/%d' % (r['tick'], r['anim_dir'], r['dir_cur']) for r in at11['rows'] if r['tick'] in (1, 2, 3, 4, 21, 22, 23, 24)))
    S.append('')

    # ---------------- AT12 walking air swing
    at12 = run_named(sc('AT12', 'steady Moving east (warm-up as S-B), tick 1 Right + Cross edge (walking jump), tick 6 Square edge (air swing) with Right still held, Right held through tick 30',
                        {1: RT | CR, 6: RT | SQ}, 30, after=RT, warm='east'), outdir, store)
    S.append('== AT12  steady Moving east, tick 1 Right + Cross edge (walking jump), tick 6 Square edge (air swing), Right held through tick 30 (30 ticks; %d warm-up ticks -%d..-1)' % (
        len(at12['warm']), len(at12['warm'])))
    S += describe(at12)
    sw12 = [r['tick'] for r in at12['rows'] if r['tick'] > 1 and r['rec_ptr'] != 0]
    if sw12:
        a12 = at12['rows'][sw12[0] - 1]['anim_cur']
        e12 = swing_end(at12, a12, sw12[0])
        S.append('=> air swing animation %s starts on tick %d; chains to anim %s on tick %s' % (h(a12), sw12[0], h(e12[1]) if e12 else 'None', e12[0] if e12 else 'None'))
    land12 = [r['tick'] for r in at12['rows'] if r['tick'] > 1 and r['on_ground'] == 1]
    S.append('first tick with IsOnGround 1 after the jump: %s; PosX - start (16.16) per tick 1..25: %s' % (
        land12[0] if land12 else 'none', ' '.join('t%d:%d' % (r['tick'], r['pos_x'] - at12['pre']['pos_x']) for r in at12['rows'] if r['tick'] <= 25)))
    S.append('')

    # ---------------- AT8
    plat = lambda col, row: (1, 0, 0, 0) if col < 5 else (0, 0, 0, 0)       # platform of height byte 1 (terrain 1 << 20 = 16 px) for columns 0..4, floor 0 beyond
    chosen = None
    cand = []
    for sx in range(108, 140):
        r = A.run(sc('AT8s', '', {1: RT | SQ}, 3, after=RT, warm='east', cells=plat, start=(sx, 100)))
        grounded_before = r['pre']['on_ground'] == 1 and r['pre']['terrain'] == 1 << 20
        left_on_t1 = r['rows'][0]['on_ground'] == 0 and r['rows'][0]['rec_ptr'] != 0
        cand.append((sx, grounded_before, left_on_t1, r['pre']['pos_x'] / 65536.0))
        if grounded_before and left_on_t1 and chosen is None:
            chosen = sx
    S.append('== AT8  walking off a ledge during a ground swing.  Field: columns 0..4 (x < 120 px) height byte 1 (terrain 16 px), columns 5.. height 0.')
    S.append('search of the start column (observed condition: hero on the platform after the warm-up, swing started on tick 1, IsOnGround 0 after tick 1): %s' % (
        'start x = %d px' % chosen if chosen is not None else 'NOT FOUND'))
    S.append('search table (start x, grounded on the platform after warm-up, left the ground on tick 1, x after warm-up px): ' + '; '.join(
        '%d,%s,%s,%.2f' % (a, int(b), int(cc), d) for a, b, cc, d in cand if a in range((chosen or 120) - 2, (chosen or 120) + 3)))
    if chosen is not None:
        at8 = run_named(sc('AT8', 'ledge: ground swing started while the hero is still on the platform; the platform edge passes under him on tick 1', {1: RT | SQ}, 60,
                           after=RT, warm='east', cells=plat, start=(chosen, 100)), outdir, store)
        S += describe(at8)
        at8n = run_named(sc('AT8n', 'control: same with the start one pixel to the left (the hero stays on the platform)', {1: RT | SQ}, 60, after=RT, warm='east', cells=plat,
                            start=(chosen - 1, 100)), outdir, store)
        S.append('-- AT8n control (start x = %d px, one pixel to the left): anim_cur changes: %s; on_ground after tick 1..3: %s' % (
            chosen - 1, ', '.join('t%d %s->%s' % (t, h(a), h(b)) for t, a, b in anim_changes(at8n, 'anim_cur')), [r['on_ground'] for r in at8n['rows'][:3]]))
    at8b = run_named(sc('AT8b', 'floor drop: Idle, Square edge at tick 1; the harness sets every cell height byte to 0 before tick 7 (platform height 1 before)', {1: SQ}, 40,
                        cells=lambda col, row: (1, 0, 0, 0), before={7: all_floor(0)}), outdir, store)
    S.append('== AT8b  floor drop under an Idle hero (field changed by the harness before tick 7: all cells 1 -> 0, so the hero loses the ground in the middle of the swing)')
    S += describe(at8b)
    S.append('')

    # ---------------- AT9
    W_ = []
    wl = ['# AT9: one press per weapon (WeaponId set by the real AddItem 0x8004E530 + SetPlayerWeaponId 0x8004E484 with the slot read from the item property table 0x800B9FE8)',
          '# columns: ground press from Idle at tick 1 (AT9g) and air press (Cross at tick 1, Square at tick 5: AT9a).  Record bytes read from the binary at the pointer that was written.']
    for item, label in WEAPONS:
        for kind, holds, n in (('g', {1: SQ}, 70), ('a', {1: CR, 5: SQ}, 70)):
            nm = 'at9/AT9%s-item%02d' % (kind, item)
            r = run_named(sc(nm, '%s, %s press' % (label, 'ground' if kind == 'g' else 'air'), holds, n, item=item), outdir, store)
            m = r['machine']
            press_tick = 1 if kind == 'g' else 5
            row = r['rows'][press_tick - 1]
            ptr = row['rec_ptr']
            recb = [m.peek(ptr + i) & 0xFF for i in range(4)] if ptr else None
            wrec = r['meta']['weapon_rec_index']
            chg = m.peek(A.WEAPON_TAB + 13 * wrec) & 0xFF
            spawns = [(t, e) for t, e in events_of(r) if e.startswith('spawn')]
            sounds = [e for t, e in events_of(r) if t == press_tick and e.startswith('sound')]
            ch = anim_changes(r, 'anim_cur')
            endp = [x for x in ch if x[0] > press_tick]
            pw = ptr_writes(r)
            set_t = pw[0][0] if pw else None
            clr = [t for t, p, cc in pw if p == 0]
            cnt_at_clear = None
            if clr:
                cnt_at_clear = r['rows'][clr[0] - 1]['rec_cnt']
            line = ('%-18s %-6s item %2d slot %s -> item %s, weapon record %2d, chargeable byte %d | press t%d -> anim %s (sound at press %s) | record @%s = (anim,type,delay,z) %s | '
                    'pointer set t%s, cleared t%s with counter %s | spawn %s | swing ends %s | max charge timer %d') % (
                label, 'ground' if kind == 'g' else 'air', item, r['meta']['weapon_slot'], r['meta']['weapon_item_now'], wrec, chg, press_tick, h(row['anim_target']), sounds or '-',
                h(ptr) if ptr else '0', ['%02X' % b for b in recb] if recb else None, set_t, clr[0] if clr else None, cnt_at_clear,
                ['t%d %s' % (t, e) for t, e in spawns] or 'none', ['t%d %s->%s' % (t, h(a), h(b)) for t, a, b in endp[:2]] or 'no change (no swing)',
                max(x['charge_timer'] for x in r['rows']))
            wl.append(line)
    open(os.path.join(outdir, 'weapons.txt'), 'w').write('\n'.join(wl) + '\n')
    S.append('== AT9  weapon table exercised (details in weapons.txt)')
    S += wl[2:]
    S.append('')

    # ---------------- AT10 no weapon
    at10 = run_named(sc('AT10', 'no weapon (SetPlayerWeaponId(-1)), Idle, Square at tick 1', {1: SQ}, 40, no_weapon=True), outdir, store)
    S.append('== AT10  no weapon equipped (WeaponId -1): the binary reads the byte at 0x80028C13')
    S += describe(at10)
    S.append('byte at 0x80028C13 = %d' % (at10['machine'].peek(0x80028C13) & 0xFF))
    S.append('')

    # ---------------- X1 charge probe (informational)
    x1 = run_named(sc('X1', 'INFORMATIONAL: Steel Edge, Square held ticks 1..90 then released (charge: aura/spark/sound are STUBS journalled, not modelled)', {t: SQ for t in range(1, 91)}, 130, item=2),
                   outdir, store)
    S.append('== X1  (informational, not asked) Steel Edge, Square held 1..90, released at 91')
    S += describe(x1)
    S.append('')

    h2 = {t: SQ for t in range(1, 90)}
    h2[80] = SQ | CR                                                # Cross while Square is held, at tick 80 (the charge counts from the end of the swing and is not full yet)
    x2 = run_named(sc('X2', 'INFORMATIONAL: Steel Edge, Square held 1..89, Cross edge at tick 80 (jump), Square released at tick 90 in the air with a full charge', h2, 120, item=2), outdir, store)
    S.append('== X2  (informational, not asked) Steel Edge, Square held 1..89, jump at tick 80, release in the air at tick 90 (charge full)')
    S += describe(x2)
    S.append('charge timer per tick 80..93: ' + ' '.join('t%d:%d' % (r['tick'], r['charge_timer']) for r in x2['rows'] if 80 <= r['tick'] <= 93) + '; on_ground per tick 88..92: ' + ' '.join(
        't%d:%d' % (r['tick'], r['on_ground']) for r in x2['rows'] if 88 <= r['tick'] <= 92))
    S.append('')

    open(os.path.join(outdir, 'summary.txt'), 'w', encoding='utf-8').write('\n'.join(S) + '\n')
    import hazards_attack
    nh = hazards_attack.check(A.COV, outdir)
    rc = 1 if nh else 0
    if DIFF2:
        d = ['# two-interpreter comparison: every named scenario run on emu.py (the oracle) and on mips.py (via interp2.py, written separately), tick by tick:',
             '# rows (all trace columns + events), the hero 0x294-byte slot after EVERY tick, the pad/charge/weapon/stats memory at the end, the stub journal']
        bad = 0
        for nm, n, parts in DIFF:
            ok = all(parts.values())
            bad += 0 if ok else 1
            d.append('%-22s %3d ticks  %s  %s' % (nm, n, 'EQUAL' if ok else 'DIFFERENT', '' if ok else str(parts)))
        d.append('VERDICT: %d scenarios, %s' % (len(DIFF), 'all identical on both interpreters' if not bad else '%d DIFFERENT' % bad))
        open(os.path.join(outdir, 'diff2-attack.txt'), 'w', encoding='utf-8').write('\n'.join(d) + '\n')
        print(d[-1])
        rc = rc or (1 if bad else 0)
    return rc


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')))
