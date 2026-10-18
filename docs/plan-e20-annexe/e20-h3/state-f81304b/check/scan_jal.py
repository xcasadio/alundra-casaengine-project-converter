#!/usr/bin/env python3
"""Counter-check helper (own code): list the `jal <target>` call sites in the text of ALUN_CD.EXE (France), read-only.
Usage: python -B scan_jal.py 0x8002ed64 [0x...]"""
import struct
import sys

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
with open(EXE_PATH, "rb") as f:
    DATA = f.read()
PC0, GP0, T_ADDR, T_SIZE = struct.unpack_from("<IIII", DATA, 0x10)
end = min(len(DATA), 0x800 + T_SIZE)
targets = [int(a, 16) for a in sys.argv[1:]]
for t in targets:
    sites = []
    for o in range(0x800, end - 3, 4):
        w = struct.unpack_from("<I", DATA, o)[0]
        if (w >> 26) == 3 and (((w & 0x03FFFFFF) << 2) | 0x80000000) == t:
            sites.append(T_ADDR + (o - 0x800))
    print(f"jal {t:#010x}: {len(sites)} sites")
    print("  " + ", ".join(f"{s:#010x}" for s in sites))
