import sys, os
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'cs'))
import gl
from machine import M32, s32
R, L, U, D = gl.RIGHT, gl.LEFT, gl.UP, gl.DOWN
EXTRA = [('setptr',0x9C,0),('spd_f4',0xF4,1),('acc_f0',0xF0,1),('dir94',0x94,1),('tgtX',0xBC,1),('tgtY',0xC0,1),('stepX',0xD4,1),('stepY',0xD8,1)]
def extra(m, t, r):
    base = gl.HERO
    r['x'] = {n: (m.peeks(base+o) if sg else m.peek(base+o)) for n,o,sg in EXTRA}
    # the set record: hu @+8, byte @+0xD of the set pointed by +0x9C
    p = r['x']['setptr']
    r['x']['set_speed'] = m.peek(p+8) & 0xFFFF
    r['x']['set_acc'] = m.peek(p+0xC) >> 8 & 0xF   # byte @+0xD (little endian word at +0xC)
def show(title, seq, hero_px=(100,100), direction=24, crate_x=None, cells=None, pre=None, first=1):
    m = gl.world(cells=cells, hero_px=hero_px, direction=direction)
    cr = []
    if crate_x is not None:
        cr = [gl.add_crate(m, crate_x, hero_px[1])]
    pads, n = gl.pad_script(seq)
    rows = gl.run(m, n, pads, crates=cr, post=extra, pre=pre)
    print('==', title)
    for r in rows:
        h = r['hero']; x = r['x']
        if r['tick'] < first: continue
        print('t%02d pad=%04X anim_t=%2d anim_c=%2d dir_t=%2d | set(spd=%3d acc=%d) cache(spd=%3d acc=%d) tgt=(%.4f,%.4f) step=(%.4f,%.4f) | f=(%.4f,%.4f) pos=(%.4f,%.4f) adj=%d xcoll=%X' % (
            r['tick'], r['hold'], h['anim_t'], h['anim_c'], h['dir_t'], x['set_speed'], x['set_acc'], x['spd_f4'], x['acc_f0'],
            x['tgtX']/65536, x['tgtY']/65536, x['stepX']/65536, x['stepY']/65536, h['fx']/65536, h['fy']/65536, h['px']/65536, h['py']/65536, h['adj'], h['xcoll']))
    return rows
if __name__ == '__main__':
    show('walk east, release at t10 (open)', [(9,R),(5,0)])
    show('walk west, release at t10', [(9,L),(5,0)], hero_px=(200,100), direction=8)
    show('walk north', [(9,U),(5,0)], direction=0x10)
    show('walk south', [(9,D),(5,0)], direction=0)
    show('walk SE diagonal', [(9,R|D),(5,0)], direction=0x18)
