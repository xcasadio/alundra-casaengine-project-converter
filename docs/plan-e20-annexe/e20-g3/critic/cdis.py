"""Critic: own read-only disassembler of ALUN_CD.EXE (France).
Usage: python -B -I dis.py START END [START END ...]   (hex virtual addresses)"""
import sys, struct, hashlib
sys.path.append(r"C:\Users\casad\AppData\Roaming\Python\Python314\site-packages")
import capstone

P = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
D = open(P, "rb").read()
assert hashlib.sha1(D).hexdigest() == "a705f8dab665645af5dfaf8fb348c7926b5caa58"
T_ADDR = struct.unpack_from("<I", D, 0x18)[0]
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def word(a):
    return struct.unpack_from("<I", D, 0x800 + a - T_ADDR)[0]


def dis(a, b):
    o = 0x800 + a - T_ADDR
    for i in md.disasm(D[o:o + b - a], a):
        print(f"{i.address:#010x}  {word(i.address):08x}  {i.mnemonic:8s} {i.op_str}")


if __name__ == "__main__":
    args = sys.argv[1:]
    for k in range(0, len(args), 2):
        print(f"; ---- {args[k]} .. {args[k+1]}")
        dis(int(args[k], 16), int(args[k + 1], 16))
