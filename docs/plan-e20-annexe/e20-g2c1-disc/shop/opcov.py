"""opcov.py : which opcodes of the shop programs have a `case 0x..` in Alundra/Events/EventProgramRunner.cs. READ-ONLY."""
import re, sys, collections
sys.path.insert(0, '.')
import evlib as L
src = open(L.ROOT + '/Alundra/Events/EventProgramRunner.cs', encoding='utf-8-sig').read()
cases = set(int(m.group(1), 16) for m in re.finditer(r'case 0x([0-9A-Fa-f]{2}):', src))
# programs per map: (slot, table index) lists that matter for the shop
SHOP = {
 163: [('A',3),('B',2),('B',3),('B',4),('C',4)],
 258: [('A',3),('B',2),('B',3),('B',4),('B',5),('B',6),('B',7),('C',4)],
 298: [('A',3),('B',2),('B',3),('B',4),('B',5),('B',6),('B',7),('C',4)],
 398: [('A',1),('A',2),('A',3),('A',4),('B',1),('B',2),('B',3),('B',4),('B',5),('B',6),('B',7),('B',9),('B',10),('C',1),('C',3),('C',4),('C',5),('C',6)],
 134: [],
}
tot = collections.defaultdict(lambda: collections.defaultdict(set))
for m, progs in SHOP.items():
    doc = L.load_events(m)
    for X, i in progs:
        s = doc['EventCodes%sTable' % X][i]
        for pc, op, v, bad in L.sweep(doc, s, L.prog_end(doc, s)):
            if v is None or op in (0x00, 0xFF): continue
            tot[op][m].add('%s%d' % (X, i))
missing = []
for op in sorted(tot):
    nm = L.SIZES.get(op, (0, '?'))[1]
    ok = op in cases
    print('0x%02X %-45s %s  used in %s' % (op, nm, 'implemented' if ok else 'NO CASE', {m: sorted(v) for m, v in tot[op].items()}))
    if not ok: missing.append(op)
print('missing', ['0x%02X' % o for o in missing])
