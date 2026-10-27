"""dump_table.py (h4c1 T1): writes the 194 bytes of the table 0x8002343C of ALUN_CD.EXE (France): 32 entries of 6 bytes (u16 tick, s16 speed of Z, s16 delta of direction) and the sentinel word 0xFFFF,
as one hexadecimal line, and its decoding, for the test (t1). Read-only on the executable.
usage: python -B dump_table.py PATH_OF_ALUN_CD.EXE OUT_HEX_FILE"""
import hashlib
import struct
import sys

exe = open(sys.argv[1], 'rb').read()
assert hashlib.sha1(exe).hexdigest() == 'a705f8dab665645af5dfaf8fb348c7926b5caa58', 'not the France executable'
text_addr = struct.unpack_from('<I', exe, 0x18)[0]
off = 0x800 + (0x8002343C - text_addr)
raw = exe[off:off + 194]
assert len(raw) == 194
with open(sys.argv[2], 'w') as f:
    f.write(raw.hex() + '\n')
for i in range(32):
    t, v, d = struct.unpack_from('<Hhh', raw, 6 * i)
    print(i, t, v, d)
print('sentinel', hex(struct.unpack_from('<H', raw, 192)[0]))
