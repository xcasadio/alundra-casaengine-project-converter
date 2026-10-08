import sys, struct, capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
data = open(EXE, 'rb').read()
t_addr = struct.unpack_from('<I', data, 0x18)[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
def dis(start, end):
    off = start - t_addr + 0x800
    code = data[off:off + (end - start)]
    for ins in md.disasm(code, start):
        print(f"{ins.address:08X}  {ins.mnemonic:8s} {ins.op_str}")
for arg in sys.argv[1:]:
    a, b = arg.split('-')
    print(f"--- {a}-{b}")
    dis(int(a, 16), int(b, 16))
