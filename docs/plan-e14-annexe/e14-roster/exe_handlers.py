"""Native handler tables of ALUN_CD.EXE (France): every non-null entry of the slot tables A, C, D, E, F, the span of each
function (upper bound = start of the next known handler, tightened by a branch/return analysis), its call targets and
random-generator sites. Read-only on the EXE. Writes cache/exe_handlers.pkl (scratch folder).
Run: python -B exe_handlers.py"""
import os
import pickle
import re
import sys
import time

import capstone

import exelib
import decomp_index

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache")
os.makedirs(OUT, exist_ok=True)

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def decode_all():
    """Word-by-word decode of the whole text segment: addr -> (mnemonic, op_str, raw word)."""
    cache = os.path.join(OUT, "exe_ins.pkl")
    if os.path.exists(cache):
        with open(cache, "rb") as fh:
            return pickle.load(fh)
    code = exelib.DATA[0x800:0x800 + exelib.T_SIZE]
    ins = {}
    for a in range(0, len(code) - 3, 4):
        addr = exelib.T_ADDR + a
        got = None
        for x in md.disasm(code[a:a + 4], addr):
            got = (x.mnemonic, x.op_str)
        ins[addr] = got
    with open(cache, "wb") as fh:
        pickle.dump(ins, fh)
    return ins


def parse_target(op):
    try:
        return int(op.strip(), 0)
    except ValueError:
        return None


def analyse(addr, limit_end, ins):
    """Walk from addr; returns dict(end, jal set, rng count, jump_table_use)."""
    a = addr
    max_target = addr
    jals = {}
    rng = 0
    jr_other = False
    n = 0
    end = None
    while a < limit_end:
        x = ins.get(a)
        if x is None:
            a += 4
            continue
        mn, op = x
        if mn == "jal":
            t = parse_target(op)
            if t is not None:
                jals[t] = jals.get(t, 0) + 1
        elif mn == "lui" and op.replace(" ", "").endswith(",0x7d2b"):
            rng += 1
        elif mn in ("j", "b", "beq", "bne", "beqz", "bnez", "blez", "bgtz", "bltz", "bgez", "bgezal", "bltzal", "bc1t", "bc1f"):
            t = parse_target(op.split(",")[-1])
            if t is not None and t > max_target and t < limit_end:
                max_target = t
        elif mn == "jr":
            if op.strip() == "$ra":
                if a + 8 > max_target and not jr_other:
                    end = a + 8
                    break
            else:
                jr_other = True
        a += 4
        n += 1
    if end is None:
        end = limit_end
    # recount calls/rng strictly inside [addr, end)
    jals2 = {}
    rng2 = 0
    for b in range(addr, end, 4):
        x = ins.get(b)
        if not x:
            continue
        mn, op = x
        if mn == "jal":
            t = parse_target(op)
            if t is not None:
                jals2[t] = jals2.get(t, 0) + 1
        elif mn == "lui" and op.replace(" ", "").endswith(",0x7d2b"):
            rng2 += 1
    return {"end": end, "jals": jals2, "rng": rng2, "jr_other": jr_other}


def main():
    t0 = time.time()
    ins = decode_all()
    print("decoded", len(ins), f"{time.time() - t0:.0f}s", flush=True)
    entries = {}  # (slot, code) -> addr
    for slot in (0, 2, 3, 4, 5):
        base = exelib.slot_table_base(slot)
        for code in range(256):
            addr = exelib.u32(base + 4 * code)
            if addr:
                entries[(slot, code)] = addr
    addrs = sorted(set(entries.values()))
    res = {}
    for (slot, code), addr in sorted(entries.items()):
        i = addrs.index(addr)
        nxt = addrs[i + 1] if i + 1 < len(addrs) else addr + 0x800
        info = analyse(addr, nxt, ins)
        res[(slot, code)] = {"addr": addr, "next": nxt, **info}
    null_codes = {(slot, code) for slot in (0, 2, 3, 4, 5) for code in range(256) if (slot, code) not in entries}
    with open(os.path.join(OUT, "exe_handlers.pkl"), "wb") as fh:
        pickle.dump({"entries": res, "null": null_codes, "addrs": addrs}, fh)
    print("entries", len(res), "distinct addrs", len(addrs), f"{time.time() - t0:.0f}s")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
