"""make_tables.py - compact markdown timelines from out/json (so that the report numbers are copied, not retyped)."""
import json, os, sys, re
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')

def load(name):
    return json.load(open(os.path.join(OUT, 'json', name + '.json')))

def row(name, slot, S, ks, start_abs=None):
    rows = load(name)
    byt = {r['tick']: r for r in rows}
    out = ['| tick | status | x | y | z | ForceX | ForceZ | adj | gnd | hit box x,z (w x d x h) | events |', '|---|---|---|---|---|---|---|---|---|---|---|']
    for k in ks:
        r = byt.get(S + k)
        if r is None:
            continue
        d = r['slots'].get(str(slot))
        ev = []
        if r['draws']:
            ev.append('RNG+%d' % r['draws'])
        ev += [re.sub(r',x=[-0-9.]+,y=[-0-9.]+,z=[-0-9.]+', '', e) if False else e for e in r['effects']]
        ev += [j for j in r['journal'] if not j.startswith('stub') and j != 'snd(0)']
        evs = '; '.join(ev)
        if len(evs) > 140:
            evs = evs[:137] + '...'
        if d is None:
            out.append('| S+%d | absent | | | | | | | | | %s |' % (k, evs))
            continue
        box = ('%.1f,%.1f (%dx%dx%d)' % (d['hbx'] / 65536, d['hbz'] / 65536, (d['bw'] + 1) // 65536, (d['bd'] + 1) // 65536, (d['bh'] + 1) // 65536)) if d['box1d4'] else 'none'
        out.append('| S+%d | %d | %.3f | %.3f | %.4f | %d | %d | %d | %d | %s | %s |' % (k, d['status'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['fx'], d['fz'], d['adj_13C'], d['ground_144'], box, evs))
    return '\n'.join(out)

blocks = []
def add(title, name, slot, S, ks):
    blocks.append('### ' + title)
    blocks.append(row(name, slot, S, ks))
    blocks.append('')

add('arrow3 east, hero x0=100, wall column 15', 'arrow3_east_wall', 1, 10, list(range(0, 3)) + list(range(40, 62)))
add('arrow3 vs patched Beannoid (SizeZ 32) at x=300', 'arrow3_target', 2, 10, list(range(28, 34)))
add('charged arrow 7 east, wall column 15', 'charged7_east_wall', 1, 105, list(range(0, 6)) + list(range(40, 46)))
add('fire ball 11 east flat', 'fire11_flat', 1, 12, list(range(0, 14)))
add('ice ball 14 east flat', 'ice14_flat', 1, 12, list(range(0, 13)))
add('fire ball 11, wall column 6', 'fire11_wall_col6', 1, 12, list(range(4, 13)))
add('fire ball 11, 32 px step from column 6', 'fire11_step32_col6', 1, 12, list(range(4, 13)))
add('fire ball 11, 16 px step from column 6', 'fire11_step16_col6', 1, 12, list(range(4, 11)))
add('ice ball 14, wall column 6', 'ice14_wall_col6', 1, 12, list(range(4, 12)))
add('ice ball 14, 16 px step from column 6', 'ice14_step16_col6', 1, 12, list(range(4, 11)))
open(os.path.join(OUT, 'tables.md'), 'w').write('\n'.join(blocks))
print('\n'.join(blocks))
