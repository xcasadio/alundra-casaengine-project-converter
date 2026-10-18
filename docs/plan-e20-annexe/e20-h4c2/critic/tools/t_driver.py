"""t_driver.py - the earth-2 driver 0x800354D0, the guard 0x800351B8, the wrapper 0x8003634C, E13 0x8007F30C and the cancel 0x80034BDC
run on MY interpreter (myemu.py) against MY Python models written from my own reading of the disassembly (tools/mydis.py output)."""
import random, struct, sys, json
from mydis import u32, s16, u16, s32 as ds32
from myemu import Emu, M32, s32

HERO = 0x80127D30
STRIDE = 0x294
def ent(i): return HERO + STRIDE * i
CNT, PHASE, ITEM = 0x8012715C, 0x80127160, 0x80127164
SEED = 0x80098708
COUNT = 0x80134360
KICK = 0x800DD874
TABLE = 0x80023504
X32, Y32 = 0x80023654, 0x80023694

# ---- tables read from the EXE
SCHED = []
a = TABLE
while True:
    t = u16(a)
    if t == 0xFFFF:
        break
    SCHED.append((t, s16(a + 2)))
    a += 4

def lcg(s): return (s * 0x7D2B89DD + 0xE06A02E7) & M32
def hi32(x): return (x * 32) >> 32       # multu hi of x*32 == x >> 27

def model_driver(st):
    """st: dict with phase, counter, anim, slope, hx, hy, hz, seed, spawn (None | dict(terrain, ent)), world (list of (type,status,blocked,anim)), count.
    Returns dict of results. Written from my reading of 0x800354D0 / 0x800351B8 / 0x80034BDC."""
    o = dict(st)
    o['world'] = [dict(w) for w in st['world']]
    o['spawns'] = []; o['destroys'] = []; o['forcez'] = []
    def cancel():
        # 0x800350C0 with item 0x2C -> 0x80034BDC then item = 0
        for i in range(1, st['count'] + 1):
            w = o['world'][i]
            if w['type'] == 0x11 and w['status'] == 2:
                w['status'] = 3; w['blocked'] = 0; w['anim'] = 1
        o['item'] = 0
    def guard():
        if o['slope'] == 4 or (o['anim'] - 0x32) & M32 >= 2:
            cancel(); return 1
        return 0
    ph = o['phase']
    if ph == 1:
        if guard(): o['ret'] = 1; return o
        if o['counter'] < 0x118: o['ret'] = 0; return o
        o['anim'] = 0x34; o['phase'] = 2; o['ret'] = 0; return o
    if ph == 2:
        o['ret'] = 0 if o['anim'] == 0x34 else 1; return o
    if ph != 0:
        o['ret'] = 0; return o
    if guard(): o['ret'] = 1; return o
    ctr = o['counter']
    seed = o['seed']
    k = 0
    while k < len(SCHED) and SCHED[k][0] <= ctr:
        if SCHED[k][0] == ctr:
            p = SCHED[k][1]
            seed = lcg(seed); r1 = hi32(seed)
            seed = lcg(seed); r2 = hi32(seed)
            x = (o['hx'] + s16(X32 + 2 * r1) * (p + 0xA00)) & M32
            y = (o['hy'] + s16(Y32 + 2 * r1) * (p + 0xA00)) & M32
            z = (o['hz'] + 0x1000000) & M32
            o['spawns'].append((x, y, z, r2))
            sp = st['spawn']
            if sp is not None:
                if s32((o['hz'] + 0x1000000) & M32) < s32(sp['terrain']):
                    o['destroys'].append(1)
                else:
                    o['forcez'].append(0xFFF80000)
        k += 1
    o['seed'] = seed
    if k == len(SCHED):
        o['phase'] = 1
    o['ret'] = 0
    return o

def build(st):
    e = Emu()
    e.poke(CNT, st['counter']); e.poke(PHASE, st['phase']); e.poke(ITEM, 0x2C)
    e.poke(SEED, st['seed']); e.poke(COUNT, st['count'])
    e.poke(HERO + 0x88, st['anim']); e.poke(HERO + 0x18C, st['slope'])
    e.poke(HERO + 0x114, st['hx']); e.poke(HERO + 0x118, st['hy']); e.poke(HERO + 0x11C, st['hz'])
    for i, w in enumerate(st['world']):
        if i == 0: continue
        e.poke(ent(i) + 0x68, w['type']); e.poke(ent(i) + 0x10, w['status'])
        e.poke(ent(i) + 0x20, w['blocked']); e.poke(ent(i) + 0x88, w['anim'])
    box = {'spawns': [], 'destroys': [], 'forcez': []}
    newent = ent(60)
    def spawn(m):
        sp = m.r[29]
        args = (m.r[4], m.r[5], m.r[6], m.r[7], m.rw(sp + 0x10), m.rw(sp + 0x14), m.rw(sp + 0x18))
        box['spawns'].append(args)
        if st['spawn'] is None:
            m.r[2] = 0
        else:
            m.poke(newent + 0x138, st['spawn']['terrain'] & M32)
            m.r[2] = newent
    def destroy(m):
        box['destroys'].append(m.r[4]); m.r[2] = 0
    e.hooks[0x80039F58] = spawn
    e.hooks[0x8003A774] = destroy
    return e, box, newent

