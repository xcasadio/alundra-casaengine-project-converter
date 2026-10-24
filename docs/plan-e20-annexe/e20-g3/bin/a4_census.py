"""a4_census.py - disc-e20g3 A1/A4: read-only census of the header flags of every sprite record and every placed entity (alundra-project/Data/sprite-records.json, Entities/*.entity names,
import sys
data-extracted/data/map_N.json).  Writes out/a4_census.txt.
Header word (entity +0x6C) = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16  (EntityFlags.cs:10-20).  Bits used here:
  0x80 Collidable (member of the collidable list when not carried and anim flag 0x80 clear), 0x100 gravity, 0x600 pickup kind, bit0 ClassA, bit3 ClassB, bit1 HitsClassB, bit2 HitsClassA.
Anim flag: AnimSets[].Acceleration byte (= set byte 0xD, copied to entity +0xB4 at every animation start, 0x80038B68-0x80038B70); bit 0x80 = no entity collision for that animation."""
import json
import glob
import os
import re
import collections
R = r"D:/development/repo/alundra-casaengine-project-converter"
SPR = json.load(open(R + '/alundra-project/Data/sprite-records.json', encoding='utf-8-sig'))
NAMES = {}
for f in glob.glob(R + '/alundra-project/Entities/*/*.entity'):
    j = json.load(open(f, encoding='utf-8-sig'))
    NAMES[j['id']] = j['name']
out = []
P = out.append

rows = []
for k, v in SPR.items():
    mf, cp = v['MoreFlags'], v['CanPickup']
    acc = [a['Acceleration'] for a in v['AnimSets'] if a is not None]
    n80 = sum(1 for x in acc if x & 0x80)
    n40 = sum(1 for x in acc if x & 0x40)
    rows.append(dict(id=k, name=NAMES.get(k, '?'), mf=mf, cp=cp, kind=(cp >> 1) & 3, col=bool(mf & 0x80), grav=bool(cp & 1), nsets=len(acc), n80=n80, n40=n40,
                     C=v['ProgramTick'], E=v['ProgramDeactivate'], size=(v['SizeX'], v['SizeY'], v['SizeZ'])))
P('records %d ; Collidable(0x80) %d ; not collidable %d' % (len(rows), sum(r['col'] for r in rows), sum(not r['col'] for r in rows)))
P('records with at least one animation set whose Acceleration byte has 0x80 (no entity collision during that animation): %d' % sum(1 for r in rows if r['n80']))
P('')
P('-- by (ClassA bit0, ClassB bit3, Collidable bit7) over the 395 records --')
c = collections.Counter(((r['mf'] & 1) != 0, (r['mf'] & 8) != 0, r['col']) for r in rows)
for k2, n in sorted(c.items()):
    P('  ClassA=%d ClassB=%d Collidable=%d : %d records' % (k2[0], k2[1], k2[2], n))
P('')
P('-- the 17 liftable records (CanPickup kind != 0) --')
for r in sorted([r for r in rows if r['kind']], key=lambda r: (r['kind'], r['name'])):
    P('  kind %d %-46s MoreFlags=0x%02X Collidable=%s Gravity=%s C=%d E=%d size=%s animsets=%d with0x80=%d with0x40=%d' % (r['kind'], r['name'], r['mf'], r['col'], r['grav'], r['C'], r['E'], r['size'], r['nsets'], r['n80'], r['n40']))
P('')
P('-- records that are NOT liftable, ClassA (monster side) --')
mon = [r for r in rows if (r['mf'] & 1) and not r['kind']]
P('  %d records ClassA ; Collidable %d ; not collidable %d' % (len(mon), sum(r['col'] for r in mon), sum(not r['col'] for r in mon)))
for r in mon:
    if r['col']:
        P('    COLLIDABLE monster-side %-44s MoreFlags=0x%02X Acc0x80 sets=%d' % (r['name'], r['mf'], r['n80']))
P('')
P('-- records ClassB or neither class bits (player side / NPCs / objects), Collidable --')
oth = [r for r in rows if not (r['mf'] & 1) and not r['kind']]
P('  %d records ; Collidable %d ; not collidable %d' % (len(oth), sum(r['col'] for r in oth), sum(not r['col'] for r in oth)))
P('  names of Collidable ones:')
names = sorted(set((r['name'], r['mf']) for r in oth if r['col']))
for nm, mf in names:
    P('    %-50s MoreFlags=0x%02X' % (nm, mf))
P('')
P('-- records whose animation sets carry 0x80 --')
for r in rows:
    if r['n80']:
        P('  %-50s MoreFlags=0x%02X sets with 0x80: %d of %d' % (r['name'], r['mf'], r['n80'], r['nsets']))

# ---- placements over all maps ----
D = R + '/data-extracted/data'
tot = collections.Counter()
by_flag = collections.Counter()
liftable_place = collections.Counter()
mapcount = 0
for f in sorted(glob.glob(D + '/map_*.json'), key=lambda p: int(re.search(r'map_(\d+)', p).group(1)) if re.search(r'map_(\d+)\.json$', p) else -1):
    if not re.search(r'map_\d+\.json$', f):
        continue
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']
    recs = si['SpriteRecords']
    mapcount += 1
    for e in si['Entities']['Entities']:
        if e is None or not e.get('IsEnabled', 1):
            continue
        i = e['SpriteTableIndex']
        if i >= len(recs) or recs[i] is None:
            continue
        h = recs[i]['Header']
        mf, cp = h['MoreFlags'], h['CanPickup']
        tot['placements'] += 1
        by_flag[('Collidable' if mf & 0x80 else 'not collidable', 'ClassA' if mf & 1 else ('ClassB' if mf & 8 else 'noClass'))] += 1
        kind = (cp >> 1) & 3
        if kind:
            liftable_place[(kind, 'Collidable' if mf & 0x80 else 'not collidable')] += 1
P('')
P('-- placements, enabled records of %d maps (data-extracted) --' % mapcount)
P('  total %d' % tot['placements'])
for k2, n in sorted(by_flag.items()):
    P('  %-15s %-8s : %d' % (k2[0], k2[1], n))
P('  liftable placements by (kind, collidable at header level): %s' % dict(liftable_place))
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'a4_census.txt'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('\n'.join(out))
