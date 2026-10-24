import sys, os
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'cs'))
import gl
import k0lib as K
H = gl.HERO
R = gl.RIGHT

def snap(m, t, r):
    r['w'] = dict(fx=m.peeks(H+0xC4), fz=m.peeks(H+0xB8), flags=m.peek(H+0x6C), terr=m.peeks(H+0x138), mz=m.peeks(H+0x1E0), pz=m.peeks(H+0x11C))

def scene(kind, start_x, n=26):
    """kind: ground | nograv | top"""
    if kind == 'top':
        m = gl.world(hero_px=(start_x, 104), direction=24)
        # row of supporting crates at cells (col 16..20,row 12) -> (204..,104) like the reader but my own coordinates; stack of 2 at col 22
        pos = [(16, 12, 0), (18, 12, 0), (20, 12, 0), (22, 12, 0), (22, 12, 2)]
        slots, info = K.spawn_record_entities(m, 'map_392', 36, pos)
        names = ['A1','A2','A3','Blow','Bup']
        m.poke(H+0x114, int(start_x*65536)); m.poke(H+0x11C, int(16*65536)+2)
        pads, tot = gl.pad_script([(2,0),(n-2,R)])
        return m, slots, names, pads, tot
    m = gl.world(hero_px=(start_x, 100), direction=24)
    c = gl.add_crate(m, 130, 100)
    if kind == 'nograv':
        m.poke(H+0x6C, m.peek(H+0x6C) & ~0x100 & 0xFFFFFFFF)
    pads, tot = gl.pad_script([(2,0),(n-2,R)])
    return m, [c], ['crate'], pads, tot

def run(kind, start_x, n=26):
    m, slots, names, pads, tot = scene(kind, start_x, n)
    pre = None
    if kind == 'mut_nonground':
        # MUTATION (oracle only): 0x80037840 `beqz v1,0x80037908` -> `j 0x80037908` (same delay slot): the entity is never in the 'walking on the terrain' branch
        old = m.peek(0x80037840)
        new = 0x08000000 | ((0x80037908 >> 2) & 0x3FFFFFF)
        print('patch 0x80037840: %08X -> %08X' % (old, new))
        m.poke(0x80037840, new)
    rows = gl.run(m, tot, pads, crates=slots, post=snap, pre=pre)
    nm = {K.ent(s): n_ for s, n_ in zip(slots, names)}
    print('== %s start_x=%s flags6C=%X' % (kind, start_x, rows[0]['w']['flags']))
    flush = contact = adj = None
    prev = None
    for r in rows:
        h = r['hero']
        if r['tick'] > 2:
            print('t%02d fx=%.4f x=%.4f z=%.2f fz=%.3f terr=%.2f adj=%d xcoll=%s' % (r['tick'], r['w']['fx']/65536, h['px']/65536, h['pz']/65536, r['w']['fz']/65536, r['w']['terr']/65536, h['adj'], nm.get(h['xcoll'], hex(h['xcoll']) if h['xcoll'] else '-')))
if __name__ == '__main__':
    k = sys.argv[1]; x = int(sys.argv[2]); run(k, x, int(sys.argv[3]) if len(sys.argv) > 3 else 26)
