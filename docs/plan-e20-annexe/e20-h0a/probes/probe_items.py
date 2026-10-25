"""probe: for each swim item scenario, what 0x8003499C sees (s0 = the promoted item) and returns."""
import sys, os
BASE = sys.argv[1]   # a COPY of docs/plan-e20-annexe/e20-d (the oracle harness), run from the repository root with python -B
sys.path.insert(0, BASE)
sys.path.insert(0, os.path.join(BASE, 'tools'))
import sscen as S
import rworld as R

LOG = []
orig = R.H.htick if hasattr(R, 'H') else None


def run_item(it):
    spec = dict(name='P-item%02X' % it, n=14, pad=[(10, S.CIR), (11, 0)], hero_px=S.CENTER, layout=S.allwater(), inv='item%02X' % it, hero_anim=0x1D)
    seen = []

    def ev(t, slots_m=None):
        pass
    import machine as MM
    # wrap exec1 of the Machine class
    cls = MM.Machine
    old = cls.exec1

    def exec1(self, pc, w):
        if pc == 0x800349B0:
            seen.append(('s0', self.tick_no, self.r[16] & 0xFFFFFFFF, hex(self.peek(0x80127164) & 0xFFFFFFFF), hex(self.peek(0x800DC4B8) & 0xFFFFFFFF)))
        elif pc == 0x80034AB8:
            seen.append(('ret', self.tick_no, self.r[2] & 0xFFFFFFFF))
        elif pc == 0x800490FC:
            seen.append(('snd', self.tick_no, self.r[4] & 0xFFFFFFFF, 'ra=%x' % (self.r[31] & 0xFFFFFFFF)))
        return old(self, pc, w)
    cls.exec1 = exec1
    oi = cls.install_stubs
    def inst(self):
        oi(self)
        o = self.hooks[0x800490FC]
        def h(m):
            seen.append(('snd', m.tick_no, m.r[4] & 0xFFFFFFFF, 'ra=%x' % (m.r[31] & 0xFFFFFFFF)))
            o(m)
        self.hooks[0x800490FC] = h
    cls.install_stubs = inst
    try:
        r = R.run_r(spec)
    finally:
        cls.exec1 = old
        cls.install_stubs = oi
    return seen


for it in range(0x1F, 0x33):
    print('item %02X' % it, run_item(it))
