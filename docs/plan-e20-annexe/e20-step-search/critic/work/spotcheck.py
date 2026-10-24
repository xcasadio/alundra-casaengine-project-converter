"""spotcheck.py - replays the scenario worlds of studies A/B/F (cell wall, crate in air / on support / on the ground, point-blank arrow, 12 arrow starts) against the REAL code
of ALUN_CD.EXE in the interpreter, tick by tick: the model's state after each tick is the real code's (full entity block diff), i.e. the studies are measurements of the
binary's own function, not only of the model."""
import sys, os, copy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import xyscen as SC
import xyfuzz as FZ
import studyB as SB
import studyA as SA
from xyscen import U


def tick_both(w, addr, fx, fy):
    """one tick on the model and on the real code, same input world; returns (n_mismatch, model_result)"""
    E = w.ents[addr]
    E[0xe4], E[0xe8] = fx, fy
    E[0x13c] = 0
    w_real = copy.deepcopy(w)
    r = SC.tick(w, addr, fx, fy)
    # the model world w already contains the tick's output; the real code starts from the world as it was BEFORE the tick: w_real
    ret_real, mem, _ = FZ.run_real(w_real, addr)
    # re-run the model on a fresh copy to compare entity blocks
    w_m = copy.deepcopy(w_real)
    ret_m = SC.XM.compute_xy(w_m, addr, None)
    diffs = FZ.compare(w_m, addr, ret_m, ret_real, mem)
    return len(diffs), r


bad = 0
total = 0
# A: cell wall, 4 ticks, several D
for F in (159744, 196608, 393216, 56473):
    for D in list(range(1, 60)) + list(range(1000, F, max(1, F // 150))):
        w = SA.world()
        w.ents[SA.SUBJ][0x114] = SA.FMAX - D
        SC.XM.set_mod(w.ents[SA.SUBJ])
        for _ in range(4):
            n, _r = tick_both(w, SA.SUBJ, F, 0)
            bad += n
            total += 1
# B: crate, three modes
for mode in ("air", "support", "ground"):
    for F in (159744, 196608, 393216):
        for D in list(range(1, 40)) + list(range(1000, F, max(1, F // 120))):
            w = SB.world(mode)
            w.ents[SB.SUBJ][0x114] = SB.FMAX - D
            SC.XM.set_mod(w.ents[SB.SUBJ])
            for _ in range(4):
                n, _r = tick_both(w, SB.SUBJ, F, 0)
                bad += n
                total += 1
# F: point-blank arrow, x0 = 100..109 : 3 px then 6 px
for x0 in range(100, 110):
    w = SC.new_world()
    SC.wall_columns(w, 5)
    SC.put(w, SA.SUBJ, 0, (x0 * U, 100 * U, 1), d=24)
    for k in range(6):
        n, _r = tick_both(w, SA.SUBJ, 3 * U if k == 0 else 6 * U, 0)
        bad += n
        total += 1
print("scenario ticks replayed on the real code: %d ; ticks with any difference in the 0x294-byte entity block or in the return value: %d" % (total, bad))
