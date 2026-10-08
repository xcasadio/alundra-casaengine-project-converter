"""Counter-check (O-E19-78): the binary's map-event freeze window on schedules the discovery did NOT measure (close at +24 / +30;
L1 +24, L1 back +66, close +108), to compare with the candidate port's per-tick values measured by VerifyO78Probes.P1.
Unlike menuopen_window.py, UpdateWorld 0x8002E058 runs for real (its printf/debug callees stubbed), and the record is taken by a hook on
RunMapEvents 0x8003C67C itself (its first test is `0x800DC4B8 & 0x48`, 0x8003C680-0x8003C6A0); UpdateEntities 0x8003B388 and the effects
0x8003C410 are stubbed (no world)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my_bin as B

RUNMAPEVENTS, UPDATEENTITIES, EFFECTS = 0x8003C67C, 0x8003B388, 0x8003C410
SCHED = {
    'close24': {0: B.START, 24: B.START},
    'close30': {0: B.START, 30: B.START},
    'close19 (discovery)': {0: B.START, 19: B.START},
    'L1@24 back@66 close@108': {0: B.START, 24: B.L1, 66: B.L1, 108: B.START},
}
P = 5
for name, pr in SCHED.items():
    m = B.machine()
    del m.hooks[B.UPDATEWORLD]               # the real UpdateWorld
    for a in (0x80081FFC, 0x80082918):        # its sprintf/print helpers
        m.hooks[a] = lambda mm: mm.r.__setitem__(2, 0)
    st = {'f': 0}
    ran = []
    def rme(mm):
        if (mm.rw(B.CTRL) & 0x48) == 0:
            ran.append(st['f'] - P)
        mm.r[2] = 0
    m.hooks[RUNMAPEVENTS] = rme
    m.hooks[UPDATEENTITIES] = lambda mm: mm.r.__setitem__(2, 0)
    m.hooks[EFFECTS] = lambda mm: mm.r.__setitem__(2, 0)
    calls = {'n': 0}
    for f in range(1, P + 180):
        st['f'] = f
        m.call(B.DISPATCHER)
        w = pr.get(f - P, 0)
        m.hooks[B.PADREAD] = (lambda x: (lambda mm: mm.r.__setitem__(2, x)))(w)
        m.call(B.UPDATE, 0)
    allf = list(range(1 - P, 180))
    skipped = [x for x in allf if x not in set(ran)]
    runs = []
    for s in skipped:
        if runs and runs[-1][1] == s - 1:
            runs[-1][1] = s
        else:
            runs.append([s, s])
    print('%-26s RunMapEvents reached=%d; skipped in Update(P+a..P+b): %s' % (name, len(ran), ', '.join('%d..%d' % (a, b) for a, b in runs)))
