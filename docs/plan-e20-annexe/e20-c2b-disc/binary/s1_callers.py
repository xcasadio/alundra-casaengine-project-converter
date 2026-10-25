"""S1: callers of 0x80031A68 (jal, j, pointer words), and of MovePlayer 0x80031B50."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mx
for tgt in (0x80031A68, 0x80031B50, 0x8003A374, 0x8003634C, 0x8002F884, 0x800350C0):
    print("target %08x" % tgt)
    for a in mx.callsites(tgt, ops=(2, 3)):
        w = mx.word(a)
        print("   %s at %08x in %s" % ('jal' if (w >> 26) == 3 else 'j', a, mx.name(a)))
    refs = mx.data_refs(tgt)
    print("   data words == target:", [hex(x) for x in refs])
    # also lui/addiu or lui/ori pairs materialising the address (indirect calls)
    hi = (tgt + 0x8000) >> 16 & 0xFFFF
    lo = tgt & 0xFFFF
    cand = []
    for i in range(mx.N):
        w = mx.W[i]
        if (w >> 26) == 15 and (w & 0xFFFF) in (tgt >> 16, ((tgt + 0x8000) >> 16) & 0xFFFF):
            # look ahead 4 instrs for addiu/ori with lo
            for j in range(1, 6):
                if i + j < mx.N:
                    v = mx.W[i + j]
                    if (v >> 26) in (9, 13) and (v & 0xFFFF) == lo:
                        cand.append(mx.TEXT0 + 4 * i)
    print("   lui/addiu materialisations:", [hex(x) for x in cand])
