"""crit_template.py (critic of h4c1, own script on a COPY of the other agent's harness): is a case recycled by the slot pass (0x80038634) clean?
For the earth-1 cast, after every tick: the raw words +0x00 (index), +0x98 (animation row), +0x1C (counter), +0x10 (status) of the 64 cases, and the template.
Prints, for each case that had status 4 at the end of tick t-1 and is NOT active at the end of tick t (freed, not yet reborn), its +0x98 / +0x1C / index."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'src'))
import fw
import pworld as P
import world as W
import c1_pworld as C1

ENT0, STRIDE = W.ENT0, W.STRIDE
TEMPLATE = 0x80134368
log = []


_orig_run_pass = W.run_pass
post_slotpass = []


def run_pass_wrapped(m, addr):
    r = _orig_run_pass(m, addr)
    if addr == 0x80038634:
        rec = {}
        for s in range(64):
            e = ENT0 + STRIDE * s
            rec[s] = dict(status=m.peeks(e + 0x10), idx=m.peeks(e), row=m.peeks(e + 0x98), cnt=m.peeks(e + 0x1C), anim=m.peeks(e + 0x88), dirv=m.peeks(e + 0x8C), hp=m.peeks(e + 0x14), flags=m.peek(e + 0x6C), typ=m.peeks(e + 0x68))
        post_slotpass.append((m.tick_no, rec))
    return r


W.run_pass = run_pass_wrapped


def snapshot(m, t):
    rec = {}
    for s in range(64):
        e = ENT0 + STRIDE * s
        rec[s] = dict(status=m.peeks(e + 0x10), idx=m.peeks(e), row=m.peeks(e + 0x98), cnt=m.peeks(e + 0x1C), type=m.peeks(e + 0x68))
    log.append((t, rec))


def main():
    types = tuple(P.PROJECTILE_TYPES) + (16,)
    spec = dict(item=None, n=75, holds={1: W.CIRCLE}, hero_px=(100, 100), direction=24, cells=None, targets=[], seed=0x12345678,
                types=types, keep=set(P.REAL) | {C1.E3}, target_kind=None, init=[C1.init_tome(0x2B)], after_tick=[snapshot])
    res = fw.run(spec)
    m = res['m']
    print('template +0x98 =', m.peeks(TEMPLATE + 0x98), ' +0x1C =', m.peeks(TEMPLATE + 0x1C), ' +0x48 =', m.peeks(TEMPLATE + 0x48), ' +0x10 =', m.peeks(TEMPLATE + 0x10))
    print('--- state of the cases right after the slot pass, for cases that were status 4 before it')
    prevlog = {t: rec for t, rec in log}
    for t, rec in post_slotpass:
        before = prevlog.get(t - 1)
        if before is None:
            continue
        for s in range(1, 64):
            if before[s]['status'] == 4:
                r = rec[s]
                print('tick %3d case %2d: status 4 (row=%d cnt=%d idx=%d type=%d) -> after slot pass status=%d row=%d cnt=%d idx=%d anim=%d dir=%d hp=%d flags=%X type=%d' % (
                    t, s, before[s]['row'], before[s]['cnt'], before[s]['idx'], before[s]['type'], r['status'], r['row'], r['cnt'], r['idx'], r['anim'], r['dirv'], r['hp'], r['flags'], r['typ']))
    prev = None
    n = 0
    for t, rec in log:
        if prev is not None:
            for s in range(1, 64):
                if prev[s]['status'] == 4 and rec[s]['status'] == 0:
                    n += 1
                    print('tick %3d case %2d freed: before(row=%d cnt=%d idx=%d) -> after(row=%d cnt=%d idx=%d status=%d)' % (
                        t, s, prev[s]['row'], prev[s]['cnt'], prev[s]['idx'], rec[s]['row'], rec[s]['cnt'], rec[s]['idx'], rec[s]['status']))
        prev = rec
    print('freed cases seen:', n)
    # the cases the rocks used, and their index word at the end
    print('idx words of cases 0..15 at the end:', [log[-1][1][s]['idx'] for s in range(16)])
    # rocks whose predecessor in the same case had a non-zero row: list (tick, case, prev row, new row, direction)
    prevrow = {}
    for t, rec in log:
        for s in range(1, 64):
            r = rec[s]
            if r['status'] != 0 and prevrow.get(s, (None,))[0] == 'dead':
                pass
    return res


if __name__ == '__main__':
    main()
