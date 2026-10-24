"""xymut.py - mutation check of the model/fuzz pair: each mutant edits ONE spot of xymodel.py; the generic and the focused fuzz (1200 + 1200 vectors)
must show at least one mismatch (a mutant that loops forever is stopped by a guard and counts as killed). Usage: python -I xymut.py"""
import sys, os, random, types
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
SRC = open(os.path.join(HERE, "xymodel.py"), encoding="utf-8").read()
GUARD_OLD = "            rec = dict(s2=s2"
GUARD_NEW = "            _N[0] += 1\n            if s5 > 80 or _N[0] > 3000: raise RuntimeError('runaway')\n            rec = dict(s2=s2"
RESET_OLD = "    E = w.ents[a]\n    sp28 = 0"
RESET_NEW = "    E = w.ents[a]\n    _N[0] = 0\n    sp28 = 0"

MUTANTS = {
    "return the s4 of the LAST attempt instead of the s4 saved at the top of it":
        [("return sp34                        # 0x80037DFC, no recompute", "return s4"),
         ("        E[0x138] = s32(ground_height(w, a))\n        return sp34", "        E[0x138] = s32(ground_height(w, a))\n        return s4")],
    "halving of -1 not special-cased (accept path)":
        [("                s2 = 0 if s2 == -1 else (s2 >> 1)\n                s1 = 0 if s1 == -1 else (s1 >> 1)\n                if sp24:\n                    if s2 == 0 and s1 == 0:",
          "                s2 = (s2 >> 1)\n                s1 = 0 if s1 == -1 else (s1 >> 1)\n                if sp24:\n                    if s2 == 0 and s1 == 0:")],
    "ground branch also writes s4 when the snapped Z is clear":
        [("                    # else: jump straight to the wall test (0x8003791C) with s4 left UNTOUCHED", "                    else:\n                        s4 = c")],
    "ground branch: s4 reset to 0 when the snapped Z is clear":
        [("                    # else: jump straight to the wall test (0x8003791C) with s4 left UNTOUCHED", "                    else:\n                        s4 = 0")],
    "ground tolerance symmetric 0x30000":
        [("tol = 0x30000 if v >= 0 else 0x30003", "tol = 0x30000")],
    "ForceAdjusted also when sub-steps were accepted":
        [("        if sp2c:\n            pass\n", "        if sp2c:\n            fa = True\n")],
    "slide allowed twice":
        [("            sp28 = 1\n            d = E.get(0x8c, 0) & M32", "            sp28 = 0\n            d = E.get(0x8c, 0) & M32")],
    "accepted first attempt keeps halving":
        [("                if s5 == 0:\n                    return sp34", "                if s5 == 99:\n                    return sp34")],
    "hero wall: lock timer rule dropped":
        [("        if t4 == 0x20:", "        if False:")],
    "candidate: carried-entity exemption dropped":
        [("    if E.get(0x28, 0) != 0:\n        return 0\n", "")],
    "NoObstacleSlide ignored":
        [("(E.get(0x6c, 0) & 0x2000) or", "False or")],
    "failed attempt does not restore Z":
        [("E[0x114], E[0x118], E[0x11c] = s32(s7), s32(fp), s32(sp30)", "E[0x114], E[0x118] = s32(s7), s32(fp)")],
    "diagonal loop continues when ONE half is non-zero":
        [("                if s2 != 0 and s1 != 0:\n                    s5 += 1\n                    continue", "                if s2 != 0 or s1 != 0:\n                    s5 += 1\n                    continue")],
    "stale-s4: s4 not kept across attempts (reset to 0 at each attempt top)":
        [("            sp34 = s4\n", "            sp34 = s4\n            s4 = 0\n")],
}

import xyfuzz as FZ


def run(src, gen, n=1200, seed=5):
    mod = types.ModuleType("xymodel_mut")
    src = src.replace(GUARD_OLD, GUARD_NEW, 1).replace(RESET_OLD, RESET_NEW, 1)
    src = "_N = [0]\n" + src
    exec(compile(src, "xymodel_mut", "exec"), mod.__dict__)
    FZ.XM = mod
    rnd = random.Random(seed)
    bad = 0
    for k in range(n):
        w, a = gen(rnd)
        w.stairs = [FZ.s32(FZ.mips.Mem().rw(0x800236D4 + 4 * i)) for i in range(24)]
        w2 = FZ.clone_world(w)
        try:
            rm = mod.compute_xy(w2, a, None)
        except RuntimeError:
            bad += 1
            continue
        rr, m, _ = FZ.run_real(w, a)
        if FZ.compare(w2, a, rm, rr, m):
            bad += 1
    return bad


if __name__ == "__main__":
    print("baseline (no mutation) mismatching vectors: generic %d, focused %d" % (run(SRC, FZ.gen_world), run(SRC, FZ.gen_world_focus)), flush=True)
    for name, edits in MUTANTS.items():
        src = SRC
        ok = True
        for old, new in edits:
            if old not in src:
                print("PATTERN NOT FOUND for", name, flush=True)
                ok = False
                break
            src = src.replace(old, new, 1)
        if ok:
            g, f = run(src, FZ.gen_world), run(src, FZ.gen_world_focus)
            print("%-80s generic %4d / 1200   focused %4d / 1200   %s" % (name, g, f, "KILLED" if (g or f) else "SURVIVED"), flush=True)
