"""decomp_variants.py - measures, on the same random vectors as vec.py, how often a model of the DECOMPILATION's behaviour differs from the real code:
 E3: random direction from the LOW 5 bits of the word (Random.Next() % 32) instead of the high 5 bits ((s*32)>>32)  [StartFlying, C18 state 0]
 E4: D12 where the Orc mace never parries (the nested type test is dead code)
 E1/E2 were measured by the mutants of mut.py (264 and 93 mismatches on 4 500 vectors)."""
import sys
src = open('vec.py', encoding='utf-8').read()
head = src.split('# ---- helpers: relpos x2')[0]
sys.argv = ['x', '2000']
exec(compile(head, 'vec_head', 'exec'))
import models as M
from h import Rng

class LowRng(Rng):
    """like the decompilation's Random.Next() % n for the direction draws of 32"""
    def below(self, n):
        self.s = M.lcg(self.s) if hasattr(M, 'lcg') else ((self.s * 0x7D2B89DD + 0xE06A02E7) & M32)
        r = (self.s % 32) if n == 32 else (self.s * n) >> 32
        self.draws.append((n, r))
        return r

# E3 on StartFlying and C16 / C18
for name, fn, ctype, anims in (('C16 (StartFlying)', M.c16, 341, [0]), ('C18 state 0', M.c18, 455, [0])):
    n = bad = 0
    for i in range(6000):
        hero = mk_hero(); e = mk_self(hero, ctype, anims); seed = rnd.randrange(1 << 32)
        a = e.copy(); ra = Rng(seed); fn(a, hero, ra, [], gdt, 7)
        b = e.copy(); rb = LowRng(seed); fn(b, hero, rb, [], gdt, 7)
        if len(ra.draws) == 0: continue
        n += 1; bad += (bytes(a.raw) != bytes(b.raw))
    print('E3 %s: %d of %d vectors that draw differ when the direction uses the low bits' % (name, bad, n))

# E4: D12 for the mace without any parry
n = bad = 0
for i in range(20000):
    hero = mk_hero(); e = mk_self(hero, 341, list(range(0, 9)))
    nib = pick([1, 2, 3, 4, 0]); seed = rnd.randrange(1 << 32); hp = pick([0, 1])
    a = e.copy(); ra = Rng(seed); mj = []
    M.d12(a, hero, ra, mj, gdt, lambda ent, _h=hp: _h, nib)
    b = e.copy(); rb = Rng(seed)
    # decompilation: for type 0x155 the nested test is dead code -> never a parry
    b.set32(M.TDIR, M.dir_away(b, hero, gdt))
    if ((nib - 1) & M32) < 3:
        rb.below(100)
    if hp: b.set8(M.B3, 1)
    b.set32(M.TANIM, 4)
    n += 1; bad += (bytes(a.raw) != bytes(b.raw))
print('E4 D12 on the mace: %d of %d vectors differ if the mace never parries (%.1f%% of the hits with nibble 1 to 3 at random animations)' % (bad, n, 100.0 * bad / n))
