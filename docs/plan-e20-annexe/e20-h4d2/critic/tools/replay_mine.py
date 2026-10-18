"""replay_mine.py (critic h4d2): replays THEIR vector files (e20-h4b format) from an EMPTY memory on MY OWN interpreter (myemu.py), with my own stubs.
Memory = the EXE image (instruction fetch + its tables) + the bytes listed in 'reads' + the bytes the call wrote.  A data read outside the image that is neither listed nor written = INCOMPLETE.
Compares: return value v0 (unless stop_at), the final write map (outside the callee frame region 0x801FF000..0x801FFF00), the journal of the stubbed callees.
Also records the PCs executed (coverage of each function over the whole file).
usage: python -B replay_mine.py VECFILE [VECFILE...]"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib, myemu
from myemu import M, s32

IMG_LO = 0x80020000
IMG_HI = IMG_LO + len(lib.DATA) - lib.OFF
FRAME_LO, FRAME_HI = 0x801FF000, 0x801FFF00
EFFECT_RET = 0x801E8000


class SparseEmu(myemu.Emu):
    def __init__(self, reads):
        self.r = [0] * 32
        self.hi = self.lo = 0
        self.pc = 0
        self.hooks = {}
        self.log = []
        self.steps = 0
        self._ldreg = 0
        self.rd = {}            # byte address -> value (listed reads)
        for a, n, v in reads:
            for i in range(n):
                self.rd[(a + i) & M] = (v >> (8 * i)) & 0xFF
        self.wr = {}            # byte address -> value (written)
        self.wlog = []          # (addr, size, value) in order
        self.incomplete = []
        self.cov = set()
        self.journal = []

    def _byte(self, a, fetch=False):
        a &= M
        if a in self.wr:
            return self.wr[a]
        if a in self.rd:
            return self.rd[a]
        if IMG_LO <= a < IMG_HI:
            return lib.DATA[a - IMG_LO + lib.OFF]
        self.incomplete.append(a)
        return 0

    def rb(self, a): return self._byte(a)
    def rh(self, a): return self._byte(a) | self._byte(a + 1) << 8
    def rw(self, a): return self.rh(a) | self.rh(a + 2) << 16

    def wb(self, a, v):
        self.wr[a & M] = v & 0xFF
        self.wlog.append((a & M, 1, v & 0xFF))

    def wh(self, a, v):
        for i in range(2): self.wr[(a + i) & M] = (v >> (8 * i)) & 0xFF
        self.wlog.append((a & M, 2, v & 0xFFFF))

    def ww(self, a, v):
        for i in range(4): self.wr[(a + i) & M] = (v >> (8 * i)) & 0xFF
        self.wlog.append((a & M, 4, v & M))

    def exec1(self, pc, w):
        self.cov.add(pc)
        op = w >> 26
        if op in (0x22, 0x26, 0x2A, 0x2E):
            raise RuntimeError('unaligned op in a replayed function at %08x' % pc)
        return super().exec1(pc, w)


def run_vector(v, fn_addr, stubs):
    m = SparseEmu(v['reads'])
    sr = {int(k): x for k, x in v.get('stub_spawn_ret', {}).items()}
    sp0 = 0x801FFF00

    def a(i):
        return m.rw(m.r[29] + 0x10 + 4 * i)

    def spawn(mm):
        args = [mm.r[4], mm.r[5], mm.r[6], mm.r[7]] + [mm.rw(mm.r[29] + 0x10), mm.rw(mm.r[29] + 0x14), mm.rw(mm.r[29] + 0x18)]
        mm.journal.append(['spawn_39F58', [x & M for x in args]])
        mm.r[2] = sr.get(mm.r[6] & M, 0)

    def effect(mm):
        args = [mm.r[4], mm.r[5], mm.r[6], mm.r[7], mm.rw(mm.r[29] + 0x10), mm.rw(mm.r[29] + 0x14)]
        mm.journal.append(['effect_3BDD8', [x & M for x in args]])
        mm.r[2] = EFFECT_RET

    def destroy(mm):
        mm.journal.append(['destroy_3A59C', [mm.r[4] & M, mm.r[5] & M]])
        mm.r[2] = 0
    for k in stubs:
        m.hooks[k] = {0x80039F58: spawn, 0x8003BDD8: effect, 0x8003A59C: destroy}[k]
    args = v.get('args', [])
    for i, x in enumerate(args[:4]):
        m.r[4 + i] = x & M
    m.r[29] = sp0
    m.r[31] = 0xFFFFFFF0
    m.pc = fn_addr
    stop_at = int(v['stop_at'], 16) if v.get('stop_at') else None
    n = 0
    while m.pc != 0xFFFFFFF0 and m.pc != stop_at:
        h = m.hooks.get(m.pc)
        if h is not None:
            h(m)
            m._ldreg = 0
            m.pc = m.r[31]
            continue
        m.step()
        n += 1
        if n > 400000:
            raise RuntimeError('step limit')
    return m


def final_writes(m):
    d = {}
    for a, n, val in m.wlog:
        if FRAME_LO <= a < FRAME_HI:
            continue
        for i in range(n):
            d[(a + i) & M] = (val >> (8 * i)) & 0xFF
    return d


def theirs_writes(v):
    d = {}
    for a, n, val in v['writes']:
        if FRAME_LO <= a < FRAME_HI:
            continue
        for i in range(n):
            d[(a + i) & M] = (val >> (8 * i)) & 0xFF
    return d


def main(paths):
    allcov = {}
    for p in paths:
        data = json.load(open(p))
        fn = int(data['function'], 16) if isinstance(data['function'], str) else data['function']
        vecs = data['vectors']
        stubs = (0x80039F58, 0x8003BDD8, 0x8003A59C)
        bad = inc = 0
        cov = set()
        nb = 0
        first_bad = []
        for v in vecs:
            m = run_vector(v, int(v['fn'], 16), stubs)
            cov |= m.cov
            errs = []
            if m.incomplete:
                inc += 1
                errs.append('INCOMPLETE reads %s' % [hex(x) for x in m.incomplete[:3]])
            if not v.get('stop_at') and 'ret' in v and (m.r[2] & M) != (v['ret'] & M):
                errs.append('ret mine %#x theirs %#x' % (m.r[2] & M, v['ret'] & M))
            if final_writes(m) != theirs_writes(v):
                mine, th = final_writes(m), theirs_writes(v)
                errs.append('writes: mine-only %s theirs-only %s diff %s' % ([hex(x) for x in sorted(set(mine) - set(th))[:3]], [hex(x) for x in sorted(set(th) - set(mine))[:3]],
                                                                          [hex(x) for x in sorted(set(mine) & set(th)) if mine[x] != th[x]][:3]))
            tj = [[j[1], [x & M for x in j[2]]] for j in v.get('journal', [])]
            if m.journal != tj:
                errs.append('journal mine %s theirs %s' % (m.journal[:2], tj[:2]))
            if errs:
                bad += 1
                if len(first_bad) < 4:
                    first_bad.append((v.get('id'), errs))
        print('%-45s vectors %5d  mismatches %d  incomplete %d  distinct PCs run %d' % (os.path.basename(p), len(vecs), bad, inc, len(cov)))
        for fb in first_bad:
            print('   ', fb)
        allcov[os.path.basename(p)] = sorted(cov)
    json.dump(allcov, open(os.path.join(HERE, '..', 'replay_mine_coverage.json'), 'w'))


if __name__ == '__main__':
    main(sys.argv[1:])
