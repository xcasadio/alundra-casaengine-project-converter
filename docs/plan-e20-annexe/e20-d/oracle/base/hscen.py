"""hscen.py - E20.c2-0: the scenarios (kept by docs/plan-e20-heros.md 1.11 and check.md section c), the readers of a run and the text / JSON writers.

The specs hold SETUP (inventory, stats, positions, targets, cells, forced animations) and INPUTS (pad word per tick, control flags, events between two ticks).  They hold no expected
output; the expected values live in hmodel.py and hmut.py.  Everything printed is read from the emulated machine.

Kept: A1 (H3 extended to 200 ticks, and the pad-0 contact variant at 20 px), A2, A4, A6, B1, B2, D1, E2, E3, F1, F2, F4, F5, G; C1 and E1 are INFORMATIONAL (E20.i, E18).
Removed by check.md: A3, A5, A7, B3, B4, C2, C3, D2, F3, F6 and group H.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import c1world as C
import scen as S
import world as W
import hworld as H

RIGHT, UP = W.RIGHT, W.UP
UNSERIALIZED = ('cells', 'setup', 'target_init', 'before', 'mutate')


def mk(name, desc, dx=20, dy=0, n=40, cells_word=None, **kw):
    sp = S.spec(name, desc, dx, dy, n=n, **kw)
    if cells_word is not None:
        sp['cells_word'] = cells_word
        sp['cells'] = H.fill(cells_word)
    return sp


# ---- the scenario list ----------------------------------------------------------------------------------------------------------------
def scenarios():
    L = []

    # A1: H3 (hero 60 px west of the Beannoid, walks east) for 200 ticks; and the pad-0 contact variant at 20 px (the c1a pin "WhileTheContactLasts" geometry)
    L.append(dict(name='A1', info=False, desc='toucher branch: H3 extended to 200 ticks, and the pad-0 contact variant at 20 px (260 ticks)', runs=[
        mk('A1', 'H3 again, 200 ticks: kit, Beannoid of map 14, HP 10/10 (stats), Right held from tick 1; hit at 17, hurt at 18', 60, n=200, after=RIGHT),
        mk('A1-pad0', 'the hero at rest 20 px west of the Beannoid, no pad, 260 ticks', 20, n=260)]))

    # A2: armour x HP against the Orc of map 15 (attribute 0x02, power 4); contact at tick 1, hurt at tick 2
    runs = []
    for inv in ('kit', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet'):
        for hp, hpmax in ((12, 12), (5, 12), (3, 3)):
            runs.append(mk('A2-%s-%d-%d' % (inv, hp, hpmax), 'Orc of map 15 (attribute 0x02, power 4) 20 px east of the resting hero; inventory %s, stats HP %d / %d' % (inv, hp, hpmax),
                           20, n=40, kind='map15-orc', inv=inv, hp=hp, hpmax=hpmax))
    # the same damage with a synthetic attack pair (attribute 0x02, power 4) on the Beannoid: the toucher's CurrentAttack is all the hero's sheet reads
    for inv in ('kit', 'leather'):
        runs.append(mk('A2-syn-%s' % inv, 'Beannoid of map 14 whose CurrentAttack := the synthetic pair (attribute 0x02, power 4) 20 px east of the resting hero; inventory %s, stats HP 12 / 12' % inv,
                       20, n=40, inv=inv, hp=12, hpmax=12, target_init={0: H.set_attack(0x02, 4)}))
    L.append(dict(name='A2', info=False, desc='damage per armour x HP against the Orc (18 runs x 40 ticks) and two synthetic-attack cross-checks', runs=runs))

    # A4: bearings (8 compass + 4 near-diagonals): the hero is placed so that (hero - Beannoid) = (vx, vy) px
    runs = []
    for lab, vx, vy in (('E', 14, 0), ('W', -14, 0), ('S', 0, 10), ('N', 0, -10), ('SE', 10, 8), ('SW', -10, 8), ('NE', 10, -8), ('NW', -10, -8),
                        ('ENE', 16, -3), ('WSW', -16, 3), ('SSE', 3, 12), ('NNW', -3, -12)):
        runs.append(mk('A4-%s' % lab, 'bearing %s: hero - Beannoid = (%d, %d) px, resting, 8 ticks' % (lab, vx, vy), -vx, vy, n=8, bearing=(vx, vy)))
    L.append(dict(name='A4', info=False, desc='knock-back direction (+0x8C) after the hurt, 12 bearings x 8 ticks', runs=runs))

    # A6: the invulnerability window.  T = 2 (hurt tick of the pad-0 contact at 20 px).  The hero is put back in contact (PosX := the contact position, a harness write between two ticks)
    # at tick T + k, k = 117..122; and 'keep' puts him back in contact on every tick from tick 3 on
    runs = []
    for k in (117, 118, 119, 120, 121, 122):
        runs.append(mk('A6-back%d' % (2 + k), 'pad-0 contact at 20 px (touch at 1, hurt at T = 2); at tick %d (T + %d) PosX := the contact position (232 px)' % (2 + k, k), 20, n=132,
                       events={2 + k: [('teleport_x', 232)]}, window_k=k))
    runs.append(mk('A6-keep', 'pad-0 contact at 20 px; PosX := 232 px before every tick from 3 to 260: the contact is kept for 260 ticks', 20, n=260,
                   events={t: [('teleport_x', 232)] for t in range(3, 261)}))
    L.append(dict(name='A6', info=False, desc='invulnerability window: +0x1CC 0x78, no new touch until it is 0; boundary at hurt + 118 .. + 122', runs=runs))

    # B1: hazard cells (cost 1, 2, 3) x boots level 0..3, no toucher, whole field painted
    runs = []
    for word in (0x80, 0x100, 0x180):
        for inv in ('kit', 'b1a', 'b1b', 'b1c'):
            runs.append(mk('B1-%03X-%s' % (word, inv), 'hazard cell word 0x%X on the whole field, inventory %s (boots level %d), hero resting, no toucher, 125 ticks' % (
                word, inv, {'kit': 0, 'b1a': 1, 'b1b': 2, 'b1c': 3}[inv]), 60, n=125, targets=[], cells_word=word, inv=inv))
    L.append(dict(name='B1', info=False, desc='hazard cost 1 / 2 / 3 x boots level 0..3 (12 runs x 125 ticks)', runs=runs))

    # B2: the HP floor
    L.append(dict(name='B2', info=False, desc='hazard HP floor: HP 2 with cost 3, HP 1 with cost 1, HP 3 with cost 3', runs=[
        mk('B2-hp2-cost3', 'stats HP 2 / 10, cost-3 cells', 60, n=80, targets=[], cells_word=0x180, hp=2),
        mk('B2-hp1-cost1', 'stats HP 1 / 10, cost-1 cells', 60, n=80, targets=[], cells_word=0x80, hp=1),
        mk('B2-hp3-cost3', 'stats HP 3 / 10, cost-3 cells', 60, n=80, targets=[], cells_word=0x180, hp=3)]))

    # D1: the control-flag sweep with a toucher in contact (pad-0 contact at 20 px), the stats HP poked to 6 at tick 1 (the END mirror must overwrite it unless the tick gate stops MovePlayer)
    runs = []
    for v in (0x00, 0x20, 0x24, 0x30, 0x10, 0x04, 0x28, 0x08, 0x40, 0x48, 0x80, 0xA0):
        runs.append(mk('D1-%02X' % v, '[0x800DC4B8] := 0x%02X from tick 1; stats HP poked to 6 (real SetPlayerHp) before tick 1; pad-0 contact at 20 px, 60 ticks' % v, 20, n=60,
                       events={1: [('flags', v), ('stats_hp', 6)]}, flags_value=v))
    runs.append(mk('D1-20-release', '[0x800DC4B8] := 0x20 from tick 1, released to 0 before tick 40; stats HP poked to 6 before tick 1; 140 ticks', 20, n=140,
                   events={1: [('flags', 0x20), ('stats_hp', 6)], 40: [('flags', 0)]}, flags_value=0x20))
    # the tick gate AFTER a hurt: +0x1CC is frozen, the lists pass alone keeps recomputing the draw list from it (119 & 3 == 3: hidden; 118: visible)
    runs.append(mk('D1-48-hold-hidden', 'hurt at tick 2 (+0x1CC 119, hero hidden); [0x800DC4B8] := 0x48 before tick 3 (frozen +0x1CC 119 -> the lists pass keeps him out of the draw list), 20 ticks', 20, n=20,
                   events={3: [('flags', 0x48)]}, flags_value=0x48))
    runs.append(mk('D1-48-hold-visible', 'as above from tick 4 (frozen +0x1CC 118 -> the hero stays in the draw list), 20 ticks', 20, n=20, events={4: [('flags', 0x48)]}, flags_value=0x48))
    runs.append(mk('D1-48-hold-release', 'as D1-48-hold-hidden, released to 0 before tick 12: the countdown and the blink resume, 30 ticks', 20, n=30, events={3: [('flags', 0x48)], 12: [('flags', 0)]}, flags_value=0x48))
    L.append(dict(name='D1', info=False, desc='control-flag sweep [0x800DC4B8]: the tick gate & 0x48, the locked branch & 0x34, exactly 0x20 reaches the damage entry', runs=runs))

    # E1 (informational for E18): HP floor and the death branch
    L.append(dict(name='E1', info=True, desc='INFORMATIONAL (E18): HP 1 + Beannoid, HP 2 + Orc, HP 2 + cost-3 cells; HP floors at 0, the death branch is entered one tick later', runs=[
        mk('E1-hp1-bean', 'stats HP 1 / 10, Beannoid contact at 20 px', 20, n=100, hp=1),
        mk('E1-hp2-orc', 'stats HP 2 / 10, Orc contact at 20 px', 20, n=100, hp=2, kind='map15-orc'),
        mk('E1-hp2-cost3', 'stats HP 2 / 10, cost-3 cells, no toucher', 60, n=100, targets=[], cells_word=0x180, hp=2)]))

    # E2: the entity wins over the stats; the vessel
    L.append(dict(name='E2', info=False, desc='the entity wins: stats HP written from outside, the real IncreaseHpMaxAndCreateEffect 0x800333AC between two ticks', runs=[
        mk('E2-stats-hp', 'stats HP := 3 (real SetPlayerHp) before tick 3; entity HP 10/10 stays, the stats follow at the next END', 60, n=10, targets=[], events={3: [('stats_hp', 3)]}),
        mk('E2-stats-hpmax', 'stats HpMax := 4 (real SetPlayerHpMax) before tick 3; entity 10/10; the stats follow', 60, n=10, targets=[], events={3: [('stats_hpmax', 4)]}),
        mk('E2-vessel', 'real 0x800333AC(hero) before tick 3, HP 5 / 10: entity 11/11, the stats follow', 60, n=10, targets=[], hp=5, events={3: [('vessel',)]}),
        mk('E2-vessel49', 'as above from 49 / 49: entity 50/50 then 50 / 51 more', 60, n=10, targets=[], hp=49, hpmax=49, events={3: [('vessel',)]}),
        mk('E2-vessel50', 'from 50 / 50: the entity is 51/51 (no cap there), the stats are clamped to 50/50', 60, n=10, targets=[], hp=50, hpmax=50, events={3: [('vessel',)]})]))

    # E3: the hero created from the stats
    L.append(dict(name='E3', info=False, desc='hero created from the stats by the real SetPlayerHpMax / SetPlayerHp and the two getters', runs=[
        mk('E3-12-4', 'stats HpMax 12, HP 4', 60, n=3, targets=[], hp=4, hpmax=12),
        mk('E3-60-70', 'stats HpMax 60 (clamped to 50), HP 70', 60, n=3, targets=[], hp=70, hpmax=60),
        mk('E3-5-9', 'stats HpMax 5, HP 9 (clamped to 5)', 60, n=3, targets=[], hp=9, hpmax=5),
        mk('E3-neg', 'stats HpMax -1, HP -3 (both 0)', 60, n=3, targets=[], hp=-3, hpmax=-1)]))

    # F: regeneration
    L.append(dict(name='F1', info=False, desc='regeneration with the tonic (period 201 ticks), HP 5/10, no pad, 450 ticks', runs=[
        mk('F1', 'tonic 0x38 equipped, stats HP 5 / 10, no pad, no target', 60, n=450, targets=[], hp=5, inv='tonic')]))
    L.append(dict(name='F2', info=False, desc='as F1 with Up held at tick 100 (the pad gate zeroes the counters)', runs=[
        mk('F2', 'as F1; Up held at tick 100 only', 60, n=450, targets=[], hp=5, inv='tonic', holds={100: UP})]))
    L.append(dict(name='F4', info=False, desc='no regeneration at HP == HpMax and at HP 0', runs=[
        mk('F4-full', 'tonic, HP 10 / 10', 60, n=250, targets=[], inv='tonic'),
        mk('F4-zero', 'tonic, HP 0 / 10 at the start; [0x800DC4B8] := 0 before every tick so that the game-over request of the death branch does not close the flags gate', 60, n=250,
           targets=[], hp=0, inv='tonic', flags_every_tick=0)]))
    L.append(dict(name='F5', info=False, desc='only the accessory regenerates: ring 0x37 (regenerates), holy sword + cloth armour (does not), 450 ticks', runs=[
        mk('F5-ring37', 'ring 0x37 equipped, HP 5 / 10', 60, n=450, targets=[], hp=5, inv='ring37'),
        mk('F5-armour-weapon', 'holy sword and cloth armour only, HP 5 / 10', 60, n=450, targets=[], hp=5, inv='holy')]))

    # G: boots and sheet, K1 style
    runs = []
    for inv in ('kit', 'b19', 'b1a', 'b1b', 'b1c', 'b1a1c', 'b1b1a', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet', 'charm'):
        runs.append(mk('G-%s' % inv, 'inventory %s: boots level and sheet bytes 3..13 after the real 0x800307E8 (setup warm-up and the END of each tick), 3 ticks' % inv, 60, n=3, targets=[], inv=inv))
    L.append(dict(name='G', info=False, desc='boots level and hero sheet per inventory (K1 style)', runs=runs))

    # C1 (informational for E20.i): deep water
    L.append(dict(name='C1', info=True, desc='INFORMATIONAL (E20.i): deep water everywhere (Slope_18c 4), boots 0x1B; hurt state 0x3A / 0x3B', runs=[
        mk('C1-bean', 'water everywhere (cell word 0x800), boots 0x1B, Beannoid contact at 20 px, 100 ticks', 20, n=100, cells_word=0x800, inv='b1b'),
        mk('C1-fish', 'water everywhere, boots 0x1B, the Fish of map 14 in animation 3 (frame box present) 10 px east, 100 ticks', 10, n=100, cells_word=0x800, inv='b1b',
           kind='map14-fish', target_init={0: S.bean_anim(3)})]))
    return L


def find(name):
    for sc in scenarios():
        for r in sc['runs']:
            if r['name'] == name:
                return r
    raise KeyError(name)


# ---- readers ---------------------------------------------------------------------------------------------------------------------------------
def changes(res, key, who='hero', group=None):
    out = []
    first = res['pre']
    prev = first['hero'][key] if group is None else first['g'][key]
    for r in res['rows']:
        v = r['hero'][key] if group is None else r['g'][key]
        if v != prev:
            out.append((r['tick'], prev, v))
        prev = v
    return out


def hurt_ticks(res):
    """ticks at which the hero's +0x1CC rises (the hurt, or the 0x20 entry)"""
    out = []
    prev = res['pre']['hero']['c1cc']
    for r in res['rows']:
        v = r['hero']['c1cc']
        if v > prev:
            out.append(r['tick'])
        prev = v
    return out


