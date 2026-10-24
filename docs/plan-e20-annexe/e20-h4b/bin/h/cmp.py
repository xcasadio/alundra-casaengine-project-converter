"""cmp.py - for each read of 0x80134360 : the register loaded and the first compare using it (slt/slti/blez/bltz/bgtz/bgez/beq/bne), within 12 instructions."""
import tools
sites = [int(l.split()[0],16) for l in open('../out/ref_80134360.txt') if l.strip().startswith('0x')]
for s in sites:
    i = tools.ins_at(s)
    if i.mnemonic == 'sw':
        print(hex(s), 'WRITE', tools.text(s).split(':',1)[1].strip()); continue
    r = i.op_str.split(',')[0].strip()
    res = None
    for k in range(1, 13):
        a = s + 4*k
        j = tools.ins_at(a)
        ops = [o.strip() for o in j.op_str.split(',')]
        if j.mnemonic in ('slt','sltu','slti','sltiu','blez','bltz','bgtz','bgez','beq','bne','beqz','bnez') and r in ops:
            res = (hex(a), j.mnemonic, j.op_str); break
        if ops and ops[0] == r and j.mnemonic not in ('sw','sh','sb','nop') and k>0:
            res = ('overwritten', hex(a), j.mnemonic, j.op_str); break
    print(hex(s), 'reg', r, '->', res)
