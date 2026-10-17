import json, collections, sys
f = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps\Overworld\Overworld 3,1-14\tilemap\Overworld 3,1-14.tileMap"
d = json.load(open(f, encoding='utf-8'))
cp = d.get('custom_properties') or d.get('CustomProperties')
print('cp keys', sorted(cp.keys())[:20])
c = json.loads(cp['AlundraCells'])
print('cells keys', list(c.keys()))
w = c['walkability']; h = c['height']
W = 52
print('len', len(w))
cut = [(i % W, i // W, h[i]) for i, v in enumerate(w) if v & 2]
print('cuttable', len(cut))
rows = collections.Counter(r for _, r, _ in cut)
print('min row', min(rows), 'rows', sorted(rows.items()))
# cells in a window
def win(c0, c1, r0, r1):
    return sorted([(x, y, hh) for x, y, hh in cut if c0 <= x <= c1 and r0 <= y <= r1], key=lambda t: (t[1], t[0]))
print('cols 0..20 rows 0..15:', win(0, 20, 0, 15))
print('cols 5..12 rows 0..20:', win(5, 12, 0, 20))
print('cols 3..12 rows 20..30:', win(3, 12, 20, 30))
print('near swim col 36..44 rows 44..52:', win(36, 44, 44, 52))
