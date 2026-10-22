"""gen_jumptable.py - E20.e0c T0 (g): the jump table 0x800230D4 of GiveItem (0x57 entries) for the 53 types of the 152 placed objects (placed.json of ../posed-rev3/) and for the objects of the loot tables.
usage: python -B gen_jumptable.py <placed.json>"""
import json, sys, collections
import exelib as e
placed = json.load(open(sys.argv[1]))
GROUP = {0x80033f40: 'item0 (nothing)', 0x80033f48: 'herb 0x24 (inventory first, body 0x80032E50)', 0x80033f6c: 'Potion L 0x25 (inventory first, body 0x80033274)',
         0x80033f90: 'Extrait magique 0x26 (inventory first, body 0x800335AC)', 0x80033fa8: 'Breuvage 0x27 (inventory first, body 0x80033A2C)', 0x80033fcc: 'Potion S 0x29 (inventory first, body 0x800330FC)',
         0x80033ff0: 'money 1', 0x80034000: 'money 5', 0x80034010: 'money 10', 0x80034020: 'money 30', 0x80034030: 'beak 0x4F', 0x80034040: '0x50 (0x8003382C, not read: not placed, not in the tables)',
         0x80034054: 'Petit cristal 0x51 (body 0x800335AC)', 0x80034068: 'Grand cristal 0x52 (body 0x800336F0)', 0x8003407c: 'vase de vie 0x53 (body 0x800333AC)',
         0x80034090: 'heart small 0x54 (0x80032E2C)', 0x800340a4: 'heart medium 0x55 (0x80032EEC)', 0x800340b8: 'heart large 0x56 (0x80032F84)', 0x800340cc: 'simple add (0x8004E530)'}
rows = []
count = collections.Counter(o['item'] for o in placed)
native = collections.Counter(o['item'] for o in placed if o['C'] == 0)
for item in sorted(count):
    t = e.u32(0x800230D4 + 4 * item)
    rows.append(dict(item=item, type=item + 0x1E, name=[o['name'] for o in placed if o['item'] == item][0], placed=count[item], nativeC=native.get(item, 0), target=hex(t), group=GROUP.get(t, '?'),
                     octet4=e.u8(0x800C5F7C + 8 * item + 4), octet5=e.u8(0x800C5F7C + 8 * item + 5)))
loot = [0x24, 0x45, 0x46, 0x47, 0x48, 0x51, 0x54, 0x55, 0x56]
doc = dict(source='E20.e0c T0 (g): GiveItem jump table 0x800230D4', types=len(rows), placedObjects=sum(count.values()), nativeObjects=sum(native.values()), rows=rows,
           lootItems=[dict(item=i, target=hex(e.u32(0x800230D4 + 4 * i)), group=GROUP.get(e.u32(0x800230D4 + 4 * i), '?')) for i in loot],
           groups=collections.Counter(r['group'] for r in rows))
json.dump(doc, open('give-jump-table.json', 'w'), indent=1)
print(len(rows), sum(count.values()), sum(native.values()))
for g, n in sorted(doc['groups'].items()): print(n, g)
assert all(r['group'] != '?' for r in rows)
