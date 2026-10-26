import sys
import spellworld as S
import world as W
from scen_lib import *
from scen_zone import spawn
m, STATS = mk(None, indices=(1, 2, 253))
H = W.ent(0)
b = spawn(m, H, 0, 2, 200, 200, 0, 0x10)
for off in range(0, 0x294):
    v = m.rb(b+off)
    if v in (62, 30, 12):
        print('offset %#x byte %d' % (off, v))
