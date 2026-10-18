"""compare_idx.py (critic of h4c1): per-rock E3 / status-4 ticks of the e20-h4 harness without and with the slot index word (outputs crit_noidx.json / crit_idx.json of work/)."""
import json
import sys
base = sys.argv[1]
def load(f):
    d = json.load(open(f))
    out = []
    for k, b in d['births'].items():
        tr = b['track']
        out.append((b['birth'], b['slot'], next((r['t'] for r in tr if r['status'] == 3), None), next((r['t'] for r in tr if r['status'] == 4), None), tr[-1]['x'] >> 16, tr[-1]['y'] >> 16))
    return sorted(out)
a = load(base + '/crit_noidx.json'); b = load(base + '/crit_idx.json')
diff = sum(1 for x, y in zip(a, b) if x[2:4] != y[2:4] or x[4:] != y[4:])
for x, y in zip(a, b):
    print('noidx', x, 'idx', y, '' if x[2:4] == y[2:4] and x[4:] == y[4:] else '<-- differs')
print('rocks with a different E3 tick / status-4 tick / final xy:', diff, 'of', len(a))
