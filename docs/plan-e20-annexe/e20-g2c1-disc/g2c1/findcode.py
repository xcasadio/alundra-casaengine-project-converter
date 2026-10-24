"""findcode.py ADDR... : which (slot, code) of the master table 0x80098F4C dispatches into the functions starting at (or containing) ADDR. Read-only."""
import sys, struct
sys.path.insert(0, 'work')
import exelib
targets = [int(a, 16) for a in sys.argv[1:]]
for slot in range(6):
    base = exelib.slot_table_base(slot)
    for code in range(0, 200):
        try:
            p = exelib.u32(base + 4 * code)
        except Exception:
            break
        if not (0x80010000 <= p < 0x800F0000):
            continue
        for t in targets:
            if p == t:
                print('slot %s code %d -> %08x' % ('ABCDEF'[slot], code, p))
