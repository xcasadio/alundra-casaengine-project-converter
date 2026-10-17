import json, glob, os, sys
# usage: v_map.py MAPID COL0 COL1 ROW0 ROW1
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
mid = sys.argv[1]
c0, c1, r0, r1 = [int(a) for a in sys.argv[2:6]]
for f in glob.glob(os.path.join(root, '*', '*-' + mid, 'tilemap', '*.tileMap')):
    d = json.load(open(f, encoding='utf-8'))
    c = json.loads(d['custom_properties']['AlundraCells'])
    w = c['walkability']; h = c['height']; off = c['wall_tiles_offset']
    W = 52
    print(f)
    cut = [(i % W, i // W, h[i], off[i]) for i, v in enumerate(w) if v & 2]
    print('cuttable', len(cut))
    sel = sorted([t for t in cut if c0 <= t[0] <= c1 and r0 <= t[1] <= r1], key=lambda t: (t[1], t[0]))
    print('window', (c0, c1, r0, r1), '->', sel)
    # height map of window
    for r in range(r0, r1 + 1):
        print('%2d' % r, ' '.join('%2d%s' % (h[r * W + x], '*' if w[r * W + x] & 2 else ' ') for x in range(c0, c1 + 1)))
