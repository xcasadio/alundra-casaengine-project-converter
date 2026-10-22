"""cell_census.py - which cell words (walkability | ground_property << 8) the exported maps use, and what slope/word combos the C4 maps have.
Run: python -B -I -X utf8 cell_census.py"""
import json, glob, os, re, collections
root = "D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps"
words = collections.Counter()
slope4 = collections.Counter()
w1001 = collections.Counter()
per_map = {}
for f in glob.glob(root + "/*/*/tilemap/*.tileMap"):
    f = f.replace("\\", "/")
    folder = os.path.basename(os.path.dirname(os.path.dirname(f)))
    mm = re.search(r"-(\d+)$", folder)
    if not mm: continue
    mid = int(mm.group(1))
    tm = json.load(open(f, encoding='utf-8'))
    cells = json.loads(tm['custom_properties']['AlundraCells'])
    n = len(cells['walkability'])
    c1001 = 0
    for i in range(n):
        wk, gp, sl = cells['walkability'][i], cells['ground_property'][i], cells['slope'][i]
        w = wk | (gp << 8)
        words[w] += 1
        if sl == 4:
            slope4[w] += 1
        if (w & 0x1001) == 0x1001:
            c1001 += 1
            w1001[(w, sl)] += 1
    per_map[mid] = c1001
print('distinct cell words (count of cells over all maps), top 25:')
for w, c in words.most_common(25):
    print('  0x%04X : %d%s' % (w, c, '   <== (w & 0x1001) == 0x1001' if (w & 0x1001) == 0x1001 else ''))
print('words of slope-4 cells:', [('0x%04X' % w, c) for w, c in slope4.most_common(10)])
print('cells with (w & 0x1001) == 0x1001 by (word, slope):', [(('0x%04X' % w, s), c) for (w, s), c in w1001.most_common(15)])
print('maps with such cells:', sum(1 for v in per_map.values() if v), 'of', len(per_map))
for m in (22, 27, 29, 30, 70, 72, 73, 79, 86, 87, 113, 140, 158, 400, 441):
    print('  map', m, 'cells with word&0x1001==0x1001:', per_map.get(m))