def hidden_ticks(res):
    return [r['tick'] for r in res['rows'] if not r['hero']['visible']]


def sound_ticks(res):
    out = []
    for r in res['rows']:
        for k, a in r['journal_raw']:
            if k == 'sound':
                out.append((r['tick'], a if not isinstance(a, list) else a[0]))
    return out


def rngs(vals, cap=24):
    """compact run-length listing of ticks"""
    vals = list(vals)
    if not vals:
        return 'none'
    parts, i = [], 0
    while i < len(vals):
        j = i
        while j + 1 < len(vals) and vals[j + 1] == vals[j] + 1:
            j += 1
        parts.append(str(vals[i]) if i == j else '%d-%d' % (vals[i], vals[j]))
        i = j + 1
    s = ','.join(parts)
    return s if len(parts) <= cap else ','.join(parts[:cap]) + ',...(%d ranges)' % len(parts)


def facts(res):
    h = [r['hero'] for r in res['rows']]
    g = [r['g'] for r in res['rows']]
    L = []
    L.append('hurt entries (+0x1CC rises) at ticks: %s' % rngs(hurt_ticks(res)))
    L.append('hero touched by the hit pass (TouchingEntity set after the tick) at ticks: %s' % rngs([r['tick'] for r in res['rows'] if r['hero']['touching_slot'] is not None]))
    L.append('hero HP (+0x14) changes: %s' % (', '.join('t%d %d->%d' % c for c in changes(res, 'hp')[:14]) or 'none'))
    L.append('hero HpMax (+0x18) changes: %s' % (', '.join('t%d %d->%d' % c for c in changes(res, 'hpmax_18')) or 'none'))
    L.append('stats HP / HpMax changes: %s | %s' % (', '.join('t%d %d->%d' % c for c in changes(res, 'stats_hp', group='g')[:14]) or 'none', ', '.join('t%d %d->%d' % c for c in changes(res, 'stats_hpmax', group='g')) or 'none'))
    L.append('hero anim_target (+0x88) changes: %s' % (', '.join('t%d 0x%X->0x%X' % c for c in changes(res, 'anim_target')[:12]) or 'none'))
    L.append('hero dir_target (+0x8C) changes: %s' % (', '.join('t%d %d->%d' % c for c in changes(res, 'dir_target')[:12]) or 'none'))
    L.append('hero +0x1CC at the first hurt tick(s): %s' % ([h[t - 1]['c1cc'] for t in hurt_ticks(res)[:3]] or 'none'))
    L.append('hero not in the draw list (blink) at ticks: %s' % rngs(hidden_ticks(res), cap=12))
    L.append('sounds (tick, id): %s' % (sound_ticks(res)[:8] or 'none'))
    L.append('control flags [0x800DC4B8] changes: %s; boots level %d' % (', '.join('t%d 0x%X->0x%X' % c for c in changes(res, 'ctl_flags', group='g')[:8]) or 'none', g[0]['boots']))
    L.append('sheet responses 1..11 (bytes 3..13 of 0x80127008): %s' % ' '.join('%02X' % g[0]['sheet_r%d' % i] for i in range(1, 12)))
    L.append('regeneration counters: max %s, resets (tick, from): %s' % ([max(x['regen%d' % i] for x in g) for i in range(3)], [(r['tick'], p['g']['regen0']) for p, r in zip([res['pre']] + res['rows'], res['rows']) if p['g']['regen0'] and not r['g']['regen0']][:6]))
    L.append('hero position x (px): start %s, end %s; slope_18c at the end %d; VramOR at the end 0x%X' % (C.px(res['pre']['hero']['pos_x']), C.px(h[-1]['pos_x']), h[-1]['slope_18c'], h[-1]['vram_or_180']))
    L.append('unwritten-byte reads during the run: %s' % (res['meta']['uninit_reads'] or 'none'))
    return L


