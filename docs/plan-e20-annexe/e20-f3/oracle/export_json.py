"""export_json.py - E20.f3 T0: the JSON twins of the text outputs of the oracle.  Every row is a row of the oracle (pworld.run): {tick, hold, rng, rec_ptr, rec_cnt, charge, journal, slots}, with
slots[<n>].<key> the keys of pworld.FIELDS (status, cnt1c, reset_AC, px, py, pz, fx, fy, fz, adj_13C, ground_144, hbx, hby, hbz, bw, bd, bh, atk_attr, atk_pow, type68, flags6C, loot3c, c1d0,
touch_slot, box1d4, ...).  Positions and forces are RAW 16.16 integers (divide by 65536 for pixels); rawZ is the binary's frame (the DLL's frame is this minus 1 unit, ADR-0026).
Nothing here knows an expected value: every number is read from the emulated RAM.  The tests of f3a, f3b and f3c read these files by key.

usage (repository root):  python -B docs/plan-e20-annexe/e20-f3/oracle/export_json.py        writes out/*.json
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import pworld as P
import world as W
import hitfx as H
from wave_run import run_wave, RELEASE
from wave_hit import first_hit
from wave_grass import grass, cut_events
import wave_only
import wave_z
import dirtest

OUT = os.path.join(HERE, 'out')
PROJ = (3, 4, 7, 11, 12, 14, 15, 27, 28, 29)


def dump(name, obj):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name), 'w') as f:
        json.dump(obj, f, indent=None, sort_keys=True, separators=(',', ':'))
        f.write('\n')


def rows_of(res, first, last):
    return [r for r in res['rows'] if first <= r['tick'] <= last]


def spec_of(**kw):
    return kw


def waves():
    for sword, direction in (('steel', 'east'), ('steel', 'north'), ('steel', 'west'), ('steel', 'south'), ('demon', 'east'), ('holy', 'east')):
        res = run_wave('w', sword=sword, direction=direction, n=150)
        dump('wave_%s_%s.json' % (sword, direction), dict(scenario='wave', sword=sword, direction=direction, release_tick=RELEASE, first_tick=100, last_tick=150, rows=rows_of(res, 100, 150)))


def wave_hits():
    for direction, xp, yp in (('east', 12, 12), ('north', 7, 6)):
        h, res = first_hit('steel', direction, xp, yp)
        dump('wave_hit_%s.json' % direction, dict(scenario='wave_hit', direction=direction, target_record=[xp, yp], hits_tick_touch_slot=h, rows=rows_of(res, 110, 150)))
    sweep = []
    for direction, specs in (('east', [(xp, 11) for xp in range(6, 19)]), ('north', [(7, yp) for yp in range(2, 12)]), ('west', [(xp, 11) for xp in range(1, 9)]), ('south', [(7, yp) for yp in range(10, 21)])):
        for xp, yp in specs:
            sweep.append(dict(direction=direction, record=[xp, yp], rel_px=[12 * xp + 12 - 100, 8 * yp + 8 - 100], hits_tick_touch_slot=wave_only.hits(direction, xp, yp)))
    dump('wave_only_sweep.json', dict(scenario='wave_only', note='hero HitsClassA cleared; touch slot 2 = the wave', rows=sweep))


def wave_z_runs():
    out = []
    flat2 = lambda c, r: (2, 0, 0, 0)
    ledge1 = lambda c, r: (1 if c >= 5 else 0, 0, 0, 0)
    ledge1_far = lambda c, r: (1 if c >= 6 else 0, 0, 0, 0)
    for name, hp, cells in (('flat height byte 2 everywhere', (100, 100), flat2), ('ledge columns >= 5, hero x=112', (112, 100), ledge1), ('ledge columns >= 5, hero x=104', (104, 100), ledge1),
                            ('ledge columns >= 6, hero x=130', (130, 100), ledge1_far)):
        res = wave_z.run_wave(name, sword='steel', direction='east', n=150, cells=cells, hero_px=hp)
        out.append(dict(name=name, hero_px=hp, rows=rows_of(res, 95, 146)))
    dump('wave_z.json', dict(scenario='wave_z', runs=out))


def wave_grass_runs():
    out = []
    for direction in ('east', 'north'):
        res = run_wave('g', direction=direction, n=150, cells=grass)
        out.append(dict(direction=direction, cuts_tick_col_row_x_y_z_rng=cut_events(res), rows=rows_of(res, 114, 146)))
    dump('wave_grass.json', dict(scenario='wave_grass', runs=out))


def arena():
    from wave_mut import patch
    out = []
    for name, init in (('control', None), ('E10_removed', [patch((0x8007F244, 0x00000000))])):
        res = run_wave('a', direction='east', n=190, targets=[(11, 11, 0)], hero_px=(104, 96), init=init)
        hits = [dict(tick=r['tick'], attacker=('wave' if r['slots'][1]['touch_slot'] == 2 else ('hero' if r['slots'][1]['touch_slot'] == 0 else r['slots'][1]['touch_slot']))) for r in res['rows'] if r['slots'].get(1) and r['slots'][1]['c1d0'] == 25]
        out.append(dict(name=name, release_tick_oracle=RELEASE, arena_tick_is_oracle_tick_minus=5, hits=hits, rows=rows_of(res, 100, 190)))
    dump('arena_probe.json', dict(scenario='arena_probe', runs=out))


def hero_fractional():
    """O-F3-2: the oracle accepts a fractional hero position; the wave is born at the hero's X and Y unchanged (raw 16.16)."""
    def frac(m):
        m.poke(P.ENT0 + 0x114, (100 << 16) | 0x8000)
        m.poke(P.ENT0 + 0x118, (100 << 16) | 0x4000)
    res = run_wave('f', direction='east', n=150, init=[frac])
    w = [r['slots'][1] for r in res['rows'] if r['slots'].get(1)]
    dump('hero_fractional.json', dict(scenario='hero_fractional', hero_raw_xy=[(100 << 16) | 0x8000, (100 << 16) | 0x4000],
                                      wave_first_px_py_pz=[w[0]['px'], w[0]['py'], w[0]['pz']], wave_px_values=sorted(set(d['px'] for d in w)), wave_py_values=sorted(set(d['py'] for d in w)),
                                      wave_hbx_first=w[0]['hbx'], rows=rows_of(res, 114, 124)))


