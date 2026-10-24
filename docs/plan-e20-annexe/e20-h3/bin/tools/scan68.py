"""scan68.py - sites of ALUN_CD.EXE that load +0x68 (the sprite type) of an entity and compare it with 1 or 2 (global sprites 'beans' and 'bomb') within the next 6 instructions."""
import struct, capstone, exelib as e
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
ins = {}
for a in range(e.T_ADDR, e.T_ADDR + e.T_SIZE, 4):
    w = e.u32(a)
    i = list(md.disasm(struct.pack('<I', w), a))
    if i: ins[a] = i[0]
hits = []
for a, i in ins.items():
    if i.mnemonic == 'lw' and i.op_str.replace(' ', '').endswith(',0x68(' + i.op_str.split('(')[-1]) and ',0x68(' in i.op_str.replace(' ', ''):
        reg = i.op_str.split(',')[0].strip()
        for k in range(1, 7):
            j = ins.get(a + 4 * k)
            if not j: break
            s = j.mnemonic + ' ' + j.op_str
            if j.mnemonic == 'addiu' and '$zero' in j.op_str and j.op_str.split(',')[-1].strip() in ('1', '2'):
                hits.append((a, i.op_str, s))
                break
known = [(0x80034224, 0x80034320), (0x8003453C, 0x80034680), (0x80035204, 0x80035260), (0x800352C4, 0x80035320), (0x80034ACC, 0x80034BDC)]
for a, o, s in hits:
    tag = 'known' if any(x <= a < y for x, y in known) else 'OTHER'
    print('%08X %-26s -> %s  [%s]' % (a, o, s, tag))
