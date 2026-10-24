"""dis.py START END  - read-only capstone listing of ALUN_CD.EXE (France). Run: python -B -I -X utf8 dis.py 0x80038064 0x80038360"""
import struct, sys
sys.path.insert(0, r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
T = struct.unpack_from('<I', DATA, 0x18)[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
a = int(sys.argv[1], 16); b = int(sys.argv[2], 16)
for ad in range(a, b, 4):
    w = struct.unpack_from('<I', DATA, 0x800 + ad - T)[0]
    t = ''
    for i in md.disasm(struct.pack('<I', w), ad):
        t = '%-9s %s' % (i.mnemonic, i.op_str)
    print('0x%08x  %08x  %s' % (ad, w, t))