def shot_case(name, holds, n, direction, item=5):
    res = P.run(dict(item=item, n=n, holds=holds, after=0, direction=direction))
    spawn = None
    arrow_slot = None
    for r in res['rows']:
        for s, d in r['slots'].items():
            if s != 0 and d['type68'] in PROJ and spawn is None:
                spawn, arrow_slot = r['tick'], s
    rows = []
    for r in res['rows']:
        h = r['slots'][0]
        a = r['slots'].get(arrow_slot) if arrow_slot is not None else None
        if r['tick'] <= 14 or (spawn and r['tick'] >= spawn - 3 and r['tick'] <= spawn + 1):
            rows.append(dict(tick=r['tick'], hold=r['hold'], hero=dict(anim_t=h['anim_t'], dir_t=h['dir_t'], adir=h['adir'], ground_144=h['ground_144'], pz=h['pz']), rec_ptr=r['rec_ptr'], rec_cnt=r['rec_cnt'],
                             arrow=None if not a or a['type68'] not in PROJ else dict(type68=a['type68'], dir_t=a['dir_t'], adir=a['adir'], pz=a['pz'], px=a['px'])))
    arrow = None
    if spawn:
        a = next(r['slots'][arrow_slot] for r in res['rows'] if r['tick'] == spawn)
        arrow = dict(slot=arrow_slot, tick=spawn, type68=a['type68'], dir_t=a['dir_t'], adir=a['adir'], pz=a['pz'], px=a['px'])
    return dict(name=name, item=item, hero_direction_word=direction, holds={str(k): v for k, v in holds.items()}, spawn_tick=spawn, arrow=arrow, rows=rows)


