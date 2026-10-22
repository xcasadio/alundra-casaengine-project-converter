"""fulldis.py - full linear disassembly of the text segment of ALUN_CD.EXE into full.dis (read-only on the binary)."""
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.skipdata = True
start = exelib.T_ADDR
size = exelib.T_SIZE
code = exelib.DATA[0x800:0x800 + size]
n = 0
with open('full.dis', 'w') as f:
    for ins in md.disasm(code, start):
        f.write(f'{ins.address:#010x}  {ins.mnemonic:8s} {ins.op_str}\n')
        n += 1
print('instructions', n, 'text', hex(start), hex(size))