HERO_COLS = [
    ('tick', lambda r, e: r['tick']), ('hold', lambda r, e: '0x%04X' % r['hold']), ('ctl', lambda r, e: '0x%X' % r['g']['ctl_flags']),
    ('anim88', lambda r, e: '0x%X' % e['anim_target']), ('dir8C', lambda r, e: e['dir_target']), ('anim90', lambda r, e: '0x%X' % e['anim_cur']),
    ('pos_x', lambda r, e: C.px(e['pos_x'])), ('pos_y', lambda r, e: C.px(e['pos_y'])), ('force_x', lambda r, e: e['force_x']), ('ground', lambda r, e: e['on_ground']),
    ('touching', lambda r, e: '-' if e['touching_slot'] is None else 'slot%d' % e['touching_slot']), ('c1CC', lambda r, e: e['c1cc']), ('c1D0', lambda r, e: e['c1d0']),
    ('cnt1C', lambda r, e: e['frame_counter_1C']), ('hp14', lambda r, e: e['hp']), ('hpmax18', lambda r, e: e['hpmax_18']),
    ('vramOR', lambda r, e: '0x%X' % e['vram_or_180']), ('slope18C', lambda r, e: e['slope_18c']), ('animEnd_AC', lambda r, e: e['anim_end_AC']),
    ('visible', lambda r, e: e['visible']), ('vis_count', lambda r, e: e['visible_count']), ('stats_hp', lambda r, e: r['g']['stats_hp']), ('stats_max', lambda r, e: r['g']['stats_hpmax']),
    ('boots', lambda r, e: r['g']['boots']), ('regen012', lambda r, e: '%d/%d/%d' % (r['g']['regen0'], r['g']['regen1'], r['g']['regen2'])),
    ('charge', lambda r, e: r['g']['charge']), ('wptr', lambda r, e: '0x%X' % r['g']['wptr']), ('journal', lambda r, e: ' | '.join(r.get('journal', [])) or '-'),
]


