"""gen_e28_vectors.py - h4d1 discovery (read-only on the binary and the repository): E28 (0x8007FB58, the body of the fire-2 column at its death) recorded from the REAL code
with the h4b critic's interpreter (cmips.py, copied in h4b/critic/), in the format of e20-h4b/bin/vec/ (reads / writes / ret) plus the two stubbed callees' journals:
  "effect_calls": every call of CreateEffectEntity 0x8003BDD8 as (a0, a1, a2, a3, [sp+0x10], [sp+0x14]) read in the CALLER's frame (the callee frame is the stub's: sp is E28's own),
  "destroy_calls": every call of DestroyEntity 0x8003A59C as (a0, a1).
The vectors cover: the generator word over the whole 32-bit range (including the values that make the draw reach 0 and 0x1800), the entity position/Z over positive and negative raw values,
and the sentinels listed in the README of this folder.  usage: python -B gen_e28_vectors.py [N] [seed]  -> writes ../keys/vec_e28_8007FB58.json"""
import sys
import os
import json
import random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4b', 'critic'))
from cmips import Mem, Cpu, M, s32  # noqa: E402

FN = 0x8007FB58
RNG = 0x80098708
ENT = 0x80127D30 + 0x294 * 5
SP = 0x801FFF00


def w32(ram, a, v):
    v &= M
    for i in range(4):
        ram[a + i] = (v >> (8 * i)) & 0xFF


def record(seed, x, y, z):
    ram = {}
    w32(ram, RNG, seed)
    w32(ram, ENT + 0x114, x)
    w32(ram, ENT + 0x118, y)
    w32(ram, ENT + 0x11C, z)
    m = Mem(dict(ram), strict=False)
    eff, des = [], []

    def effect(c):
        # E28's own frame: its sp is the sp at the jal (the stub does not push a frame), the 5th and 6th arguments are at sp+0x10 and sp+0x14
        sp = c.r[29]
        eff.append([c.r[4] & M, c.r[5] & M, c.r[6] & M, c.r[7] & M, m.rd(sp + 0x10, 4), m.rd(sp + 0x14, 4)])
        c.r[2] = 0x801E8000

    def destroy(c):
        des.append([c.r[4] & M, c.r[5] & M])
        c.r[2] = 0

    c = Cpu(m, stubs={0x8003BDD8: effect, 0x8003A59C: destroy})
    c.run(FN, [ENT])
    reads = [[a, n, v] for a, n, v in m.rlog if not (0x801FFE00 <= a < 0x801FFF00)]
    wl = {}
    order = []
    for a, n, v in m.wlog:
        if 0x801FFE00 <= a < 0x801FFF00:
            continue
        if (a, n) not in wl:
            order.append((a, n))
        wl[(a, n)] = v
    return dict(fn='0x%08x' % FN, args=[ENT], reads=reads, writes=[[a, n, wl[(a, n)]] for a, n in order], effect_calls=eff, destroy_calls=des)


if __name__ == '__main__':
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 200
    rnd = random.Random(int(sys.argv[2]) if len(sys.argv) > 2 else 20261018)
    vecs = []
    for i in range(n):
        seed = rnd.getrandbits(32)
        x = rnd.randrange(-(200 << 16), 1300 << 16) & M
        y = rnd.randrange(-(200 << 16), 1000 << 16) & M
        z = rnd.choice([0, 1, 2, rnd.randrange(0, 100 << 16), rnd.getrandbits(32)])
        v = record(seed, x, y, z)
        v['id'] = i
        vecs.append(v)
    # sentinels: the seed of the key (rng0 at tick 155 of fire2_cast_pf) and of the unpacked cast
    for seed in (0xC4F6D6C2, 0x12345678):
        v = record(seed, 100 << 16, 100 << 16, 2)
        v['id'] = len(vecs)
        v['note'] = 'the seed of the world key (hero at 100,100, Z raw 2)'
        vecs.append(v)
    out = os.path.join(HERE, '..', 'keys', 'vec_e28_8007FB58.json')
    json.dump(dict(function='0x8007FB58', note='E28 (fire 2): 8 x (draw < 0x1801, r = draw + 0x320, CreateEffectEntity(0, 19, 2, X + TabX[4k]*r, Y + TabY[4k]*r, Z)), then DestroyEntity(self, -1)', vectors=vecs), open(out, 'w'))
    # self-check on the decoded semantics
    TX = [0, -543, -768, -543, 0, 543, 768, 543]
    TY = [512, 362, 0, -362, -512, -362, 0, 362]
    bad = 0
    for v in vecs:
        seed = [r for r in v['reads'] if r[0] == RNG][0][2]
        x = [r for r in v['reads'] if r[0] == ENT + 0x114][0][2]
        y = [r for r in v['reads'] if r[0] == ENT + 0x118][0][2]
        z = [r for r in v['reads'] if r[0] == ENT + 0x11C][0][2]
        s = seed
        exp = []
        for k in range(8):
            s = (s * 0x7D2B89DD + 0xE06A02E7) & M
            r = (s * 0x1801) >> 32
            rp = r + 0x320
            exp.append([0, 19, 2, (x + TX[k] * rp) & M, (y + TY[k] * rp) & M, z])
        if exp != v['effect_calls'] or v['destroy_calls'] != [[ENT, M]] or [r for r in v['writes'] if r[0] == RNG][0][2] != s:
            bad += 1
    print('vectors', len(vecs), 'file', os.path.normpath(out), 'model-vs-real mismatches', bad)
