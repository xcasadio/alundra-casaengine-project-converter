"""s1_table.py - static dump of the loot table 0x80028E0C (128 rows x 16 bytes) and item property table 0x800C5F7C (8 bytes per item); chain analysis."""
import sys, json
sys.path.insert(0,'.')
import xdis
D = xdis.DATA
def rb(a): return D[a - xdis.BASE + xdis.OFF]
TAB = 0x80028E0C
rows = [[rb(TAB + 16*r + c) for c in range(16)] for r in range(128)]
nz = [r for r in range(128) if any(rows[r])]
print('nonzero rows', len(nz), nz)
for r in nz:
    print('row %3d (code 0x%02X): %s' % (r, 0x80|r, ' '.join('%02X' % x for x in rows[r])))
# which rows reference other rows
refs = {}
for r in nz:
    for x in rows[r]:
        if x & 0x80: refs.setdefault(r, set()).add(x & 0x7F)
print('rows with chain entries (entry & 0x80):', {k: sorted(v) for k, v in refs.items()})
print('bytes >= 0x62 and < 0x80 inside table (resolve to 0):', sorted({x for r in nz for x in rows[r] if 0x62 <= x < 0x80}))
# row whose code lies beyond the table? code & 0x7F always 0..127 -> in range
# properties
PROP = 0x800C5F7C
props = [[rb(PROP + 8*i + k) for k in range(8)] for i in range(0x62)]
json.dump({'table': rows, 'props': props}, open('out/loot_table.json','w'))
print('item props: item -> bytes 4..7 (lifetime flag, sound, f3, f4) for items with nonzero byte4 or in table:')
used = sorted({x for r in nz for x in rows[r] if 0 < x < 0x62})
print('items reachable from the table (direct):', used)
for i in used:
    print('  item %2d  props %s' % (i, ' '.join('%02X' % x for x in props[i])))
