"""Brute-force scan: every load/store whose immediate is in 0x6F18..0x6F47 (any base), plus every addiu rX,rY,0x6f18 (block base);
for the latter, list loads off rX within the next 60 instructions. Over-approximate on purpose; filter by code range."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
ins = [i for i in lib.all_ins() if lo <= i.address < hi]
out = []
for k, i in enumerate(ins):
    m = i.mnemonic
    if m in ('lhu','lh','lbu','lb','lw','sh','sw','sb'):
        ops = i.op_str.split(', ')
        if len(ops) == 2 and '(' in ops[1]:
            imm = ops[1].split('(')[0]
            try: v = int(imm, 0)
            except: continue
            if 0x6f18 <= v < 0x6f48:
                out.append('%08x %-4s %s   [imm 0x%x -> 0x8012%04x if base=0x80120000]' % (i.address, m, i.op_str, v, v))
    if m == 'addiu' and i.op_str.endswith('0x6f18'):
        r = i.op_str.split(', ')[0]
        for j in ins[k+1:k+80]:
            if j.mnemonic in ('lhu','lh','lbu','lb','lw') and j.op_str.endswith('(%s)' % r):
                out.append('%08x %-4s %s   [off block base %s set at %08x]' % (j.address, j.mnemonic, j.op_str, r, i.address))
            if j.op_str.split(', ')[0] == r and j.mnemonic not in ('sw','sh','sb'):
                break
for l in out: print(l)
print(len(out), 'hits')
