"""dscan.py - which native D (touch) and C (tick) handlers of ALUN_CD.EXE read the attack of the touching entity (lw ..,0x224 then lw ..,0x1C8 then lbu / andi 0xF) and which attribute nibbles they compare with."""
import struct, capstone, exelib as e
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def ins_at(a):
    return list(md.disasm(struct.pack('<I', e.u32(a)), a))[0]
def scan(slot, name):
    seen = {}
    for code in range(256):
        try:
            h = e.handler(slot, code)
        except Exception:
            continue
        if h:
            seen.setdefault(h, []).append(code)
    out = []
    for h, codes in sorted(seen.items()):
        # walk linearly to the first jr ra followed by delay slot; the handlers are small
        a = h; n = 0; reads224 = False; reads1c8 = False; consts = set()
        while n < 4000:
            i = ins_at(a)
            s = i.mnemonic + ' ' + i.op_str
            if '0x224(' in s: reads224 = True
            if '0x1c8(' in s: reads1c8 = True
            if i.mnemonic == 'addiu' and ', $zero,' in s and reads1c8:
                try: consts.add(int(s.split(',')[-1], 0))
                except Exception: pass
            if i.mnemonic == 'jr' and 'ra' in s:
                n += 2; break
            a += 4; n += 1
        out.append((h, codes, (a - h) // 4 + 2, reads224, reads1c8, sorted(consts)))
    return out
for slot, name in ((3, 'D'), (2, 'C'), (4, 'E'), (0, 'A'), (5, 'F')):
    res = scan(slot, name)
    print('== slot', name, 'distinct handlers', len(res))
    for h, codes, sz, r224, r1c8, consts in res:
        if r224 or r1c8:
            print('  %08X codes %s size~%d reads+0x224=%s reads+0x1C8=%s consts_after=%s' % (h, codes, sz, r224, r1c8, consts))
