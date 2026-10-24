import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib, struct
targets = [int(a,16) for a in sys.argv[1:]]
ins = lib.all_ins()
for t in targets:
    hits = []
    for i in ins:
        if i.mnemonic in ('jal','j') and i.op_str == '0x%x' % t:
            hits.append('%s@%08x' % (i.mnemonic, i.address))
    # data pointers
    D = lib.DATA
    for o in range(lib.OFF, len(D)-3, 4):
        if struct.unpack_from('<I', D, o)[0] == t:
            hits.append('ptr@%08x' % (lib.BASE + o - lib.OFF))
    # lui/addiu pairs
    hi = (t + 0x8000) >> 16; lo16 = t & 0xFFFF
    lo_s = lo16 - 0x10000 if lo16 & 0x8000 else lo16
    for k, i in enumerate(ins):
        if i.mnemonic == 'addiu' and i.op_str.endswith(', %s' % hex(lo_s)) or (i.mnemonic=='addiu' and i.op_str.endswith(', -%s' % hex(-lo_s)) if lo_s<0 else False):
            for j in ins[max(0,k-8):k]:
                if j.mnemonic == 'lui' and j.op_str.endswith(hex(hi)):
                    hits.append('lui/addiu@%08x' % i.address); break
    print('%08x:' % t, ' '.join(hits))