def run_case(st, entry=0x800354D0):
    e, box, newent = build(st)
    ret = e.run(entry, (0x2C,))
    return e, box, newent, ret

def compare(st):
    e, box, newent, ret = run_case(st)
    m = model_driver(st)
    bad = []
    if ret != m['ret']: bad.append(('ret', ret, m['ret']))
    sp = [(a[2], a[3], a[4], a[5], a[6]) for a in box['spawns']]
    # a0 = hero, a1 = 0, a2 = 0x11, a3 = x ; stack 0x10 = y, 0x14 = z, 0x18 = dir
    sp2 = [(a[3], a[4], a[5], a[6]) for a in box['spawns']]
    if sp2 != m['spawns']: bad.append(('spawns', sp2, m['spawns']))
    for a in box['spawns']:
        if (a[0], a[1], a[2]) != (HERO, 0, 0x11): bad.append(('spawn-head', a[:3]))
    if len(box['destroys']) != len(m['destroys']): bad.append(('destroys', box['destroys'], m['destroys']))
    fz = [e.peek(newent + 0xB8)] if (st['spawn'] is not None and m['forcez']) else []
    if st['spawn'] is not None and m['forcez'] and fz != [0xFFF80000]: bad.append(('forcez', fz))
    if e.peek(SEED) != m['seed']: bad.append(('seed', hex(e.peek(SEED)), hex(m['seed'])))
    if s32(e.peek(PHASE)) != m['phase']: bad.append(('phase', e.peek(PHASE), m['phase']))
    if s32(e.peek(HERO + 0x88)) != s32(m['anim'] if not isinstance(m['anim'], int) else m['anim']): bad.append(('anim', e.peek(HERO + 0x88), m['anim']))
    exp_item = m.get('item', 0x2C)
    if e.peek(ITEM) != exp_item: bad.append(('item', e.peek(ITEM), exp_item))
    for i in range(1, st['count'] + 1):
        w = m['world'][i]
        if (s32(e.peek(ent(i) + 0x10)), s32(e.peek(ent(i) + 0x20)), s32(e.peek(ent(i) + 0x88))) != (w['status'], w['blocked'], w['anim']):
            bad.append(('world', i, (e.peek(ent(i) + 0x10), e.peek(ent(i) + 0x20), e.peek(ent(i) + 0x88)), w))
    return bad, e

def rand_state(rng):
    count = rng.choice([0, 1, 3, 8, 20, 63])
    world = [dict(type=0, status=2, blocked=0, anim=0)]
    for i in range(1, 64):
        world.append(dict(type=rng.choice([0x11, 0x11, 0x10, 0x15, 0x111, 5]), status=rng.choice([0, 1, 2, 2, 3, 4]),
                          blocked=rng.choice([0, 0, 5]), anim=rng.choice([0, 2, 7])))
    phase = rng.choice([0, 0, 0, 1, 1, 2, 3, -1])
    st = dict(phase=phase, counter=rng.choice([0, 1, 15, 16, 17, 32, 100, 223, 224, 225, 239, 240, 241, 0x117, 0x118, 0x119, 300, rng.randrange(0, 400)]),
              anim=rng.choice([0x32, 0x33, 0x34, 0x31, 0, 1, 0x35, 0x2D]), slope=rng.choice([0, 0, 0, 1, 4, 2]),
              hx=rng.randrange(0, 1 << 26), hy=rng.randrange(0, 1 << 26), hz=rng.choice([1, 0x10001, 0, 0x7FF0000, rng.randrange(0, 1 << 27)]),
              seed=rng.randrange(0, 1 << 32), count=count, world=world,
              spawn=rng.choice([None, dict(terrain=0), dict(terrain=0x1000001), dict(terrain=0x1000000), dict(terrain=-5 & M32)]))
    # make the 224 / 240 / 16 boundaries frequent
    return st

if __name__ == '__main__':
    rng = random.Random(int(sys.argv[1]) if len(sys.argv) > 1 else 12345)
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 3000
    bad_total = 0
    cov = set()
    stats = dict(ret0=0, ret1=0, phase=[0, 0, 0, 0])
    for it in range(n):
        st = rand_state(rng)
        # hero z: in the model compare as s32
        bad, e = compare(st)
        cov |= e.cov
        if bad:
            bad_total += 1
            if bad_total <= 5: print('MISMATCH', st['phase'], st['counter'], bad)
    print('cases', n, 'mismatches', bad_total, 'distinct pcs covered', len(cov))
    drv = [c for c in cov if 0x800354D0 <= c < 0x80035790]
    allpcs = set(range(0x800354D0, 0x80035790, 4))
    print('driver pcs covered', len(drv), 'of', len(allpcs))
    json.dump(sorted(hex(c) for c in allpcs - cov), open('../out/t_driver_uncovered.json', 'w'))
