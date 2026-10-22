"""Critic-own: for every sm3 site, which logic-entity retargets (0x42 / 0x43) and which Result writers are flow-reachable in the SAME program
(state-free over-approximation, same edges as reach8.py). A retarget before a site would make owner != logic entity at that site."""
import csv
import mycorpus as c
import reach8 as r

rows = list(csv.DictReader(open('../../sm3/corpus-lines-sm3.tsv', encoding='utf-8'), delimiter='\t'))
byprog = {}
for row in rows:
    byprog.setdefault((int(row['map']), row['slot'], int(row['program'])), []).append(int(row['pc']))
for (mid, slot, prog), pcs in sorted(byprog.items()):
    b, d = c.load(mid)
    codes = d['Codes']
    entry = d['EventCodes%sTable' % slot][prog]
    marks = set()
    seen = set()
    st = [entry]
    for _ in range(2):
        seen = set()
        st = [entry]
        while st:
            pc = st.pop()
            if pc in seen or pc < 0 or pc >= len(codes):
                continue
            seen.add(pc)
            st.extend(r.succ(codes, pc, entry, marks))
    ops = {codes[p] for p in seen}
    retarget = sorted(hex(o) for o in ops if o in (0x42, 0x43))
    print('%3d %s[%2d] sites %-22s retargets reachable: %s' % (mid, slot, prog, pcs, retarget or '-'))
