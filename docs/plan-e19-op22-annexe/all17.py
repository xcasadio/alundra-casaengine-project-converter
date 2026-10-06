"""all17.py - the 17 sites of 0x22/0x23 run on the REAL ALUN_CD.EXE code (MIPS interpreter, real P, real RunScript, real handlers, real cells).
Three handler variants on the same physics:
  bin   : the real handler 0x8003DA70 (binary frame: PosZ_b compared with Height << 19)
  lit   : the port today (E19.h1b2): handler in the DLL frame PosZ_dll = PosZ_b - 1, literal target Height << 19
  shift : the DLL frame, target (Height << 19) - 1   (algebraically the binary rule)
  hyb   : shifted target when the root can carry it (target < 2^24), literal above (the Q rule)
  dir   : literal on an ascent, shifted on a descent (the 'per direction' reading of D-E19-94)
calls = ticks between the first call at the pc (memo) and the call that leaves the pc.
"""
import sys
sys.path.insert(0, '.')
from real import *
sys.stdout.reconfigure(encoding="utf-8")

H22 = 0x8003DA70


class Driver:
    def __init__(self, mid, rec, variant, ent_index=None):
        self.w = new_world(mid)
        self.idx, self.info = spawn(self.w, rec, ent_index)
        self.e = eaddr(self.idx)
        self.m = self.w.m
        self.variant = variant
        self.tick_no = 0
        self.rows = []
        self.target_port = None
        self.dir_sign = 0
        self.install(variant)

    def install(self, variant):
        if variant == 'bin':
            return
        m = self.m
        drv = self

        def hook(mm):
            # a0 = logic entity, a2 = &state.curPtr, a3 = state ; same contract as the real handler
            a0, a2, a3 = mm.r[4], mm.r[6], mm.r[7]
            pc = mm.rw(a2)
            if s32(mm.rw(a3 + 8)) != s32(pc):
                mm.ww(a3 + 8, pc)
                rec = mm.rw(a0 + 0x44)
                hgt = mm.rb(rec + 9)
                lit = hgt << 19
                if variant == 'lit':
                    t = lit
                elif variant == 'shift':
                    t = lit - 1
                elif variant == 'hyb':
                    t = lit - 1 if lit - 1 < (1 << 24) else lit
                elif variant == 'dir':
                    t = None      # decided at the first test (needs the direction of the force)
                mm.ww(a3 + 12, (t if t is not None else 0) & 0xFFFFFFFF)
                drv.dir_pending = (variant == 'dir')
                drv.lit = lit
                mm.r[2] = 0
                return
            posz = s32(mm.rw(a0 + 0x11C)) - 1          # the DLL frame: binary - 1
            fz = s32(mm.rw(a0 + 0xB8))
            if variant == 'dir' and getattr(drv, 'dir_pending', False):
                # the direction of travel = the sign of the force at the first test (ForceZ was written by the 1B of the same call)
                drv.dir_pending = False
                t = drv.lit if fz > 0 else drv.lit - 1
                mm.ww(a3 + 12, t & 0xFFFFFFFF)
            target = s32(mm.rw(a3 + 12))
            if target == posz:
                mm.r[2] = 1
                return
            delta = target - posz
            if delta > 0:
                if delta < fz:
                    mm.ww(a0 + 0xB8, delta & 0xFFFFFFFF)
            else:
                if fz < delta:
                    mm.ww(a0 + 0xB8, delta & 0xFFFFFFFF)
            mm.r[2] = 0
        m.hooks[H22] = hook

    def set_pc(self, pc):
        e = self.e
        self.m.ww(e + F['st'], CODE + pc)
        self.m.ww(e + F['st'] + 4, CODE + pc)
        self.m.ww(e + F['kindmark'], 2)

    def pc(self):
        return self.m.rw(self.e + F['st'] + 4) - CODE

    def step(self, script=True):
        if script:
            self.w.runscript(self.idx, 2)
        pc = self.pc()
        self.w.build_lists()
        self.w.physics()
        s = self.w.state(self.idx)
        self.rows.append((self.tick_no, pc, s))
        self.tick_no += 1
        return pc, s

    def run_until(self, pred, maxt=6000, script=True):
        for _ in range(maxt):
            pc, s = self.step(script)
            if pred(pc, s):
                return True
        return False


