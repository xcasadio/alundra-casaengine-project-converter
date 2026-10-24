"""c3_contact_ticks.py - E20.g1b T0 (b2): c3_contact.py with the contact of EACH tick printed (hero +0x130, the entity that blocked the step of the tick before).
The cases, the pad scripts and the verdicts are c3_contact.py's, unchanged; the only addition is the per-tick table (tick, pad word, hero x, contact, animation, +0x13C).
The verdicts are written to out/c3-contact-ticks-verdicts.txt and compared with out/c3-contact.txt: a difference is a STOP (exit code 1).
Run: python -B -I c3_contact_ticks.py        (from a copy: it writes in ./out)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import c3_contact as C3

R, SQ, UP = gl.RIGHT, gl.SQUARE, gl.UP
CASES = {
    'A hold RIGHT t1-9, release+Square at t10': ([(9, R), (1, SQ), (10, 0)], 10),
    'B hold RIGHT t1-9, release at t10, Square at t11': ([(9, R), (1, 0), (1, SQ), (10, 0)], 11),
    'C hold RIGHT t1-9, Square at t10 still holding RIGHT': ([(9, R), (1, R | SQ), (10, R)], 10),
    'D release t10-14, RIGHT again t15, Square at t15 (first tick of the 2nd hold)': ([(9, R), (5, 0), (1, R | SQ), (10, R)], 15),
    'D2 same with Square at t16': ([(9, R), (5, 0), (1, R), (1, R | SQ), (10, R)], 16),
    'E Square at t5 (first tick touching: contact appears after the physics of t5)': ([(4, R), (1, R | SQ), (10, R)], 5),
    'E2 Square at t6': ([(5, R), (1, R | SQ), (10, R)], 6),
    'F Square HELD from t1 with RIGHT': ([(20, R | SQ)], 6),
    'G UP held (not pushing the crate), Square at t10': ([(9, UP), (1, UP | SQ), (10, 0)], 10),
}


def main():
    verdicts, table = [], []
    for name, (seq, sq) in CASES.items():
        rows = C3.run(seq)
        verdicts.append('%-84s %s' % (name, C3.verdict(rows, sq)))
        table.append('== ' + name)
        for r in rows:
            h = r['hero']
            table.append('t%02d hold=0x%04X x=%.4f xcoll=%s anim=%d adj=%d carried2C=%X' % (r['tick'], r['hold'], h['px'] / 65536, ('crate' if h['xcoll'] else '-'), h['anim_t'], h['adj'], h['carried2C']))
    vt = '\n'.join(verdicts) + '\n'
    open(os.path.join(gl.OUT, 'c3-contact-ticks-verdicts.txt'), 'w').write(vt)
    open(os.path.join(gl.OUT, 'c3-contact-ticks.txt'), 'w').write('\n'.join(table) + '\n')
    ref = open(os.path.join(gl.OUT, 'c3-contact.txt')).read()
    print(vt, end='')
    if ref != vt:
        print('STOP: the verdicts differ from out/c3-contact.txt')
        sys.exit(1)
    print('verdicts equal to out/c3-contact.txt')


if __name__ == '__main__':
    main()
