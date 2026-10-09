"""mutations.py - D2: does the validation have teeth?  Each mutation patches ONE instruction word of the emulated binary (RAM copy, never the file) and
re-runs the six cases; the case(s) that must notice it are named in advance.  A mutation that leaves every case PASS would mean that the cases cannot see
that piece of behaviour.  writes outdir/mutations.txt, exit code 0 = every mutation is caught by the case named."""
import io
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import hitworld as H
import cases as C

# (name, address, old word (checked), new word, case expected to FAIL (1-6), what the original word is)
MUTATIONS = [
    ('HitsClassA bit dropped from the class mask', 0x800393A0, 0x30420001, 0x30420000, 1, 'andi v0,v0,1 after srl $s2,2'),
    ('HitsClassB bit dropped from the class mask', 0x800393A8, 0x30630008, 0x30630000, 1, 'andi v1,v1,8 after sll $s2,2'),
    ('overlap not enlarged by one, X axis, d >= 0', 0x80039480, 0x24420001, 0x24420000, 2, 'addiu v0,v0,1 (target size + 1)'),
    ('overlap not enlarged by one, Y axis, d >= 0', 0x800394C0, 0x24420001, 0x24420000, 2, 'addiu v0,v0,1'),
    ('overlap not enlarged by one, Z axis, d >= 0', 0x80039500, 0x24420001, 0x24420000, 2, 'addiu v0,v0,1'),
    ('overlap not enlarged by one, X axis, d < 0', 0x80039494, 0x25430001, 0x25430000, 2, 'addiu v1,t2,1 (attacker size + 1)'),
    ('overlap enlarged by two, X axis, d >= 0', 0x80039480, 0x24420001, 0x24420002, 2, 'addiu v0,v0,1'),
    ('response 0x80 no longer immune', 0x800395EC, 0x1043001F, 0x00000000, 3, 'beq v0,v1,0x8003966c'),
    ('minimum damage 1 -> 0', 0x80044764, 0x24050001, 0x24050000, 4, 'addiu a1,zero,1'),
    ('target +0x1D0 no longer blocks a second attacker', 0x80039424, 0x144000C9, 0x00000000, 5, 'bnez v0,0x8003974c'),
    ('attacker stops after its first target', 0x80039758, 0x1440FF28, 0x00000000, 6, 'bnez v0,0x800393fc'),
    ('HitCounter not incremented', 0x80039680, 0xAE220220, 0x00000000, 1, 'sw v0,0x220(s1)'),
    ('TouchingEntity not stored', 0x80039668, 0xAE110224, 0x00000000, 1, 'sw s1,0x224(s0)'),
]


def patch(W, addr, old, new):
    o = addr & 0x1FFFFFFF
    cur = int.from_bytes(W.snap[0][o:o + 4], 'little')
    if cur != old:
        raise RuntimeError('word at %08X is %08X, expected %08X' % (addr, cur, old))
    ram = bytearray(W.snap[0])
    ram[o:o + 4] = new.to_bytes(4, 'little')
    W.snap = (bytes(ram), W.snap[1])


def failing_cases(W):
    fails = []
    for k, (title, fn) in enumerate(C.SIX, 1):
        n_ok, n, _ = C.run_group(W, fn(W), io.StringIO())
        if n_ok != n:
            fails.append((k, n - n_ok, n))
    return fails


def main(outdir):
    os.makedirs(outdir, exist_ok=True)
    lines = []
    ok = True
    base = failing_cases(H.World())
    lines.append('baseline (no mutation): failing cases %s' % (base or 'none'))
    ok &= not base
    for name, addr, old, new, want, what in MUTATIONS:
        W = H.World()
        patch(W, addr, old, new)
        fails = failing_cases(W)
        caught = any(k == want for k, _, _ in fails)
        ok &= caught
        lines.append('%-5s %-52s %08X: %08X -> %08X (%s)  failing cases: %s  expected case %d: %s' % (
            'CAUGHT' if caught else 'MISSED', name, addr, old, new, what, ', '.join('case %d (%d/%d runs)' % f for f in fails) or 'none', want, 'caught' if caught else 'NOT caught'))
    open(os.path.join(outdir, 'mutations.txt'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')))
