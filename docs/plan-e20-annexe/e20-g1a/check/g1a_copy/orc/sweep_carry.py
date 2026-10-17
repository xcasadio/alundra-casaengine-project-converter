"""sweep_carry.py - the carry/throw state group of the REAL MovePlayer (0x80031B50) against the Python model g1a_models.carry_group.
World = the E20 oracle (real InitializeEntity, real map-record spawner for a crate of map 392, real MovePlayer); harness writes: the hero's state/ground/flags, the crate flags, the pad words.
Each vector restores a RAM snapshot, writes the inputs, calls MovePlayer ONCE (0x80031B50) and compares hero +0x88/+0x8C/+0x2C, the crate's +0x24/+0x8C/position/+0x28, the offsets.
Usage: python -B -I sweep_carry.py [N]"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..'))
import gl
import k0lib as K
from machine import M32, s32
import g1a_models as GM
from h import Ent

N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
MUT = (int(sys.argv[2], 16), int(sys.argv[3], 16)) if len(sys.argv) >= 4 else None
rnd = random.Random(20261017)
GOLD_PATH = os.environ.get('GOLDEN')
GOLD = []
m_ox = m_oy = m_oz = 0

m = gl.world(hero_px=(100, 100), direction=24)
c = gl.add_crate(m, 130, 100)
HERO = K.ent(0)
CR = K.ent(c)
# one idle tick so that the lists/counters are the binary's own
pad = gl.W.Pad()
gl.W.tick(m, pad, 0)
SNAP = (bytes(m.ram), bytes(m.init), list(m.r), m.hi, m.lo)
COV = set()
m.watch_uninit = False
if MUT:
    _ram = bytearray(SNAP[0]); import struct as _st; _st.pack_into('<I', _ram, MUT[0] & 0x1FFFFFFF, MUT[1]); SNAP = (bytes(_ram),) + SNAP[1:]
    m.cov.clear()


def restore():
    m.ram[:] = SNAP[0]
    m.init[:] = SNAP[1]
    m.r[:] = SNAP[2]
    m.hi, m.lo = SNAP[3], SNAP[4]


def pk(a, v):
    m.poke(a, v & M32)


def hero_img():
    return Ent(bytes(m.ram[HERO & 0x1FFFFFFF:(HERO & 0x1FFFFFFF) + 0x294]))


def crate_img():
    return Ent(bytes(m.ram[CR & 0x1FFFFFFF:(CR & 0x1FFFFFFF) + 0x294]))


STATES = [0x05, 0x06, 0x07, 0x0B, 0x0C, 0x2E, 0x2F, 0x30, 0x09, 0x0A]
DIRSEL = [m.peeks(0x80022C6C + 4 * i) for i in range(16)]
mism = 0
tot = 0
first = []
from collections import Counter
cnt = Counter()
for i in range(N):
    restore()
    state = rnd.choice(STATES)
    kind = rnd.choice([1, 1, 3, 2, 0])
    ground = rnd.choice([0, 1])
    carried = rnd.random() < 0.9
    hold = rnd.choice([0, 0, 0x1000, 0x2000, 0x4000, 0x8000, 0x3000, 0x6000, 0xC000, 0x9000, 0xF000]) | rnd.choice([0, 0x40, 0x80, 0xC0])
    pressed = rnd.choice([0, 0x40, 0x80, 0xC0]) & (hold | 0)
    if rnd.random() < 0.2:
        pressed = rnd.choice([0x40, 0x80, 0xC0])      # even if not in hold (the words are independent in the binary)
    v180 = rnd.choice([0, 0, 0x2000, 0x4000, 0x6000])
    face = rnd.randrange(4)
    hdir = rnd.randrange(32)
    # inputs
    hx, hy, hz = rnd.randrange(60, 300) << 16 | rnd.randrange(0, 65536), rnd.randrange(60, 300) << 16 | rnd.randrange(0, 65536), rnd.randrange(0, 40) << 16 | rnd.randrange(0, 65536)
    pk(HERO + 0x114, hx); pk(HERO + 0x118, hy); pk(HERO + 0x11C, hz)
    pk(HERO + 0x88, state); pk(HERO + 0x144, ground); pk(HERO + 0x180, v180); pk(HERO + 0x98, face); pk(HERO + 0x8C, hdir)
    fl = m.peek(CR + 0x6C)
    fl = (fl & ~0x600) | (kind << 9)
    pk(CR + 0x6C, fl)
    if carried:
        pk(HERO + 0x2C, CR); pk(CR + 0x28, HERO)
        ox, oy, oz = rnd.choice([21, -21, 0, 7]) << 16, rnd.choice([0, 15, -15, 3]) << 16, rnd.choice([32, 32, 32, 20, 0, 40]) << 16
        pk(HERO + 0x30, ox); pk(HERO + 0x34, oy); pk(HERO + 0x38, oz)
        m_ox, m_oy, m_oz = ox, oy, oz
        pk(CR + 0x114, hx + ox); pk(CR + 0x118, hy + oy); pk(CR + 0x11C, hz + oz)
    else:
        pk(CR + 0x114, rnd.randrange(60, 300) << 16); pk(CR + 0x118, rnd.randrange(60, 300) << 16); pk(CR + 0x11C, rnd.randrange(0, 40) << 16)
        pk(HERO + 0x2C, 0); pk(CR + 0x28, rnd.choice([0, 0, HERO]))
        pk(HERO + 0x30, rnd.randrange(1, 1 << 22)); pk(HERO + 0x34, rnd.randrange(0, 1 << 22)); pk(HERO + 0x38, rnd.randrange(0, 1 << 22))     # stale offsets after a throw
    # pad words
    m.poke16(gl.W.PAD + 0x10, hold); m.poke16(gl.W.PAD + 0x12, pressed); m.poke16(gl.W.PAD + 0x14, 0); m.poke16(gl.W.PAD + 0x16, hold)
    h0, c0 = hero_img(), crate_img()
    hz = m.peeks(HERO + 0x11C)
    m.call(0x80031B50, limit=3_000_000)
    h1, c1 = hero_img(), crate_img()
    # model
    mh, mc = h0.copy(), c0.copy()
    s3 = hold >> 12
    GM.carry_group(state, mh, mc if carried else None, hold, pressed, [], DIRSEL[s3])
    # compare the fields the group writes
    got = (h1.u32(0x88), h1.u32(0x8C), h1.u32(0x2C), c1.u32(0x24), c1.u32(0x8C), c1.u32(0x114), c1.u32(0x118), c1.u32(0x11C))
    # the model works on the images BEFORE UpdatePlayerCarriedEntity(0) (the real run does it first): it only changes hero offsets, not what we compare
    want = (mh.u32(0x88), mh.u32(0x8C), mh.u32(0x2C), mc.u32(0x24), mc.u32(0x8C), mc.u32(0x114), mc.u32(0x118), mc.u32(0x11C))
    tot += 1
    cnt[state] += 1
    if GOLD_PATH and len(GOLD) < 700:
        GOLD.append(dict(state=state, kind=kind, ground=ground, carried=carried, hold=hold, pressed=pressed, vram180=v180, face=face, hero_dir_before=hdir, hero_xyz=[hx, hy, hz], offsets=[m_ox, m_oy, m_oz], out=dict(hero_state=got[0], hero_dir=got[1], hero_carried=got[2], crate_t24=got[3], crate_dir=got[4], crate_xyz=list(got[5:]))))
    if got != want:
        mism += 1
        if len(first) < 6:
            first.append((state, kind, ground, carried, hex(hold), hex(pressed), hex(v180), face, [hex(x) for x in got], [hex(x) for x in want]))
print('carry group sweep: %d vectors, %d mismatches' % (tot, mism))
RANGES = [(0x80031DF8, 0x80031E34), (0x80031D54, 0x80031D68), (0x800322B4, 0x800324B8), (0x80031D68, 0x80031D80)]
for a, b in RANGES:
    allp = list(range(a, b, 4)); miss = [p for p in allp if p not in m.cov]
    print('coverage %08x-%08x: %d/%d' % (a, b, len(allp) - len(miss), len(allp)), ('uncovered ' + ' '.join('%08x' % p for p in miss)) if miss else '')
print('vectors per state:', {hex(k): v for k, v in sorted(cnt.items())})
for f in first:
    print('MISMATCH', f)

if GOLD_PATH:
    import json
    json.dump(GOLD, open(GOLD_PATH, 'w'))
    print('golden vectors written:', len(GOLD))
sys.exit(1 if mism else 0)
