"""hitfx_mut.py - bin agent: teeth of the hit-pass table.  One-word patches of the emulated binary; the table must change (the expectation is the decoded rule, not recomputed).
M1 gate mask 0xC0 -> 0x80 at 0x800395E8; M2 nibble 0xA -> 0xB at 0x800395F8 (effect 4 condition); M3 effect id 5 -> 4 at 0x80039644; M4 gate branch beq -> bne at 0x800395EC; control: no patch."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hitfx as H
import pworld as P
import c1world as C

def run(patches):
    m, meta = H.build()
    for a, w in patches:
        m.poke(a, w)
    bad = []
    n = 0
    for nib in range(16):
        attr = nib if nib else 0x80
        for resp in (0x00, 0x10, 0x40, 0x80, 0xBF, 0xC0, 0xFF, 0x81):
            r = H.trial(m, attr, 1, resp, nib)
            immune, fx = H.expected(nib, resp)
            got = [int(e.split('id=')[1].split(',')[0]) for e in r['effects']]
            ok = got == fx and (r['touch'] == 0) == immune
            n += 1
            if not ok:
                bad.append((nib, resp, got, fx))
    return n, bad

if __name__ == '__main__':
    out = []
    muts = (('control (no patch)', []),
            ('M1 mask 0xC0 -> 0x80 at 0x800395E8', [(0x800395E8, 0x30420080)]),
            ('M2 nibble 0xA -> 0xB at 0x800395F8', [(0x800395F8, 0x2402000B)]),
            ('M3 effect id 5 -> 4 at 0x80039644', [(0x80039644, 0x24050004)]),
            ('M4 gate beq -> bne at 0x800395EC', [(0x800395EC, 0x1443001F)]))
    for name, p in muts:
        n, bad = run(p)
        out.append('%-42s: %d pairs, %d differ from the decoded rule%s' % (name, n, len(bad), ('  e.g. ' + str(bad[:3])) if bad else ''))
        print(out[-1])
    open(os.path.join(H.OUT, 'hitfx_mutations.txt'), 'w').write('\n'.join(out) + '\n')
