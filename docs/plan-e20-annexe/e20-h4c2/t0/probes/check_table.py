"""check_table.py - E20.h4c2 T0: the 15 entries (u16 tick, s16 p) and the sentinel of the table 0x80023504, and the 32-direction tables 0x80023654 / 0x80023694, read in ALUN_CD.EXE (France) and compared with the literals of the probe and of the DLL.
usage: python -I check_table.py   (reads the binary, writes nothing)"""
import struct
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
data = open(EXE, 'rb').read()
import hashlib
print('sha1', hashlib.sha1(data).hexdigest())
off = lambda a: 0x800 + a - 0x80020000
ent = []
a = 0x80023504
for i in range(16):
    t, p = struct.unpack_from('<Hh', data, off(a + 4 * i))
    ent.append((t, p))
print('entries', ent)
ticks = [16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 224, 240]
ok = all(ent[i] == (ticks[i], 512 * (i + 1)) for i in range(15)) and ent[15][0] == 0xFFFF
print('table equals the literals of the probe (ticks, p = 512 (i+1)) and the sentinel 0xFFFF:', ok)
X = [struct.unpack_from('<h', data, off(0x80023654 + 2 * i))[0] for i in range(32)]
Y = [struct.unpack_from('<h', data, off(0x80023694 + 2 * i))[0] for i in range(32)]
print('X32', X)
print('Y32', Y)
