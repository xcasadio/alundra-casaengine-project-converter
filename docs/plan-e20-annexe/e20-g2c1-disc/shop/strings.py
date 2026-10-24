"""strings.py MAP : the local strings (dialog ids 128+index) of a map, non empty only. READ-ONLY."""
import sys
sys.path.insert(0, '.')
import evlib as L
BS = chr(92)
m = int(sys.argv[1])
d = L.load_map(m)
for i, t in enumerate(d['Strings']):
    if t:
        s = t.replace(BS + 'N', ' / ').replace(BS + 'A', ' // ').replace(BS + 'C', '')
        print('%d (0x%02X)  %s' % (128 + i, 128 + i, s))
