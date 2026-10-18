"""check_startselect.py - E20.h1 discovery: (1) replays the Start+Select / Start-menu vectors (vec_ss/) from an EMPTY memory on the real code (the same replay as replay_h1.py);
(2) compares them with MY model of the two blocks read in lst/pad_menu_8002B9F0.txt (the Start+Select counter 0x8002BB64-0x8002BC4C and the Start-menu request 0x8002BC58-0x8002BD04):
only the bytes of the blocks are compared (the counter, the hero's life and +0x1CC, the stats' life, the inventory, 0x800A8284, the menu request, the decrement of 0x800DC4B4).
usage: python -B check_startselect.py"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import h1rig                  # noqa: E402
h1rig.STUBS.update({0x8002E38C: (1, 0), 0x8002E058: (0, 0), 0x8004B1D4: (0, 0), 0x80055570: (0, 0)})
import replay_h1 as R         # noqa: E402
import model_h1 as M          # noqa: E402

CTRL = 0x800DC4B8
GUARD2 = 0x800DC4B4
BLOCK = 0x800C4980
HOLD = 0x80126F28
PRESSED = 0x80126F2A
COUNTER_SS = 0x800986F0
MENU_REQ = 0x800DC4C4
HUD0 = 0x800A8284


def model(c, s3):
    hero = M.HERO
    # ---- Start + Select (0x8002BB64-0x8002BC4C)
    reset = (c.rw(CTRL) != 0 or c.rw(hero + 0x20) != 0 or c.rw(M.IN_USE) != 0 or c.rh(HOLD) != 0x900 or c.rw(GUARD2) != 0 or c.rw(BLOCK) != 0)
    if reset:
        c.ww(COUNTER_SS, 0)
    else:
        s0 = c.rw(COUNTER_SS)
        if s0 != 0x78:
            c.ww(COUNTER_SS, s0 + 1)
        elif c.rw(hero + 0x14) != 0:
            if M.s32(c.rw(M.INV_PTR)) is not None and c.rh((c.rw(M.INV_PTR) + 4 * 0x27 + 2) & M.M) != 0:       # GetNumberOfItem(0x27) != 0 (0x8004E428), then UseItem(0x27) (0x8004E5C4)
                M.use_item(c, 0x27)
            c.ww(COUNTER_SS, 0)
            c.ww(hero + 0x14, 0)
            c.ww(hero + 0x1CC, s0)
            stats = c.rw(M.STATS_PTR)                                   # SetPlayerHp(0) (0x8004DD30)
            hpmax = M.s16(c.rh(stats + 2))
            if hpmax < 0:
                c.wh(stats + 0, hpmax)
            else:
                c.wh(stats + 0, 0)
            c.ww(HUD0, 0)
    c.stub(0x8002E058, [])
    # ---- the Start menu request (0x8002BC58-0x8002BD04)
    g = c.rw(GUARD2)
    if g != 0:
        c.ww(GUARD2, g - 1)
    if not (c.rw(CTRL) != 0 or c.rw(hero + 0x20) != 0 or c.rw(M.IN_USE) != 0 or (c.rh(PRESSED) & 0x803) == 0 or c.rw(GUARD2) != 0 or (c.rh(HOLD) & 0x100) != 0 or c.rw(BLOCK) != 0):
        if c.stub(0x80055570, []) == 0:
            c.ww(MENU_REQ, 1)


WATCHED = {COUNTER_SS, M.HERO + 0x14, M.HERO + 0x1CC, HUD0, MENU_REQ, GUARD2}


def watched(addr):
    base = addr & ~3
    return base in WATCHED or M.STATS_PTR == base or (0x801E1000 <= addr < 0x801E1008) or (0x801E2000 <= addr < 0x801E2400)


def main():
    d = json.load(open(os.path.join(HERE, 'vec_ss', 'vec_startselect_8002BAEC.json')))
    bad = 0
    for v in d['vectors']:
        ok, msg, pcs = R.replay_one(v)
        if not ok:
            bad += 1
            print('REPLAY FAIL', v['label'], msg)
    print('%d vectors replayed from an empty memory on the real code: %s' % (len(d['vectors']), 'all ok' if bad == 0 else '%d FAIL' % bad))
    mism = 0
    for v in d['vectors']:
        stubs = {int(a, 16): list(r) for a, r in v['stubs'].items()}
        c = M.Ctx(v['reads'], stubs)
        # the model only reads what it needs; the callees of the head and the tail of the function are stubbed in the vector: consume their entries so only 0x8002E058 and 0x80055570 are checked
        for k in (0x8002E38C, 0x8004B1D4):
            c.rets[k] = []
        try:
            model(c, v['args'][0])
        except M.Unlisted as e:
            print('MODEL UNLISTED', v['label'], e)
            mism += 1
            continue
        want = {}
        for a, n, x in v['writes']:
            for i in range(n):
                if watched(a + i):
                    want[a + i] = (x >> (8 * i)) & 0xFF
        got = {a: b for a, b in c.out.items() if watched(a)}
        if want != got:
            mism += 1
            print('MODEL MISMATCH', v['label'], sorted(set(want.items()) ^ set(got.items()))[:3])
        called = [x[0] for x in v['calls']]
        mc = [x[0] for x in c.calls]
        if [x for x in called if x in ('0x8002e058', '0x80055570')] != mc:
            mism += 1
            print('CALLS MISMATCH', v['label'], called, mc)
    print('model of the two blocks vs the real code: %s' % ('0 mismatches on %d vectors' % len(d['vectors']) if mism == 0 else '%d mismatches' % mism))
    return 1 if (bad or mism) else 0


if __name__ == '__main__':
    sys.exit(main())
