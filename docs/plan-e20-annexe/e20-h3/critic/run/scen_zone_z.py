from scen_zone import *
rw = Rigw()
print('Z scan: target z (bomb on ground, box 31 high; target 28 high)')
for tz in (-30, -28, -27, -20, 0, 20, 29, 30, 31, 32, 33, 40):
    print('  tz %4d -> %s' % (tz, trial(rw, 0, 0, tz=tz)))
