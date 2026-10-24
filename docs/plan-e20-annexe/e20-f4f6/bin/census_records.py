"""Census of map entity records that instantiate GLOBAL-page sprites 3..15 and 27..29 (record byte 5 bit 0x80 clear = page 0, byte 6 = index).
Binary rule: 0x8003A1B8 (record loader) a0 = rec[5] & 0x80 -> 0x80039B28(page, rec[6]); page 0 = global block 0x80126EC0.
"""
import json, os, sys, glob
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
interest = set(list(range(1, 20)) + [27, 28, 29])
out = []
tot = 0
for n in range(0, 483):
    p = os.path.join(D, 'map_%d.json' % n)
    if not os.path.exists(p):
        continue
    d = json.load(open(p, encoding='utf-8-sig'))
    si = d.get('SpriteInfo') or {}
    ents = ((si.get('Entities') or {}).get('Entities')) or []
    for i, e in enumerate(ents):
        if not e: continue
        tot += 1
        if (e['SpriteDirection'] & 0x80) == 0 and e['SpriteTableIndex'] in interest and e['IsEnabled']:
            out.append((n, i, e['SpriteTableIndex'], e['SpriteDirection'], e['XPos'], e['YPos'], e['Height'], e['EventCodesA_LoadIndex'], e['EventCodesB_MapIndex'], e['EventCodesC_TickIndex'], e['EventCodesD_TouchIndex'], e['EventCodesE_DeactivateIndex'], e['EventCodesF_InteractIndex'], e['Contents']))
print('records scanned', tot)
print('global-page records with index in %s: %d' % (sorted(interest), len(out)))
print('map rec idx dir XPos YPos H A B C D E F Contents')
for r in out:
    print(*r)
