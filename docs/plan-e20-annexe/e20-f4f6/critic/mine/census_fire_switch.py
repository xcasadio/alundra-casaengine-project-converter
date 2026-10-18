"""Critic census: map programs that test the attack type of the toucher (opcode 0x87 [search, nibble]) with the fire (6) or ice (7) nibble.
Linear byte scan of EventCodes.Codes (false positives possible: report as candidates).  python -B -I census_fire_switch.py"""
import json, glob, re
D = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/'
res = {}
for p in glob.glob(D + 'map_*.json'):
    m = re.search(r'map_(\d+)\.json$', p)
    if not m:
        continue
    idx = int(m.group(1))
    d = json.load(open(p, encoding='utf-8'))
    codes = [c & 0xFF for c in d['SpriteInfo']['EventCodes']['Codes']]
    ents = d['SpriteInfo']['Entities']['Entities']
    for nib in (6, 7, 9, 10):
        hits = [i for i in range(len(codes) - 2) if codes[i] == 0x87 and codes[i + 1] == 0x80 and codes[i + 2] == nib]
        if hits:
            res.setdefault(nib, []).append((idx, hits))
for nib in sorted(res):
    print('nibble', nib, 'maps', len(res[nib]), [(i, len(h)) for i, h in res[nib]][:40])
