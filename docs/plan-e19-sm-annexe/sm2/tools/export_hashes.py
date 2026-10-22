"""sha256 of the exported files the sm2 real-bytes tests read (events.json) and of the tileMaps the arcs would load. python -B export_hashes.py"""
import hashlib, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, tilemap2 as T

for m in (55, 56, 57, 59, 60, 307, 344, 442):
    base, folder = C.paths(m)
    ev = folder + '/events/' + base + '.events.json'
    tm = T.tilemap_path(m)
    data = open(ev, 'rb').read()
    print('%d\tevents.json\t%s\t%d\t%s' % (m, hashlib.sha256(data).hexdigest(), len(data), ev.split('/Maps/')[1]))
    print('%d\ttileMap\t%s\t%d' % (m, hashlib.sha256(open(tm, 'rb').read()).hexdigest(), os.path.getsize(tm)))
idx = open(C.PROJECT + '/Maps/world-index.json', 'rb').read()
print('world-index.json\t%s\t%d' % (hashlib.sha256(idx).hexdigest(), len(idx)))
