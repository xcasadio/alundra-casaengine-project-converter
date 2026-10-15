"""critic: flags of the K0 consumers in Data/sprite-records.json (global page, BankKey '0'..'255' strings for the global page?). python -B -I census_flags.py"""
import json, collections
d = json.load(open("D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"))
bits = [(0x1, 'ClassA'), (0x8, 'ClassB'), (0x10, 'DeactImpact'), (0x20, 'DeactHit'), (0x40, 'DeactAnimEnd'), (0x80, 'Collidable'), (0x100, 'Gravity'), (0x2000, 'NoObstacleSlide'), (0x4000, 'NoRiders'), (0x100000, 'DestroySlidingSlope'), (0x200000, 'DestroyVram')]
keys = collections.Counter(v['BankKey'] for v in d.values())
print("distinct BankKey values", len(keys), list(keys.items())[:6])
def flags(v):
    return v['MoreFlags'] | (v['CanPickup'] << 8) | (v['FlagsPortraitShadowType'] << 16)
want = ['79', '91', '36']
rows = {}
for g, v in d.items():
    rows.setdefault(v['BankKey'], []).append((g, v))
for k in want:
    for g, v in rows.get(k, []):
        f = flags(v)
        print("BankKey %-4s flags 0x%06X %s | off(%d,%d,%d) size(%d,%d,%d) contents %d" % (k, f, ",".join(n for b, n in bits if f & b), v['OffsetX'], v['OffsetY'], v['OffsetZ'], v['SizeX'], v['SizeY'], v['SizeZ'], v['Contents']))
# what does BankKey look like for map-page sprites
print([k for k in keys if not k.isdigit()][:10])
