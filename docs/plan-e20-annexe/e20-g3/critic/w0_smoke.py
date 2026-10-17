"""w0_smoke.py - critic: cell source check, 12 crates spawned in file order, collidable list content after 2 idle ticks (read-only)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my392 as G

print('map constants', G.CONST)
print('cells checked against the exported tilemap: (count, differing, dims) =', G.check_cells())
m, S = G.world((720, 216), 8)
rows = G.run(m, [(2, 0)], S)
K = G.K
for r, s in S.items():
    e = K.ent(s)
    print('rec%-2d slot %2d pos (%.4f, %.4f, %.4f) flags6C %#x box min (%.4f %.4f %.4f) size-1 (%#x %#x %#x)' % (
        r, s, m.peeks(e + 0x114) / G.U, m.peeks(e + 0x118) / G.U, m.peeks(e + 0x11C) / G.U, m.peek(e + 0x6C),
        m.peeks(e + 0x1D8) / G.U, m.peeks(e + 0x1DC) / G.U, m.peeks(e + 0x1E0) / G.U, m.peek(e + 0x1F0), m.peek(e + 0x1F4), m.peek(e + 0x1F8)))
h = K.ent(0)
print('hero pos (%.4f, %.4f, %.6f) flags %#x size-1 (%#x %#x %#x) off (%d %d %d)' % (m.peeks(h + 0x114) / G.U, m.peeks(h + 0x118) / G.U, m.peeks(h + 0x11C) / G.U,
      m.peek(h + 0x6C), m.peek(h + 0x1F0), m.peek(h + 0x1F4), m.peek(h + 0x1F8), m.peeks(h + 0x1E4) / G.U, m.peeks(h + 0x1E8) / G.U, m.peeks(h + 0x1EC) / G.U))
n = m.peeks(0x80127D2C)
lst = [(m.peek(0x80127C28 + 4 * i) - K.ent(0)) // 0x294 for i in range(n)]
print('collidable list (slots, in order):', lst, 'active count', m.peeks(0x80127D28))
