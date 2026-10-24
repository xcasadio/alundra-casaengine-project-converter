import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl

m = gl.world(hero_px=(100, 100), direction=24)
c = gl.add_crate(m, 130, 100)
print('crate slot', c, 'hero pos', m.peeks(gl.HERO + 0x114) / 65536, m.peeks(gl.HERO + 0x118) / 65536, 'crate', m.peeks(gl.K.ent(c) + 0x114) / 65536, m.peeks(gl.K.ent(c) + 0x118) / 65536)
e = gl.K.ent(c)
print('crate flags6C %X size/offs: 1E4=%d 1E8=%d 1EC=%d 1F0=%d 1F4=%d 1F8=%d  sheet %X' % (m.peek(e + 0x6C), m.peeks(e + 0x1E4), m.peeks(e + 0x1E8), m.peeks(e + 0x1EC), m.peeks(e + 0x1F0), m.peeks(e + 0x1F4), m.peeks(e + 0x1F8), m.peek(e + 0x1C4)))
pads, n = gl.pad_script([(30, gl.RIGHT)])
rows = gl.run(m, 30, pads, crates=[c])
print(gl.table(rows, [c], last=30))
