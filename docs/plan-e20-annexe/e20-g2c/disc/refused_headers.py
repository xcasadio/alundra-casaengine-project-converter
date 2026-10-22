"""refused_headers.py - O-G-10: which liftable headers does the REAL record spawner 0x8003A1B8 refuse in the oracle with the hero at cell (4,4), and why (the zone test)?
For every liftable header family (the 17 of g1a) and the torch: the first placement of each (map, sprite) is spawned as K.spawn_record_entities does (ents[0] = template, zone from the record).
Then the same placement with the hero moved to the centre of its zone: accepted?  Run: python -B -I -X utf8 refused_headers.py"""
import os, sys, json, re, glob
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
import gl
import k0lib as K
R = "D:/development/repo/alundra-casaengine-project-converter"
fams = {}
for f in sorted(p for p in glob.glob(R + '/data-extracted/data/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None: continue
        h = r['Header']
        kind = (h['CanPickup'] >> 1) & 3
        if kind or h['ProgramTick'] == 70:
            k = (i, h['ProgramTick'], h['ProgramDeactivate'], h['MoreFlags'], h['Contents'], kind)
            fams.setdefault(k, []).append((n, e))
out = []
for k, lst in sorted(fams.items()):
    maps = sorted(set(n for n, _ in lst))
    if k[0] not in (34, 37, 107, 111, 112, 113, 117, 137): maps = maps[:10]
    # the spawner takes ents[0] of the map; test each map's first placement, report refusals
    res = []
    for n in maps:
        key = ('map_%d' % n, k[0])
        K._CACHE.pop(key, None)
        try:
            m = gl.world(hero_px=(100, 100), direction=24)
            K.spawn_record_entities(m, 'map_%d' % n, k[0], [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
            res.append((n, True, None))
        except RuntimeError:
            e0 = [e for e in K._record('map_%d' % n, k[0])[1]][0]
            zone = (e0['XMin'], e0['XMax'], e0['YMin'], e0['YMax'])
            cx, cy = (zone[0] + zone[1]) // 2, (zone[2] + zone[3]) // 2
            # hero moved to the centre of the zone (cell -> pixel, 24 px cells)
            K._CACHE.pop(key, None)
            try:
                m = gl.world(hero_px=(cx * 24 + 12, cy * 24 + 12), direction=24)
                K.spawn_record_entities(m, 'map_%d' % n, k[0], [(10, 11, 0)], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
                ok2 = True
            except RuntimeError:
                ok2 = False
            res.append((n, False, (zone, ok2)))
    refused = [(n, z) for n, ok, z in res if not ok]
    out.append('sprite %3d C%d E%d MF=0x%02X Contents=%d kind=%d : %d placements on %d maps ; first placement refused (hero at cell 4,4) on maps %s' % (
        k[0], k[1], k[2], k[3], k[4], k[5], len(lst), len(maps), [(n, 'zone x%d..%d y%d..%d' % (z[0][0], z[0][1], z[0][2], z[0][3]), 'accepted with the hero in the zone' if z[1] else 'STILL REFUSED') for n, z in refused][:8]))
    print(out[-1], flush=True)
open(os.path.join(HERE, 'refused_headers.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
