"""Exhaustive proof that comparing a tile as unsigned instead of signed cannot change the result of 0x3C/0x3D: the bounds are bytes (0..255), so for every
(lo, hi) and every 32-bit tile t, lo <= t <= hi holds for the signed value iff it holds for the unsigned one.
Exhaustive over lo, hi in 0..255 and over t in [-1024, 1024] plus the 32-bit extremes; the argument for the rest is the same (a tile outside 0..255 fails
one of the two bounds in both readings)."""
M32 = 0xFFFFFFFF
tiles = list(range(-1024, 1025)) + [0x7FFFFFFF, -0x80000000, 0x80000000 - 1, -0x7FFFFFFF, 0x10000, -0x10000]
diff = 0
n = 0
for lo in range(256):
    for hi in range(256):
        for t in tiles:
            n += 1
            s = lo <= t <= hi
            u = lo <= (t & M32) <= hi
            if s != u:
                diff += 1
print('cases', n, 'differences', diff)
