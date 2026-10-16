"""startstate.py - D1: dump the hero's starting values (plan 1.2 D1, "Case du heros") as the oracle builds them, before and after the first tick,
with the 14 first record bytes of the balance sheet the hero points at.  writes out/start-state.txt"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import oracle
import world as W


def main(outdir):
    m, meta = oracle.setup()
    P = W.ent(0)
    L = []

    def dump(tag):
        L.append('== %s' % tag)
        for name, off in (('status +0x10', 0x10), ('HP +0x14', 0x14), ('HP max +0x18', 0x18), ('+0x20', 0x20), ('flags +0x6C', 0x6C), ('sprite record +0x64', 0x64),
                          ('TargetAnimationId +0x88', 0x88), ('TargetDirection +0x8C', 0x8C), ('CurrentAnimationId +0x90', 0x90), ('AnimDirection +0x98', 0x98),
                          ('IsOnGround +0x144', 0x144), ('CollidedWithEntityZ +0x140', 0x140), ('+0x180', 0x180), ('+0x188', 0x188), ('+0x1C4 balance sheet', 0x1C4),
                          ('+0x1CC invulnerability', 0x1CC), ('+0x220 HitCounter', 0x220), ('+0x224 TouchingEntity', 0x224),
                          ('PosX +0x114', 0x114), ('PosY +0x118', 0x118), ('PosZ +0x11C', 0x11C),
                          ('ModX +0x1E4', 0x1E4), ('ModY +0x1E8', 0x1E8), ('ModZ +0x1EC', 0x1EC), ('W +0x1F0', 0x1F0), ('H +0x1F4', 0x1F4), ('D +0x1F8', 0x1F8)):
            L.append('  %-28s 0x%08X  %d' % (name, m.peek(P + off), m.peeks(P + off)))
        L.append('  [0x80127000] boots level     %d' % m.peeks(0x80127000))
        st = m.peek(0x80176318)
        L.append('  [0x80176318] -> g_playerStats 0x%08X: Hp %d, HpMax %d, WeaponId +0xA %d, armour +0xC %d' % (
            st, m.peeks(st) & 0xFFFF, m.peek(st + 2) & 0xFFFF, m.peek(st + 0xA) & 0xFFFF, m.peek(st + 0xC) & 0xFFFF))
        L.append('  [0x80134360] last entity     %d' % m.peeks(0x80134360))
        L.append('  [0x800DC4B8] state flags     0x%X' % m.peek(0x800DC4B8))
        L.append('  [0x80127108] [0x8012710C] [0x80127110] [0x80127114]: %d %d 0x%X %d' % tuple([m.peeks(0x80127108), m.peeks(0x8012710C), m.peek(0x80127110), m.peeks(0x80127114)]))
        rec = m.peek(P + 0x1C4)
        L.append('  balance sheet bytes at 0x%08X: %s' % (rec, ' '.join('%02X' % m.ram[(rec + i) & 0x1FFFFFFF] for i in range(16))))
        L.append('  working copy 0x80127008 bytes:   %s' % ' '.join('%02X' % m.ram[(0x80127008 + i) & 0x1FFFFFFF] for i in range(16)))
    dump('before tick 1 (after InitializeEntity + the plan starting values)')
    pad = W.Pad()
    W.tick(m, pad, 0)
    dump('after one tick with no input')
    L.append('kit added by the new-game loop: %s' % meta['kit'])
    open(os.path.join(outdir, 'start-state.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    print('\n'.join(L))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out'))
