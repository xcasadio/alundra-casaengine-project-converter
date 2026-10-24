"""dis.py <start> <end> : disassembly read-only d'ALUN_CD.EXE (France) avec capstone. Lance avec python -I."""
import sys, struct
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
assert DATA[:8] == b"PS-X EXE"
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)
def off(a): return 0x800 + (a - T_ADDR)
def u32(a): return struct.unpack_from("<I", DATA, off(a))[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
if __name__ == "__main__":
    a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
    for addr in range(a, b, 4):
        w = u32(addr)
        txt = ''
        for i in md.disasm(struct.pack('<I', w), addr):
            txt = '%-9s %s' % (i.mnemonic, i.op_str)
        print('0x%08x  %08x  %s' % (addr, w, txt))
