"""portals.py DEST... : every portal (all maps) whose DestMapId is in DEST, plus the portals of the DEST maps themselves. READ-ONLY."""
import sys, glob, json, re
ROOT = 'D:/development/repo/alundra-casaengine-project-converter'
dest = set(int(a) for a in sys.argv[1:])
for f in sorted(glob.glob(ROOT + '/alundra-project/Maps/*/*/tilemap/*.tmj')):
    f = f.replace(chr(92), '/')
    mm = re.search(r'-(\d+)\.tmj$', f)
    if not mm: continue
    src = int(mm.group(1))
    tm = json.load(open(f, encoding='utf-8-sig'))
    for l in tm['layers']:
        if l['name'] != 'Portals': continue
        for o in l.get('objects', []):
            p = {q['name']: q['value'] for q in o.get('properties', [])}
            if int(p.get('DestMapId', -1)) in dest or src in dest:
                print('src %3d portal %s (%s,%s)-(%s,%s) -> map %s tile (%s,%s) z %s flags %s' % (src, p.get('Index'), p.get('X1'), p.get('Y1'), p.get('X2'), p.get('Y2'), p.get('DestMapId'), p.get('DestTileX'), p.get('DestTileY'), p.get('ZLevel'), p.get('Flags')))
