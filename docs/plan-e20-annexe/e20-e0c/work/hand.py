import exelib as e, sys
# for each slot enumerate entries until pointer not in text
tabs = {}
for s in range(6):
    b = e.slot_table_base(s)
    print(e.SLOT_NAMES[s], hex(b))
want = [int(x,16) for x in sys.argv[1:]]
ents = []
for s in range(6):
    b = e.slot_table_base(s)
    if not b: continue
    for c in range(256):
        try:
            p = e.u32(b+4*c)
        except Exception:
            break
        if 0x80020000 <= p < 0x80100000:
            ents.append((p, e.SLOT_NAMES[s], c))
ents.sort()
import bisect
keys = [x[0] for x in ents]
for w in want:
    i = bisect.bisect_right(keys, w)-1
    print(hex(w), 'in/after handler', ents[i][1], ents[i][2], hex(ents[i][0]), 'next', hex(ents[i+1][0]) if i+1 < len(ents) else None)
