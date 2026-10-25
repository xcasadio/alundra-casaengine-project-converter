"""ApplyItem (0x80033DBC) on the REAL code, for the magic seed (item 0x50, body 0x8003382C) and the neighbours the chest uses (ApplyItem is the chest's
giver: the pickup's giver GiveItem 0x80033F00 is another function). Stubs: sound (0x800490FC), effect creation (0x8003BDD8), AddMoney (0x8004DFD8),
AddItem (0x8004E530) and the falcon counter (0x8004E6EC) are logged only for the items that are not the seed. Player stats, SetPlayerMpMax/SetPlayerMp and
the random generator step run for real. Writes the result as text."""
import sys, random
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import f255_oracle as F
m = F.m
M32 = F.M32
J = []
POOL = 0x801B0000
pool_i = [0]
FX = {}

def h_fx(mc):
    sp = mc.r[29]
    p = POOL + 0x100 * pool_i[0]
    pool_i[0] += 1
    for a in range(p, p + 0x100):
        mc.wb(a, 0)
    FX[p] = None
    J.append(('fx', p, mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    mc.r[2] = p

m.hooks[0x8003BDD8] = h_fx
m.hooks[0x800490FC] = lambda mc: J.append(('sound', mc.r[4] & M32))
m.hooks[0x8004DFD8] = lambda mc: J.append(('addmoney', mc.r[4] & M32))
m.hooks[0x8004E6EC] = lambda mc: J.append(('falcon', mc.r[4] & M32))
m.hooks[0x8004E530] = lambda mc: J.append(('additem', mc.r[4] & M32))
m.hooks[0x800335AC] = lambda mc: J.append(('body_0x800335AC',))
m.hooks[0x800336F0] = lambda mc: J.append(('body_0x800336F0',))
m.hooks[0x800333AC] = lambda mc: J.append(('body_0x800333AC',))
m.hooks[0x80032E2C] = lambda mc: J.append(('body_0x80032E2C',))
m.hooks[0x80032EEC] = lambda mc: J.append(('body_0x80032EEC',))
m.hooks[0x80032F84] = lambda mc: J.append(('body_0x80032F84',))
RNG = 0x80098708

def stats(mp, mpmax):
    m.ww(0x80176318, F.STATS)
    for a in range(F.STATS, F.STATS + 0x40):
        m.wb(a, 0)
    m.wh(F.STATS + 4, mp)
    m.wh(F.STATS + 6, mpmax)

def apply(item, mp, mpmax, rng=0xB017C93D, hero=(0x00100000, 0x00200000, 0x00300000)):
    stats(mp, mpmax)
    m.ww(RNG, rng)
    m.ww(F.HERO + 0x114, hero[0])
    m.ww(F.HERO + 0x118, hero[1])
    m.ww(F.HERO + 0x11C, hero[2])
    J.clear()
    pool_i[0] = 0
    m.call(0x80033DBC, F.HERO, item)
    mp2 = m.rhs(F.STATS + 4)
    mpmax2 = m.rhs(F.STATS + 6)
    return mp2, mpmax2, list(J), m.rw(RNG)

def rng_steps(start, end):
    s = start
    for k in range(0, 40):
        if s == end:
            return k
        s = lcg(s)
    return -1

def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32

if __name__ == '__main__':
    print('== ApplyItem(0x50) on the real code: (Mp, MpMax) before -> after')
    for mpmax in range(0, 6):
        for mp in sorted(set([0, mpmax // 2, mpmax])):
            if mp > mpmax:
                continue
            a, b, j, rng = apply(0x50, mp, mpmax)
            sounds = [t[1] for t in j if t[0] == 'sound']
            nfx = sum(1 for t in j if t[0] == 'fx')
            steps = rng_steps(0xB017C93D, rng)
            print('  MpMax %d Mp %d -> Mp %d MpMax %d | sounds %s | effects %d | generator steps %d' % (mpmax, mp, a, b, [hex(s) for s in sounds], nfx, steps))
    print()
    print('== journal of ApplyItem(0x50) from (Mp 0, MpMax 0), hero at (0x100000, 0x200000, 0x300000), generator 0xB017C93D')
    a, b, j, rng = apply(0x50, 0, 0)
    for t in j:
        print('  ', t if t[0] != 'fx' else ('fx', 'ptr', 'parent=%d' % t[2], 'id=%d' % t[3], 'anim=%d' % t[4], 'X=%#x' % t[5], 'Y=%#x' % t[6], 'Z=%#x' % t[7]))
    print('  effect structs after the call (fields +0x3C +0x40 +0x54 +0x58 +0x5C, signed):')
    for k, p in enumerate(sorted(FX)):
        print('   fx%02d' % k, [m.rws(p + o) for o in (0x3C, 0x40, 0x54, 0x58, 0x5C)])
    print()
    print('== the other ApplyItem cases (what the chest gives)')
    for item in (0x24, 0x25, 0x27, 0x29, 0x3D, 0x45, 0x46, 0x47, 0x48, 0x4F, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x02, 0x2C):
        a, b, j, rng = apply(item, 1, 3)
        print('  item 0x%02x ->' % item, [t for t in j if t[0] != 'fx'], 'fx', sum(1 for t in j if t[0] == 'fx'), 'Mp/MpMax', a, b)
