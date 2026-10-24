"""rest_census.py - (ADAPTED to C23) C4 placements (and the 49 native torches) of data-extracted, each spawned by the REAL record spawner at its REAL position in a world with the REAL cells
of its map (exported tilemap), run NTICK ticks with C4 / C70+E20 REAL: does anything change at rest (animation, status, journal)?  Hero stays at (100,100) far from the object.
Run: python -B -I -X utf8 rest_census.py [limit]"""
import os, sys, json, re, glob, collections
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
import gl
import k0lib as K
from machine import M32
gl.CFIELDS.extend([('slope18C', 0x18C, 1)])
R = "D:/development/repo/alundra-casaengine-project-converter"
C4, E0, C70, E20 = 0x80066984, 0x8007ED10, 0x8007B7B0, 0x8007F6C8
NTICK = 6
limit = int(sys.argv[1]) if len(sys.argv) > 1 else 10 ** 9
todo = []
for f in sorted(p for p in glob.glob(R + '/data-extracted/data/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None: continue
        h = r['Header']
        if h["ProgramTick"] not in (23,) or not ((h['CanPickup'] >> 1) & 3): continue
        if any(e[k2] for k2 in ('EventCodesA_LoadIndex', 'EventCodesC_TickIndex', 'EventCodesE_DeactivateIndex')): continue
        todo.append((n, k, i, e))
res = collections.Counter(); detail = []
cache_cells = {}
for (n, k, i, e) in todo[:limit]:
    try:
        if n not in cache_cells:
            cache_cells[n] = K.__dict__.get('cells_from_export') and None
        import gl as _g
        f_, meta = K.cells_from_export(n)
        cells = lambda col, row, f_=f_: f_(col, row)
    except KeyError:
        cells = None
    keep = {0x8006B848, 0x8007ED30}
    m = gl.world(keep=keep, cells=cells, hero_px=(100, 100))
    key = ('map_%d' % n, i)
    K._CACHE.pop(key, None)
    rec_, ents_, ts_ = K._record('map_%d' % n, i)
    K._CACHE[key] = (rec_, [dict(e, XMin=0, YMin=0, XMax=255, YMax=255)] + ents_[1:], ts_)
    slots, info = K.spawn_record_entities(m, 'map_%d' % n, i, [(e['XPos'], e['YPos'], e['Height'])], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    s = slots[0]; en = K.ent(s)
    before = (m.peek(en + 0x88), m.peek(en + 0x10), m.peeks(en + 0x11C), m.peek(en + 0x6C))
    pads, nn = gl.pad_script([(NTICK, 0)])
    rows = gl.run(m, nn, pads, crates=[s])
    last = rows[-1]['c'][s]
    j = [x for r in rows for x in r['journal'] if not x.startswith('stub')]
    after = (last['anim_t'], last['status'], last['pz'], last['flags6C'])
    changed = (before[0] != after[0]) or (before[1] != after[1]) or (before[3] != after[3]) or j
    res['changed' if changed else 'inert'] += 1
    if changed:
        detail.append('map %d rec %d sprite %d at (%.1f,%.1f,z %.1f): anim %d->%d status %d->%d z %.1f->%.1f slope18C %d ground %d journal %s' % (
            n, k, i, last['px'] / 65536, last['py'] / 65536, before[2] / 65536, before[0], after[0], before[1], after[1], before[2] / 65536, after[2] / 65536, last['slope18C'], last['ground'], j[:4]))
print(dict(res), 'of', min(limit, len(todo)))
for d in detail[:40]:
    print('  ', d)
open(os.path.join(HERE, 'q1_rest_c23.txt'), 'w', encoding='utf-8').write(str(dict(res)) + '\n' + '\n'.join(detail) + '\n')
