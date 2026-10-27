"""dump_table.py (h4c2 T1): writes the 64 bytes of the table 0x80023504 of ALUN_CD.EXE (France): 15 entries of 4 bytes (u16 tick, s16 p) and the sentinel entry (u16 0xFFFF, s16 0),
as one hexadecimal line, and prints its decoding, for the test (t1). Read-only on the executable.
usage: python -B dump_table.py PATH_OF_ALUN_CD.EXE OUT_HEX_FILE"""
import hashlib
import struct
import sys

exe = open(sys.argv[1], 'rb').read()
assert hashlib.sha1(exe).hexdigest() == 'a705f8dab665645af5dfaf8fb348c7926b5caa58', 'not the France executable'
text_addr = struct.unpack_from('<I', exe, 0x18)[0]
off = 0x800 + (0x80023504 - text_addr)
raw = exe[off:off + 64]
assert len(raw) == 64
with open(sys.argv[2], 'w') as f:
    f.write(raw.hex() + '\n')
for i in range(16):
    t, p = struct.unpack_from('<Hh', raw, 4 * i)
    print(i, t, p)
