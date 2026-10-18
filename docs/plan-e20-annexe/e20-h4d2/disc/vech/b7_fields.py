"""b7_fields.py - widths of the accesses to +0x274..+0x277 (Bytes[0..3]) and +0x280..+0x293 (AIValues) per native handler: where a WORD access exists the DLL's byte[4] / short[10] cannot hold the value."""
import re, collections, funcs, handler_map, tools
hm = handler_map.tables()
inv = {}
for k, (b, c) in hm.items():
    for code, p in c.items():
        inv.setdefault(p, []).append('%s%d' % (k, code))
WID = {0x20: ('lb', 1), 0x24: ('lbu', 1), 0x28: ('sb', 1), 0x21: ('lh', 2), 0x25: ('lhu', 2), 0x29: ('sh', 2), 0x23: ('lw', 4), 0x2B: ('sw', 4)}
def scan(offs):
    res = collections.OrderedDict()
    for a in range(tools.T_ADDR, tools.T_END, 4):
        w = tools.w32(a)
        op = w >> 26
        if op in WID and (w & 0xFFFF) in offs:
            f = funcs.func_of(a)
            res.setdefault(f, []).append((a, WID[op][0], w & 0xFFFF))
    return res
def show(title, offs):
    print('==', title)
    for f, l in scan(offs).items():
        kinds = collections.Counter((o, m) for _, m, o in l)
        names = inv.get(f, [])
        print(' %08X %-10s' % (f, ','.join(names) or '(helper)'), ' '.join('%s@%#x x%d' % (m, o, n) for (o, m), n in sorted(kinds.items())))
show('+0x274..0x277 (Bytes[0..3] in the DLL)', {0x274, 0x275, 0x276, 0x277})
show('+0x280..0x293 (AIValues short[10] in the DLL)', set(range(0x280, 0x294, 2)))
