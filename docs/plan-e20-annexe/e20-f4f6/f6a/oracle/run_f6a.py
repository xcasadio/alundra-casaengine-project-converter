"""run_f6a.py - E20.f6a T0: the NEW keys of the tile attributes of a marked entity with gravity (the four-corner rule of 0x80038064 on the fire and ice balls), played by the real code of the binary on the machine of the annex.
Run from a COPY of the annex (it imports the frozen harness of ../../bin and writes ./out/json): python -B run_f6a.py
Scenarios (ball: item 16 = fire rod, item 14 = ice rod, uncharged, east, flat field, Square at tick 1 -> S = 12; the cell tuple is (height byte, slope byte, walkability, ground property)):
  fire11_dry_corner_c6r5 / _c7r5 / _c6r6 / _c7r6 : deep water (0x08) on columns 5 to 9 but ONE dry cell under one of the four corners of the body (the ball lands at x = 166, its corners are in the columns 6 and 7 and the rows 5 and 6): the minimum over the
      four corners is 0, whichever corner is dry (a rule that ignores one corner is killed by exactly one of the four);
  fire11_step16_water : a step of 16 px (height byte 1) from column 6 on, water (0x08) everywhere (columns 5 to 9): the ball lands ON the step at x = 148, the two corners over the column 5 are not posed -> +0x18C stays 0;
  ice14_step16_water  : the same with the ice rod.
No scenario draws before the destruction or the explosion read: the tests compare the draws of the key (0) with the steps of the DLL minus one (one step per tick), so no _pf form is needed.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
BIN = os.path.join(HERE, '..', '..', 'bin')
sys.path.insert(0, BIN)
import fw
import run_balls as B
import run_arrows as A

OUT = os.path.join(HERE, 'out')
os.makedirs(os.path.join(OUT, 'json'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'traces'), exist_ok=True)
A.OUT = OUT          # run_arrows.save writes under run_arrows.OUT: redirect it to f6a/oracle/out (the frozen bin/out is never touched)
B.OUT = OUT


def water_except(*dry):
    def f(col, row):
        if col < 5 or col > 9:
            return (0, 0, 0, 0)
        return (0, 0, 0, 0) if (col, row) in dry else (0, 0, 0, 8)
    return f


def step_water(col, row):
    if col < 5 or col > 9:
        return (0, 0, 0, 0)
    return (1, 0, 0, 8) if col >= 6 else (0, 0, 0, 8)


SCEN = [
    ('fire11_dry_corner_c6r5', 16, 40, water_except((6, 5))),
    ('fire11_dry_corner_c7r5', 16, 40, water_except((7, 5))),
    ('fire11_dry_corner_c6r6', 16, 40, water_except((6, 6))),
    ('fire11_dry_corner_c7r6', 16, 40, water_except((7, 6))),
    ('fire11_step16_water', 16, 60, step_water),
    ('ice14_step16_water', 14, 60, step_water),
]


def main():
    for name, item, n, cells in SCEN:
        B.ball(name, item, n, cells=cells, title=name)
    json.dump([s[0] for s in SCEN], open(os.path.join(OUT, 'scenarios.json'), 'w'))


if __name__ == '__main__':
    main()
