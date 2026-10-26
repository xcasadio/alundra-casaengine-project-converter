"""classify_progs.py - classify the C programs of the 56 water drops: simple wait / flag-gated (0x30) / zone-gated (0x3B) / native only; waits histogram; the temporary flags used.  Reads census_drops.txt."""
import re, collections
txt = open('census_drops.txt', encoding='utf-8').read().splitlines()
progs = []
for l in txt:
    m = re.match(r'map (\d+) C\[(\d+)\] @\d+\.\.\d+: (.*)', l)
    if m:
        progs.append((int(m.group(1)), int(m.group(2)), m.group(3)))
cls = collections.Counter(); waits = collections.Counter(); flags = []
per = collections.defaultdict(collections.Counter)
for mp, ci, p in progs:
    if p.startswith('0x37') and '0x30' not in p:
        k = 'simple wait N then 40[2,0]'
        w = int(re.match(r'0x37\[55, (\d+)\]', p).group(1)); waits[w] += 1
    elif p.startswith('0x30'):
        k = 'flag-gated: 0x30 [flag] ? wait B : (wait A, set flag, native)'
        f = re.match(r'0x30\[48, (\d+), (\d+),', p); flags.append((mp, ci, hex((int(f.group(2)) << 8) | int(f.group(1)))))
    elif p.startswith('0x00 0x3B'):
        k = 'zone-gated: loop until the hero is in the tile box (0x3B), then wait N, then 40[2,0]'
    else:
        k = 'other'
    cls[k] += 1; per[mp][k] += 1
for k, v in cls.items(): print(v, k)
print('scripted programs total', sum(cls.values()), '(the placements share programs: 56 enabled placements, 52 scripted + 4 native spawned by 92 B[1] from rec 15 = rec 15 itself is 1 placement)')
print('simple waits histogram', sorted(waits.items()))
print('flags (map, C index, flag id):', flags)
for mp in sorted(per): print('map', mp, dict(per[mp]))
