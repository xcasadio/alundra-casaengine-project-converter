"""My OWN rules model (written from my reading of 0x80036A84-0x80036BBC, 0x800375E0, 0x80036BFC, 0x80036D94, 0x80038064)
against the real code on random single-entity scenarios (terrain only: flat cells). Not derived from the first discovery's zmodel."""
import sys, os, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # h2 annex edit: was sys.path.insert(0, '.') (cwd-dependent)
from vz import *
sys.stdout.reconfigure(encoding="utf-8")
def s32_(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x
class M:
    def __init__(s, posz, forceZ, grav, modz, depth, T, G, V):
        s.posz, s.forceZ, s.grav, s.modz, s.depth, s.T, s.G, s.V = posz, forceZ, grav, modz, depth, T, G, V
        s.contact = 0; s.ffz = 0; s.onground = 0
    def tick(s, imp=0):
        s.contact = 0
        # forces (non-hero branch 0x80036ab8..0x80036bb8)
        if imp != 0:
            if (imp & 0xFFFF) == 0x8000 and not s.grav: s.forceZ = 0
            else: s.forceZ = imp << 8
        elif s.grav:
            v = s.forceZ - (s.G << 8); lim = s.V << 8
            if abs(v) > lim: v = lim if v > 0 else -lim
            s.forceZ = v
        F = s.ffz = s.forceZ
        mod = s.posz + s.modz           # ModdedPosZ, computed in the pre-pass
        if F > 0:
            tgt = mod + s.depth + F
            if tgt > 0x7800000:
                s.posz = 0x77FFFFF - s.modz - s.depth; s.contact = 1
                if s.grav: s.forceZ = 0
            else: s.posz += F
        else:
            if s.T >= mod + F:
                s.posz = s.T + 1 - s.modz; s.contact = 1
                if s.grav: s.forceZ = 0
            else: s.posz += F
        s.onground = 1 if s.T + 1 >= s.posz else 0
rnd = random.Random(20261006)
bad = 0; n = 0; ticks = 0
kinds = {"cap": 0, "land": 0, "rise": 0}
for sc in range(700):
    hb = rnd.choice([0, 1, 2, 5, 20])
    T = hb * 16 * U
    G, V = rnd.choice([(128, 4096), (3, 256), (128, 4096), (10, 40)])
    grav = rnd.random() < 0.6
    sizez = rnd.choice([0, 0, 16, 32]); offz = rnd.choice([0, 0, 0, 4, 8])
    depth = ((sizez << 16) - 1) if sizez else 0; modz = offz << 16
    mode = rnd.random()
    if mode < 0.15: posz = rnd.randint(1700, 1920) * U + rnd.randint(-5, 5)      # cap zone
    elif mode < 0.4: posz = T + 1 - modz + rnd.randint(-3, 3)                     # around rest
    else: posz = T + rnd.randint(0, 200 * U)
    fz = rnd.choice([0, 0, rnd.randint(-300000, 300000), rnd.randint(-3000000, 3000000), 32768, -32768, 1 << 20])
    w = World(G, V); w.flat(hb)
    flags = 0x80 | (0x100 if grav else 0)
    w.add(1, flags, (24 * U, 16 * U, posz), size=(0, 0, sizez), off=(0, 0, offz), forceZ=fz)
    w.wr(1, 'posz', posz)
    m = M(posz, fz, grav, modz, depth, T, G, V)
    for t in range(30):
        imp = 0
        r = rnd.random()
        if r < 0.05: imp = rnd.choice([1360, 0x8000, 0x8000 - 65536 if False else 40, -80, 0x8000]); 
        if r > 0.93:                                # script-like writes (same on both sides)
            nf = rnd.choice([32768, -32768, 0, 65536, -65536, 8 * U, -8 * U]); w.wr(1, 'forceZ', nf); m.forceZ = nf
        if 0.90 < r <= 0.93:                        # gravity toggle (0x16 / 0x17)
            grav2 = not m.grav; m.grav = grav2
            fl = w.m.rw(eaddr(1) + F['flags']); fl = (fl | 0x100) if grav2 else (fl & ~0x100); w.m.ww(eaddr(1) + F['flags'], fl)
        w.wr(1, 'imp', imp)                         # +0xF8 impulse word (lh in the animation code: signed 16)
        w.tick(); m.tick(s32_(imp) if imp else 0)
        s = w.state(1); ticks += 1
        mine = (m.posz, m.forceZ, m.ffz, m.contact, m.onground)
        real = (s["PosZ"], s["ForceZ"], s["FFZ"], s["contact"], s["onground"])
        if mine != real:
            bad += 1
            if bad <= 5: print("MISMATCH sc", sc, "t", t, "mine", mine, "real", real, dict(hb=hb, G=G, V=V, grav=m.grav, modz=modz, depth=depth, imp=imp))
            break
        if m.contact and m.forceZ == 0 and m.ffz > 0: kinds["cap"] += 1
        elif m.contact: kinds["land"] += 1
        elif m.ffz > 0: kinds["rise"] += 1
    n += 1
print("scenarios", n, "ticks", ticks, "mismatching scenarios", bad, "coverage", kinds)
