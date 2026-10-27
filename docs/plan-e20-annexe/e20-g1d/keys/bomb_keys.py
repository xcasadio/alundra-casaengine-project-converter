"""E20.g1d T0 (c): bomb_keys. Derived from the discovery's bomb_keys.py (docs/plan-e20-annexe/e20-g1d-disc/carried-draw/bomb_keys.py): the same scenario
(scen_bomb1: Circle at tick 1 with the item 0x23, the bomb is born in the hands, lifted to Z 32, carried), but EVERY tick is printed in the common format.
Oracle: copy of docs/plan-e20-annexe/e20-h3/critic/run in $G1D_ORC/oracle_h3. After each tick the binary's sort driver 0x800399B8 is run."""
import os
import sys
import keyrows as K
sys.path.insert(0, os.path.join(K.ORC, 'oracle_h3'))
from scen_lib import *  # noqa: E402,F401,F403

rows = []


def on_tick(m, t, rec):
    W.run_pass(m, 0x800399B8)
    bombs = [i for i, e in rec['ents'].items() if e['sp'] == 2 and e['st'] in (1, 2, 3)]
    if not bombs:
        return
    rows.append((t, K.fields(m, HERO), K.fields(m, W.ent(bombs[0]))))


m, STATS = mk(0x23)
run(m, 120, lambda t: W.CIRCLE if t == 1 else 0, on_tick=on_tick)
print(K.HEADER)
for t, h, o in rows:
    print(K.line('bomb_keys', t, h, o))
