"""ladder_oracle.py - scratch copy, NOT part of the repository.

Synthetic ladder on the D1 oracle machine (real ALUN_CD.EXE code for MovePlayer and the tick passes):
  - every cell height byte 0, gp 0, except
  - rows 0..TOP_ROW_LAST (y < 16*(TOP_ROW_LAST+1)) : height byte TOP_H (the upper ground), gp 0
  - ladder foot: rows LADDER_ROWS at columns LADDER_COLS, height 0, gp 0x0C (slope 6 = climbing wall)
The hero starts south of the ladder, facing north, Up held every tick.  Prints one line per tick.

usage: python -B ladder_oracle.py [ticks] [top_h_units] [start_y_px] [hold_after_exit]
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine
import world as W
import oracle as O

TICKS = int(sys.argv[1]) if len(sys.argv) > 1 else 90
TOP_H = int(sys.argv[2]) if len(sys.argv) > 2 else 2          # 2 units = 32 px
START_Y = int(sys.argv[3]) if len(sys.argv) > 3 else 190
HOLD_AFTER = sys.argv[4] if len(sys.argv) > 4 else 'up'       # 'up' or 'none'
TOP_ROW_LAST = 9
LADDER_ROWS = (10, 11)
LADDER_COLS = (10,)
START_X = 251                                                  # box x1 = 241, x2 = 262: inside column 10 (240..263)


def cells(col, row):
    if row <= TOP_ROW_LAST:
        return (TOP_H, 0, 0, 0)
    if row in LADDER_ROWS and col in LADDER_COLS:
        return (0, 0, 0, 0x0C)
    return (0, 0, 0, 0)


def main():
    m = Machine()
    W.build_world(m, gravity=128, visc=4096, cells=cells)
    W.init_bss(m)
    W.load_balance(m, 0)
    W.init_stats(m)
    W.place_hero(m, START_X, START_Y, direction=0x10)
    W.init_pad2(m)
    m.poke(0x80098708, 0x12345678)
    m.uninit_reads.clear()
    P = W.ent(0)
    pad = W.Pad()
    F = W.F
    print('tick hold animT animC dir forceY forceZ posY(px) posZ(px) posZ(raw) onGround coll terrain floor slope18c(+0x18C) adj13C')
    exited = None
    for t in range(1, TICKS + 1):
        hold = W.UP
        if exited is not None and HOLD_AFTER == 'none':
            hold = 0
        m.tick_no = t
        W.tick(m, pad, hold)
        g = lambda o: m.peeks(P + o)
        animT = g(F['anim_target']); animC = g(F['anim_cur'])
        posz = g(F['posz'])
        line = '%3d 0x%04X %3d %3d %3d %8d %8d %9.4f %9.4f %10d %d %d %9.4f %9.4f %d %d' % (
            t, hold, animT, animC, g(F['dir_target']), g(F['forcey']), g(F['forcez']), g(F['posy']) / 65536.0, posz / 65536.0, posz,
            g(F['ground']), g(F['coll']), g(F['terrain']) / 65536.0, g(F['floor']) / 65536.0, m.peeks(P + 0x18C), m.peeks(P + 0x13C))
        print(line)
        if exited is None and animT == 0 and t > 3 and g(F['forcez']) != 0:
            exited = t
    print('uninitialised reads during ticks:', len(m.uninit_reads))


if __name__ == '__main__':
    main()
