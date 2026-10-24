"""scan4.py - placements (raw data-extracted maps) of the bomb-reactive global sprites: 253 'Racine destructible a la bombe' (native D32), 241/242/243 lids (map programs with 0x87 [.,3]), plus the map-page sprites whose header D code (native) tests the bomb nibble (none found by dscan)."""
import sys, json, glob, re, collections
sys.stdout.reconfigure(encoding='utf-8')
ROOT = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
res = collections.defaultdict(list)
for f in glob.glob(ROOT + "/map_*.json"):
    mm = re.search(r'map_(\d+)\.json$', f.replace(chr(92), '/'))
    if not mm: continue
    mid = int(mm.group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    for i, e in enumerate(d['SpriteInfo']['Entities']['Entities']):
        if not e or not e.get('IsEnabled'): continue
        if e['SpriteDirection'] & 0x80: continue
        t = e['SpriteTableIndex']
        if t in (253, 241, 242, 243):
            res[t].append((mid, i, e['XPos'], e['YPos'], e['Height'], e['EventCodesD_TouchIndex']))
for t in sorted(res):
    maps = sorted(set(x[0] for x in res[t]))
    print('sprite', t, 'placements', len(res[t]), 'maps', len(maps), maps)
chain = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
for t in sorted(res):
    c = [x for x in res[t] if x[0] in chain]
    print('  on the story chain:', t, c)
