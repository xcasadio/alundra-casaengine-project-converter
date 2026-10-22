"""gen_itemtable.py - E20.e0c T0 (l3): the 98 rows of the object table 0x800C5F7C of ALUN_CD.EXE (France), octets 4 to 7 (the columns of item-drop-properties.json: Field1 = the rule of the 600 ticks,
SoundSfxIndex = the pickup sound, 0 = key object, Field3, Field4) and the inventory maximum of g_itemsProperties column 3 where the binary table is readable. Writes item-table.json."""
import json
import exelib as e
rows = []
for item in range(98):
    base = 0x800C5F7C + 8 * item
    rows.append([e.u8(base + 4), e.u8(base + 5), e.u8(base + 6), e.u8(base + 7)])
nonzero0 = [i for i, r in enumerate(rows) if r[0]]
json.dump(dict(source='E20.e0c T0 (l3): octets 4..7 of the 98 rows of 0x800C5F7C of ALUN_CD.EXE (France)', columns=['Field1 (octet 4)', 'SoundSfxIndex (octet 5)', 'Field3 (octet 6)', 'Field4 (octet 7)'], rows=rows,
               column0Nonzero=nonzero0), open('item-table.json', 'w'), indent=1)
print(len(rows), [hex(i) for i in nonzero0], sorted(set(r[1] for r in rows)))
