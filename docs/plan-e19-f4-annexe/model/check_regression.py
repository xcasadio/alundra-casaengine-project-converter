"""(1) my rig-free runner with the annex F4Box reproduces the annex values.json (7 scenarios, every column);
(2) f4_model_h.F4BoxH with h = 56 reproduces it too (the extension is a pure generalisation)."""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C
import f4_model as F
import f4_model_h as FH

E = C.entity
SCEN = [
    ('S1', [(0x0D, E(0x104, True), b'AB', True, None)], 60),
    ('S2', [(0x0D, E(0xFF, True), b'AB', True, None)], 60),
    ('S3', [(0x0D, E(0x10C, False), b'AB', True, None)], 60),
    ('S4', [(0x0D, E(0xFF, False), b'AB', True, None)], 60),
    ('S5', [(0x0D, E(0x104, True), b'AB', True, None), (0x0D, E(0x104, True), b'CD', True, None)], 110),
    ('S6', [(0xC4, E(0x104, True), b'AB', True, 0x10C)], 60),
    ('S7', [(0x5C, E(0x104, True), b'AB', False, None)], 60),
]
annex = json.load(open(os.path.join(HERE, '..', 'values.json')))
titles = list(annex)


def same(a, b):
    return json.dumps(a, sort_keys=True) == json.dumps(b, sort_keys=True)


bad = 0
for box_cls in (F.F4Box, FH.F4BoxH):
    for (name, d, frames), title in zip(SCEN, titles):
        assert title.startswith(name), (title, name)
        rows = C.run(d, frames=frames, box_cls=box_cls)
        ref = annex[title]
        for r, a in zip(rows, ref):
            mine = dict(frame=r['frame'], rel=r['frame'] - 3, phase=r['phase'], box_y=r['y'], name=r['nb'], portrait=r['pt'], events=r['events'])
            # json turns tuples into lists: normalise
            mine = json.loads(json.dumps(mine))
            if not same(mine, a):
                bad += 1
                print('DIFF', box_cls.__name__, name, r['frame'], mine, a)
                break
    print(box_cls.__name__, 'done')
print('mismatching scenarios:', bad)
