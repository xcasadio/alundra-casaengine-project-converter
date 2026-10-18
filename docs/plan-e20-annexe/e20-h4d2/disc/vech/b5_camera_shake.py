"""b5_camera_shake.py - bricks (5) the camera kick [0x800DD874] and (6) the shake block 0x800E4338.
 (A) sway step 0x8002C894 (per-tick step of the 9-word block)
 (B) camera scroll step 0x8002CDA0 (run to 0x8002CF38, after the X/Y clamps): scroll += ((target - (scroll+half)) >> 4) + shake offset + kick ; kicks reset
 (C) E13 0x8007F30C (the only writer of the Y kick)
 (D) fire 2 driver 0x80035C64 (+ guard 0x800351B8 + cancel 0x800350C0 -> 0x80034E08) : save / per-tick / restore of the block through the 9-word buffer 0x80127138
Own models from the disassembly, compared with the REAL code on random vectors, with mutants and replayable vector files (../vec/vec_*.json).
usage: python -B b5_camera_shake.py [N] [seed]"""
import json, os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vtrace as V
from machine import s32, M32

OUT = os.path.join(HERE, '..', 'vec')
BLK = 0x800E4338
NAMES = ['Flag', 'LimitX', 'LimitY', 'SpeedX', 'SpeedY', 'OffsetX', 'OffsetY', 'ReachX', 'ReachY']
SCROLLX, SCROLLY, FIRST = 0x800E4328, 0x800E432C, 0x800E42B8
KICKX, KICKY = 0x800DD870, 0x800DD874
BUF = 0x80127138
HERO = 0x80127D30
ITEM, PHASE, COUNTER, PTR, IDENT = 0x80127164, 0x80127160, 0x8012715C, 0x80127124, 0x80127128
ENT0, STRIDE, COUNT = 0x80127D30, 0x294, 0x80134360


def ent(i):
    return ENT0 + STRIDE * i


def blk(m):
    return [s32(m.peek(BLK + 4 * k)) for k in range(9)]


def setblk(m, v):
    for k in range(9):
        m.poke(BLK + 4 * k, v[k] & M32)


# ------------------------------------------------------------------------------------------ (A) sway step
def sway_model(b, mut=None):
    f, lx, ly, sx, sy, ox, oy, rx, ry = list(b)
    if f == 0:
        return [f, 0, 0, 0, 0, 0, 0, rx, ry]
    if lx == 0 or sx == 0:
        ox = 0
    elif rx != 0:
        ox += sx
        if not (ox < lx):
            ox, rx = lx, 0
    else:
        ox -= sx
        if not (-lx < ox):
            ox, rx = -lx, 1
    if ly == 0:
        oy = 0
    elif ry != 0:
        oy += sy
        if not (oy < ly):
            oy, ry = ly, 0
    else:
        oy -= sy
        if not (-ly < oy):
            oy, ry = -ly, 1
    if mut == 'y_zero_speed_clears':
        if sy == 0:
            oy = 0
    if mut == 'x_zero_speed_keeps' and sx == 0:
        pass
    return [f, lx, ly, sx, sy, ox, oy, rx, ry]


def rnd_blk(rnd):
    pick = lambda: rnd.choice([0, 1, 2, 3, 5, 7, 16, 100, -1, -5, 255])
    return [rnd.choice([0, 1, 1, 1, 2]), pick(), pick(), pick(), pick(), pick(), pick(), rnd.choice([0, 1, 2, -1]), rnd.choice([0, 1, 2, -1])]


