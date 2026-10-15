import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import k0lib as K
import k0_carry as KC
import world as W
# build the rider scene, run 5 ticks, then teleport the platform and step the passes one by one
a = KC.rider(n=5)
m, (p, c), init, rows = a
pe, ce = K.ent(p), K.ent(c)
print('support before', hex(m.peek(ce + 0x12C)), 'platform', hex(pe))
m.poke(pe + 0x11C, m.peeks(pe + 0x11C) + 64 * 65536)
pad = W.Pad()
for a_ in W.PASSES:
    W.run_pass(m, a_)
    print(hex(a_), 'support', hex(m.peek(ce + 0x12C)), 'rider Z', m.peeks(ce + 0x11C))
