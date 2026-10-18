"""c1_make_keys.py (h4c1 discovery, own script on COPIES of the e20-h4 / e20-f4f6 harnesses): writes the REMADE earth-1 keys (pworld machine: slot index words written as 0x8003B24C does, E3 REAL, sprite 16 installed,
real SpawnWarpEntity, real hit pass; effects / sound stubs journalled; the RNG per-frame step of 0x8002BD0C is NOT modelled by the harness, see the report) as slim JSON, one file per scenario, plus a manifest.
usage: python -B c1_make_keys.py OUTDIR"""
import hashlib
import json
import os
import sys
import c1_pworld as C1
import world as W
import fw
import pworld as PW

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
P = W.ent(0)
E3 = 0x8007EDA0
KEEP = set(PW.REAL) | {E3}
TYPES = tuple(PW.PROJECTILE_TYPES) + (16,)


def cells_cols(c0, c1, val):
    def f(col, row):
        return val if c0 <= col <= c1 else (0, 0, 0, 0)
    return f


def slim(res, name, extra=None):
    rows = []
    m = res['m']
    for r in res['rows']:
        d = dict(tick=r['tick'], draws=r['draws'], rng0=r['rng0'], rng1=r['rng1'],
                 hero=dict(anim_t=r['slots'][0]['anim_t'], px=r['slots'][0]['px'], py=r['slots'][0]['py'], pz=r['slots'][0]['pz']) if 0 in r['slots'] else None,
                 item=r.get('item'),
                 rocks={}, others={}, sounds=[j for j in r['journal'] if j.startswith('snd') and j != 'snd(0)'], effects=r['effects'])
        for s, e in r['slots'].items():
            if s == 0:
                continue
            rec = dict(status=e['status'], type=e['type68'], px=e['px'], py=e['py'], pz=e['pz'], fx=e['fx'], fy=e['fy'], fz=e['fz'], anim_t=e['anim_t'], anim_c=e['anim_c'], dir_t=e['dir_t'], adir=e['adir'],
                       reset_AC=e['reset_AC'], flags6C=e['flags6C'], adj_13C=e['adj_13C'], ground_144=e['ground_144'], slope_18C=e['slope_18C'], cnt1c=e['cnt1c'],
                       hbx=e['hbx'], hby=e['hby'], hbz=e['hbz'], bw=e['bw'], bd=e['bd'], bh=e['bh'], touch_slot=e['touch_slot'], c1d0=e['c1d0'], atk_attr=e['atk_attr'], atk_pow=e['atk_pow'])
            (d['rocks'] if e['type68'] == 16 else d['others'])[str(s)] = rec
        rows.append(d)
    path = os.path.join(OUT, name + '.json')
    json.dump(dict(scenario=name, meta=extra or {}, rows=rows), open(path, 'w'), sort_keys=True, default=str)
    return path


def with_item(spec_extra=None):
    log = []
    def at(m, t):
        log.append((t, m.peeks(0x80127164), m.peeks(0x80127160), m.peeks(0x8012715C), m.peeks(P + 0x88), m.peek(P + 0x18C) if False else 0))
    return log, at


def run(name, n=140, hero=(100, 100), direction=24, targets=(), cells=None, poke31=None, item=0x2B):
    log, at = with_item()
    def ticker(m, pad, hold):
        if poke31 is not None and m.tick_no == poke31:
            m.poke(P + 0x88, 0x31)
        W.tick(m, pad, hold)
    spec = dict(item=None, n=n, holds={1: W.CIRCLE}, hero_px=hero, direction=direction, cells=cells, targets=list(targets), seed=0x12345678, types=TYPES, keep=KEEP,
                init=[C1.init_tome(item)], ticker=ticker, after_tick=[at])
    res = fw.run(spec)
    for r in res['rows']:
        t = r['tick']
        e = next(x for x in log if x[0] == t)
        r['item'] = dict(item_in_use=e[1], phase=e[2], counter=e[3], hero_anim=e[4])
    return res, slim(res, name, dict(hero=list(hero), direction=direction, targets=list(targets), poke31=poke31, item=item, n=n, target_slots=res['meta'].get('target_slots')))


scen = []
scen.append(('earth1_flat_east', dict()))
for d, nm in ((0, 'south'), (8, 'west'), (16, 'north')):
    scen.append(('earth1_flat_' + nm, dict(direction=d)))
scen.append(('earth1_deepwater_cols7_9', dict(cells=cells_cols(7, 9, (0, 0, 0, 0x08)))))
for (px, py) in ((132, 104), (108, 136), (180, 104), (108, 56), (60, 104), (132, 136), (204, 104), (84, 104)):
    scen.append(('earth1_vs_beannoid_%d_%d' % (px, py), dict(targets=[((px - 12) // 12, (py - 8) // 8, 0)], n=160)))
for T in (30, 60, 98, 101):
    scen.append(('earth1_interrupt_%d' % T, dict(poke31=T, n=T + 80)))
scen.append(('earth1_flat_east_x200', dict(hero=(200, 100))))
man = []
for name, kw in scen:
    res, path = run(name, **kw)
    h = hashlib.md5(open(path, 'rb').read()).hexdigest()
    man.append('%s  %s' % (h, os.path.basename(path)))
    print(name, 'ok', h)
open(os.path.join(OUT, 'manifest.txt'), 'w').write('\n'.join(man) + '\n')
