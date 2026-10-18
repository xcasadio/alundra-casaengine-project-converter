"""c0_loaddelay.py - static scan: instruction right after a load that READS the loaded register (R3000 load-delay hazard: it sees the OLD value;
my interpreter, like the other agent's, gives the NEW value). Reports hazards in the whole text and flags those in the functions of interest."""
import struct
from cmips import *

LOADS = {32, 33, 34, 35, 36, 37, 38, 39}  # lb lh lwl lw lbu lhu lwr (+ 39 unused)
lo, hi = 0x80020000, IMG_END


def regs_read(ins):
    op = ins >> 26
    rs = (ins >> 21) & 31
    rt = (ins >> 16) & 31
    fn = ins & 63
    if op == 0:
        if fn in (0, 2, 3):
            return {rt}
        if fn in (8, 9, 17, 19):
            return {rs}
        if fn in (16, 18, 12, 13):
            return set()
        return {rs, rt}
    if op in (2, 3):
        return set()
    if op == 15:
        return set()
    if op in (4, 5):
        return {rs, rt}
    if op in (6, 7, 1):
        return {rs}
    if op in (40, 41, 43, 42, 46):
        return {rs, rt}
    return {rs}


hz = []
a = lo
while a < hi - 8:
    ins = img_u32(a)
    op = ins >> 26
    if op in (32, 33, 35, 36, 37):
        rt = (ins >> 16) & 31
        if rt:
            nx = img_u32(a + 4)
            if rt in regs_read(nx):
                hz.append(a)
    a += 4
print("load-delay hazards in whole text:", len(hz))
for x in hz[:40]:
    print(hex(x))
import sys
ranges = {"push": (0x800366FC, 0x800367E4), "forcepass": (0x80036828, 0x80036BE0), "slotpass": (0x80038634, 0x800386D0), "lists": (0x800384F4, 0x80038634),
          "query": (0x8003AF70, 0x8003B0E8), "sort": (0x8003AE04, 0x8003AF70), "camera": (0x8002CDA0, 0x8002CF40), "sway": (0x8002C894, 0x8002C9F0),
          "e13": (0x8007F30C, 0x8007F400), "fire2": (0x80035C64, 0x80035E80), "cancel": (0x80034E08, 0x80035000), "birth": (0x80039D04, 0x80039D44)}
for k, (s, e) in ranges.items():
    print(k, [hex(x) for x in hz if s <= x < e])
