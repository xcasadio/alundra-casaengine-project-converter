"""vec_b0.py - the b0 helpers NOT used by the b1 handlers but named in the brief: TryAttackPlayer 0x80080704 (against the decompilation's transcription),
SetEntityRandomDirection 0x80080030 (model written from the disassembly). REAL code vs model, random vectors."""
import random
import models as M
from h import World, Ent, Rng, ENT0, STRIDE, M32, s32
w = World()
gdt = lambda dx, dy: w.call(0x8002E814, dx & M32, dy & M32)
rnd = random.Random(7)
REL = 0x80141100

def try_attack_player_decomp(adir, rel, max_h, max_z):
    """EntityGameplayManager.cs:348-430 transcribed (the decompilation)"""
    if max_z < rel[2]:
        return False
    if adir == 1:
        if rel[0] < 2 and -1 < rel[4] and rel[1] <= max_h: return True
        if 1 < rel[1]: return False
        return not (max_h < rel[0])
    elif adir < 2:
        if adir != 0: return False
        if rel[0] < 2 and rel[4] < 1 and rel[1] <= max_h: return True
        if 1 < rel[1]: return False
        return not (max_h < rel[0])
    else:
        if adir == 2:
            if rel[1] < 2 and -1 < rel[3] and rel[0] <= max_h: return True
            if 1 < rel[0]: return False
            return rel[1] <= max_h
        if adir != 3: return False
        if rel[1] < 2 and rel[3] < 1 and rel[0] <= max_h: return True
        if 1 < rel[0]: return False
        return not (max_h < rel[1])

def set_random_direction(e, hero, anim, rng_range, rng, gdt):
    r = rng.below(0x2000)
    if r & 1:
        e.set32(M.TDIR, gdt(s32(hero.s32(M.POSX) - e.s32(M.POSX)), s32(hero.s32(M.POSY) - e.s32(M.POSY))))
    else:
        e.set32(M.TDIR, r >> 8)
    e.set32(M.TANIM, anim & 0xFF)
    if (rng_range & 0xFFFF) != 0:
        e.set16(M.AI1, rng.below(16) + rng_range)

bad_tap = n_tap = 0
bad_srd = n_srd = 0
for i in range(30000):
    adir = rnd.randrange(0, 5)
    rel_abs = [rnd.randrange(0, 6), rnd.randrange(0, 6), rnd.choice([0, 1, 0x100000, 0x100001, rnd.randrange(0, 0x300000)])]
    rel = rel_abs + [rnd.choice([-1, 1]) * rel_abs[0] if rel_abs[0] else 0, rnd.choice([-1, 1]) * rel_abs[1] if rel_abs[1] else 0, rnd.choice([-1, 1]) * rel_abs[2] if rel_abs[2] else 0]
    max_h = rnd.randrange(0, 6)
    max_z = rnd.choice([0, 0x100000, 0x200000, rnd.randrange(0, 0x300000)])
    e = Ent(); e.set32(M.ADIR, adir)
    w.put(5, e)
    for k in range(6):
        w.m.ww(REL + 4 * k, rel[k])
    v0 = w.call(0x80080704, ENT0 + 5 * STRIDE, REL, max_h, max_z) & M32
    want = int(try_attack_player_decomp(adir, rel, max_h, max_z))
    n_tap += 1
    bad_tap += (v0 != want)
for i in range(5000):
    hero = Ent(); hero.set32(M.POSX, rnd.randrange(0, 60 << 16)); hero.set32(M.POSY, rnd.randrange(0, 40 << 16))
    e = Ent(); e.set32(M.POSX, rnd.randrange(0, 60 << 16)); e.set32(M.POSY, rnd.randrange(0, 40 << 16)); e.set16(M.AI1, 77)
    seed = rnd.randrange(1 << 32)
    anim = rnd.randrange(0, 20); rg = rnd.choice([0, 0x1E, 0x3C, rnd.randrange(0, 300), 0x10000])
    w.j.clear(); w.put(0, hero); w.put(5, e); w.set_rng(seed)
    w.call(0x80080030, ENT0 + 5 * STRIDE, anim, rg)
    me = e.copy(); mr = Rng(seed)
    set_random_direction(me, hero, anim, rg, mr, gdt)
    n_srd += 1
    if bytes(w.get(5).raw) != bytes(me.raw) or w.get_rng() != mr.s:
        bad_srd += 1
print('TryAttackPlayer 0x80080704: real code vs the decompilation transcribed: %d differences in %d vectors' % (bad_tap, n_tap))
print('SetEntityRandomDirection 0x80080030: real code vs model: %d differences in %d vectors' % (bad_srd, n_srd))
