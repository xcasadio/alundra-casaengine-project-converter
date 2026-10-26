"""overlap.py - which water drops fall onto a torch at its record position?  XY boxes from the sprite headers: torch Offset(-10,-7) Size(21,15); drop Offset(-2,-1) Size(4,3); positions x = XPos*12+12, y = YPos*8+8
(record positions; the drops born by script 0x8A at the positions of the opcode: 92 B[1] x4 at (756..900, 168)).  Overlap rule of EntitySupport.TryFindSupport (asymmetric, simplified here to the closed interval overlap of the two boxes).
Run: python -B -I -X utf8 overlap.py"""
import json
R = r"D:/development/repo/alundra-casaengine-project-converter"
TORCH = (-10, -7, 21, 15); DROP = (-2, -1, 4, 3)
def overlap(a, pa, b, pb):
    ax0, ay0 = pa[0] + a[0], pa[1] + a[1]; ax1, ay1 = ax0 + a[2], ay0 + a[3]
    bx0, by0 = pb[0] + b[0], pb[1] + b[1]; bx1, by1 = bx0 + b[2], by0 + b[3]
    return ax0 < bx1 and bx0 < ax1 and ay0 < by1 and by0 < ay1
for n in (87, 88, 90, 92, 94):
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    drops, torches = [], []
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled']: continue
        i = e['SpriteTableIndex']; r = si['SpriteRecords'][i] if i < len(si['SpriteRecords']) else None
        if r is None: continue
        h = r['Header']; p = (e['XPos'] * 12 + 12, e['YPos'] * 8 + 8)
        if i == 135 and h['ProgramTick'] == 71: drops.append((k, p, e['SpriteDirection']))
        if i == 137 and h['ProgramTick'] == 70: torches.append((k, p, e['SpriteDirection']))
    if n == 92:   # the four drops born by 92 B[1]: 0x8A rec 15 at (756,168) (804,168) (852,168) (900,168)
        drops = [x for x in drops if x[0] != 15] + [('15@%d' % (i + 1), (756 + 48 * i, 168), 128) for i in range(4)]
    hits = [(dk, tk) for dk, dp, dd in drops for tk, tp, td in torches if overlap(DROP, dp, TORCH, tp)]
    print('map %d: drops that fall on a torch at its record position (drop rec, torch rec): %s' % (n, hits))
