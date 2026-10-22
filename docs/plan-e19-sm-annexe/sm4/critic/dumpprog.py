"""dumpprog.py MAP SLOT PROG [START_PC] [COUNT] : linear decode (by the size table at HEAD) of a program from the export."""
import sys, json
sys.path.insert(0, '.')
import census_critic as c

mid = int(sys.argv[1]); slot = sys.argv[2]; prog = int(sys.argv[3])
idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
base, ev, tm = c.load_map(mid, idx)
codes = ev['Codes']
tabs = c.tables(ev)
entry = tabs[c.SLOTS.index(slot)][prog]
start = int(sys.argv[4]) if len(sys.argv) > 4 else entry
count = int(sys.argv[5]) if len(sys.argv) > 5 else 60
print(base, slot, prog, 'entry', entry, 'table', tabs[c.SLOTS.index(slot)])
pc = entry if start < 0 else start
n = 0
while n < count and pc < len(codes):
    op = codes[pc]
    size = c.SIZES.get(op, 1) or 1
    ops = ' '.join('%02X' % b for b in codes[pc + 1:pc + size])
    print('%5d  %02X %-40s %s' % (pc, op, c.NAMES.get(op, '?'), ops))
    pc += size
    n += 1
    if op == 0xFF:
        break
