"""studyG.py - outcome classes of ComputeXYPosition over the random vectors of xyfuzz (model == binary on all of them): which (contact, ForceAdjusted) pairs occur
for which path through the function.  Classes are derived from the per-attempt trace of the model, not from the listing."""
import sys, os, random, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyfuzz as FZ
import xymodel as XM

rows = collections.Counter()
examples = {}
for gen, n, seed in ((FZ.gen_world, 6000, 31), (FZ.gen_world_focus, 6000, 32)):
    rnd = random.Random(seed)
    for k in range(n):
        w, a = gen(rnd)
        w.stairs = [FZ.s32(FZ.mips.Mem().rw(0x800236D4 + 4 * i)) for i in range(24)]
        E = w.ents[a]
        fa0 = E.get(0x13c, 0)
        E[0x13c] = 0
        tr = []
        ret = XM.compute_xy(w, a, tr)
        fa = E[0x13c]
        flags = E.get(0x6c, 0)
        if not tr:
            cls = "A  no force (both words 0)"
        else:
            acc = [not r["blocked"] for r in tr]
            restarts = sum(1 for r in tr if r["s5"] == 0) - 1
            if len(tr) == 1 and acc[0]:
                cls = "B  full step accepted at the first attempt"
            elif any(acc):
                cls = "C  halving search, >= 1 sub-step accepted (loop ends on exhaustion or on an accepted halving)"
            else:
                why = []
                if flags & 0x2000:
                    why.append("NoObstacleSlide")
                if tr[-1]["s4_after"] != 0:
                    why.append("s4!=0 at the end")
                if E.get(0x8c, 0) >= 0x20:
                    why.append("dir>=0x20")
                if restarts > 0:
                    why.append("after %d slide restart(s)" % restarts)
                cls = "D  no sub-step accepted: " + (", ".join(why) if why else "slide table says ForceAdjusted")
            if restarts > 0 and not cls.startswith("D"):
                cls += " [slide restart happened]"
        key = (cls, "ret!=0" if ret else "ret==0", "FA=1" if fa else "FA=0")
        rows[key] += 1
        examples.setdefault(key, (gen.__name__, k))
lines = ["class | ret | FA | count (of 12000 vectors)"]
for key in sorted(rows):
    lines.append("%-110s | %s | %s | %d" % (key[0], key[1], key[2], rows[key]))
out = "\n".join(lines)
print(out)
open(os.path.join(HERE, "..", "out", "studyG.txt"), "w", encoding="utf-8").write(out + "\n")
