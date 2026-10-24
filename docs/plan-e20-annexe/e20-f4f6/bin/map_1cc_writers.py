"""Which native handlers write entity +0x1CC (DamagedTickCounter = the E17/hit-pass target skip)?  Stores found by scan_offs.py; enclosing function = nearest
preceding 'addiu sp,sp,-N' prologue; then matched against the 5 handler tables (0x80098F4C)."""
import sys, re
sys.path.insert(0, 'src')
import lib, exe_fp as E
ins = lib.all_ins()
addr_index = {i.address: k for k, i in enumerate(ins)}
sites = [0x8002BC2C, 0x80031C2C, 0x80031C9C, 0x8004002C, 0x80061EA0, 0x80065DF8, 0x8006EB7C, 0x80070554, 0x80070BEC, 0x80071B30, 0x80072504, 0x80073570, 0x80073574, 0x80074824, 0x80074D98, 0x80076D60, 0x80077A64, 0x8007991C, 0x8007C428, 0x8007D964, 0x8007D968]
reads = [0x80031ABC, 0x8003942C, 0x800622C8, 0x80062690, 0x80070400]
tables = {}
for k, name in enumerate('ABCDEF'):
    p = E.u32(0x80098F4C + 4 * k)
    if p >= 0x80000000:
        for idx in range(256):
            h = E.u32(p + 4 * idx)
            if 0x80020000 <= h < 0x800CA800:
                tables.setdefault(h, []).append('%s%d' % (name, idx))
def func_start(a):
    k = addr_index[a]
    while k > 0:
        i = ins[k]
        if i.mnemonic == 'addiu' and i.op_str.startswith('$sp, $sp, -'):
            return i.address
        k -= 1
    return None
def report(lst, label):
    print(label)
    for s in lst:
        f = func_start(s)
        print('  site %08X in function %08X  registered as %s' % (s, f, tables.get(f)))
report(sites, 'WRITERS of +0x1CC')
report(reads, 'READERS of +0x1CC (displacement 0x1CC, base biased cases excluded)')
