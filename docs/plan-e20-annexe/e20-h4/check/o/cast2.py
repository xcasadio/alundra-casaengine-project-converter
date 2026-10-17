"""cast2.py - second experiment: real SpawnWarpEntity + entity passes, earth tome upper."""
import sys
import spellworld as S
import world as W

item = int(sys.argv[1], 0) if len(sys.argv) > 1 else 0x2B
nt = int(sys.argv[2], 0) if len(sys.argv) > 2 else 140
m, STATS, placed = S.make(item)
print('placed', {k: hex(v) for k, v in placed.items()})
P = W.ent(0)


def on_tick(m, t):
    sl = S.slots(m)
    if t in (1, 2, 22, 23, 24, 30, 31, 32, 40, 60, 100):
        print('tick', t, 'hero anim', hex(m.peeks(P + 0x88)), 'slots', sl)
        for i in sl:
            if i == 0:
                continue
            e = S.snap_entity(m, i)
            print('   ', {k: (hex(v) if k in ('flags', 'touching', 'curatk') else v) for k, v in e.items() if k in ('slot', 'status', 'type', 'flags', 'x', 'y', 'z', 'fz', 'tanim', 'canim', 'tdir', 'animend', 'ground', 'hitc')})


S.run_cast(m, STATS, nt, on_tick=on_tick)
print('uninit reads:', len(m.uninit_reads))
for k, c in sorted(m.uninit_reads.items())[:20]:
    print(' ', hex(k[0]), hex(k[1]), k[2], c)
for j in m.journal[:40]:
    print(j)
