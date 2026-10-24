import sys, os
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'cs'))
import gl
H = gl.HERO; R = gl.RIGHT
def run(k, mut):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100)
    m.poke(H+0x114, 100*65536 + k*2048)
    if mut:
        m.poke(0x80037840, 0x08000000 | ((0x80037908 >> 2) & 0x3FFFFFF))
    pads, n = gl.pad_script([(14, R)])
    rows = gl.run(m, n, pads, crates=[c])
    # first tick where x reaches flush (stops increasing) and first tick with contact
    xs = [r['hero']['px'] for r in rows]
    flush = next(i for i in range(1, len(xs)) if xs[i] - xs[i-1] < 2*65536 and xs[i] > 100*65536 + 5*65536) + 1
    contact = next((r['tick'] for r in rows if r['hero']['xcoll']), None)
    return flush, contact
for mut in (False, True):
    res = [run(k, mut) for k in range(32)]
    same = sum(1 for f, c in res if c == f); late = sum(1 for f, c in res if c == f+1); other = 32 - same - late
    print('mutation non-ground=%s : contact at flush tick %d/32, one tick later %d/32, other %d/32' % (mut, same, late, other))
    print('  ', res)
