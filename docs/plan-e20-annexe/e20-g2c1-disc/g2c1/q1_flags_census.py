"""q1_flags_census.py - which sprite headers (map pages) carry DestroyOnSlidingSlope (0x100000) / DestroyOnVramFlags (0x200000) with the Gravity bit (0x100): who the feed of +0x18C / +0x180 / +0x184
would act on. flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16 (EntityFlags.cs doc, binary 0x80039DA4-0x80039DD0). READ-ONLY.
Run: python -B -I -X utf8 q1_flags_census.py"""
import glob, json, re, collections
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
seen = {}
placed = collections.Counter()
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if e is None or not e['IsEnabled']:
            continue
        i = e['SpriteTableIndex']
        r = recs[i] if i < len(recs) else None
        if r is None:
            continue
        h = r['Header']
        key = (i, h['ProgramTick'], h['ProgramDeactivate'])
        fl = (h['MoreFlags'] & 0xFF) | ((h['CanPickup'] & 0xFF) << 8) | ((h.get('FlagsPortraitShadowType', 0) & 0xFF) << 16)
        seen[i] = (fl, h['ProgramTick'], h['ProgramDeactivate'])
        placed[i] += 1
print('keys of one header:', sorted(h.keys()))
def show(mask, label):
    sel = sorted(i for i, (fl, c, e) in seen.items() if fl & mask)
    print(label, 'sprite types (map pages):', len(sel), 'placements:', sum(placed[i] for i in sel))
    withg = [i for i in sel if seen[i][0] & 0x100]
    print('   of which with Gravity 0x100:', len(withg), 'placements', sum(placed[i] for i in withg))
    print('   with Gravity, C4 family (C4):', [i for i in withg if seen[i][1] == 4], ' C23:', [i for i in withg if seen[i][1] == 23], ' C70:', [i for i in withg if seen[i][1] == 70])
    print('   others with Gravity (index, C, E, flags):', [(i, seen[i][1], seen[i][2], hex(seen[i][0])) for i in withg if seen[i][1] not in (4, 23, 70)][:40])
show(0x100000, 'DestroyOnSlidingSlope 0x100000')
show(0x200000, 'DestroyOnVramFlags    0x200000')
