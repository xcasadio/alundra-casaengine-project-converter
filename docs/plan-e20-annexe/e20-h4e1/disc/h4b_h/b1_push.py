"""b1_push.py - brick (1): the force pass 0x800366FC (ApplyEntityForces: slide table + push +0xCC/+0xD0 + map bound + ForceAdjusted).
Own model written from the disassembly (../out/dis_800366FC.txt), compared with the REAL code on random vectors; mutants; replayable vector file.
usage: python -B b1_push.py [N] [seed]"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'vec')

TAB_X = 0x80028B94
TAB_Y = 0x80028BD4
MAPINFO_PTR = 0x800E4334
ENT = 0x80127D30 + 0x294 * 5
MAPINFO = 0x801D0000


def table(m, a):
    return [s32(m.peek(a + 4 * i)) for i in range(16)]


def model(e, shift_byte, tx, ty, mut=None):
    """e: dict of the entity fields read; returns dict of the fields written (+0xCC +0xD0 zeroed, +0xDC, +0xE0, +0x13C only when clamped)"""
    idx = e[0x188] & (0x7 if mut == 'idx7' else 0xF)
    sh = (shift_byte & 0xFF) & (0xF if mut == 'mask15' else 31)    # lb then srav: only the low 5 bits count
    if mut == 'noshift':
        sh = 0
    px_, py_ = e[0xCC], e[0xD0]
    if mut == 'swap_push':
        px_, py_ = py_, px_
    if mut == 'nopush':
        px_ = py_ = 0
    ax = s32(e[0xC4] + tx[idx])
    ay = s32(e[0xC8] + ty[idx])
    if mut == 'pushbefore':
        ax = s32(ax + px_); ay = s32(ay + py_)
    ax = s32(ax) >> sh
    ay = s32(ay) >> sh
    if mut != 'pushbefore':
        ax = s32(ax + px_); ay = s32(ay + py_)
    if mut == 'tablefirst_shift':
        ax = s32(e[0xC4] + (tx[idx] >> sh) + px_) ; ay = s32(e[0xC8] + (ty[idx] >> sh) + py_)   # the decompilation's order
    px, py = e[0x114], e[0x118]
    flag = None

    def clampaxis(a, pos, lo, hi):
        nonlocal flag
        n = s32(pos + a)
        if mut == 'swapbounds':
            lo, hi = hi, lo
        if mut == 'maxfirst':
            if s32(hi) < n:
                flag = 1; return s32(hi - pos)
            if n < s32(lo):
                flag = 1; return s32(lo - pos)
            return a
        if mut == 'unsigned':
            if (n & M32) < (lo & M32):
                flag = 1; return s32(lo - pos)
            if (hi & M32) < (n & M32):
                flag = 1; return s32(hi - pos)
            return a
        if n < s32(lo):
            flag = 1; return s32(lo - pos)
        if s32(hi) < n:
            flag = 1; return s32(hi - pos)
        return a
    ax_c = clampaxis(ax, px, e[0x108], e[0xFC])
    ay_c = clampaxis(ay, py, e[0x10C], e[0x100])
    if mut == 'noflag':
        flag = None
    out = {0xCC: 0, 0xD0: 0, 0xDC: ax_c & M32, 0xE0: ay_c & M32}
    if flag:
        out[0x13C] = 1
    if mut == 'nozero':
        out.pop(0xCC); out.pop(0xD0)
    return out


FIELDS = [0x188, 0xCC, 0xD0, 0xC4, 0xC8, 0x108, 0xFC, 0x10C, 0x100, 0x114, 0x118, 0x13C]


def rnd_vec(rnd, m):
    e = {}
    kind = rnd.random()
    e[0x188] = rnd.choice([rnd.randrange(0, 16), rnd.randrange(0, 1 << 32)])
    big = [0, 0, 0x30000, -0x30000, 0x20000, -0x20000, 196608, 131072, 0x7FFFFFFF, -0x80000000, rnd.randrange(-1 << 22, 1 << 22), rnd.randrange(-1 << 31, 1 << 31)]
    e[0xCC] = rnd.choice(big) & M32
    e[0xD0] = rnd.choice(big) & M32
    e[0xC4] = rnd.choice([0, rnd.randrange(-(1 << 20), 1 << 20), rnd.randrange(-1 << 31, 1 << 31), 0x7FFFFFFF]) & M32
    e[0xC8] = rnd.choice([0, rnd.randrange(-(1 << 20), 1 << 20), rnd.randrange(-1 << 31, 1 << 31), -0x80000000]) & M32
    px = rnd.randrange(0, 0x4E00000)
    py = rnd.randrange(0, 0x3C00000)
    e[0x114], e[0x118] = px, py
    # bounds: usually min<=pos<=max, sometimes the position outside or min>max
    mode = rnd.random()
    if mode < 0.7:
        e[0x108] = -rnd.randrange(0, 0x100000) & M32; e[0xFC] = 0x4E00000 - rnd.randrange(0, 0x200000)
        e[0x10C] = -rnd.randrange(0, 0x100000) & M32; e[0x100] = 0x3C00000 - rnd.randrange(0, 0x200000)
    elif mode < 0.85:
        e[0x108] = rnd.randrange(-1 << 31, 1 << 31) & M32; e[0xFC] = rnd.randrange(-1 << 31, 1 << 31) & M32
        e[0x10C] = rnd.randrange(-1 << 31, 1 << 31) & M32; e[0x100] = rnd.randrange(-1 << 31, 1 << 31) & M32
    else:
        e[0x108] = (px - 0x10000 * rnd.randrange(0, 4)) & M32; e[0xFC] = (px + 0x10000 * rnd.randrange(0, 4)) & M32
        e[0x10C] = (py - 0x10000 * rnd.randrange(0, 4)) & M32; e[0x100] = (py + 0x10000 * rnd.randrange(0, 4)) & M32
    e[0x13C] = rnd.choice([0, 0, 1, 7])
    shift = rnd.choice([0, 0, 0, 1, 4, 31, 32, 33, 0x7F, 0x80, 0xE0, rnd.randrange(0, 256)])
    return e, shift


def setup(m, e, shift):
    m.poke(MAPINFO_PTR, MAPINFO)
    m.poke8(MAPINFO + 8, shift)
    for k in range(0x294 // 4):
        m.poke(ENT + 4 * k, 0xA5A5A500 + k & M32)
    for off, v in e.items():
        m.poke(ENT + off, v & M32)


def main(N, seed, write_vectors=True):
    rnd = random.Random(seed)
    m = V.TM()
    tx, ty = table(m, TAB_X), table(m, TAB_Y)
    m.cov.clear()
    mism = 0
    vecs = []
    mut_names = ['noshift', 'pushbefore', 'nopush', 'swap_push', 'swapbounds', 'noflag', 'nozero', 'idx7', 'mask15', 'maxfirst', 'unsigned', 'tablefirst_shift']
    mut_killed = {k: 0 for k in mut_names}
    clamp = {'x': 0, 'y': 0, 'none': 0}  # x = flag newly set, none = unchanged
    for n in range(N):
        e, shift = rnd_vec(rnd, m)
        setup(m, e, shift)
        before13c = e[0x13C]
        vec = V.run(m, 0x800366FC, (ENT,))
        want = model(e, shift, tx, ty)
        got = {off: m.peek(ENT + off) for off in (0xCC, 0xD0, 0xDC, 0xE0, 0x13C)}
        exp = {0xCC: 0, 0xD0: 0, 0xDC: want[0xDC], 0xE0: want[0xE0], 0x13C: want.get(0x13C, before13c)}
        if got != exp:
            mism += 1
            if mism <= 5:
                print('MISMATCH', n, {hex(k): hex(v) for k, v in got.items()}, {hex(k): hex(v) for k, v in exp.items()}, 'shift', shift)
        for mu in mut_names:
            w2 = model(e, shift, tx, ty, mut=mu)
            ex2 = {0xCC: w2.get(0xCC, e[0xCC]), 0xD0: w2.get(0xD0, e[0xD0]), 0xDC: w2[0xDC], 0xE0: w2[0xE0], 0x13C: w2.get(0x13C, before13c)}
            if ex2 != got:
                mut_killed[mu] += 1
        if got[0x13C] == 1 and before13c == 0:
            clamp['x'] += 1
        elif got[0x13C] == before13c:
            clamp['none'] += 1
        vecs.append({"id": n, "entity_base": "%#010x" % ENT, "shift_byte": shift, **vec})
    cov = len([p for p in range(0x800366FC, 0x80036828, 4) if p in m.cov])
    total = len([p for p in range(0x800366FC, 0x800367E4, 4)])
    print('vectors', N, 'seed', seed, 'mismatches', mism)
    print('coverage 0x800366FC..0x800367E4: %d/%d instructions' % (len([p for p in range(0x800366FC, 0x800367E4, 4) if p in m.cov]), total))
    print('mutants killed (vectors that distinguish): ', mut_killed)
    print('vectors with ForceAdjusted newly set:', clamp)
    if write_vectors:
        os.makedirs(OUT, exist_ok=True)
        keep = vecs[:200]
        with open(os.path.join(OUT, 'vec_push_800366FC.json'), 'w') as f:
            json.dump({"function": "0x800366FC", "note": "ApplyEntityForces; reads/writes are every data word touched by the real code; replay with vtrace.replay", "seed": seed, "vectors": keep}, f, indent=0)
        # replay from empty memory
        bad = 0
        for v in keep:
            ok, msg = V.replay(v)
            if not ok:
                bad += 1
                if bad <= 3:
                    print('REPLAY FAIL', v['id'], msg)
        print('replay from empty memory: %d/%d vectors reproduce the writes' % (len(keep) - bad, len(keep)))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    sys.exit(1 if main(N, seed) else 0)
