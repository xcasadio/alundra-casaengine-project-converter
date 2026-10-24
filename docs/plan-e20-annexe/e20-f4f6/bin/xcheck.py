"""xcheck.py - the same scenarios on the two independent interpreters (emu.py via machine.py, and mips.py via interp2.py): rows, effects (with velocities), journal and RNG must be identical."""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'src'))
import fw, world as W
import interp2
import c1world as C

SQ = W.SQUARE
H95 = {t: SQ for t in range(1, 96)}
def cols(c0, c1, val):
    return lambda col, row: val if c0 <= col <= c1 else (0, 0, 0, 0)
scen = {
  'arrow3_wall': dict(item=5, n=75, holds={1: SQ}, after=0, cells=fw.wall_col(15)),
  'arrow4_target': dict(item=6, n=60, holds={1: SQ}, after=0, targets=[(24, 12, 0)]),
  'charged7_grass_wall': dict(item=6, n=160, holds=H95, after=0, cells=lambda c, r: (0, 0, 0x40, 0) if c == 15 else ((0, 0, 0x02, 0) if 5 <= c <= 12 and r in (5, 6) else (0, 0, 0, 0))),
  'fire11_step32': dict(item=16, n=80, holds={1: SQ}, after=0, cells=cols(6, 9, (2, 0, 0, 0))),
  'fire11_step16': dict(item=16, n=80, holds={1: SQ}, after=0, cells=cols(6, 9, (1, 0, 0, 0))),
  'ice14_target': dict(item=14, n=80, holds={1: SQ}, after=0, targets=[(14, 12, 0)]),
  'fire11_water': dict(item=16, n=40, holds={1: SQ}, after=0, cells=cols(5, 9, (0, 0, 0, 8))),
  'fire12_wall': dict(item=16, n=140, holds=H95, after=0, cells=fw.wall_col(6)),
}
ok = True
for name, sp in scen.items():
    t0 = time.time()
    a = fw.run(dict(sp))
    b = fw.run(dict(sp, ticker=interp2.tick2))
    same = True
    for ra, rb in zip(a['rows'], b['rows']):
        for k in ('slots', 'journal', 'effects', 'rng0', 'rng1', 'draws'):
            if ra[k] != rb[k]:
                print(name, 'DIFF at tick', ra['tick'], 'in', k)
                same = False
                break
        if not same:
            break
    print('%-22s two interpreters identical over %d ticks: %s  (%.1fs)' % (name, len(a['rows']), same, time.time() - t0))
    ok = ok and same
sys.exit(0 if ok else 1)
