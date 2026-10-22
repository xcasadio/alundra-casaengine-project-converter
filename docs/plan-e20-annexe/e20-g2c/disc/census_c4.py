"""census_c4.py - read-only census of the C4 family (ProgramTick == 4 with a pickup kind) and of the torches (C70/E20) over data-extracted/data/map_*.json.
Run: python -B -I census_c4.py"""
import json, glob, re, collections, os, sys
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
HERE = os.path.dirname(os.path.abspath(__file__))
fam = collections.defaultdict(lambda: collections.Counter())      # header key -> map -> placements
hdrs = {}
per_sprite_maps = collections.defaultdict(lambda: collections.defaultdict(list))
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if e is None: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None: continue
        h = r['Header']
        kind = (h['CanPickup'] >> 1) & 3
        if h['ProgramTick'] in (4, 70) and (kind or h['ProgramTick'] == 70 or True):
            if h['ProgramTick'] == 4 or h['ProgramTick'] == 70:
                key = (i, h['ProgramTick'], h['ProgramDeactivate'], h['ProgramLoad'], h['ProgramTouch'], h['ProgramInteract'], h['MoreFlags'], h['CanPickup'], h['BreakEffect'], h['Contents'], (h['SizeX'], h['SizeY'], h['SizeZ']), (h['OffsetX'], h['OffsetY'], h['OffsetZ']))
                hdrs[key] = hdrs.get(key, 0)
                fam[key][n] += 1 if e['IsEnabled'] else 0
                per_sprite_maps[i][n].append((e['IsEnabled'], e.get('XPos'), e.get('YPos'), e.get('Height')))

tot = 0
out = []
for key, c in sorted(fam.items(), key=lambda kv: (kv[0][1], kv[0][0])):
    i, tick, deact, load, touch, inter, mf, cp, brk, cont, size, off = key
    n = sum(c.values()); tot += n
    out.append('sprite %3d  C%d E%d Load%d Touch%d Inter%d MoreFlags=0x%02X CanPickup=0x%X(kind %d) Break=%d Contents=%d size=%s off=%s : %d enabled placements on %d maps %s' % (i, tick, deact, load, touch, inter, mf, cp, (cp >> 1) & 3, brk, cont, size, off, n, len(c), sorted(c)))
print('\n'.join(out))
open(os.path.join(HERE, 'census_c4.txt'), 'w').write('\n'.join(out) + '\n')
# totals C4 vs C70
for tick in (4, 70):
    print('C%d total enabled placements' % tick, sum(sum(c.values()) for k, c in fam.items() if k[1] == tick))
