"""cliff_scen.py - h4e1 CRITIC: does the 'IsOnGround => destroy without effect' branch of the wind-1 driver (0x80036148-0x80036158) ever fire on the REAL code?
It reuses the h4e1 harness (wind1_key.run, FAITHFUL slot init, per-frame generator step) and only changes the terrain: cells with height byte HB (x16 px) for x >= 120 px
(hero at (100,100), the random spawn rectangle is x in [4,196), y in [36,164)). A birth with X >= 120 px starts inside a cliff higher than hero Z + 128 px when HB*16 >= 128.
usage: python -B cliff_scen.py HB OUTDIR"""
import sys
import os
import json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import wind1_key as K

hb = int(sys.argv[1])
out = sys.argv[2]
os.makedirs(out, exist_ok=True)
name = 'wind1_cliff_hb%d_pf' % hb
res = K.run(name, ticks=140, cells=lambda col, row: (hb, 0, 0, 0) if col >= 5 else (0, 0, 0, 0))
json.dump(res, open(os.path.join(out, name + '.json'), 'w'), sort_keys=True)
open(os.path.join(out, name + '.txt'), 'w').write(K.summary(res))
print(K.summary(res))
for r in res['rows']:
    for s in r['spawns']:
        e = r['ents'].get(str(s['ret']) if False else s['ret'])
        print('  tick', r['t'], 'spawn ret slot', s['ret'], 'x px %.2f y px %.2f' % ((s['args'][3] if s['args'][3] < 2 ** 31 else s['args'][3] - 2 ** 32) / 65536, s['args'][4] / 65536),
              'ground word of the newborn (end of tick):', (e or {}).get('ground'), 'status', (e or {}).get('status'), 'z', (e or {}).get('z'))
