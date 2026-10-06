# Where does the binary pose / clear MenuOpen relative to Update(P)/RS? (state after each phase, open at P, close (Start) at P+19)
import my_bin as B
m = B.machine(); P = 5; pr = {0: B.START, 19: B.START}
for f in range(1, P + 45):
    m.call(B.DISPATCHER); rs = m.rw(B.CTRL) & 8
    w = pr.get(f - P, 0); m.hooks[B.PADREAD] = (lambda x: (lambda mm: mm.r.__setitem__(2, x)))(w)
    m.call(B.UPDATE, 0); up = m.rw(B.CTRL) & 8
    if f - P in (-1, 0, 1, 2, 36, 37, 38, 39): print('frame P%+d: MenuOpen after RS=%d after Update=%d' % (f - P, rs >> 3, up >> 3))
