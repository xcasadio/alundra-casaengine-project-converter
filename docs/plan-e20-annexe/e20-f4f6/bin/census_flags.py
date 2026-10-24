"""Which sprite headers carry flags 0x100000 (DestroyOnSlidingSlope: pick rule +0x18C == 4 -> DestroyEntity(e,6)) and 0x200000 (DestroyOnVramFlags: pick rule (+0x180 & 0x8004) -> DestroyEntity(e,-1)).
flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16 (header u16@0x10 | u8@0x12 << 16)."""
import json, os, collections
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
res = {0x100000: collections.defaultdict(set), 0x200000: collections.defaultdict(set)}
codes = {}
def scan(page, n, recs):
    for i, r in enumerate(recs):
        if not r:
            continue
        h = r['Header']
        f = h['MoreFlags'] | (h['CanPickup'] << 8) | (h['FlagsPortraitShadowType'] << 16)
        for bit in res:
            if f & bit:
                res[bit][(page, i)].add(n)
                codes[(page, i)] = (h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract'], f)
d = json.load(open(D + '/map_alundra.json', encoding='utf-8-sig'))
scan('G', 'alundra', d['SpriteInfo']['SpriteRecords'])
for n in range(0, 483):
    p = os.path.join(D, 'map_%d.json' % n)
    if not os.path.exists(p):
        continue
    d = json.load(open(p, encoding='utf-8-sig'))
    scan('L', n, (d.get('SpriteInfo') or {}).get('SpriteRecords') or [])
for bit in res:
    print('flag 0x%X: %d distinct (page,index) sprites' % (bit, len(res[bit])))
    for k, v in sorted(res[bit].items(), key=lambda kv: (kv[0][0], kv[0][1])):
        print('   page %s index %3d  codes A%d C%d D%d E%d F%d flags=0x%06X  maps=%d %s' % (k[0], k[1], *codes[k][:5], codes[k][5], len(v), sorted(v, key=str)[:6]))
