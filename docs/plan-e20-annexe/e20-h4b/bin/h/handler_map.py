"""handler_map.py - dump the native handler tables (master 0x80098F4C = pointers to the A..F tables, 256 codes each) and map a function address to (slot, code)."""
import exelib, sys
def tables():
    out = {}
    for s in range(6):
        base = exelib.slot_table_base(s)
        if base == 0:
            continue
        codes = {}
        for c in range(256):
            p = exelib.u32(base + 4 * c)
            if 0x80010000 <= p < 0x800CA800:
                codes[c] = p
        out[exelib.SLOT_NAMES[s]] = (base, codes)
    return out
if __name__ == '__main__':
    t = tables()
    for k, (base, codes) in t.items():
        print(k, hex(base), len(codes))
    inv = {}
    for k, (base, codes) in t.items():
        for c, p in codes.items():
            inv.setdefault(p, []).append((k, c))
    for p in sorted(inv):
        print(hex(p), inv[p])
