import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl
import k0lib as K
name='etoile107'
mapname, sprite, extra = S.OBJ[name]
S.widen(mapname, sprite)
m = gl.world(keep={S.C4} | set(extra))
c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
p = S.add_plate(m, 178, 100)
pe = K.ent(p)
print('plate before ticks: pos z raw', m.peeks(pe+0x11C)/65536, 'box +0x1F8', m.peeks(pe+0x1F8)/65536, '+0x1EC', m.peeks(pe+0x1EC)/65536, 'floor', m.peeks(pe+0x134)/65536, 'terr', m.peeks(pe+0x138)/65536, 'flags', hex(m.peek(pe+0x6C)))
pads, nn = gl.pad_script([(5, 0)])
rows = gl.run(m, nn, pads, crates=[c, p])
print([ (r['tick'], r['c'][p]['pz']/65536) for r in rows])
