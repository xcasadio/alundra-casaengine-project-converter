"""studyA.py - the halving search against a CELL wall: how far from the exact contact does the binary stop, tick by tick, and what does an
exact-contact mover (the engine's bisection, float32 model) give for the same starts.
Subject: hero-sized box, east (TargetDirection 24), wall = cells with attribute 0x40 from column 5 (x >= 120): the exact contact is x = 109.0.
Usage: python -I studyA.py
"""
import sys, os, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import numpy as np
import xyscen as SC
from xyscen import U

SUBJ = SC.ent_addr(1)
FMAX = 109 * U                       # greatest free PosX (raw): right corner floor(px/65536 + 11 - 2^-16) <= 119


def world():
    w = SC.new_world()
    SC.wall_columns(w, 5)
    SC.put(w, SUBJ, 0, (100 * U, 100 * U, 1), d=24)
    return w


def binary_ticks(start, force, nticks=4):
    w = world()
    w.ents[SUBJ][0x114] = start
    XM = SC.XM
    XM.set_mod(w.ents[SUBJ])
    out = []
    for _ in range(nticks):
        out.append(SC.tick(w, SUBJ, force, 0))
    return out


# ---- the engine's AdvanceBlockedAxisToContact in float32 (CharacterControllerComponent.cs:1231-1298) -------------------------------------------
f32 = np.float32


def engine_axis(x_raw, amount_raw, blocked_raw):
    """x/amount as the DLL hands them (float px). blocked_raw(xf) -> bool on the candidate root, converted with round(x*65536) as the probe does."""
    x = f32(x_raw / 65536.0)
    amount = f32(amount_raw / 65536.0)

    def blocked(xf):
        return blocked_raw(int(np.round(np.float64(xf) * 65536.0)))

    if not blocked(f32(x + amount)):
        return f32(x + amount)
    probe = np.nextafter(x, f32(np.inf)) if amount > 0 else np.nextafter(x, f32(-np.inf))
    probe_amount = f32(probe - x)
    if blocked(f32(x + probe_amount)):
        return x
    free, bl = f32(0), f32(1)
    for _ in range(24):
        mid = f32(f32(0.5) * f32(free + bl))
        cand = f32(x + f32(amount * mid))
        if blocked(cand):
            bl = mid
        else:
            free = mid
    if free == 0:
        return f32(x + probe_amount)
    adv = f32(amount * free)
    for _ in range(4):
        if not blocked(f32(x + adv)):
            return f32(x + adv)
        adv = np.nextafter(adv, f32(0))
    return f32(x + adv)


def main():
    forces = [159744, 79872, 196608, 393216, 106496, 53248, 56473, 37652]
    names = {159744: "2.4375 px (hero walk)", 79872: "1.21875 px", 196608: "3 px (arrow)", 393216: "6 px (charged arrow / fire)", 106496: "1.625 px", 53248: "0.8125 px",
             56473: "0.8617 px (diagonal comp.)", 37652: "0.5745 px (diagonal comp.)"}
    lines = []
    P = lines.append
    P("STUDY A - binary halving search vs a cell wall (exact contact at PosX = 109.0 = %d raw)" % FMAX)
    P("D = distance from the start to the exact contact (raw units); tick 1 is the first tick whose step is shortened (D < force).")
    P("")
    for F in forces:
        deficits = collections.Counter()
        n = 0
        tick2_creep = 0
        fa_offset = collections.Counter()      # first tick (1..4) with ForceAdjusted
        fa_offset_engine = collections.Counter()
        eng_def = collections.Counter()
        ret_nonzero = 0
        total_tick_to_exact = collections.Counter()
        step = max(1, F // 997)
        Ds = list(range(1, F, step)) + list(range(1, 40)) + [F - 1]
        for D in Ds:
            r = binary_ticks(FMAX - D, F, 5)
            S = r[0]["dx"]
            deficits[D - S] += 1
            n += 1
            first = next((i + 1 for i, t in enumerate(r) if t["fa"]), 0)
            fa_offset[first] += 1
            # tick at which the exact contact is reached
            reach = next((i + 1 for i, t in enumerate(r) if t["x"] == FMAX), 0)
            total_tick_to_exact[reach] += 1
            # engine model
            xe = engine_axis(FMAX - D, F, lambda rawx: rawx > FMAX)
            eng_def[FMAX - int(np.round(np.float64(xe) * 65536.0))] += 1
        P("force %d raw = %s ; %d starts" % (F, names[F], n))
        P("  binary: deficit (D - S) at tick 1, raw units: " + ", ".join("%d:%d" % (k, deficits[k]) for k in sorted(deficits)[:12]) + ("  ... max %d" % max(deficits)))
        P("  binary: tick at which PosX == exact contact (0 = not within 5 ticks): " + ", ".join("%d:%d" % (k, total_tick_to_exact[k]) for k in sorted(total_tick_to_exact)))
        P("  binary: first tick with ForceAdjusted (0 = none in 5): " + ", ".join("%d:%d" % (k, fa_offset[k]) for k in sorted(fa_offset)))
        P("  engine model (float32 bisection, 24 it.): deficit FMAX - x after tick 1, raw: " + ", ".join("%d:%d" % (k, eng_def[k]) for k in sorted(eng_def)[:12]))
        P("")
    out = "\n".join(lines)
    print(out)
    open(os.path.join(HERE, "..", "out", "studyA.txt"), "w", encoding="utf-8").write(out + "\n")


if __name__ == "__main__":
    main()
