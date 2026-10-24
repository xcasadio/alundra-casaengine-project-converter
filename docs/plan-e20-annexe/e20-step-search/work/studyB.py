"""studyB.py - the CONTACT (+0x130, the return value [sp+0x34]) of the halving search against a CRATE (entity obstacle), per mode:
  air      : no Gravity flag (0x100)                       -> s4 is assigned at every attempt
  ground   : Gravity flag, ForceZ 0, terrain within the 3 px window (PosZ = terrain + 1)  -> the 'walk on the ground' branch (s4 NOT assigned when the snapped Z is clear)
  support  : Gravity flag, ForceZ 0, but standing on a crate 16 px above the terrain (|terrain - ModZ - 1| >= 3 px)  -> the branch is NOT taken: same as air for s4
The DLL/engine always reports the obstacle on the tick the step is shortened (the last blocked test of the bisection names it).
Usage: python -I studyB.py
"""
import sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)
CRATE = SC.ent_addr(2)
CRATE_LEFT = 130 * U                     # crate PosX + ModX (left edge)
# subject mx = px - 10 px ; free iff omx >= mx + 21 px  (size, see xymodel.find_candidate)  -> greatest free px = CRATE_LEFT + 10 px - 21 px
FMAX = CRATE_LEFT + 10 * U - 21 * U


def world(mode):
    w = SC.new_world()
    if mode == "support":
        pz = 16 * U + 1
        SC.wall_columns(w, 0, 51, attr=0, height_byte=0)
    else:
        pz = 1
    flags = 0x80 | (0x100 if mode in ("ground", "support") else 0)
    SC.put(w, SUBJ, flags, (100 * U, 100 * U, pz), d=24)
    # a crate 24x16 footprint, 32 px tall, whose Z box overlaps the subject in all three modes
    E = SC.put(w, CRATE, 0x80, (CRATE_LEFT + 12 * U, 100 * U, 0), off=(-12, -8, 0), size=(24, 16), depth=48, d=0)
    w.collidables = [CRATE, SUBJ]
    return w


def run(mode, start, force, nticks=4):
    w = world(mode)
    E = w.ents[SUBJ]
    E[0x114] = start
    SC.XM.set_mod(E)
    return [SC.tick(w, SUBJ, force, 0) for _ in range(nticks)]


def main():
    lines = []
    P = lines.append
    P("STUDY B - contact (+0x130) of the halving search against a crate; exact contact at PosX = %d raw (%.4f px)" % (FMAX, FMAX / U))
    P("'ret' = value stored at +0x130 (the crate address when non-zero). D = raw distance from the start to the exact contact.")
    P("")
    for force, fname in ((159744, "2.4375 px"), (196608, "3 px"), (393216, "6 px"), (79872, "1.21875 px"), (106496, "1.625 px")):
        for mode in ("air", "support", "ground"):
            step = max(1, force // 997)
            Ds = list(range(1, force, step)) + list(range(1, 40))
            c_ret = [collections.Counter() for _ in range(4)]
            fa = collections.Counter()
            for D in Ds:
                r = run(mode, FMAX - D, force, 4)
                for i in range(4):
                    c_ret[i][r[i]["ret"] != 0] += 1
                fa[next((i + 1 for i, t in enumerate(r) if t["fa"]), 0)] += 1
            n = len(Ds)
            P("force %-10s mode %-8s starts %d : P(ret!=0) tick1 %.3f  tick2 %.3f  tick3 %.3f  tick4 %.3f ; first FA tick: %s" % (
                fname, mode, n, c_ret[0][True] / n, c_ret[1][True] / n, c_ret[2][True] / n, c_ret[3][True] / n,
                ", ".join("%d:%d" % (k, fa[k]) for k in sorted(fa))))
        P("")
    out = "\n".join(lines)
    print(out)
    open(os.path.join(HERE, "..", "out", "studyB.txt"), "w", encoding="utf-8").write(out + "\n")


if __name__ == "__main__":
    main()
