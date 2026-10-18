"""loops.py - for every reader of 0x80134360: the guard branch right after the load and the first backward branch (loop end) with its compare."""
import sys, struct, tools, re
sites = [int(l.split()[0],16) for l in open('../out/ref_80134360.txt') if l.strip().startswith('0x')]
def parse_branch(a):
    i = tools.ins_at(a)
    if i is None: return None
    m = i.mnemonic
    if m in ('beq','bne','blez','bgtz','bltz','bgez','beqz','bnez','b'):
        tgt = int(i.op_str.split(',')[-1].strip(), 16)
        return m, tgt, i.op_str
    return None
for s in sites:
    i = tools.ins_at(s)
    print('=====', hex(s), tools.text(s).split(':',1)[1].strip())
    if i.mnemonic == 'sw':
        print('   WRITE'); continue
    # first branch after the load (guard)
    a = s + 4
    for k in range(10):
        b = parse_branch(a)
        if b:
            print('   guard   ', tools.text(a), '  -> target', hex(b[1]))
            break
        a += 4
    # first backward branch after the load
    a = s + 4
    found = False
    for k in range(200):
        b = parse_branch(a)
        if b and b[1] < a and b[1] <= s + 0x10 :
            for t in range(a - 16, a + 8, 4):
                print('   loopend ', tools.text(t))
            found = True
            break
        if tools.w32(a) == 0x03E00008: break
        a += 4
    if not found:
        print('   (no backward branch to near the load before function end)')
