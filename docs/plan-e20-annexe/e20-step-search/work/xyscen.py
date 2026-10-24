"""xyscen.py - scenario helpers on top of xymodel.py (the model of ComputeXYPosition 0x80037730, replayed 32 000 times against the real code).
All distances are in 16.16 raw units unless the name says px.  Subject box = the hero's (offset -10,-7,0 ; size 21x15x32), crate = 24x16xN.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mips
import xymodel as XM
from xymodel import s32, M32

U = 65536
TILES = 0x80310000
HERO = XM.HERO
STRIDE = 0x294


def ent_addr(i):
    return HERO + STRIDE * i


def new_world():
    w = XM.World()
    w.tile_base = TILES
    for x in range(0, 1248):
        w.xtab[x] = min(x // 24, 51)
    w.stairs = [s32(mips.Mem().rw(0x800236D4 + 4 * i)) for i in range(24)]
    for i in range(8):
        w.ents[ent_addr(i)] = {}
    return w


def wall_columns(w, first_col, last_col=51, rows=range(0, 60), attr=0x40, height_byte=0):
    for c in range(first_col, last_col + 1):
        for r in rows:
            w.cell[(c, r)] = (attr, 0, height_byte)


def set_box(E, off, size, depth_px):
    ox, oy, oz = off
    sx, sy = size
    E[0x1e4], E[0x1e8], E[0x1ec] = ox * U, oy * U, oz * U
    E[0x1f0], E[0x1f4] = sx * U - 1, sy * U - 1
    E[0x1f8] = depth_px * U - 1 if depth_px else 0


def put(w, addr, flags, pos, off=(-10, -7, 0), size=(21, 15), depth=32, d=24, fx=0, fy=0, forcez=0):
    E = w.ents[addr]
    set_box(E, off, size, depth)
    E[0x6c] = flags
    E[0x8c] = d
    E[0xe4], E[0xe8] = fx, fy
    E[0x114], E[0x118], E[0x11c] = pos
    E[0xb8] = forcez
    XM.set_mod(E)
    return E


def tick(w, addr, fx, fy, trace=None):
    """one physics tick of the subject: forces written, ForceAdjusted cleared (the pre-pass 0x80038364 does it), ComputeXY run."""
    E = w.ents[addr]
    E[0xe4], E[0xe8] = fx, fy
    E[0x13c] = 0
    x0, y0, z0 = E[0x114], E[0x118], E[0x11c]
    ret = XM.compute_xy(w, addr, trace)
    return dict(dx=E[0x114] - x0, dy=E[0x118] - y0, dz=E[0x11c] - z0, ret=ret, fa=E[0x13c], x=E[0x114], y=E[0x118], z=E[0x11c])
