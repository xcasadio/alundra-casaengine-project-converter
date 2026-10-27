"""Census of belt (current) cells and map header bytes over the 483 maps of data-extracted (a copy of the remaster extraction).
Cell word = Walk | GP<<8 | Slope<<16 | Height<<24 (MapTile.Flags). Belt = Slope & 0xC0 != 0.
Output: out_census_belts.txt (per map line) + summary."""
import json, os, sys, collections
ROOT = r'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
OUT = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2/out_census_belts.txt'

def info_of(n):
    # header bytes only: stream-read map_N.json 'Info' without loading bitmaps is hard; just json.load
    with open(os.path.join(ROOT, 'map_%d.json' % n), 'r', encoding='utf-8-sig') as f:
        m = json.load(f)
    i = m['Info']
    return {k: i[k] for k in ('Gravity', 'ZViscosity', 'SlideEffectId', 'BalanceLevel')}

lines = []
tot = collections.Counter()
slide_maps = {}
belt_maps = {}
for n in range(0, 483):
    p = os.path.join(ROOT, 'tiled', 'map_%d.alundra.json' % n)
    if not os.path.exists(p):
        continue
    with open(p, 'r', encoding='utf-8-sig') as f:
        d = json.load(f)
    cells = d['Cells']
    belt = [c for c in cells if c['Slope'] & 0xC0]
    try:
        inf = info_of(n)
    except Exception as e:
        inf = {'err': str(e)}
    if inf.get('SlideEffectId'):
        slide_maps[n] = inf
    if not belt:
        continue
    cnt = collections.Counter()
    for c in belt:
        s = c['Slope']
        gp = c['GroundProperty']
        deep = (gp & 0xE) == 8          # (cell word >> 8) & 0xE00 == 0x800 -> gp & 0xE == 8
        lock = bool(s & 0x80)
        direc = (s >> 4) & 3
        gate = (s >> 2) & 3            # 0x04 level-matched, 0x08 not matched
        cnt['belt'] += 1
        cnt['deep' if deep else 'dry'] += 1
        cnt['lock' if lock else 'nolock'] += 1
        cnt['dir%d' % direc] += 1
        cnt['gate%d' % gate] += 1
        if deep and lock:
            cnt['deep_lock'] += 1
        if deep and not lock:
            cnt['deep_nolock'] += 1
        if not deep and lock:
            cnt['dry_lock'] += 1
        cnt['gp%d' % gp] += 1
    belt_maps[n] = cnt
    lines.append('map %3d  %s  %s' % (n, json.dumps(inf), ' '.join('%s=%d' % kv for kv in sorted(cnt.items()))))
    tot.update(cnt)
with open(OUT, 'w') as f:
    f.write('\n'.join(lines) + '\n')
    f.write('\nSlideEffectId != 0: %s\n' % json.dumps(slide_maps))
    f.write('\nbelt maps: %d  %s\n' % (len(belt_maps), sorted(belt_maps)))
    f.write('totals: %s\n' % dict(tot))
print(open(OUT).read()[-6000:])
