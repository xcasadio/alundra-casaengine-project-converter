"""E20.b2 T0 (c): the phase-2 dispatch of an entity event (0x800386D0): does a non-zero entity-code index at the slot (& 0x7F) run the map script and skip the native sprite handler?"""
import struct, sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(EXE, 'rb').read()
BASE, OFF = 0x80020000, 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
a, n = int(sys.argv[1], 16), int(sys.argv[2], 16)
o = a - BASE + OFF
for i in md.disasm(D[o:o + n], a):
    print('%08X  %-8s %s' % (i.address, i.mnemonic, i.op_str))