def run_sway(m, rnd, N):
    mism, vecs = 0, []
    killed = {'y_zero_speed_clears': 0}
    m.cov.clear()
    for n in range(N):
        b = rnd_blk(rnd)
        setblk(m, b)
        vec = V.run(m, 0x8002C894, ())
        got = blk(m)
        want = sway_model(b)
        if got != want:
            mism += 1
            if mism <= 3:
                print('SWAY MISMATCH', b, got, want)
        for mu in killed:
            if sway_model(b, mu) != want:
                killed[mu] += 1
        if n < 80:
            vecs.append({"id": n, **vec})
    cov = len([p for p in range(0x8002C894, 0x8002C9E0, 4) if p in m.cov])
    print('(A) sway step 0x8002C894: vectors %d mismatches %d coverage %d/%d mutants distinguished %s' % (N, mism, cov, (0x8002C9E0 - 0x8002C894) // 4, killed))
    bad = sum(1 for v in vecs if not V.replay(v)[0])
    print('    replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x8002C894", "note": "block layout +0 Flag, +4 LimitX, +8 LimitY, +0xC SpeedX, +0x10 SpeedY, +0x14 OffsetX, +0x18 OffsetY, +0x1C ReachX, +0x20 ReachY", "vectors": vecs}, open(os.path.join(OUT, 'vec_sway_8002C894.json'), 'w'))
    return mism


# ------------------------------------------------------------------------------------------ (B) camera scroll
def cam_model(st, tx, ty, tz, mut=None):
    """st: dict first, sx, sy, blk(9), kx, ky -> new dict (the block after the sway step)"""
    b = sway_model(st['blk'])
    sx, sy = st['sx'], st['sy']
    if st['first']:
        sx = tx - 0xA0
        sy = (ty - tz) - 0x88
        first = 0
    else:
        first = st['first']
        ky = 0 if mut == 'nokick' else st['ky']
        sx = sx + ((tx - (sx + 0xA0)) >> 4) + b[5] + st['kx']
        sy = sy + (((ty - tz) - (sy + 0x88)) >> 4) + b[6] + ky
    if sx < 0:
        sx = 0
    elif sx >= 0x3A0:
        sx = 0x39F
    if sy < 0:
        sy = 0
    elif sy >= 0x2D0:
        sy = 0x2CF
    return {'first': first, 'sx': sx, 'sy': sy, 'blk': b, 'kx': 0, 'ky': 0}


def run_cam(m, rnd, N):
    mism, vecs = 0, []
    killed = {'nokick': 0}
    m.cov.clear()
    for n in range(N):
        st = {'first': rnd.choice([0, 0, 0, 1]), 'sx': rnd.randrange(-5, 0x3C0), 'sy': rnd.randrange(-5, 0x2F0),
              'blk': rnd_blk(rnd), 'kx': rnd.choice([0, 0, 0, 3, -3]), 'ky': rnd.choice([0, 0, 0, 2, 2, -2, 7])}
        tx, ty, tz = rnd.randrange(0, 1248), rnd.randrange(0, 960), rnd.choice([0, 0, 16, 64, 200])
        m.poke(FIRST, st['first']); m.poke(SCROLLX, st['sx']); m.poke(SCROLLY, st['sy']); setblk(m, st['blk'])
        m.poke(KICKX, st['kx']); m.poke(KICKY, st['ky'])
        vec = V.run(m, 0x8002CDA0, (0, tx, ty, tz), stop_at=0x8002CF38)
        want = cam_model(st, tx, ty, tz)
        got = {'first': m.peek(FIRST), 'sx': s32(m.peek(SCROLLX)), 'sy': s32(m.peek(SCROLLY)), 'blk': blk(m), 'kx': m.peek(KICKX), 'ky': m.peek(KICKY)}
        if got != want:
            mism += 1
            if mism <= 3:
                print('CAM MISMATCH', st, (tx, ty, tz), got, want)
        for mu in killed:
            if cam_model(st, tx, ty, tz, mu) != want:
                killed[mu] += 1
        if n < 100:
            vecs.append({"id": n, "target": [tx, ty, tz], **vec})
    print('(B) camera scroll 0x8002CDA0..0x8002CF38: vectors %d mismatches %d mutants distinguished %s' % (N, mism, killed))
    bad = sum(1 for v in vecs if not V.replay(v)[0])
    print('    replay from empty memory: %d/%d' % (len(vecs) - bad, len(vecs)))
    json.dump({"function": "0x8002CDA0", "stop_at": "0x8002CF38", "note": "args a1,a2,a3 = target X, Y, Z in pixels (hero PosX/Y/Z >> 16); the vector ends after the scroll clamps", "vectors": vecs}, open(os.path.join(OUT, 'vec_camera_8002CDA0.json'), 'w'))
    # the kick's life: +2 on Y for one tick then the smoothing pulls back
    st = {'first': 0, 'sx': 200, 'sy': 300, 'blk': [0] * 9, 'kx': 0, 'ky': 2}
    row = []
    for t in range(6):
        st = cam_model(st, 360, 436, 0)             # ty - tz - 0x88 == 300: the camera is centred on its target
        row.append(st['sy'])
    print('    kick life (model; scrollY starts at 300, target centred, kick +2 at t0):', row)
    return mism


# ------------------------------------------------------------------------------------------ (C) E13
def run_e13(m, rnd, N):
    mism, vecs = 0, []
    m.cov.clear()
    m.hooks[0x8003A59C] = lambda mm: (mm.journal.append((mm.tick_no, 'destroy_3A59C', [mm.r[4] & M32, mm.r[5] & M32])), None)[1]
    e = ent(7)
    for n in range(N):
        anim = rnd.choice([0, 1, 1, 2, 5])
        aend = rnd.choice([0, 1, 1, 2])
        flags = rnd.randrange(0, 1 << 16)
        m.poke(e + 0x88, anim); m.poke(e + 0xAC, aend); m.poke(e + 0x6C, flags); m.poke(KICKY, rnd.choice([0, 0, 5]))
        k0 = m.peek(KICKY)
        n_j = len(m.journal)
        vec = V.run(m, 0x8007F30C, (e,))
        destroyed = len(m.journal) > n_j
        if anim == 1:
            want = {'k': k0, 'anim': 1, 'flags': flags, 'destroy': aend == 1}
        else:
            want = {'k': 2, 'anim': 1, 'flags': (flags | 0x40) & ~0x30 & M32, 'destroy': False}
        got = {'k': m.peek(KICKY), 'anim': m.peek(e + 0x88), 'flags': m.peek(e + 0x6C), 'destroy': destroyed}
        if got != want:
            mism += 1
            if mism <= 3:
                print('E13 MISMATCH', anim, aend, got, want)
        if n < 40:
            vec['journal_destroy'] = destroyed
            vecs.append({"id": n, **vec})
    m.hooks.pop(0x8003A59C, None)
    print('(C) E13 0x8007F30C: vectors %d mismatches %d (kick = 2 written only when TargetAnim != 1; destroy via 0x8003A59C when anim==1 and AnimEnd==1)' % (N, mism))
    json.dump({"function": "0x8007F30C", "note": "E13; DestroyEntity 0x8003A59C is stubbed (journal): vector field journal_destroy tells whether it was called", "vectors": vecs}, open(os.path.join(OUT, 'vec_e13_8007F30C.json'), 'w'))
    return mism


# ------------------------------------------------------------------------------------------ (D) fire 2
class S2:
    pass


def guard_model(hero_slope, hero_anim):
    """returns (result, cancelled)"""
    if hero_slope == 4:
        return 1, True
    if 0x32 <= hero_anim <= 0x33:
        return 0, False
    return 1, True


def fire2_model(st, mut=None):
    """st dict: phase, counter, item, ptr, ident, buf(9), blk(9), hero {anim, slope, x,y,z}, spawn_ret, spawn_id, ent21 [(status, type,...)] ; returns (ret, new st, spawn_called)"""
    st = json.loads(json.dumps(st))
    spawned = None

    def cancel():
        if st['item'] == 0:
            return
        if st['item'] == 0x30:
            st['blk'] = list(st['buf'])
            for e in st['ents']:
                if e['type'] == 0x15 and e['status'] == 2:
                    e['status'] = 3
                    e['f20'] = 0
        st['item'] = 0

    def guard():
        r, c = guard_model(st['hero']['slope'], st['hero']['anim'])
        if c:
            cancel()
        return r

    def phase1():
        g = guard()
        if g:
            st['blk'] = list(st['buf']) if mut != 'no_restore_on_guard' else st['blk']
            return 1
        if st['spawn_id_now'] == st['ident']:
            c = st['counter']
            b = list(st['blk'])
            b[0] = 1
            b[2] = c >> 4
            b[4] = c >> 4
            b[1] = c >> 6
            b[3] = c >> 6
            st['blk'] = b
            return 0
        st['blk'] = list(st['buf'])
        st['hero']['anim'] = 0x34
        st['phase'] += 1
        return 0

    ph = st['phase']
    if ph == 1:
        return phase1(), st, spawned
    if ph == 0:
        g = guard()
        if g:
            return 1, st, spawned
        if st['hero']['anim'] != 0x33:
            return 0, st, spawned
        spawned = [st['hero']['x'], st['hero']['y'], st['hero']['z']]
        st['ptr'] = st['spawn_ret']
        if st['spawn_ret'] == 0:
            return 1, st, spawned
        st['buf'] = list(st['blk'])
        st['phase'] += 1
        st['ident'] = st['spawn_id']
        st['spawn_id_now'] = st['spawn_id']           # same entity just born: identity matches
        return phase1(), st, spawned
    if ph == 2:
        return (0 if st['hero']['anim'] == 0x34 else 1), st, spawned
    return 0, st, spawned


def run_fire2(m, rnd, N):
    mism, vecs = 0, []
    killed = {'no_restore_on_guard': 0}
    m.cov.clear()
    SPAWNED = 0x80129000                     # the entity the spawn stub returns
    for n in range(N):
        st = {'phase': rnd.choice([0, 0, 1, 1, 1, 2, 3]), 'counter': rnd.choice([0, 1, 15, 16, 63, 64, 100, 255, 1000, rnd.randrange(0, 2000)]),
              'item': rnd.choice([0x30, 0x30, 0x30, 0x2B, 0]), 'ptr': SPAWNED, 'ident': rnd.randrange(1, 50), 'buf': [rnd.randrange(-9, 300) for _ in range(9)],
              'blk': rnd_blk(rnd), 'spawn_ret': rnd.choice([SPAWNED, SPAWNED, 0]), 'spawn_id': rnd.randrange(1, 50),
              'hero': {'anim': rnd.choice([0x32, 0x33, 0x33, 0x34, 0x31, 0, 0x2D]), 'slope': rnd.choice([0, 0, 0, 4, 1]), 'x': rnd.randrange(0, 1 << 30), 'y': rnd.randrange(0, 1 << 30), 'z': rnd.randrange(0, 1 << 24)},
              'ents': []}
        # the entity pointed to by [0x80127124]: alive when its id (+4) equals the stored identity
        alive = rnd.random() < 0.7
        st['spawn_id_now'] = st['ident'] if alive else st['ident'] + 1
        if st['phase'] == 0:
            st['spawn_id_now'] = st['spawn_id']
        cnt = rnd.choice([1, 2, 3, 5])
        for i in range(1, 6):
            st['ents'].append({'type': rnd.choice([0x15, 0x15, 0x14, 0x13]), 'status': rnd.choice([0, 2, 3, 2]), 'f20': rnd.choice([0, 9])})
        m.poke(COUNT, 5)
        for i, e in enumerate(st['ents']):
            m.poke(ent(i + 1) + 0x68, e['type']); m.poke(ent(i + 1) + 0x10, e['status']); m.poke(ent(i + 1) + 0x20, e['f20'])
        m.poke(PHASE, st['phase']); m.poke(COUNTER, st['counter']); m.poke(ITEM, st['item']); m.poke(PTR, SPAWNED); m.poke(IDENT, st['ident'])
        for k in range(9):
            m.poke(BUF + 4 * k, st['buf'][k] & M32)
        setblk(m, st['blk'])
        m.poke(SPAWNED + 4, st['spawn_id_now'] if st['phase'] != 0 else st['spawn_id'])
        h = st['hero']
        m.poke(HERO + 0x88, h['anim']); m.poke(HERO + 0x18C, h['slope']); m.poke(HERO + 0x114, h['x']); m.poke(HERO + 0x118, h['y']); m.poke(HERO + 0x11C, h['z'])
        spawn_calls = []

        def spawn_stub(mm, ret=st['spawn_ret'], calls=spawn_calls):
            sp = mm.r[29]
            calls.append([mm.r[4] & M32, mm.r[5] & M32, mm.r[6] & M32, mm.r[7] & M32, mm.peek(sp + 0x10), mm.peek(sp + 0x14), mm.peek(sp + 0x18)])
            mm.r[2] = ret
        m.hooks[0x80039F58] = spawn_stub
        vec = V.run(m, 0x80035C64, ())
        vec['stub_spawn_ret'] = st['spawn_ret']
        ret, want, spawned = fire2_model(st)
        got_hero_anim = m.peek(HERO + 0x88)
        got = {'ret': vec['ret'], 'blk': blk(m), 'buf': [s32(m.peek(BUF + 4 * k)) for k in range(9)], 'phase': m.peek(PHASE), 'item': m.peek(ITEM),
               'anim': got_hero_anim, 'ident': m.peek(IDENT), 'ptr': m.peek(PTR),
               'ents': [(s32(m.peek(ent(i + 1) + 0x10)), m.peek(ent(i + 1) + 0x20)) for i in range(5)]}
        exp = {'ret': ret, 'blk': want['blk'], 'buf': want['buf'], 'phase': want['phase'], 'item': want['item'], 'anim': want['hero']['anim'], 'ident': want['ident'], 'ptr': want['ptr'],
               'ents': [(e['status'], e['f20']) for e in want['ents']]}
        if got != exp or (len(spawn_calls) > 0) != (spawned is not None) or (spawned and spawn_calls[0][3:7] != [h['x'], h['y'], h['z'], 0]):
            mism += 1
            if mism <= 3:
                print('FIRE2 MISMATCH', {k: (got[k], exp[k]) for k in got if got[k] != exp[k]}, 'spawn', spawn_calls, spawned)
        mm = fire2_model(st, 'no_restore_on_guard')
        if mm[0] != ret or mm[1]['blk'] != want['blk']:
            killed['no_restore_on_guard'] += 1
        if n < 60:
            vec['spawn_calls'] = spawn_calls
            vecs.append({"id": n, **vec})
    m.hooks.pop(0x80039F58, None)
    print('(D) fire 2 driver 0x80035C64 (+guard 0x800351B8 + cancel 0x800350C0/0x80034E08): vectors %d mismatches %d, mutants distinguished %s' % (N, mism, killed))
    cov = lambda a, b: (len([p for p in range(a, b, 4) if p in m.cov]), (b - a) // 4)
    print('    coverage driver %d/%d, guard %d/%d, cancel dispatcher %d/%d, cancel 0x80034E08 %d/%d' % (cov(0x80035C64, 0x80035EB0) + cov(0x800351B8, 0x80035204) + cov(0x800350C0, 0x800351B8) + cov(0x80034E08, 0x80034EC4)))
    json.dump({"function": "0x80035C64", "note": "fire 2 pilot; stub: SpawnWarpEntity 0x80039F58 returns stub_spawn_ret (replay needs hooks={0x80039F58: ...}); phases 0/1/2 as in the model; the 9-word buffer is 0x80127138", "vectors": vecs}, open(os.path.join(OUT, 'vec_fire2_80035C64.json'), 'w'))
    return mism


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    os.makedirs(OUT, exist_ok=True)
    rnd = random.Random(seed)
    m = V.TM()
    bad = run_sway(m, rnd, N) + run_cam(m, rnd, N) + run_e13(m, rnd, N) + run_fire2(m, rnd, N)
    sys.exit(1 if bad else 0)