def measure(drv, site_pc, maxt=4000, first_is_start=False):
    """run until the 0x22/0x23 at site_pc has been called and left; returns (calls, tick_first, tick_end, PosZ_b at the end)"""
    first = drv.tick_no if first_is_start else None
    for _ in range(maxt):
        t0 = drv.tick_no
        pc, s = drv.step()
        if first is None and pc == site_pc:
            first = t0
        if first is not None and pc != site_pc:
            return t0 - first, first, t0, s["PosZ"], drv
    return None, first, None, None, drv


# ---------------------------------------------------------------- the sites
def site_127(rec, entry, pcsite, variant):
    d = Driver(127, rec, variant)
    d.set_pc(entry + 12)
    return measure(d, pcsite)


def site_89(rec, variant):
    d = Driver(89, rec, variant)
    d.set_pc(644)
    return measure(d, 663)


def site_363(variant):
    d = Driver(363, 0, variant)
    d.set_pc(445)
    assert d.run_until(lambda pc, s: pc == 448), "363: the grid did not reach the flag wait"
    d.set_pc(451)
    return measure(d, 454)


def site_36c4(variant):
    d = Driver(36, 3, variant)
    d.set_pc(464)
    assert d.run_until(lambda pc, s: pc >= 475), "36 C[4]: the platform did not land"
    for _ in range(3):
        d.step(script=False)
    d.set_pc(488)
    return measure(d, 491)


def site_36c14(rec, variant):
    d = Driver(36, rec, variant)
    d.set_pc(1224)
    # the loop 1224..1236: 1B [192,255], wait 30, 6F ; leaves to 1239 on the contact
    assert d.run_until(lambda pc, s: pc >= 1239, maxt=20000), "36 C[14]: no contact"
    for _ in range(3):
        d.step(script=False)       # the animation wait (1C), physics only
    d.set_pc(1249)
    return measure(d, 1252, first_is_start=True)


def site_115(variant):
    d = Driver(115, 19, variant)
    d.set_pc(217)
    assert d.run_until(lambda pc, s: pc > 220), "115: the block did not finish its rise"
    d.set_pc(364)
    return measure(d, 367)


def all_sites(variant):
    out = []
    ENT = {5: 256, 6: 292, 7: 328, 8: 364, 10: 400, 11: 436, 12: 472, 13: 508, 14: 544, 15: 580, 16: 616, 17: 652}
    RECS = {5: 6, 6: 7, 7: 8, 8: 9, 10: 12, 11: 11, 12: 13, 13: 14, 14: 15, 15: 16, 16: 17, 17: 18}
    for c in (5, 6, 7, 8, 10, 11, 12, 13, 14, 15, 16, 17):
        pcsite = ENT[c] + 24
        out.append(("127 C[%d] @%d rec%d" % (c, pcsite, RECS[c]),) + site_127(RECS[c], ENT[c], pcsite, variant)[:4])
    for r in (20, 21, 22, 23, 24, 25):
        out.append(("89 C[21] @663 rec%d" % r,) + site_89(r, variant)[:4])
    out.append(("363 C[4] @454 rec0",) + site_363(variant)[:4])
    out.append(("36 C[4] @491 rec3",) + site_36c4(variant)[:4])
    for r in range(42, 54):
        out.append(("36 C[14] @1252 rec%d" % r,) + site_36c14(r, variant)[:4])
    out.append(("115 B[2] @367 rec19",) + site_115(variant)[:4])
    return out


if __name__ == "__main__":
    variants = sys.argv[1:] or ['bin', 'lit', 'shift', 'hyb', 'dir']
    res = {v: all_sites(v) for v in variants}
    names = [r[0] for r in res[variants[0]]]
    print("%-28s " % "site" + " ".join("%7s" % v for v in variants) + "   (calls after the first; PosZ_b at the end for bin)")
    for i, n in enumerate(names):
        print("%-28s " % n + " ".join("%7s" % res[v][i][1] for v in variants) + "   endPosZ_b=%s" % res[variants[0]][i][4])
