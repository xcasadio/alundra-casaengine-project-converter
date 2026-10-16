"""Find load/store instructions with a given immediate offset (read-only). Usage: python -B memref.py OFFSET_HEX [sw|lw|any]"""
import sys, struct
import exelib
imm = int(sys.argv[1], 16)
kinds = {"sw":0x2B,"lw":0x23,"sh":0x29,"lh":0x21,"lhu":0x25,"lbu":0x24,"sb":0x28,"lb":0x20}
want = sys.argv[2] if len(sys.argv) > 2 else "any"
n = (len(exelib.DATA) - 0x800)//4
RN = ["zero","at","v0","v1","a0","a1","a2","a3","t0","t1","t2","t3","t4","t5","t6","t7","s0","s1","s2","s3","s4","s5","s6","s7","t8","t9","k0","k1","gp","sp","fp","ra"]
inv = {v:k for k,v in kinds.items()}
for i in range(n):
    w = struct.unpack_from("<I", exelib.DATA, 0x800 + 4*i)[0]
    op = w >> 26
    if op in inv and (w & 0xffff) == imm and (want == "any" or inv[op] == want):
        rs = (w >> 21) & 31; rt = (w >> 16) & 31
        print(f"{exelib.T_ADDR+4*i:#010x}  {inv[op]} {RN[rt]}, {imm:#x}({RN[rs]})")
