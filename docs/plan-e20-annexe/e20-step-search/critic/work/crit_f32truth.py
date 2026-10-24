"""crit_f32truth.py - critic: is an INTEGER truth of XY needed for tick-exact fidelity, or does a float32 root (what the engine holds) suffice?
Uses the reader's model (xymodel, replayed 55 000 + 3000 vectors against the real code, re-run by me with fresh seeds: 0 diffs) as the stand-in for the binary.
Two movers run the same ticks against the same east cell wall:
  INT : the position truth is the 16.16 integer (what the binary has, what a DLL-owned mover would have)
  F32 : after every tick the position is stored as float32 px (the engine root) and read back as round(root*65536) (what EntityScriptProxy does)
We compare per start phase: raw deficit at tick 1, first ForceAdjusted tick, contact tick, and the final resting x.
Wall at column C (x >= 24*C): exact contact PosX = (24*C - 11) px.
"""
import sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import numpy as np
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)


def world(col):
    w = SC.new_world()
    SC.wall_columns(w, col)
    SC.put(w, SUBJ, 0, (100 * U, 100 * U, 1), d=24)
    return w


def f32_roundtrip(raw):
    return int(np.round(np.float64(np.float32(raw / 65536.0)) * 65536.0))


def run(col, start, F, truth, nticks=6):
    w = world(col)
    E = w.ents[SUBJ]
    E[0x114] = f32_roundtrip(start) if truth == "F32" else start
    SC.XM.set_mod(E)
    res = []
    for _ in range(nticks):
        if truth == "F32":
            E[0x114] = f32_roundtrip(E[0x114])
            SC.XM.set_mod(E)
        t = SC.tick(w, SUBJ, F, 0)
        if truth == "F32":
            E[0x114] = f32_roundtrip(E[0x114])
            SC.XM.set_mod(E)
            t["x"] = E[0x114]
        res.append(t)
    return res


def main():
    out = []
    for col in (10, 15, 30):       # exact contact 229, 349, 709 px
        fmax = (24 * col - 11) * U
        for F in (159744, 196608, 393216):
            diff_def = diff_fa = diff_ret = diff_final = n = 0
            ex = []
            for D in list(range(1, F, max(1, F // 997))) + list(range(1, 40)):
                a = run(col, fmax - D, F, "INT")
                b = run(col, fmax - D, F, "F32")
                n += 1
                fa_a = next((i for i, t in enumerate(a) if t["fa"]), -1)
                fa_b = next((i for i, t in enumerate(b) if t["fa"]), -1)
                if fa_a != fa_b:
                    diff_fa += 1
                    if len(ex) < 3:
                        ex.append((D, fa_a, fa_b))
                if (a[0]["x"] - (fmax - D)) != (b[0]["x"] - f32_roundtrip(fmax - D)):
                    diff_def += 1
                if a[-1]["x"] != b[-1]["x"]:
                    diff_final += 1
            out.append("col %2d contact %d px  F=%d : starts %d  firstFA tick differs %d (%.1f%%) ; tick-1 advance differs %d ; resting x differs %d  ex(D,faINT,faF32)=%s" % (
                col, 24 * col - 11, F, n, diff_fa, 100.0 * diff_fa / n, diff_def, diff_final, ex))
            print(out[-1], flush=True)
    open(os.path.join(HERE, "..", "out", "crit_f32truth.txt"), "w").write("\n".join(out) + "\n")


if __name__ == "__main__":
    main()
