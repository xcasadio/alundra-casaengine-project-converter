"""E20.g1d T0 (d) P4: the maximum default draw order of the pieces of the hero and of the 17 liftable records (D-G1D-4: K = max(14, maximum + 1)).
Read-only. The liftable records are those of alundra-project/Data/sprite-records.json with a pickup kind != 0 (as docs/plan-e20-annexe/e20-g1d-disc/carried-draw/liftables_survey.py);
the files of a record are Entities/*/bank<BankKey>_anim*.anim2d (alundra_N -> bankalundra_N, N -> bankN). Usage: python -B p4_draworder.py <project root>"""
import glob
import json
import sys

root = sys.argv[1] if len(sys.argv) > 1 else 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
records = json.load(open(root + '/Data/sprite-records.json', encoding='utf-8'))


def scan(pattern):
    files = sorted(glob.glob(root + '/Entities/' + pattern))
    mx, mn, track_props = -1, 10 ** 9, set()
    for f in files:
        d = json.load(open(f, encoding='utf-8'))
        vals = [p.get('default_draw_order', 0) for p in d.get('parts', [])]
        for t in d.get('tracks', []):
            track_props.add(t.get('property'))
            for k, v in t.items():
                if k.endswith('_keyframes') and 'draw' in k.lower():
                    vals += [kf['value'] for kf in v]
        if vals:
            mx, mn = max(mx, max(vals)), min(mn, min(vals))
    return len(files), mn, mx, sorted(p for p in track_props if p)


n, mn, mx, props = scan('Alundra/bankalundra_0_anim*.anim2d')
print('hero: files %d min %d max %d tracks %s' % (n, mn, mx, props))
overall = mx
seen = set()
rows = []
for gid, r in records.items():
    flags = r['MoreFlags'] | (r['CanPickup'] << 8) | (r['FlagsPortraitShadowType'] << 16)
    if (flags & 0x600) >> 9 == 0:
        continue
    bk = r['BankKey']
    if bk in seen:
        continue
    seen.add(bk)
    rows.append((bk, flags))
rows.sort(key=lambda x: (not x[0].startswith('alundra_'), x[0]))
liftable_max = -1
for bk, flags in rows:
    stem = 'bank' + bk if bk.startswith('alundra_') else 'bank' + bk
    n, mn, mx2, props = scan('*/' + stem + '_anim*.anim2d')
    liftable_max = max(liftable_max, mx2)
    print('liftable %-12s collidable=%-5s files %3d draw order min %d max %d' % (bk, bool(flags & 0x80), n, mn if n else -1, mx2))
print('liftable records: %d ; maximum over them: %d ; hero maximum: %d ; K = max(14, %d) = %d' % (len(rows), liftable_max, overall, liftable_max + 1, max(14, liftable_max + 1)))
