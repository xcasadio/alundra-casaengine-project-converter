"""Critic listings: capstone MIPS (little endian) of the functions at stake."""
import os
import sys
import capstone
from c_exe import load

pc0, gp0, tbase, tsize, img = load()
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = False

FUNCS = {
    "tryuseitem_8002ED64": 0x8002ED64,
    "dispatch_8003499C": 0x8003499C,
    "start_herb_80034680": 0x80034680,
    "start_lpotion_800346F0": 0x800346F0,
    "start_elixir_80034760": 0x80034760,
    "start_cureale_800347D4": 0x800347D4,
    "start_spotion_80034870": 0x80034870,
    "heal_herb_80032E50": 0x80032E50,
    "heal_water1_80032F84": 0x80032F84,
    "heal_spotion_800330FC": 0x800330FC,
    "heal_lpotion_80033274": 0x80033274,
    "heal_elixir_800336F0": 0x800336F0,
    "heal_cureale_80033A2C": 0x80033A2C,
    "useitem_8004E5C4": 0x8004E5C4,
    "promote_8004E0F8": 0x8004E0F8,
    "createeffect_8003BDD8": 0x8003BDD8,
    "frame_8002BAEC": 0x8002BAEC,
}


def dis(addr, nwords):
    o = addr - tbase
    code = bytes(img[o:o + 4 * nwords])
    return list(md.disasm(code, addr))


def func_end(addr, limit=0x400):
    """Linear scan; end after the first jr ra whose pc >= max forward target and delay slot."""
    ins = dis(addr, limit)
    maxt = addr
    for i, x in enumerate(ins):
        m = x.mnemonic
        ops = x.op_str
        if m in ("beq", "bne", "blez", "bgtz", "bltz", "bgez", "beqz", "bnez", "b", "bal", "bltzal", "bgezal", "bc0f", "bc0t"):
            try:
                t = int(ops.split(",")[-1].strip(), 0)
                if t > maxt and t < addr + 0x1000:
                    maxt = t
            except ValueError:
                pass
        if m == "j":
            t = int(ops, 0)
            if addr <= t < addr + 0x1000 and t > maxt:
                maxt = t
        if m == "jr" and "ra" in ops:
            if x.address >= maxt:
                return i + 2
    return limit


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, a in FUNCS.items():
        n = func_end(a, 0x500 if name == "frame_8002ED64" else 0x400)
        if name == "frame_8002BAEC":
            n = 0x1c0 // 4  # up to ~0x8002BCAC+, whole per-frame function is long: dump 0x500 words limited below
            n = 0x4F0 // 4
        ins = dis(a, n)
        with open(os.path.join(out, name + ".txt"), "w") as f:
            for x in ins:
                f.write("%08X  %08x  %-8s %s\n" % (x.address, int.from_bytes(x.bytes, "little"), x.mnemonic, x.op_str))
        print(name, hex(a), "words", n)
