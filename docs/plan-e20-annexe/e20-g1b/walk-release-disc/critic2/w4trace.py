import sys, os
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'cs'))
import gl, k0lib as K
from machine import M32, s32
H = gl.HERO
R = gl.RIGHT
LOG = []
def instrument(m, only_ticks):
    orig = m.exec1
    def ex(pc, w):
        if m.tick_no in only_ticks and m.r[16] == H:   # s0 == hero (inside ComputeXYPosition only meaningful there)
            r = m.r
            if pc == 0x800377C0:
                LOG.append((m.tick_no, 'HEAD  s2=%d s1=%d s4=%X s5=%d  pos=%.5f z=%.3f' % (s32(r[18]), s32(r[17]), r[20], r[21], m.peeks(H+0x114)/65536, m.peeks(H+0x11C)/65536)))
            elif pc == 0x80037834:
                LOG.append((m.tick_no, '  terr=%.3f  flags6C=%X  fz=%d  groundbranch_cond: grav=%d fz0=%d' % (s32(r[2])/65536, m.peek(H+0x6C), m.peeks(H+0xB8), (m.peek(H+0x6C)>>8)&1, int(m.peeks(H+0xB8)==0))))
            elif pc == 0x80037914:
                LOG.append((m.tick_no, '  call36f34 -> s4=%X' % r[20]))
            elif pc == 0x80037934:
                LOG.append((m.tick_no, '  terrain-check v0=%d (0 = accept)' % r[2]))
            elif pc == 0x80037dfc:
                LOG.append((m.tick_no, 'RETURN [sp+0x34]=%X' % m.peek(r[29]+0x34)))
        return orig(pc, w)
    m.exec1 = ex
def air():
    m = gl.world(); c = gl.add_crate(m, 130, 100)
    return m, c, [(3, R), (1, R|gl.CROSS), (14, R)], {5}
def ground_mut():
    m = gl.world(hero_px=(100,100), direction=24); c = gl.add_crate(m, 130, 100)
    new = 0x08000000 | ((0x80037908 >> 2) & 0x3FFFFFF); m.poke(0x80037840, new)
    return m, c, [(14, R)], {4, 5}
def ground():
    m = gl.world(hero_px=(100,100), direction=24); c = gl.add_crate(m, 130, 100)
    return m, c, [(14, R)], {4, 5}
which = sys.argv[1]
m, c, seq, ticks = {'air': air, 'ground_mut': ground_mut, 'ground': ground}[which]()
instrument(m, ticks)
pads, n = gl.pad_script(seq)
rows = gl.run(m, n, pads, crates=[c])
for t, s in LOG: print('t%02d %s' % (t, s))
for r in rows[:9]:
    h = r['hero']; print('row t%02d x=%.4f xcoll=%s' % (r['tick'], h['px']/65536, 'crate' if h['xcoll'] else '-'))
