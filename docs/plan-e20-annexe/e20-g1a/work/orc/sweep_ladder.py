"""sweep_ladder.py - the ladder-entry guard of MovePlayer (0x80031D20-0x80031D64, slope 6) against its 5-conjunct model: stick held, direction up, facing up, contact flag +0x13C, nothing carried.
The real code is run (0x80031B50) and the execution of the write at 0x80031D64 (state := 0x0E) is observed through the machine's PC set. Usage: python -B -I sweep_ladder.py [N]"""
import os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import k0lib as K
from machine import M32
N = int(sys.argv[1]) if len(sys.argv) > 1 else 4000
MUT = (int(sys.argv[2], 16), int(sys.argv[3], 16)) if len(sys.argv) >= 4 else None
rnd = random.Random(99)
m = gl.world(hero_px=(100, 100), direction=24)
c = gl.add_crate(m, 130, 100)
HERO, CR = K.ent(0), K.ent(c)
pad = gl.W.Pad(); gl.W.tick(m, pad, 0)
SNAP = (bytes(m.ram), bytes(m.init), list(m.r), m.hi, m.lo)
if MUT:
    import struct
    ram = bytearray(SNAP[0]); struct.pack_into('<I', ram, MUT[0] & 0x1FFFFFFF, MUT[1]); SNAP = (bytes(ram),) + SNAP[1:]
m.watch_uninit = False
DIRSEL = [m.peeks(0x80022C6C + 4 * i) for i in range(16)]
mism = tot = hit = 0
for i in range(N):
    m.ram[:] = SNAP[0]; m.init[:] = SNAP[1]; m.r[:] = SNAP[2]; m.hi, m.lo = SNAP[3], SNAP[4]
    state = rnd.choice([0, 1])
    hold = rnd.choice([0, 0x1000, 0x1000, 0x2000, 0x4000, 0x8000, 0x3000, 0x9000])
    s3 = hold >> 12
    hdir = rnd.choice([0x10, 0x10, 0x10, 0, 8, 0x18])
    adj = rnd.choice([0, 1, 1, 5])
    carried = rnd.random() < 0.3
    m.poke(HERO + 0x88, state); m.poke(HERO + 0x18C, 6); m.poke(HERO + 0x8C, hdir); m.poke(HERO + 0x13C, adj); m.poke(HERO + 0x144, 1)
    if carried:
        m.poke(HERO + 0x2C, CR); m.poke(CR + 0x28, HERO); m.poke(HERO + 0x38, 32 << 16)
    m.poke16(gl.W.PAD + 0x10, hold); m.poke16(gl.W.PAD + 0x12, 0); m.poke16(gl.W.PAD + 0x14, 0); m.poke16(gl.W.PAD + 0x16, hold)
    m.cov.clear()
    m.call(0x80031B50, limit=3_000_000)
    entered = 0x80031D64 in m.cov
    s1 = DIRSEL[s3] if DIRSEL[s3] != -1 else hdir
    want = (s3 != 0) and s1 == 0x10 and hdir == s1 and adj != 0 and not carried
    tot += 1; hit += entered; mism += (entered != want)
print('ladder-entry guard: %d vectors, %d entered, %d mismatches' % (tot, hit, mism))
sys.exit(1 if mism else 0)
