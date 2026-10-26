"""replay_e19.py - E20.h3a T0 (a): replays goldens_e19.json through a MODEL written for the slice (E19 = sound 0xE6, then DestroyEntity(E, -1); no data word read, none written),
from an EMPTY memory: any read of a word is an error. usage (from a copy of critic/run): python -B replay_e19.py <goldens_e19.json>"""
import json, sys
M32 = 0xFFFFFFFF


def model_E19(E, J, mem_read):
    J.append(['sound', 0xE6])
    J.append(['destroy2', E, M32])          # DestroyEntity(E, -1)


g = json.load(open(sys.argv[1]))
bad = 0; reads = 0
for v in g['vectors']:
    J = []
    E = int(v['self'], 16)
    model_E19(E, J, None)
    ok = (J == v['journal']) and not v['reads'] and not v['writes']
    if not ok:
        bad += 1; print('DEVIATION', v['self'], J, v['journal'], v['reads'], v['writes'])
    reads += len(v['reads'])
print('vectors', len(g['vectors']), 'differences', bad, 'words read by the real code', reads)
sys.exit(1 if bad else 0)
