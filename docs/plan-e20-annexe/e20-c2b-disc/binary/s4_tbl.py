import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx, bx
base = 0x80098F4C
tbl = {}
for k in range(0, 260):
    v = bx.u32(base + 4*k)
    tbl.setdefault(v, []).append(k)
for f in (0x80061eb8, 0x80074d00, 0x8006fc7c, 0x80071bfc, 0x80073d04, 0x80075a3c, 0x8007773c, 0x80079b1c, 0x8007b6ec, 0x8007c174, 0x8006790c, 0x8007fb58):
    print("%08x" % f, "table idx:", tbl.get(f), " data refs:", [hex(x) for x in mx.data_refs(f)][:6])
# print the first words of the table so we see its extent
print([hex(bx.u32(base+4*k)) for k in range(0, 12)])
