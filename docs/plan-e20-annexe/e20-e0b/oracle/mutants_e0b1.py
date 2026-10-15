"""mutants_e0b1.py - E20.e0b1 T0: one-word mutants of the emulated binary (RAM patches, never saved) against the 24 calls of the frame-function probe (s7_frame.py).

Run from the repository root:   python -B docs/plan-e20-annexe/e20-e0b/oracle/mutants_e0b1.py     (about 20 s; writes out/mutations.txt ; exit code 0 = every mutant CAUGHT)
Each mutant patches ONE instruction word of ALUN_CD.EXE in the RAM of a fresh machine (the old word is asserted against the disassembly of the discovery), runs the 24 calls of the probe
(exactly the body of s7_frame.py: its source is executed with the output redirected) and is CAUGHT when the signature of the results differs from the unmutated one, or when the machine faults.
A mutant that survives has to be proven equivalent in writing (README) or is a missing vector."""
import os
import sys
import io
import json
import hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = open(os.path.join(HERE, 's7_frame.py'), encoding='utf-8').read()
NOP = 0x00000000

# (label, address, old word, new word)
MUTANTS = [
    ('multiplier high half: lui 0x7d2b -> 0x7d2a (0x8002BD0C)', 0x8002BD0C, 0x3C037D2B, 0x3C037D2A),
    ('multiplier low half: ori 0x89dd -> 0x89dc (0x8002BD18)', 0x8002BD18, 0x346389DD, 0x346389DC),
    ('increment high half: lui 0xe06a -> 0xe06b (0x8002BD20)', 0x8002BD20, 0x3C02E06A, 0x3C02E06B),
    ('increment low half: ori 0x2e7 -> 0x2e6 (0x8002BD24)', 0x8002BD24, 0x344202E7, 0x344202E6),
    ('multiplication removed: mult -> nop (0x8002BD1C)', 0x8002BD1C, 0x00430018, NOP),
    ('squared instead of multiplied: mult v0,v1 -> mult v0,v0 (0x8002BD1C)', 0x8002BD1C, 0x00430018, 0x00420018),
    ('increment turned into a subtraction: addu -> subu (0x8002BD2C)', 0x8002BD2C, 0x00C21021, 0x00C21023),
    ('increment removed: addu -> or with zero of the product only (0x8002BD2C)', 0x8002BD2C, 0x00C21021, 0x00C01025),
    ('polarity of the beqz s3 inverted: beqz -> bnez (0x8002BD30)', 0x8002BD30, 0x12600003, 0x16600003),
    ('store of the advance removed (delay slot of the beqz, 0x8002BD34)', 0x8002BD34, 0xAC828708, NOP),
    ('store of the advance to a neighbour word: -0x78f8 -> -0x78f4 (0x8002BD34)', 0x8002BD34, 0xAC828708, 0xAC82870C),
    ('read of the state from a neighbour word: -0x78f8 -> -0x78f4 (0x8002BD14)', 0x8002BD14, 0x8C828708, 0x8C82870C),
    ('base register of the state: lui a0,0x800a -> 0x8009 (0x8002BD10)', 0x8002BD10, 0x3C04800A, 0x3C048009),
    ('clearing of [0x800DC4C4] removed (0x8002BD3C)', 0x8002BD3C, 0xAC40C4C4, NOP),
    ('request raised by a refused inventory removed (0x8002BD00)', 0x8002BD00, 0xAC62C4C4, NOP),
    ('result of DisplayInventory inverted: bnez -> beqz (0x8002BCF4)', 0x8002BCF4, 0x14400003, 0x10400003),
    ('gate test 0x48 -> 0x40 (0x8003B394)', 0x8003B394, 0x30420048, 0x30420040),
    ('gate test 0x48 -> 0x08 (0x8003B394)', 0x8003B394, 0x30420048, 0x30420008),
    ('gate test removed: andi 0x48 -> andi 0 (0x8003B394)', 0x8003B394, 0x30420048, 0x30420000),
    ('gate test polarity inverted: bnez -> beqz (0x8003B398)', 0x8003B398, 0x1440001B, 0x1040001B),
]


def run_probe(patch=None):
    """executes the whole s7_frame.py in a fresh namespace (fresh machine), with the machine patched before the 24 calls ; returns the list of results"""
    head, tail = SRC.split('\nresults = []\n')
    ns = {'__name__': 's7_mut'}
    cwd = os.getcwd()
    os.chdir(HERE)
    try:
        exec(compile(head, 's7_frame_head', 'exec'), ns)
        if patch:
            addr, old, new = patch
            got = ns['m'].peek(addr)
            assert got == old, 'old word at 0x%08X is 0x%08X, expected 0x%08X' % (addr, got, old)
            ns['m'].poke(addr, new)
        ns['open'] = lambda *a, **k: io.StringIO()
        ns['print'] = lambda *a, **k: None
        exec(compile('results = []\n' + tail, 's7_frame_tail', 'exec'), ns)
        return ns['results']
    finally:
        os.chdir(cwd)


def signature(results):
    return hashlib.sha1(json.dumps(results, sort_keys=True).encode()).hexdigest()


def main():
    base_results = run_probe()
    base = signature(base_results)
    same = json.loads(json.dumps(base_results)) == json.load(open(os.path.join(HERE, 'out', 'frame_trace.json')))
    lines = ['# E20.e0b1 T0: %d one-word mutants of the emulated binary against the 24 calls of s7_frame.py ; baseline signature %s' % (len(MUTANTS), base),
             '# CAUGHT = the signature differs (or the machine faults) ; SURVIVED = a vector is missing or the mutant is equivalent (proof in the README)']
    survived = 0
    for label, addr, old, new in MUTANTS:
        try:
            sig = signature(run_probe((addr, old, new)))
            res = 'CAUGHT' if sig != base else 'SURVIVED'
        except AssertionError:
            raise
        except Exception as e:                                   # a fault of the machine is a kill as well
            res = 'CAUGHT (fault: %s)' % type(e).__name__
        if res == 'SURVIVED':
            survived += 1
        lines.append('%-9s %s  [0x%08X: %08X -> %08X]' % (res, label, addr, old, new))
    lines.append('# %d mutants, %d survived ; the unmutated run in this process equals out/frame_trace.json: %s' % (len(MUTANTS), survived, same))
    open(os.path.join(HERE, 'out', 'mutations.txt'), 'w', newline='\n').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if survived == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
