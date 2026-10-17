"""g14_variants.py - S14: the liftable sprite variants of the corpus (C23 jars/crates, C4 stones/iron balls, C70/E20 torches, attack-less ones): lift, locked branch (mode 1), throw, break.
The handlers C23/E2 are REAL in the oracle; C4 (0x80066984), E20 (0x8007F6C8) and C70 (0x8007B7B0) are added to the REAL set here (keep=...).
Run: python -B -I g14_variants.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K

VARIANTS = [('map_392', 36, 'crate C23/E2 AttackCount 1'), ('map_103', 48, 'C23/E2 MoreFlags C4 AttackCount 1'), ('map_70', 41, 'C23/E2 AttackCount 0 break fx 2'),
            ('map_22', 34, 'C4/E2'), ('map_27', 107, 'C4/E0 size 20x12x14 AttackCount 0'), ('map_70', 117, 'C4/E2 AttackCount 7'), ('map_86', 137, 'C70/E20 torch')]

if __name__ == '__main__':
    out = []
    for mp, sp, nm in VARIANTS:
        keep = {0x80066984, 0x8007F6C8, 0x8007B7B0}
        res = {}
        for scen in ('throw', 'locked'):
            m = gl.world(keep=keep)
            try:
                c = gl.add_crate(m, 130, 100, mapname=mp, sprite=sp)
            except RuntimeError as ex:
                res[scen] = 'spawner refused (%s)' % ex
                info = dict(flags='?')
                cnt = -1
                continue
            e = K.ent(c)
            info = dict(flags='%X' % m.peek(e + 0x6C), sheetcount=m.peek(m.peek(e + 0x1C4)) >> 0 & 0xFFFFFFFF, atk=(m.peek(m.peek(e + 0x1C4) + 12) >> 16) & 0xFF if False else None)
            sheet = m.peek(e + 0x1C4)
            cnt = (m.peek((sheet + 0xE) & ~3) >> (8 * ((sheet + 0xE) & 3))) & 0xFF
            seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0)]
            if scen == 'throw':
                seq += [(1, gl.RIGHT | gl.SQUARE), (40, 0)]
                pre = None
            else:
                seq += [(40, 0)]
                def pre(m_, t):
                    if t == 60:
                        m_.poke(0x800DC4B8, 4)
            pads, n = gl.pad_script(seq)
            rows = gl.run(m, n, pads, crates=[c], pre=pre)
            lift = ana.first_tick(rows, lambda r: r['hero']['carried2C'] != 0)
            if scen == 'throw':
                s = ana.changes(rows, c, ['status', 'anim_t', 'carrier28', 'flags6C'], first=59)
                res[scen] = 'lift t%s ; hero anim after lift %s ; crate changes %s ; flight X %.1f..%.1f' % (lift, ana.changes(rows, 'hero', ['anim_t'], first=10)[:3], s[:6], rows[60]['c'][c]['px'] / 65536, max(r['c'][c]['px'] for r in rows) / 65536)
            else:
                res[scen] = 'locked at t60: hero.+0x2C at end %X ; crate status changes %s' % (rows[-1]['hero']['carried2C'], ana.changes(rows, c, ['status', 'carrier28'], first=59)[:3])
        out.append('%s sprite %d (%s) flags %s AttackCount %d:\n   THROW %s\n   LOCKED %s' % (mp, sp, nm, info['flags'], cnt, res['throw'], res['locked']))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g14-variants.txt'), 'w').write('\n'.join(out) + '\n')