def interesting_rows(res):
    """tick 0, tick 1, the last tick, and every tick where something other than the countdown moved"""
    rows = [res['pre']] + res['rows']
    keep = {0, 1, len(rows) - 1}
    for i in range(1, len(rows)):
        a, b = rows[i - 1], rows[i]
        ha, hb = a['hero'], b['hero']
        key = lambda r, x: (x['anim_target'], x['dir_target'], x['anim_cur'], x['hp'], x['hpmax_18'], x['touching_slot'], x['visible'], x['status'], r['g']['stats_hp'], r['g']['stats_hpmax'],
                            r['g']['ctl_flags'], r['g']['boots'], r['g']['charge'], r['g']['wptr'], r['g']['item_state'], bool(r.get('journal')))
        if key(a, ha) != key(b, hb) or (hb['c1cc'] > ha['c1cc']) or (hb['c1cc'] == 0 and ha['c1cc'] != 0) or (a['g']['regen0'] and not b['g']['regen0']):
            keep.add(i)
    return [rows[i] for i in sorted(keep)]


def write_run_text(res, L):
    sp = res['spec']
    L.append('### %s: %s' % (sp['name'], sp['desc']))
    L.append('# hero start px %s, pad %s, after-last 0x%X, ticks %d, inventory %s, stats HP/HpMax set %s/%s -> entity %s/%s, events %s, flags_every_tick %s, cell word %s, target kind %s'
             % (sp['hero_px'], {k: '0x%X' % v for k, v in sorted(sp['holds'].items())}, sp['after'], sp['n'], res['meta']['inventory']['name'], sp.get('hp', 10), sp.get('hpmax', 10),
                res['meta']['spawn_from_stats']['entity_hp'], res['meta']['spawn_from_stats']['entity_hpmax'],
                {k: v for k, v in sorted(sp.get('events', {}).items())} if len(sp.get('events', {})) <= 6 else '%d ticks with %s' % (len(sp['events']), sorted(set(e[0] for v in sp['events'].values() for e in v))),
                sp.get('flags_every_tick'), ('0x%X' % sp['cells_word']) if sp.get('cells_word') is not None else 'none', res['meta'].get('target_kind', 'Beannoid of map 14' if sp['targets'] else 'none')))
    L += facts(res)
    L.append('-- hero rows (tick 0, 1, the last, and every tick where something other than the +0x1CC countdown or the regeneration counters moved)')
    L.append('\t'.join(c[0] for c in HERO_COLS))
    for r in interesting_rows(res):
        L.append('\t'.join(str(c[1](r, r['hero'])) for c in HERO_COLS))
    L.append('')


