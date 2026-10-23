"""mutants_f6a.py - E20.f6a T0: one-word mutants of the tile-attribute pass 0x80038064 (the four-corner rule that writes +0x18C), on the real machine (patched RAM, never simulated), each compared with its control on the stated scenario. A mutant is KILLED
when the signature (the status, the position, the slope +0x18C, the ground flag, the effects, the sounds and the draws of every tick of the slot 1) differs from the control's. The scenarios are those of run_f6a.py and of run_balls.py / run_balls_mixed.py.
Usage (from a COPY of the annex, it writes ./out): python -B mutants_f6a.py
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'critic', 'mine'))
sys.path.insert(0, HERE)
import fw
import world as W
import mipsdis
import run_balls as B
import run_f6a as F

OUT = os.path.join(HERE, 'out')
SQ = W.SQUARE


def orig(addr):
    o = mipsdis.off(addr)
    return int.from_bytes(mipsdis._data[o:o + 4], 'little')


def patch(*pairs):
    def f(m):
        for a, w in pairs:
            m.poke(a, w & 0xFFFFFFFF)
    return f


def withop(addr, opcode):
    return (addr, (orig(addr) & 0x03FFFFFF) | (opcode << 26))


def withlow(addr, low):
    return (addr, (orig(addr) & 0xFFFF0000) | (low & 0xFFFF))


def sig(res):
    out = []
    for r in res['rows']:
        d = r['slots'].get(1)
        out.append((r['tick'], r['draws'], tuple(r['effects']), tuple(j for j in r['journal'] if j.startswith('snd')),
                    None if d is None else (d['status'], d['px'], d['py'], d['pz'], d['slope_18C'], d['coll_140'], d['adj_13C'], d['flags6C'])))
    return out


def go(item, n, cells, init=None):
    spec = dict(item=item, n=n, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=cells, targets=[], seed=0x12345678)
    if init:
        spec['init'] = init
    return fw.run(spec)


def water(c0=5, c1=9):
    return B.cells_cols(c0, c1, (0, 0, 0, 0x08))


def main():
    rows = []
    S = {
        'deepwater': lambda init=None: go(16, 40, water(), init),
        'slope1': lambda init=None: go(16, 40, B.cells_cols(5, 9, (0, 0, 0, 0x02)), init),
        'mixed': lambda init=None: go(16, 60, water(7, 9), init),
        'step': lambda init=None: go(16, 60, F.step_water, init),
    }
    for k in ('c6r5', 'c7r5', 'c6r6', 'c7r6'):
        col, row = int(k[1]), int(k[3])
        S['dry_' + k] = (lambda col, row: (lambda init=None: go(16, 40, F.water_except((col, row)), init)))(col, row)
    ctl = {k: f() for k, f in S.items()}
    dry = ['dry_c6r5', 'dry_c7r5', 'dry_c6r6', 'dry_c7r6']
    M = [
        ('SL-1', 'corner posed test bne -> beq (0x80038138): posed and not posed swapped', ['deepwater'], [patch(withop(0x80038138, 4))]),
        ('SL-2', 'minimum -> maximum: slt $v0,$v1,$t1 -> slt $v0,$t1,$v1 (0x80038140)', ['mixed'] + dry, [patch((0x80038140, 0x0123102A))]),
        ('SL-3', 'the corner loop stops after three corners: slti $v0,$t0,4 -> 3 (0x80038164)', dry, [patch(withlow(0x80038164, 3))]),
        ('SL-4', 'a corner not posed no longer sets t1 to 0: move $t1,$zero -> nop (0x80038158)', ['step'], [patch((0x80038158, 0))]),
        ('SL-5', 'the slope is t1 >> 8 instead of >> 9 (0x80038344)', ['deepwater'], [patch((0x80038344, 0x00091203))]),
        ('SL-6', 'the sentinel 0xE00 -> 0x600 (0x80038110)', ['deepwater'], [patch(withlow(0x80038110, 0x0600))]),
        ('SL-7', 'the posed height +1 -> +0 (0x80038128)', ['deepwater'], [patch(withlow(0x80038128, 0))]),
        ('SL-8', 'the gravity gate andi 0x100 -> 0x200 (0x80038104): the slope branch is never taken', ['deepwater'], [patch(withlow(0x80038104, 0x0200))]),
        ('SL-9', 'the mask andi 0xE00 -> 0x600 on the cell (0x8003813C)', ['deepwater'], [patch(withlow(0x8003813C, 0x0600))]),
        ('SL-10', 'the slope-1 case: mask andi 0xE00 -> 0xC00 (0x8003813C)', ['slope1'], [patch(withlow(0x8003813C, 0x0C00))]),
    ]
    lines = []
    for name, desc, scens, init in M:
        killed_by = [s for s in scens if sig(ctl[s]) != sig(S[s](init=init))]
        status = 'KILLED' if killed_by else 'SURVIVED'
        lines.append('%-6s %-90s %-8s %s' % (name, desc, status, ','.join(killed_by)))
        rows.append([name, desc, status, killed_by])
        print(lines[-1])
    os.makedirs(OUT, exist_ok=True)
    json.dump(rows, open(os.path.join(OUT, 'mutants_f6a.json'), 'w'), indent=1)
    open(os.path.join(OUT, 'mutants_f6a.txt'), 'w').write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
