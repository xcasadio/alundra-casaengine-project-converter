"""Native effect IncreaseHpMaxAndCreateEffect (0x800333AC, the `0x82 0x53` life vessel): the REAL code in the MIPS interpreter against a
python model on the pool model of model/fxpool.py (incl. the shared PRNG at 0x80098708). Validates a native creator with random forces."""
import sys, os, random, struct
H = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, H)
sys.path.insert(0, os.path.join(H, "..", "model"))
import cmp as C
import fxm
import fxpool as P

M32 = 0xFFFFFFFF
PRNG = 0x80098708
HERO = fxm.HERO
ENT = 0x80190000           # the entity argument (a0): +0x14 hp, +0x18 hp max
SOUND = []


def lcg(seed):
    return (seed * 0x7D2B89DD + 0xE06A02E7) & M32


def model_53(pool, hero, offx, offy, seed):
    """IncreaseHpMaxAndCreateEffect: 8 ring effects (anim 0, global sprite 14) then 4 sparks; returns the new seed."""
    for j in range(8):
        s = pool.create_free(0, 14, 0, hero[0], hero[1], hero[2] + 0x100000)
        if s is not None:
            ox, oy = offx[j], offy[j]
            s.x = P.s32(s.x + (ox << 13))
            s.y = P.s32(s.y + (oy << 13))
            s.forces = (P.s32(-(ox << 9)), P.s32(-(oy << 9)), 0)
    for i in range(4):
        s = pool.create_free(0, 14, 0, hero[0], hero[1], hero[2] + 0x100000)
        if s is not None:
            r1 = lcg(seed)
            r2 = lcg(r1)
            fx = ((r1 * 0x20001) >> 32) - 0x10000
            fy = ((r2 * 0x30001) >> 32) - 0x18000
            s.forces = (P.s32(fx), P.s32(fy), 0x40000)
            seed = r2
    return seed


def s16(v):
    return v - 0x10000 if v & 0x8000 else v


def run(seed_val, prefill, hero, label=""):
    f, p = C.new_pair(476)
    m = f.m
    for k, v in zip((0x114, 0x118, 0x11C), hero):
        m.ww(HERO + k, v & M32)
    m.ww(ENT + 0x14, 5)
    m.ww(ENT + 0x18, 7)
    m.ww(PRNG, seed_val)
    sounds = []
    m.hooks[0x800490FC] = lambda mm: sounds.append(mm.r[4])
    offx = [s16(m.rh(0x80023654 + 8 * j)) for j in range(8)]
    offy = [s16(m.rh(0x80023694 + 8 * j)) for j in range(8)]
    # pre-fill the pool with some effects of a record (script style) so that creators may fail
    for _ in range(prefill):
        f.op(bytes([0x90, 0])); p.op(bytes([0x90, 0]))
    m.call(0x800333AC, ENT)
    seed_model = model_53(p, hero, offx, offy, seed_val)
    seed_emu = m.rw(PRNG)
    errs = C.check(f, p, "after 0x53 " + label)
    if seed_emu != seed_model:
        errs.append("seed %08x != %08x" % (seed_emu, seed_model))
    # three ticks afterwards
    for t in range(3):
        f.tick(); p.tick()
        errs += C.check(f, p, "tick %d %s" % (t, label))
    return errs, offx, offy, sounds, m.rws(ENT + 0x14), m.rws(ENT + 0x18)


if __name__ == "__main__":
    rng = random.Random(53)
    bad = 0
    n = 0
    for case in range(60):
        seed = rng.getrandbits(32)
        prefill = rng.choice([0, 0, 0, 5, 118, 120, 124, 127, 128])
        hero = (rng.randrange(0, 1200) << 16, rng.randrange(0, 900) << 16, rng.randrange(0, 4) << 16)
        errs, offx, offy, sounds, hp, hpmax = run(seed, prefill, hero, "case %d" % case)
        n += 1
        if errs:
            bad += 1
            print("MISMATCH case", case, prefill, errs[:3])
    print("cases", n, "bad", bad)
    errs, offx, offy, sounds, hp, hpmax = run(0x12345678, 0, (100 << 16, 80 << 16, 0), "pin")
    print("offX[j*4] halfwords:", offx)
    print("offY[j*4] halfwords:", offy)
    print("sound ids played:", sounds, "hp, hpmax after (5,7 before):", hp, hpmax)