def directions():
    L, S, U, D, R, X = W.LEFT, W.SQUARE, W.UP, W.DOWN, W.RIGHT, W.CROSS
    cases = [
        ('dirtest_ground_square_up_t1', {1: S | U}, 16, 24),
        ('dirtest_ground_square_t1_up_held_from_t3', {1: S, **{t: U for t in range(3, 16)}}, 16, 24),
        ('dirtest_air_cross_t1_square_left_t8', {1: X, 8: S | L, 9: L, 10: L, 11: L}, 20, 24),
        ('dir_extra_air_square_t8_left_only_from_t9', {1: X, 8: S, **{t: L for t in range(9, 14)}}, 20, 24),
        ('dir_extra_air_left_held_t5_square_t8', {1: X, **{t: L for t in range(5, 14)}, 8: S | L}, 20, 24),
        ('dir_extra_ground_square_down_t1', {1: S | D}, 16, 24),
        ('dir_extra_ground_square_left_t1', {1: S | L}, 16, 24),
        ('dir_extra_ground_square_right_t1', {1: S | R}, 16, 24),
        ('dir_extra_ground_facing_north_square_t1', {1: S}, 16, 16),
        ('dir_extra_ground_facing_west_square_t1', {1: S}, 16, 8),
        ('dir_extra_ground_facing_south_square_t1', {1: S}, 16, 0),
    ]
    out = [shot_case(nm, h, n, d) for nm, h, n, d in cases]
    dump('directions.json', dict(scenario='directions', note='Hunter bow (item 5); arrow.dir_t is the direction word the real shot passed ({0 south, 0x10 north, 8 west, 0x18 east}); hero.adir is +0x98 (the index)', cases=out))


def multishot():
    cases = []
    for name, item, holds, n in (('hunter_square_t1_t25', 5, {1: W.SQUARE, 25: W.SQUARE}, 60), ('hunter_square_every_second_tick_t1_t41', 5, {t: W.SQUARE for t in range(1, 42, 2)}, 60),
                                 ('william_square_held_t1_t60', 6, {t: W.SQUARE for t in range(1, 61)}, 80)):
        res = P.run(dict(item=item, n=n, holds=holds, after=0))
        seen = {}
        for r in res['rows']:
            for s, d in r['slots'].items():
                if s != 0 and d['type68'] in PROJ and s not in seen:
                    seen[s] = dict(tick=r['tick'], slot=s, type68=d['type68'], px=d['px'], pz=d['pz'])
        alive = {str(t): {str(s): d['px'] for s, d in r['slots'].items() if s != 0 and d['type68'] in PROJ and d['status'] in (1, 2, 3)} for r in res['rows'] for t in [r['tick']] if t in (10, 20, 30, 35, 40, 50, 60)}
        cases.append(dict(name=name, item=item, births=sorted(seen.values(), key=lambda d: d['tick']), alive_px=alive))
    dump('multishot.json', dict(scenario='multishot', cases=cases))


def hitfx_table():
    m, meta = H.build()
    tgt = P.ENT0 + P.STRIDE * 1
    sheet = m.peek(tgt + 0x1C4)
    rows = []
    resp_list = [0x00, 0x10, 0x3F, 0x40, 0x7F, 0x80, 0xBF, 0xC0, 0xFF, 0x81]
    for nib in range(16):
        attr = nib if nib else 0x80
        for resp in resp_list:
            r = H.trial(m, attr, 1, resp, nib)
            immune, fx = H.expected(nib, resp)
            got = [int(e.split('id=')[1].split(',')[0]) for e in r['effects']]
            ok = got == fx and (r['touch'] == 0) == immune and r['c1d0'] == 25 and r['hitc'] == 1
            rows.append(dict(attr=attr, nib=nib, resp=resp, fx=got, sparks=r['sparks'], c1d0=r['c1d0'], touch=r['touch'], hitc=r['hitc'], rng_before=r['rng0'], rng_after=r['rng1'], other=r['other'], decoded_rule_fx=fx, decoded_rule_immune=immune, match=ok))
    variants = []
    for attr in (0x86, 0x87, 0x89, 0x8A, 0x06, 0x07, 0x09, 0x0A):
        nib = attr & 0xF
        r = H.trial(m, attr, 1, 0x10, nib)
        variants.append(dict(attr=attr, nib=nib, resp=0x10, fx=[int(e.split('id=')[1].split(',')[0]) for e in r['effects']], sparks=r['sparks'], c1d0=r['c1d0'], touch=r['touch'], hitc=r['hitc'], rng_before=r['rng0'], rng_after=r['rng1']))
    dump('hitfx_table.json', dict(scenario='hitfx_table', target_sheet_ptr=sheet, target_sprite=m.peek(tgt + 0x68), pairs=len(rows), matching=sum(1 for r in rows if r['match']), rows=rows, equipment_variants=variants))
    return rows


def main():
    waves()
    wave_hits()
    wave_z_runs()
    wave_grass_runs()
    arena()
    hero_fractional()
    directions()
    multishot()
    rows = hitfx_table()
    print('json written; hitfx pairs %d, matching %d' % (len(rows), sum(1 for r in rows if r['match'])))


if __name__ == '__main__':
    main()
