"""Join of the EXE handler tables with the decompilation's registrations (names, addresses, mismatches).
Imported by build_roster.py; run alone it prints the diff between the binary tables and SpriteEventHandlers.cs."""
import os
import pickle
import sys

import exelib
import decomp_index

HERE = os.path.dirname(os.path.abspath(__file__))
SLOT_LETTER = "ABCDEF"

# Names of helper routines of the EXE, from the plan annexes (docs/plan-e14-annexe/e14a/dll-natives.md sections 2-3,
# docs/plan-e20-annexe) or the decompilation address comments; the annex entries are listed here with their source.
ANNEX_NAMES = {
    0x8003A374: "ComputeNewHp (dll-natives.md 2.2)",
    0x8003A59C: "DestroyEntity (dll-natives.md 1.3)",
    0x8002E814: "GetDirectionToTarget (dll-natives.md 2.2)",
    0x800490FC: "PlaySfx (dll-natives.md 2.4)",
    0x8007FE20: "CalculateEntityRelativePosition (dll-natives.md 2.4)",
    0x8007FE8C: "RelativePosition2 (plan-e14-ia-native.md 0.3)",
    0x8007FEF8: "StartFlying/StartWalk (dll-natives.md 2.4)",
    0x80080144: "HandleAnimationDirection (dll-natives.md 2.4)",
    0x8008048C: "TryAttackPlayer2 (dll-natives.md 2.4)",
    0x800805C8: "TryAttackPlayerFront (dll-natives.md 2.4)",
    0x8003A7B0: "CheckAndTriggerTileEffect (AlundraWaveSwordAi.cs)",
    0x8003BE74: "CreateBreakEffect? (dll-natives.md 8)",
}


def load():
    with open(os.path.join(HERE, "cache", "exe_handlers.pkl"), "rb") as fh:
        exe = pickle.load(fh)
    addr_idx, methods = decomp_index.build_addr_index()
    regs = decomp_index.build_registrations()
    return exe, addr_idx, methods, regs


def func_name_at(addr, addr_idx):
    names = addr_idx.get(addr)
    if names:
        return names[0][0]
    return ANNEX_NAMES.get(addr)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    exe, addr_idx, methods, regs = load()
    entries = exe["entries"]
    only_exe = sorted(set(entries) - set(regs))
    only_reg = sorted(set(regs) - set(entries))
    print("EXE non-null:", len(entries), " decomp registrations:", len(regs))
    print("in EXE, not registered in decomp:", [(SLOT_LETTER[s], c) for s, c in only_exe])
    print("registered in decomp, null in EXE:", [(SLOT_LETTER[s], c, regs[(s, c)][0]) for s, c in only_reg])
    mismatch = []
    for (s, c), info in sorted(entries.items()):
        if (s, c) in regs:
            fn = regs[(s, c)][0].split(".")[-1]
            hits = addr_idx.get(info["addr"], [])
            names = [n for n, _ in hits]
            if names and fn not in names:
                mismatch.append((SLOT_LETTER[s], c, hex(info["addr"]), fn, names))
    print("decomp function name differs from the function commented at the EXE address:", len(mismatch))
    for m in mismatch[:40]:
        print(m)
    no_comment = [(SLOT_LETTER[s], c, hex(i["addr"])) for (s, c), i in sorted(entries.items()) if i["addr"] not in addr_idx]
    print("EXE handler addresses without a decompilation address comment:", len(no_comment), no_comment[:30])
