import sys, struct, time, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.argv = ['x']
import myvec_lib as V
m = V.m
RANGES = [('step', 0x8002F854, 0x8002F884), ('trylift', 0x8002EDBC, 0x8002EEAC), ('pta', 0x8002EEAC, 0x8002EFEC), ('upce', 0x8002F884, 0x8002FB00)]
def ram_off(a): return a & 0x1FFFFFFF
def getw(a): return struct.unpack_from('<I', m.ram, ram_off(a))[0]
def setw(a, w): struct.pack_into('<I', m.ram, ram_off(a), w & 0xFFFFFFFF)
def mutants(a, w):
    op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31; fn = w & 63; imm = w & 0xFFFF
    out = []
    if w == 0: return out
    if op in (4, 5): out.append(('beq<->bne', (w & 0x03FFFFFF) | ((9 - op) << 26)))
    if op in (6, 7): out.append(('blez<->bgtz', (w & 0x03FFFFFF) | ((13 - op) << 26)))
    if op == 1 and rt in (0, 1): out.append(('bltz<->bgez', (w & ~(0x1F << 16)) | ((1 - rt) << 16)))
    if op == 0 and fn in (0x2A, 0x2B): out.append(('slt<->sltu', (w & ~63) | (0x2A + 0x2B - fn)))
    if op in (0xA, 0xB): out.append(('slti<->sltiu', (w & 0x03FFFFFF) | ((0xA + 0xB - op) << 26)))
    if op in (9, 0xA, 0xB, 0xC, 0xD) and not (op == 9 and rt == 29):
        out.append(('imm+1', (w & ~0xFFFF) | ((imm + 1) & 0xFFFF)))
        out.append(('imm-1', (w & ~0xFFFF) | ((imm - 1) & 0xFFFF)))
    if op in (0x23, 0x2B) and rs != 29:
        out.append(('off+4', (w & ~0xFFFF) | ((imm + 4) & 0xFFFF)))
    if op == 0 and fn in (0x21, 0x23): out.append(('addu<->subu', (w & ~63) | (0x21 + 0x23 - fn)))
    return out
base = V.run_all(150)
assert not base, base
t0 = time.time()
tot = 0; killed = 0; surv = []
for name, lo, hi in RANGES:
    for a in range(lo, hi, 4):
        w0 = getw(a)
        for kind, w1 in mutants(a, w0):
            if w1 == w0: continue
            setw(a, w1)
            try:
                f = V.run_all(150)
            except Exception as ex:
                f = {'exc': 1}
            setw(a, w0)
            tot += 1
            if f: killed += 1
            else: surv.append((name, hex(a), kind, hex(w0), hex(w1)))
print('mutants', tot, 'killed', killed, 'survivors', len(surv), 'time', round(time.time() - t0))
for s in surv: print(s)
