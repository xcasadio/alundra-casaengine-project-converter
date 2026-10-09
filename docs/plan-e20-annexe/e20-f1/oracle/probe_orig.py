"""probe.py - f-weapons: weapon probes on a COPY of the E20.b attack oracle (tools/). Writes only into the scratch folder (out/)."""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import attack as A
import world as W

SQ, CR, RT, UPK = W.SQUARE, W.CROSS, W.RIGHT, W.UP
OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)


def collapse(events):
    out = []
    for e in events:
        k = e.split('(')[0] + '(' + (e.split('id=')[1].split(',')[0] if 'id=' in e else '')
        if out and out[-1][0] == k:
            out[-1][1] += 1
        else:
            out.append([k, 1, e])
    return ['%s x%d' % (o[2][:60], o[1]) if o[1] > 1 else o[2][:140] for o in out]


def brief(r):
    return ('t%3d hold=%04X anim_t=%02X anim_c=%02X fx=%7d fy=%7d fz=%7d pz=%7d gnd=%d chg=%2d ptr=%08X cnt=%2d hc=%d  %s' % (
        r['tick'], r['hold'], r['anim_target'], r['anim_cur'], r['force_x'], r['force_y'], r['force_z'], r['pos_z_dll'], r['on_ground'], r['charge_timer'], r['rec_ptr'],
        r['rec_cnt'], r['hit_counter_220'], ' | '.join(collapse(r['events']))))


def run(name, item, holds, n, after=0, before=None, cells=None, warm=None, direction=24, start=None):
    spec = dict(name=name, holds=holds, n=n, after=after, item=item, before=before or {}, warm=warm)
    if cells is not None:
        spec['cells'] = cells
    kw = {}
    if start is not None:
        kw['start'] = start
    res = A.run(spec, **kw)
    with open(os.path.join(OUT, name + '.txt'), 'w') as f:
        f.write('# %s item=%s meta=%s\n' % (name, item, json.dumps({k: v for k, v in res['meta'].items() if k != 'bank'})))
        for r in res['rows']:
            f.write(brief(r) + '\n')
    return res


def keyticks(res, extra=()):
    """print only ticks where something changes (animation, event, pointer) and the extra ticks"""
    prev = None
    out = []
    for r in res['rows']:
        key = (r['anim_target'], r['anim_cur'], r['rec_ptr'], tuple(e for e in r['events'] if 'sound(42)' not in e and 'sound(0)' not in e and 'sound(428)' not in e and 'sound(429)' not in e and 'effect_3BDD8' not in e), r['charge_timer'] == 60)
        if key != prev or r['tick'] in extra:
            out.append(brief(r))
        prev = key
    return out


def hold_range(a, b, word=SQ):
    return {t: word for t in range(a, b + 1)}


if __name__ == '__main__' and not (len(sys.argv) > 1 and sys.argv[1] == 'flail'):
    which = sys.argv[1:] or ['all']
    # 1. charged attack of each chargeable weapon: Square held from tick 1 to tick H, released at H+1
    scen = {
        'C2-steel': (2, 90),
        'C3-demon': (3, 90),
        'C4-holy': (4, 90),
        'C16-fire': (16, 100),
        'C14-ice': (14, 100),
        'C10-crusher': (10, 100),
        'C6-william': (6, 90),
    }
    for nm, (item, H) in scen.items():
        if 'all' in which or nm in which:
            res = run(nm, item, hold_range(1, H), H + 60)
            print('=====', nm)
            print('\n'.join(keyticks(res, extra=(H, H + 1, H + 2))))


def hc(m):
    m.poke(W.ent(0) + 0x220, 1)


def flail_main(which):
    S = SQ
    sc = {
        # ground swing, a hit registered at the end of tick 5 (HitCounter seen by MovePlayer at tick 6)
        'F-iron-ground': dict(item=9, holds={1: S}, n=40, before={6: hc}),
        'F-iron-air': dict(item=9, holds={1: CR, 5: S}, n=40, before={8: hc}),
        'F-steel-ground': dict(item=10, holds={1: S}, n=50, before={6: hc}),
        'F-steel-air': dict(item=10, holds={1: CR, 5: S}, n=50, before={8: hc}),
        'F-iron-walk': dict(item=9, holds={1: RT | S}, after=RT, warm='east', n=40, before={6: hc}),
        'F-iron-nohit': dict(item=9, holds={1: S}, n=40),
        # wall: cells east of column 4 (x >= 120 px) are 2 high (32 px): the box of the swing reaches it (or not)
        'F-iron-wall2': dict(item=9, holds={1: S}, n=40, cells=lambda c, r: (2, 0, 0, 0) if c >= 5 else (0, 0, 0, 0)),
        'F-iron-wall1': dict(item=9, holds={1: S}, n=40, cells=lambda c, r: (1, 0, 0, 0) if c >= 5 else (0, 0, 0, 0)),
        'F-iron-wk40': dict(item=9, holds={1: S}, n=40, cells=lambda c, r: (0, 0, 0x40, 0) if c >= 5 else (0, 0, 0, 0)),
        'F-steel-wall2': dict(item=10, holds={1: S}, n=50, cells=lambda c, r: (2, 0, 0, 0) if c >= 5 else (0, 0, 0, 0)),
    }
    for nm, sp in sc.items():
        if which and nm not in which:
            continue
        res = run(nm, sp['item'], sp['holds'], sp['n'], after=sp.get('after', 0), before=sp.get('before'), cells=sp.get('cells'), warm=sp.get('warm'))
        print('=====', nm)
        print('\n'.join(keyticks(res)))
        # print the box (hitbox x relative to hero) at first frames for wall variants
        if 'wall' in nm or 'wk40' in nm:
            rows = res['rows']
            for r in rows[:14]:
                print('   t%2d hitbox_x=%d box_off_x=%d box_size_x=%d  hitbox_z=%d frame_box=%x' % (r['tick'], r['hitbox_x'] >> 16, r['box_off_x'] >> 16, (r['box_size_x'] + 1) >> 16, r['hitbox_z'] >> 20, r['frame_box_1d4']))


if __name__ == '__main__' and len(sys.argv) > 1 and sys.argv[1] == 'flail':
    flail_main(sys.argv[2:])