def write_scenario(sc, results, model_results, outdir):
    name = sc['name']
    L = ['# %s: %s' % (name, sc['desc']),
         '# computed by the emulated ALUN_CD.EXE (France): real tick passes (gated by [0x800DC4B8] & 0x48 as 0x8003B388 does), real hit pass, real MovePlayer / UpdateItemEffectState; effects, sound, spawn and the',
         '# targets\' own programs are journalled stubs (c1world.py).  Per-tick values are the state AFTER the passes of that tick; tick 0 = start state.  The oracle holds no expected value: see hmodel.py / hmut.py.',
         '# status: %s' % ('INFORMATIONAL (the model is still run on it, but the death branch and the water states are outside the model)' if sc['info'] else 'kept scenario'), '']
    for res, mr in zip(results, model_results):
        write_run_text(res, L)
        bad = [(n, d) for n, o, d in mr if not o]
        L.append('model: %d rules, %s' % (len(mr), 'all hold' if not bad else 'DIFFERENT: %s' % bad))
        L.append('')
    open(os.path.join(outdir, name + '.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
    js = dict(name=name, desc=sc['desc'], informational=sc['info'], runs=[run_json(r) for r in results])
    open(os.path.join(outdir, name + '.json'), 'w', encoding='utf-8', newline='\n').write(json.dumps(js, sort_keys=True, separators=(',', ':'), default=str) + '\n')


TARGET_KEEP = ('status', 'hp', 'anim_cur', 'pos_x', 'pos_y', 'touching_slot', 'c1cc', 'c1d0', 'hit_counter', 'atk_attr', 'atk_power', 'frame_box_1D4', 'anim_flags_B4')


def run_json(res):
    """column-major compact form: {"hero": {column: [value per tick, tick 0 first]}, "g": {...}, "targets": [{...}], "tick": [...], "hold": [...], "rng": [...], "latch": [...], "journal": [...]}"""
    rows = [res['pre']] + res['rows']
    hero = {k: [r['hero'][k] for r in rows] for k in res['pre']['hero']}
    g = {k: [r['g'][k] for r in rows] for k in res['pre']['g']}
    tg = [{k: [r['targets'][i][k] for r in rows] for k in TARGET_KEEP} for i in range(len(res['pre']['targets']))]
    sp = {k: v for k, v in res['spec'].items() if k not in UNSERIALIZED}
    return dict(spec=sp, meta={k: v for k, v in res['meta'].items()}, tick=[r['tick'] for r in rows], hold=[r['hold'] for r in rows], rng=[r['rng'] for r in rows],
                latch_80146DF8=[r['latch_80146DF8'] for r in rows], hero=hero, g=g, targets=tg, journal=[r.get('journal', []) for r in rows],
                sheet_full_start=res['pre']['sheet_full'])


# ---- the key tables of the summary (every value read from the rows) ------------------------------------------------------------------------
def at_tick(res, t):
    return res['rows'][t - 1]


def summary(R):
    """R: run name -> result.  Key values per scenario."""
    L = []

    def hp_at(nm, t):
        return at_tick(R[nm], t)['hero']['hp']
    a = R['A1']
    ht = hurt_ticks(a)
    L.append('A1 (H3, 200 ticks): hit written on the hero at tick %s, hurt (+0x1CC rises) at %s: HP %d -> %d, animation 0x%X, direction %d -> %d, +0x1CC %d, sound %s; hidden %d of the 120 ticks from the hurt; '
             'animation timeline %s; hero HP at tick 200: %d (stats %d/%d); no second touch (the hero walks through the Beannoid during the window)' % (
                 rngs([r['tick'] for r in a['rows'] if r['hero']['c1d0'] == 25]), rngs(ht), a['pre']['hero']['hp'], at_tick(a, ht[0])['hero']['hp'], at_tick(a, ht[0])['hero']['anim_target'],
                 at_tick(a, ht[0] - 1)['hero']['dir_target'], at_tick(a, ht[0])['hero']['dir_target'], at_tick(a, ht[0])['hero']['c1cc'],
                 [s for t_, s in sound_ticks(a)][:2], sum(1 for r in a['rows'][ht[0] - 1:ht[0] + 119] if not r['hero']['visible']),
                 ', '.join('t%d 0x%X' % (t_, p_) for t_, _o, p_ in changes(a, 'anim_target')[:5]), a['rows'][-1]['hero']['hp'], a['rows'][-1]['g']['stats_hp'], a['rows'][-1]['g']['stats_hpmax']))
    b = R['A1-pad0']
    L.append('A1-pad0 (pad 0 at 20 px, 260 ticks): touched at %s, hurt at %s, HP %s; position x %s -> %s' % (
        rngs([r['tick'] for r in b['rows'] if r['hero']['touching_slot'] is not None]), rngs(hurt_ticks(b)), [c[2] for c in changes(b, 'hp')], C.px(b['pre']['hero']['pos_x']), C.px(b['rows'][-1]['hero']['pos_x'])))
    L.append('A2 (Orc, attribute 0x02 power 4): hero HP after the hurt (tick 2) by inventory and stats (HP/HpMax):')
    for inv in ('kit', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet'):
        cells = []
        for hp_, mx in ((12, 12), (5, 12), (3, 3)):
            r = R['A2-%s-%d-%d' % (inv, hp_, mx)]
            cells.append('%d/%d -> %d (stats %d)' % (hp_, mx, hp_at(r['spec']['name'], 2), at_tick(r, 2)['g']['stats_hp']))
        L.append('   %-9s sheet response for nibble 2 = 0x%02X: %s' % (inv, R['A2-%s-12-12' % inv]['rows'][0]['g']['sheet_r2'], ' | '.join(cells)))
    L.append('A4 (direction +0x8C at the hurt tick by bearing (vx, vy) = hero - Beannoid, px): ' + ', '.join(
        '%s (%d,%d) -> %d' % (k, R['A4-%s' % k]['spec']['bearing'][0], R['A4-%s' % k]['spec']['bearing'][1], at_tick(R['A4-%s' % k], 2)['hero']['dir_target']) for k in ('E', 'W', 'S', 'N', 'SE', 'SW', 'NE', 'NW', 'ENE', 'WSW', 'SSE', 'NNW')))
    L.append('A6 (hurt at T = 2; hero put back in contact at tick t): first touch after the hurt / second hurt: ' + ', '.join(
        't=%d (T+%d): touch %s, hurt %s' % (2 + k, k, [x['tick'] for x in R['A6-back%d' % (2 + k)]['rows'] if x['hero']['touching_slot'] is not None and x['tick'] > 2][:1], hurt_ticks(R['A6-back%d' % (2 + k)])[1:2]) for k in (117, 118, 119, 120, 121, 122)))
    k_ = R['A6-keep']
    L.append('A6-keep (contact kept for 260 ticks): touched at %s, hurt at %s, HP %s' % (rngs([r['tick'] for r in k_['rows'] if r['hero']['touching_slot'] is not None]), rngs(hurt_ticks(k_)), [c[2] for c in changes(k_, 'hp')]))
    L.append('B1 (hazard cost x boots level; hurt ticks and hero HP after each hurt):')
    for word in (0x80, 0x100, 0x180):
        cells = []
        for inv in ('kit', 'b1a', 'b1b', 'b1c'):
            r = R['B1-%03X-%s' % (word, inv)]
            cells.append('boots %d: hurts %s HP %s' % (r['rows'][0]['g']['boots'], rngs(hurt_ticks(r)), [c[2] for c in changes(r, 'hp')]))
        L.append('   word 0x%X: %s' % (word, ' | '.join(cells)))
    L.append('B2 (HP floor): ' + '; '.join('%s: HP %s (stats %s)' % (k, [c[2] for c in changes(R[k], 'hp')][:3], [c[2] for c in changes(R[k], 'stats_hp', group='g')][:3]) for k in ('B2-hp2-cost3', 'B2-hp1-cost1', 'B2-hp3-cost3')))
    L.append('D1 (flags: hurt ticks / hero HP at the end / stats HP after tick 1 / hero +0x1C at tick 1 and 60 / touched at / number of animation changes):')
    for v in (0x00, 0x20, 0x24, 0x30, 0x10, 0x04, 0x28, 0x08, 0x40, 0x48, 0x80, 0xA0):
        r = R['D1-%02X' % v]
        L.append('   0x%02X: hurts %s, HP %d, stats HP %d, +0x1C %d / %d, touched %s, anim changes %d' % (
            v, rngs(hurt_ticks(r)), r['rows'][59]['hero']['hp'], at_tick(r, 1)['g']['stats_hp'], at_tick(r, 1)['hero']['frame_counter_1C'], at_tick(r, 60)['hero']['frame_counter_1C'],
            rngs([x['tick'] for x in r['rows'] if x['hero']['touching_slot'] is not None]), len(changes(r, 'anim_target'))))
    r = R['D1-20-release']
    L.append('D1-20-release: hurts %s, touched %s, HP %s' % (rngs(hurt_ticks(r)), rngs([x['tick'] for x in r['rows'] if x['hero']['touching_slot'] is not None]), [c[2] for c in changes(r, 'hp')]))
    for k in ('D1-48-hold-hidden', 'D1-48-hold-visible', 'D1-48-hold-release'):
        r = R[k]
        L.append('%s: +0x1CC at ticks 2..%d = %s; hero hidden at ticks %s; +0x1C at ticks 2..%d = %s' % (
            k, min(r['spec']['n'], 14), [at_tick(r, t_)['hero']['c1cc'] for t_ in range(2, min(r['spec']['n'], 14) + 1)], rngs(hidden_ticks(r)), min(r['spec']['n'], 14),
            [at_tick(r, t_)['hero']['frame_counter_1C'] for t_ in range(2, min(r['spec']['n'], 14) + 1)]))
    for k in ('A2-syn-kit', 'A2-syn-leather'):
        r = R[k]
        L.append('%s (Beannoid with the synthetic attack 0x02 / 4): hero HP after the hurt %d, hurt at %s (Orc, same inventory: %d)' % (
            k, at_tick(r, 2)['hero']['hp'], rngs(hurt_ticks(r)), at_tick(R['A2-%s-12-12' % k[7:]], 2)['hero']['hp']))
    for k in ('E1-hp1-bean', 'E1-hp2-orc', 'E1-hp2-cost3'):
        r = R[k]
        L.append('%s (informational): hurts %s, HP %s, anim_target changes %s, flags changes %s' % (k, rngs(hurt_ticks(r)), [c[2] for c in changes(r, 'hp')], ', '.join('t%d 0x%X' % (t_, p_) for t_, _o, p_ in changes(r, 'anim_target')[:6]),
                                                                                              ', '.join('t%d 0x%X' % (t_, p_) for t_, _o, p_ in changes(r, 'ctl_flags', group='g')[:3]) or 'none'))
    for k in ('E2-stats-hp', 'E2-stats-hpmax', 'E2-vessel', 'E2-vessel49', 'E2-vessel50'):
        r = R[k]
        L.append('%s: entity HP/HpMax at ticks 2,3,4 = %s; stats = %s' % (k, [(at_tick(r, t_)['hero']['hp'], at_tick(r, t_)['hero']['hpmax_18']) for t_ in (2, 3, 4)], [(at_tick(r, t_)['g']['stats_hp'], at_tick(r, t_)['g']['stats_hpmax']) for t_ in (2, 3, 4)]))
    for k in ('E3-12-4', 'E3-60-70', 'E3-5-9', 'E3-neg'):
        r = R[k]
        L.append('%s: stats set %s/%s -> entity at spawn %s/%s; after tick 1: entity %d/%d, stats %d/%d' % (k, r['spec'].get('hp'), r['spec'].get('hpmax'), r['meta']['spawn_from_stats']['entity_hp'], r['meta']['spawn_from_stats']['entity_hpmax'],
                                                                                              at_tick(r, 1)['hero']['hp'], at_tick(r, 1)['hero']['hpmax_18'], at_tick(r, 1)['g']['stats_hp'], at_tick(r, 1)['g']['stats_hpmax']))
    for k in ('F1', 'F2', 'F4-full', 'F4-zero', 'F5-ring37', 'F5-armour-weapon'):
        r = R[k]
        L.append('%s: HP +1 at ticks %s (HP %s -> %s); counters max %s' % (k, [t_ for t_, o, p_ in changes(r, 'hp') if p_ > o], r['pre']['hero']['hp'], r['rows'][-1]['hero']['hp'],
                                                                           [max(x['g']['regen%d' % i] for x in r['rows']) for i in range(3)]))
    L.append('G (boots level / sheet responses 1..11 after the first tick):')
    for k, r in R.items():
        if k.startswith('G-'):
            g_ = r['rows'][0]['g']
            L.append('   %-10s boots %d, sheet %s' % (k[2:], g_['boots'], ' '.join('%02X' % g_['sheet_r%d' % i] for i in range(1, 12))))
    for k in ('C1-bean', 'C1-fish'):
        r = R[k]
        L.append('%s (informational): touched %s, hurt %s, slope_18c %d, anim_target changes %s, hero x %s -> %s' % (
            k, rngs([x['tick'] for x in r['rows'] if x['hero']['touching_slot'] is not None]), rngs(hurt_ticks(r)), r['rows'][-1]['hero']['slope_18c'],
            ', '.join('t%d 0x%X' % (t_, p_) for t_, _o, p_ in changes(r, 'anim_target')[:8]), C.px(r['pre']['hero']['pos_x']), C.px(r['rows'][-1]['hero']['pos_x'])))
    return L
