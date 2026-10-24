import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census_recipe as cr, decode_events as de
files = cr.tilemaps()
m = int(sys.argv[1])
doc = de.load_map(m)
rows = [cr.row(p, h) for p, h in cr.placements(files[m])]
props = {int(p['Index']): p for p, h in cr.placements(files[m])}
for r in rows:
    if not cr.PLAT.search(r['name'] or '') or not r['en']: continue
    p = props[r['idx']]
    print('== ent %d %s pos %s size %s SpriteDirection=%s (facing %d) ABCDEF=%s' % (r['idx'], r['name'], (r['x'], r['y'], r['z']), (r['sx'], r['sy'], r['sz']), p['SpriteDirection'], int(p['SpriteDirection']) & 3, r['prog']))
    seen = set()
    for slot, ix in (('A', 0), ('C', 2)):
        if (r['prog'][ix] & 0x7f) == 0: continue
        c = cr.prog_of(doc, slot, r['prog'][ix])
        if c is None or (slot, c[0]) in seen: continue
        seen.add((slot, c[0]))
        print('  %s[%d] @%d..%d' % (slot, r['prog'][ix] & 0x7f, c[0], c[1]))
        for pc, op, v in c[2]:
            print('     @%d 0x%02X %-34s %s' % (pc, op, de.SIZES.get(op, (0, '?'))[1], list(v[1:])))
