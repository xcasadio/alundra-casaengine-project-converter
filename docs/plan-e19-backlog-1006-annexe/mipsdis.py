import sys
import capstone

path = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
data = open(path, "rb").read()
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def dis(start, count):
    off = 0x800 + start - 0x80020000
    for i in range(count):
        w = data[off + 4 * i: off + 4 * i + 4]
        ins = list(md.disasm(w, start + 4 * i))
        if ins:
            print("%08x  %s %s" % (start + 4 * i, ins[0].mnemonic, ins[0].op_str))
        else:
            print("%08x  .word %s" % (start + 4 * i, w.hex()))


start = int(sys.argv[1], 16)
count = int(sys.argv[2])
dis(start, count)
