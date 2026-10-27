import spellworld as S
m, STATS, placed = S.make(item=0x2C, mp=4, mpmax=4, seed=0x12345678, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
init = m.init
# runs of initialised bytes above the image end
runs = []
start = None
base = 0x800CA800 & 0x1FFFFFFF
for o in range(base, 0x200000):
    if init[o] and start is None: start = o
    if (not init[o]) and start is not None:
        runs.append((start, o)); start = None
if start is not None: runs.append((start, 0x200000))
for a, b in runs:
    print('init run %08x-%08x (%d bytes)' % (a | 0x80000000, b | 0x80000000, b - a))
