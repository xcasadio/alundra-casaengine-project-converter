import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census_recipe as cr, decode_events as de
files = cr.tilemaps()
res = []
for m in sorted(files):
    rows = [cr.row(p, h) for p, h in cr.placements(files[m])]
    lifts = [r for r in rows if r['kind'] and r['hdr'][1] == 23 and r['en']]
    plats = [r for r in rows if cr.PLAT.search(r['name'] or '') and r['en']]
    if not lifts or not plats: continue
    doc = de.load_map(m)
    if doc is None: continue
    for r in plats:
        for slot, ix in (('C', 2), ('A', 0)):
            c = cr.classify(doc, slot, r['prog'][ix], None)
            if c and c['walk']:
                res.append((m, r['idx'], r['name'], slot, r['prog'][ix], (r['x'], r['y'], r['z']), len(lifts), c))
for m, idx, nm, slot, raw, pos, nl, c in res:
    print('map %d ent %d %s prog %s%d(raw %d) pos %s liftables %d walk %s gates %s' % (m, idx, nm, slot, raw & 0x7f, raw, pos, nl, c['walk'], c['gates']))
