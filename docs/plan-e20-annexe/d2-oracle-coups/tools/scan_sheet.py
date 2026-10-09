"""scan_sheet.py - D2: where does the binary store the balance-sheet pointer (+0x1C4) or touch the hero working sheet 0x80127008?  static scan, prints
every sw/lw with displacement 0x1c4 in the code range 0x80020000-0x800CA800 and every lui 0x8012 / addiu|ori|lw|sw ... 0x7008-0x7107 pair."""
import os, sys, re
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import lib
ins = lib.all_ins()
print('--- accesses with displacement 0x1c4')
for i in ins:
    if i.mnemonic in ('sw', 'lw') and re.search(r'0x1c4\(', i.op_str):
        print('%08x %s %s' % (i.address, i.mnemonic, i.op_str))
print('--- lui 0x8012 followed within 12 instructions by an immediate in 0x7008..0x7107')
for k, i in enumerate(ins):
    if i.mnemonic == 'lui' and i.op_str.endswith('0x8012'):
        reg = i.op_str.split(',')[0]
        for j in ins[k + 1:k + 13]:
            m = re.match(r'(\$\w+), (\$\w+), (-?0x[0-9a-f]+)$', j.op_str)
            m2 = re.match(r'(\$\w+), (-?0x[0-9a-f]+)\((\$\w+)\)$', j.op_str)
            if m and m.group(2) == reg and j.mnemonic in ('addiu', 'ori'):
                v = int(m.group(3), 0) & 0xFFFF
                if 0x7008 <= v <= 0x7107:
                    print('%08x %s %s  (lui at %08x)' % (j.address, j.mnemonic, j.op_str, i.address)); break
            if m2 and m2.group(3) == reg:
                v = int(m2.group(2), 0) & 0xFFFF
                if 0x7008 <= v <= 0x7107:
                    print('%08x %s %s  (lui at %08x)' % (j.address, j.mnemonic, j.op_str, i.address)); break
