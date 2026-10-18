"""fire2_summary.py - compact reading of a fire2_key.py JSON: the ticks where something changes, the effects (id, anim, x, y, z) of E28, the block series, the generator accounting.
usage: python -B fire2_summary.py KEY.json"""
import sys
import json

d = json.load(open(sys.argv[1]))
rows = d['rows']
MULT, ADD, M32 = 0x7D2B89DD, 0xE06A02E7, 0xFFFFFFFF


def step(s):
    return (s * MULT + ADD) & M32


def s32(x):
    return x if x < 2**31 else x - 2**32


print('options', d['options'])
prev = None
for r in rows:
    ents = {i: (e['status'], e['anim_c'], e['animend'], e['c20']) for i, e in r['ents'].items()}
    key = (r['hero_anim_t'], r['inuse'], r['phase'], tuple(r['block']), tuple(r['buf']), tuple(sorted(ents.items())) if len(ents) < 3 else tuple(sorted(i for i in ents)))
    fx = [j for j in r['journal'] if j[0].startswith('effect') or j[0] == 'sound']
    if key != prev or fx:
        e1 = ''
        for i, e in r['ents'].items():
            e1 += ' slot%s(st%d id%d type%d flags%#x pos(%d,%d,%d) anim_t%d anim_c%d end%d b4=%#x c20=%d atk=%#x hitc=%d)' % (
                i, e['status'], e['id'], e['type'], e['flags'], e['x'] >> 16, e['y'] >> 16, e['z'], e['anim_t'], e['anim_c'], e['animend'], e['b4'] & 0xFF, e['c20'], e['cur_atk'], e['hitc'])
        print('t=%3d hero(t %#x c %#x) inuse %#x ph %d cnt %d mp %d block %s buf %s%s' % (r['t'], r['hero_anim_t'], r['hero_anim_c'], r['inuse'], r['phase'], r['counter'], r['mp'], r['block'], r['buf'], e1))
        for name, a in fx:
            if isinstance(a, list):
                print('      %s id=%d anim=%d x=%d y=%d z=%d (raw args %s)' % (name, a[1], a[2], s32(a[3]), s32(a[4]), s32(a[5]), a[:6]))
            else:
                print('      %s %s' % (name, a))
        prev = key

print()
print('generator accounting (rng0 = head of the tick, rng1 = after the passes, rng2 = after the frame-end step if --pf):')
n_draw_ticks = 0
for r in rows:
    if r['rng1'] != r['rng0']:
        s = r['rng0']
        k = 0
        while s != r['rng1'] and k < 64:
            s = step(s)
            k += 1
        print('  t=%d : %d draw(s) in the passes (rng0 %#x -> rng1 %#x)' % (r['t'], k if s == r['rng1'] else -1, r['rng0'], r['rng1']))
        n_draw_ticks += 1
print('ticks with draws:', n_draw_ticks)
if d['options'].get('pf'):
    bad = [r['t'] for r in rows[1:] if r['rng0'] != step(rows[r['t'] - 2]['rng1'])]
    print('rng0(t+1) == step(rng1(t)) on every tick:', not bad, bad[:5])
