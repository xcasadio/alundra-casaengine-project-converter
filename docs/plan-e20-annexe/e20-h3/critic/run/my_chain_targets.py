"""my_chain_targets.py - critic: on the 30 story-chain maps, which entities can be touched by the bomb explosion (attack attribute 4)?  An entity is a candidate when its sprite flags have ClassA (0x01) or ClassB (0x08) and its balance sheet answer to type 4 (DamageResponses[3]) is not 0x80/0xC0."""
import json, collections
from my_maps import DATA
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
BAL = json.load(open(DATA + 'BALANCE.BIN.json', encoding='utf-8-sig'))['EntriesBySpriteIndex']
G = json.load(open(DATA + 'map_alundra.json', encoding='utf-8-sig'))
gl = {x['Header']['Sector5Id']: x for x in G['SpriteInfo']['SpriteRecords'] if x}
def flags(h): return h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
out = collections.defaultdict(list)
for mid in CHAIN:
    d = json.load(open(DATA + 'map_%d.json' % mid, encoding='utf-8-sig'))
    recs = d['SpriteInfo']['SpriteRecords']
    for i, e in enumerate(d['SpriteInfo']['Entities']['Entities']):
        if not e: continue
        mapPage = (e['SpriteDirection'] & 0x80) != 0
        idx = e['SpriteTableIndex']
        rec = recs[idx] if mapPage else gl.get(idx)
        if not rec: continue
        fl = flags(rec['Header'])
        sheet = 256 + idx if mapPage else idx
        if sheet > 511: continue
        resp = BAL[sheet]['DamageResponses']
        r4 = resp[3] if len(resp) > 3 else 0
        if (fl & 0x09) and (r4 & 0xC0) != 0x80 and (r4 & 0xC0) != 0xC0:
            out[mid].append((i, 'map' if mapPage else 'glob', idx, hex(fl), 'resp4=%d' % r4, 'maxhp=%d' % BAL[sheet]['MaxHp'], 'D=%d' % e['EventCodesD_TouchIndex']))
for mid in CHAIN:
    if out[mid]:
        print(mid, len(out[mid]), out[mid][:6])
print('maps with candidates:', sorted(out))
