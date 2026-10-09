"""D6 sensitivity check: prove d6_boxes.py can fail. Runs the comparison with REAL mutations injected into
what it reads back from the export (not simulated inside the test), and requires the mismatch count to rise.
Three mutations, each run separately:
  M1 every 997th exported fixture has its Depth and Height swapped;
  M2 every 991st exported fixture has OffsetZ + 1;
  M3 every 983rd exported keyframe is dropped (the volume of the previous keyframe then stays active).
Outputs go to out/mutation_* (never over the real out/ files). Run: python -B mutation_test.py
"""
import sys, os, json, io, contextlib
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import d6_boxes as d

orig_fixture_box = d.fixture_box
orig_jload = d.jload


def run(tag, patch):
    d.OUT = os.path.join(HERE, "out", "mutation_" + tag)
    os.makedirs(d.OUT, exist_ok=True)
    patch()
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        d.main()
    s = json.load(open(os.path.join(d.OUT, "summary.json")))
    d.fixture_box = orig_fixture_box
    d.jload = orig_jload
    return s["frames_with_record"], s["exact"], s["mismatch"], s["nobox_mismatch"], s["mismatch_classes"]


def m1():
    n = [0]

    def fb(fx):
        six, an = orig_fixture_box(fx)
        n[0] += 1
        if n[0] % 997 == 0:
            six = (six[0], six[1], six[2], six[3], six[5], six[4])
        return six, an
    d.fixture_box = fb


def m2():
    n = [0]

    def fb(fx):
        six, an = orig_fixture_box(fx)
        n[0] += 1
        if n[0] % 991 == 0:
            six = (six[0], six[1], six[2] + 1, six[3], six[4], six[5])
        return six, an
    d.fixture_box = fb


def m3():
    n = [0]

    def jl(path):
        o = orig_jload(path)
        if path.endswith(".anim2d") and isinstance(o, dict) and o.get("collision_keyframes"):
            kept = []
            for k in o["collision_keyframes"]:
                n[0] += 1
                if n[0] % 983 == 0:
                    continue
                kept.append(k)
            o["collision_keyframes"] = kept
        return o
    d.jload = jl


for tag, p in (("M1_swap_depth_height", m1), ("M2_offsetz_plus1", m2), ("M3_drop_keyframe", m3)):
    print(tag, run(tag, p))
