import sys
sys.argv = ['x', '1', '0']
exec(open('t_c255b.py').read().split('bad = 0\nfor it in range(NV):')[0])
# directed boundary tests on the pickup overlap: state 1, dropped (274=1), hero overlaps on 2 axes, vary the third
def trial(axis, delta, esize, hsize):
    I = Img(bytearray(STRIDE)); H = Img(bytearray(STRIDE))
    I.s(0x68, 0x45 + 0x1E); I.s(0x274, 1); I.s(0x278, 100); I.s(0x27C, 1); I.s(0x144, 0); I.s(0x284, 0)
    axes = [(0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)]
    for i, (ax, sz) in enumerate(axes):
        I.s(ax, 0x100000); H.s(ax, 0x100000); I.s(sz, 0x30000); H.s(sz, 0x30000)
    ax, sz = axes[axis]
    I.s(ax, 0x100000 + delta); I.s(sz, esize); H.s(sz, hsize)
    J.clear(); CFG.update(dialog=[0], cd=0, give=0, di=0)
    put(E, I.raw); put(HERO, H.raw); m.wh(MAPINFO + 4, 128)
    m.call(0x8007C174, E)
    real = any(t[0] == 'give' for t in J)
    mod = model(bytes(I.raw), bytes(H.raw), bytes(STRIDE), 128, [0], 0, 0, 1, 0, 0)
    modv = any(t[0] == 'give' for t in mod[1])
    return real, modv
mism = 0; n = 0; rows = []
for axis in range(3):
    for esize, hsize in ((0x20000, 0x10000), (0x10000, 0x20000), (0, 0), (5, 9)):
        for sgn in (1, -1):
            for base in (esize if sgn < 0 else hsize,):
                for k in (-2, -1, 0, 1, 2):
                    delta = sgn * (base + k)
                    r, md = trial(axis, delta, esize, hsize)
                    n += 1
                    if r != md: mism += 1
                    rows.append((axis, hex(esize), hex(hsize), delta, r))
print('boundary trials', n, 'mismatch model/real', mism)
# report the first axis rows to show the +1 boundary (e pos greater than hero: threshold hero size; hero greater: threshold e size)
for r in rows[:10]: print(r)
