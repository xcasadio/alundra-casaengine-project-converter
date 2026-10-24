"""q2_flags.py - the flags (+0x6C) of the 43 scripted trunks after their A program (set low16 by 0x62, clear low16 by 0x63, in program order, search type 0x80 = the entity itself),
starting from the header flags 0x2323E4 (sprite 117). Reports Collidable (0x80), Gravity (0x100), pickup kind (bits 9-10), NoRiders (0x4000). READ-ONLY.
Run: python -B -I -X utf8 q2_flags.py"""
import collections, glob, json, re, sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
res = collections.Counter(); per_prog = {}
for n in (72, 73, 79, 140, 158):
    d = json.load(open(D + '/map_%d.json' % n, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']; doc = de.load_map(n)
    ends = {(a, c): b for a, _, c, b in de.all_programs(doc)}
    for k, e in enumerate(si['Entities']['Entities']):
        if e is None or not e['IsEnabled'] or e['SpriteTableIndex'] != 117:
            continue
        h = recs[117]['Header']
        base = (h['MoreFlags'] & 0xFF) | ((h['CanPickup'] & 0xFF) << 8) | ((h['FlagsPortraitShadowType'] & 0xFF) << 16)
        ai = e['EventCodesA_LoadIndex']
        if not ai:
            continue
        s = doc['EventCodesATable'][ai & 0x7f]
        fl = base
        for pc, op, v, bad in de.sweep(doc, s, ends.get(('A', s), len(doc['Codes']))):
            if op == 0x62 and v and v[1] == 0x80:
                fl |= (v[3] << 8) | v[2]
            elif op == 0x63 and v and v[1] == 0x80:
                fl &= 0xFFFF0000 | (~((v[3] << 8) | v[2]) & 0xFFFF)
            elif op == 0xFF:
                break
        per_prog[(n, ai)] = fl
        res[(hex(base), hex(fl), 'Collidable' if fl & 0x80 else 'NOT collidable', 'Gravity' if fl & 0x100 else 'no gravity', 'kind%d' % ((fl >> 9) & 3), 'NoRiders' if fl & 0x4000 else 'riders ok')] += 1
for k, v in sorted(res.items()):
    print(v, k)
print('distinct A programs:', len(per_prog), 'all non collidable:', all(not (f & 0x80) for f in per_prog.values()))
