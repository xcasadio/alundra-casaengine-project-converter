import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
def body(a, maxn=3000):
    """linear scan until the first 'jr $ra' whose delay slot ends the function and no branch target beyond it seen so far"""
    far = a
    out = []
    for i in lib.dis(a, maxn):
        out.append(i)
        if i.mnemonic in ('beq','bne','beqz','bnez','bgez','bltz','blez','bgtz','b','j') :
            t = i.op_str.split(', ')[-1]
            try:
                t = int(t, 16)
                if t > far and t < a + 4*maxn: far = t
            except: pass
        if i.mnemonic == 'jr' and i.op_str == '$ra' and i.address >= far:
            out.append(lib.dis(i.address + 4, 1)[0]); break
    return out
if __name__ == '__main__':
    for s in sys.argv[1:]:
        a = int(s, 16); b = body(a)
        tg = sorted(set(i.op_str for i in b if i.mnemonic == 'jal'))
        jr = [hex(i.address) for i in b if i.mnemonic == 'jalr']
        print('%08x..%08x jal:%s jalr:%s' % (a, b[-1].address, ' '.join(tg), jr))
