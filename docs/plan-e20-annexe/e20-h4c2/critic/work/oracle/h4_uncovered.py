import h4_vec_drivers as V
mism, cnt, first, cov = V.main(1500, 7)
regions = {'wrapper': (0x8003634C, 0x800364C0), 'earth1': (0x80035320, 0x800354D0), 'earth2': (0x800354D0, 0x80035790), 'water': (0x80035790, 0x80035A84),
           'fire1': (0x80035A84, 0x80035C64), 'fire2': (0x80035C64, 0x80035EB0), 'wind1': (0x80035EB0, 0x80036218), 'wind2': (0x80036218, 0x8003634C), 'cancel dispatch': (0x800350C0, 0x800351B8)}
print('mism', mism)
for n, (a, b) in regions.items():
    miss = [x for x in range(a, b, 4) if x not in cov]
    print(n, ' '.join('%08x' % x for x in miss))
