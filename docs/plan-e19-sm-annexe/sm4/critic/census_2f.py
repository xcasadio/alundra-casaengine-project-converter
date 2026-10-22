import json, sys, collections
sys.path.insert(0, '.')
import census_critic as c

idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
modes = collections.Counter()
sites = set()
for mid in sorted(int(k) for k in idx):
    base, ev, tm = c.load_map(mid, idx)
    codes = ev['Codes']
    for slot, index, pc, op in c.walk_map(ev, tm):
        if op == 0x2F:
            sites.add((mid, slot, index, pc))
            modes[codes[pc + 3]] += 1
print(len(sites), dict(modes))
