#!/usr/bin/env python3
"""Counter-check helper: read-only access to ALUN_CD.EXE (France) to read the two hero state tables
that the DLL report left as INCONNU / FAIT-RAPPORT.  Own code (not the annex's harness)."""
import struct
import sys

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
with open(EXE_PATH, "rb") as f:
    DATA = f.read()
assert DATA[:8] == b"PS-X EXE"
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)


def off(a):
    o = 0x800 + (a - T_ADDR)
    assert 0x800 <= o < len(DATA), hex(a)
    return o


def u32(a):
    return struct.unpack_from("<I", DATA, off(a))[0]


def u8(a):
    return DATA[off(a)]


def table(base, n):
    return [u32(base + 4 * i) for i in range(n)]


main = table(0x80022D3C, 0x50)
swim = table(0x80022E7C, 0x50)
print("T_ADDR", hex(T_ADDR), "size", hex(T_SIZE), "pc0", hex(PC0))
print("main table 0x80022D3C (80 entries): handler by state")
by = {}
for i, h in enumerate(main):
    by.setdefault(h, []).append(i)
for h, states in sorted(by.items(), key=lambda kv: kv[0]):
    print(f"  {h:#010x}: {', '.join(f'{s:#04x}' for s in states)}")
print("swim table 0x80022E7C (80 entries): handler by state")
by2 = {}
for i, h in enumerate(swim):
    by2.setdefault(h, []).append(i)
for h, states in sorted(by2.items(), key=lambda kv: kv[0]):
    print(f"  {h:#010x}: {', '.join(f'{s:#04x}' for s in states)}")
print("states 0x32..0x34: main", [hex(main[i]) for i in (0x32, 0x33, 0x34)], "swim", [hex(swim[i]) for i in (0x32, 0x33, 0x34)])
# the bytes of the XA tracks and the item gates
print("xa bytes at 0x80023365+item for 0x2B..0x32:", [u8(0x80023365 + i) for i in range(0x2B, 0x33)])
