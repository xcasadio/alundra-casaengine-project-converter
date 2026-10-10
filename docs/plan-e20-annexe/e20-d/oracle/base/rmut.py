"""rmut.py - mutation tests of the run oracle: one-word patches of the emulated binary (RAM only, kworld.PATCHES, the old word is checked) that must change the run traces the way
the patched instruction says (the expected change is written in this file BEFORE the run), plus a control patch in code that is never executed (nothing may change).
A mutation is CAUGHT when the written change happens.  usage: python -B rmut.py   -> ../out/check/mutations.txt, exit 0 = every mutation caught and the control unchanged."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R
import kworld as K
import lib

OUT = os.path.join(HERE, '..', 'out', 'check')
UP, RIGHT, LEFT = R.UP, R.RIGHT, R.LEFT
TRI, CIR, CRO, SQU = R.TRI, R.CIR, R.CRO, R.SQU
CENTER = (624, 480)


def first(res, key, val, start=1):
    for r in res['rows']:
        if r['tick'] >= start and r['hero'][key] == val:
            return r['tick']
    return None


def run_with(patches, spec):
    K.PATCHES[:] = patches
    try:
        return R.run_r(spec)
    finally:
        K.PATCHES[:] = []


# probe scenarios (inputs only)
P_RESUME = dict(name='mut-resume-k10', hero_px=CENTER, n=90, pad=[(1, TRI | RIGHT), (41, TRI), (51, TRI | RIGHT)])          # re-press 10 ticks after the first no-direction tick
P_RESUME12 = dict(name='mut-resume-k12', hero_px=CENTER, n=90, pad=[(1, TRI | RIGHT), (41, TRI), (53, TRI | RIGHT)])
P_CONE = dict(name='mut-cone-UR', hero_px=CENTER, n=60, pad=[(1, TRI | RIGHT), (41, TRI | UP | RIGHT)])                       # inside the cone of a run to the east
P_START = dict(name='mut-start-T', hero_px=CENTER, n=60, pad=[(1, TRI | RIGHT)])
P_START_C = dict(name='mut-start-C', hero_px=CENTER, n=60, pad=[(1, CIR | RIGHT)])
P_DASH = dict(name='mut-circle-dash', hero_px=CENTER, n=70, pad=[(1, TRI | RIGHT), (40, TRI | RIGHT | CIR), (41, TRI | RIGHT)])
P_WALL = dict(name='mut-wall', hero_px=(100, 100), n=100, pad=[(1, TRI | RIGHT)], layout=R.Layout().rect(14, 0, 15, 59, word=0x40))


def main():
    os.makedirs(OUT, exist_ok=True)
    lines = ['# mutation tests of the run oracle (one-word patches of the emulated binary; expected change written in rmut.py before the run)']
    ok = True

    def word(a):
        return lib.u32(a)

    def check(label, patches, specs, expect_fn, expect_text):
        nonlocal ok
        base = [R.run_r(sp) for sp in specs]
        mut = [run_with(patches, sp) for sp in specs]
        good, detail = expect_fn(base, mut)
        ok = ok and good
        lines.append('%-4s %s' % ('PASS' if good else 'FAIL', label))
        lines.append('       patch: %s' % ', '.join('0x%08X: %08X -> %08X' % p for p in patches))
        lines.append('       expected: %s' % expect_text)
        lines.append('       observed: %s' % detail)

    # M1: the resume threshold of StopSprint (slti v0,v0,0xb at 0x80032190: resume when the counter reaches 11) -> 13
    a = 0x80032190
    check('M1 resume window threshold 11 -> 13', [(a, word(a), (word(a) & 0xFFFF0000) | 0x000D)], [P_RESUME, P_RESUME12],
          lambda b, m: (first(b[0], 'anim_cur', 3, 41) == 51 and first(m[0], 'anim_cur', 3, 41) != 51 and first(m[1], 'anim_cur', 3, 41) == 53 and first(b[1], 'anim_cur', 3, 41) == 53,
                        'k=10 resumes at tick %s -> %s; k=12 resumes at tick %s -> %s' % (first(b[0], 'anim_cur', 3, 41), first(m[0], 'anim_cur', 3, 41), first(b[1], 'anim_cur', 3, 41), first(m[1], 'anim_cur', 3, 41))),
          're-press at counter 11 no longer resumes at tick 51 (the fresh wind-up Sprint comes later), re-press at counter 13 still resumes at 53')
    # M2: the cone row of a run to the east, first word 0x14 -> 0x15 (a value no pad direction takes)
    a = 0x80022CEC + 36
    check('M2 cone row east: 0x14 -> 0x15', [(a, word(a), 0x15)], [P_CONE],
          lambda b, m: (first(b[0], 'anim_cur', 0x2A, 41) is None and first(m[0], 'anim_cur', 0x2A, 41) == 41,
                        'Up+Right while running east: StopSprint first tick %s -> %s' % (first(b[0], 'anim_cur', 0x2A, 41), first(m[0], 'anim_cur', 0x2A, 41))),
          'Up+Right (0x14) leaves the cone: StopSprint at tick 41')
    # M3: the run entry.  FIRST DRAFT (kept as a note): one word, the tail's Triangle test 0x80031F1C -> Circle, expected "Circle starts the run".  Found wrong by reading 0x80031FB0:
    # the PrepareSprint handler tests Triangle (andi 0x10) itself and sends the hero to Idle when it is not held, so with one word Circle gives 0x29 -> Idle -> 0x29 -> ... and never a run.
    # M3a states the reading (written before the run), M3b patches both tests.
    a, b = 0x80031F1C, 0x80031FB0
    check('M3a tail test Triangle -> Circle only (the 0x29 handler still tests Triangle)', [(a, word(a), (word(a) & 0xFFFF0000) | 0x0020)], [P_START_C],
          lambda m_b, m_m: (first(m_b[0], 'anim_cur', 3) is None and first(m_m[0], 'anim_cur', 3) is None and
                            [m_m[0]['rows'][i]['hero']['anim_cur'] for i in range(0, 8)] == [0x29, 0x00] * 4 and
                            [m_b[0]['rows'][i]['hero']['anim_cur'] for i in range(0, 8)] == [0x01] * 8,   # Right is held: the baseline walks (the first draft of this literal said Idle, a slip: the pad holds Right)
                            'Circle + Right, anim_cur ticks 1-8: %s -> %s' % (['%02X' % m_b[0]['rows'][i]['hero']['anim_cur'] for i in range(8)], ['%02X' % m_m[0]['rows'][i]['hero']['anim_cur'] for i in range(8)])),
          'baseline: Circle does nothing, Right walks (Moving 01); patched: 0x29 and Idle alternate every tick, never Sprint')
    check('M3b both Triangle tests (0x80031F1C and 0x80031FB0) -> Circle', [(a, word(a), (word(a) & 0xFFFF0000) | 0x0020), (b, word(b), (word(b) & 0xFFFF0000) | 0x0020)], [P_START, P_START_C],
          lambda m_b, m_m: (first(m_b[0], 'anim_cur', 3) == 26 and first(m_m[0], 'anim_cur', 3) is None and first(m_b[1], 'anim_cur', 3) is None and first(m_m[1], 'anim_cur', 3) == 26,
                            'Triangle: sprint at %s -> %s; Circle: sprint at %s -> %s' % (first(m_b[0], 'anim_cur', 3), first(m_m[0], 'anim_cur', 3), first(m_b[1], 'anim_cur', 3), first(m_m[1], 'anim_cur', 3))),
          'Triangle no longer starts the run, Circle does (sprint at 26)')
    # M4: Hold & 0xE0 in Sprint -> & 0xC0 (Circle no longer dashes)
    a = 0x80032104
    check('M4 sprint dash mask 0xE0 -> 0xC0', [(a, word(a), (word(a) & 0xFFFF0000) | 0x00C0)], [P_DASH],
          lambda b, m: (first(b[0], 'anim_cur', 4) == 40 and first(m[0], 'anim_cur', 4) is None, 'Circle during Sprint: Tackle at tick %s -> %s' % (first(b[0], 'anim_cur', 4), first(m[0], 'anim_cur', 4))),
          'Circle (0x20) no longer starts the Tackle')
    # M5: the wall test of Sprint, ForceAdjusted (+0x13C) -> +0x140 (CollidedWithEntityZ, 1 on the ground): bounce at the first Sprint tick
    a = 0x80032018
    check('M5 sprint wall test +0x13C -> +0x140', [(a, word(a), (word(a) & 0xFFFF0000) | 0x0140)], [P_WALL],
          lambda b, m: (first(b[0], 'anim_cur', 0x3E) == 87 and first(m[0], 'anim_cur', 0x3E) is not None and first(m[0], 'anim_cur', 0x3E) < 40,
                        'bounce 0x3E first tick %s -> %s' % (first(b[0], 'anim_cur', 0x3E), first(m[0], 'anim_cur', 0x3E))),
          'the bounce happens at the first Sprint tick instead of at the wall (tick 87)')
    # control: the first word of the death branch (never executed here) -> 0xFFFFFFFF
    a = 0x800327A4
    base = [R.run_r(P_WALL), R.run_r(P_START)]
    mut = [run_with([(a, word(a), 0xFFFFFFFF)], P_WALL), run_with([(a, word(a), 0xFFFFFFFF)], P_START)]
    same = all(ra['hero'] == rb['hero'] and ra['journal'] == rb['journal'] for x, y in zip(base, mut) for ra, rb in zip(x['rows'], y['rows']))
    ok = ok and same
    lines.append('%-4s C1 control: first word of the death branch (never executed) -> 0xFFFFFFFF' % ('PASS' if same else 'FAIL'))
    lines.append('       observed: traces of two scenarios identical on every tick and column: %s; address executed in the baseline: %s' % (same, a in base[0]['machine'].cov))
    lines.append('')
    lines.append('VERDICT %s' % ('PASS: every mutation caught, control unchanged' if ok else 'FAIL'))
    open(os.path.join(OUT, 'mutations.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
