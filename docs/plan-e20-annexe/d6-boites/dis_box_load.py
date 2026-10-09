"""D6 helper: disassemble the binary's per-frame collision-record load (UpdateAnimation tail) and the
HitBox computation, with the raw instruction words, to confirm lb/lbu and the 0xFFFF test.
Read-only; imports the E19 annex lib.py by path (never edits it).
Usage: python -B dis_box_load.py > out/dis_box_load.txt
"""
import sys, struct
sys.path.insert(0, r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-h2-annexe\tools")
sys.dont_write_bytecode = True
import lib

RANGES = [
    ("UpdateAnimation: current frame -> collision record (0x80038C14..0x80038CC0)", 0x80038C14, 0x80038CC0),
    ("UpdateHitBox-like: HitBox = pos + offset (0x80038064..0x800380C0)", 0x80038064, 0x800380C0),
]
for title, a, b in RANGES:
    print("== " + title)
    n = (b - a) // 4
    for ins in lib.dis(a, n):
        w = lib.u32(ins.address)
        print("0x%08X  %08X  %-8s %s" % (ins.address, w, ins.mnemonic, ins.op_str))
    print()
