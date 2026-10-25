import struct
EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
d = open(EXE,'rb').read()
print(d[:8], len(d))
print('pc0 %08x gp %08x tadr %08x tsize %08x' % struct.unpack_from('<IIII', d, 0x10))
