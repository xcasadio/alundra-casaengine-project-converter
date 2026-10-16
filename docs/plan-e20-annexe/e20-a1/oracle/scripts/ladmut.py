"""ladmut.py - scratch what-if on the real code (layout A: cliff hb 10, ladder strip hb 2, Up held from tick 1).
M0: no change (baseline A-up-held).
M1: at the MONT exit store (pc 0x80031E80, TargetAnimationId = 0), the harness also writes ForceZ (+0xB8) = 0 - what the DLL's exit adds ("ForceZ = 0").
M2: same as M1 but ForceZ = 0 AND the Up pad kept (it is kept in every run here); plus the gravity bit is already set by 0x80031C38 in the binary, so M1 == "DLL exit".
M3: the exit store is left alone, but ForceZ (+0xB8) = 0x10000 is replaced by 0 on the LAST climbing tick (pc 0x8003229C store), to see that a stalled last step changes nothing.
Only M1 matters for the answer; M3 is a sanity what-if.
usage: python -B ladmut.py
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rworld as R
import ladtop as L

HERO = R.HERO


def hook_after(m, pc_target, fn):
    orig = m.exec1

    def ex(pc, w):
        br = orig(pc, w)
        if pc == pc_target:
            fn(m)
        return br
    m.exec1 = ex


def run_variant(name, desc, mut):
    lay = L.ladder_layout()
    ev = {}
    if mut:
        ev[1] = lambda m, slots: mut(m)
    spec = dict(name=name, desc=desc, n=230, pad=[(1, R.UP)], layout=lay, hero_px=(100, 392), direction=16, events=ev)
    res = R.run_r(spec)
    lines = ['# %s: %s' % (name, desc)]
    for r in [res['pre']] + res['rows']:
        if r['tick'] >= 165 or r['tick'] == 0:
            lines.append(L.line(r['tick'], r['hold'], r['hero'], R.fmt_items(r.get('journal', []))))
    txt = '\n'.join(lines) + '\n'
    with open(os.path.join(L.OUT, name + '.txt'), 'w', encoding='utf-8') as f:
        f.write(txt)
    return res, txt


def m1(m):
    def poke(mm):
        mm.poke(HERO + 0xB8, 0)
    hook_after(m, 0x80031E80, poke)


def main():
    res, txt = run_variant('M1-exit-forcez0', 'Up held; at the MONT exit store 0x80031E80 the harness also writes ForceZ = 0 (the DLL exit)', m1)
    print('\n'.join(txt.splitlines()[:40]))


if __name__ == '__main__':
    main()
