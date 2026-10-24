"""crit_shadow.py - critic: would a CONTACT-ONLY shadow (DLL keeps the engine's exact-contact mover, but computes the binary's stale-s4 contact with a pure copy of ComputeXY from its own pre-move position) close O-G3-3?"""
import sys, os, copy, collections
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import studyB as B
import xyscen as SC
from xyscen import U
SUBJ, CRATE, FMAX = B.SUBJ, B.CRATE, B.FMAX
def run(mode, F, nt=4):
    mism_base = [0]*nt; mism_shadow = [0]*nt; n = 0
    for D in list(range(1, F, max(1, F//997))) + list(range(1, 40)):
        start = FMAX - D
        # true binary trajectory
        wb = B.world(mode); wb.ents[SUBJ][0x114] = start; SC.XM.set_mod(wb.ents[SUBJ])
        rb = [SC.tick(wb, SUBJ, F, 0) for _ in range(nt)]
        # DLL trajectory: engine mover = advance to the exact contact FMAX (never beyond)
        x = start; n += 1
        for t in range(nt):
            # shadow: binary contact computed from the DLL's position x (pure copy)
            ws = B.world(mode); ws.ents[SUBJ][0x114] = x; SC.XM.set_mod(ws.ents[SUBJ])
            sh = SC.tick(ws, SUBJ, F, 0)["ret"] != 0
            dll_contact = (x + F > FMAX)            # engine: step curtailed (or already touching) -> the entity is reported
            tru = rb[t]["ret"] != 0
            mism_base[t] += (dll_contact != tru)
            mism_shadow[t] += (sh != tru)
            x = min(x + F, FMAX)
    print("%-8s F=%d n=%d  DLL-as-is contact mismatch per tick: %s | with contact-only shadow: %s" % (mode, F, n,
          " ".join("%.3f" % (m/n) for m in mism_base), " ".join("%.3f" % (m/n) for m in mism_shadow)))
for mode in ("support", "ground"):
    for F in (159744, 196608, 393216):
        run(mode, F)
