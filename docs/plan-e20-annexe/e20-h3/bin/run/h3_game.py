"""h3_game.py - scenario library for the bomb and the beans in the emulated game (the h4 spell world: REAL MovePlayer, REAL SpawnWarpEntity over the global sprite page, REAL entity
passes with the native programs A/C/D/E, REAL animation / physics / hit pass; sound and effect calls are journalled stubs).

Harness writes (each scenario says which): the pad words, the cell field (optional), extra entities placed with the REAL SpawnWarpEntity (targets, a second bomb, a fake pot),
nothing else."""
import sys
import json
import spellworld as S
import world as W
from machine import s32, M32
import h4_models as M

HERO = W.ent(0)
UP, RIGHT, DOWN, LEFT = W.UP, W.RIGHT, W.DOWN, W.LEFT
CIRCLE, SQUARE, CROSS, TRIANGLE = W.CIRCLE, W.SQUARE, W.CROSS, W.TRIANGLE
DIRS = {'down': 0, 'up': 0x10, 'left': 8, 'right': 0x18}
HOLD = {'down': DOWN, 'up': UP, 'left': LEFT, 'right': RIGHT}
SP = 0x801FFF00
INDICES = (1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25)


def set_cells(m, cells):
    """rewrite the cell field like build_world does: cells(col, row) -> (height byte, slope, walkability word low byte, ground property)"""
    for row in range(60):
        for col in range(52):
            a = W.TILES + 0x604 + row * W.ROW + col * 8
            hb, sl, wk, gp = cells(col, row)
            m.poke16(a, wk | (gp << 8)); m.poke8(a + 2, sl); m.poke8(a + 3, hb)


def make(item, mp=4, mpmax=4, seed=0x12345678, cells=None, hero_px=(100, 100), hero_dir=24, indices=INDICES, hp=10, weapon=1, extra_items=()):
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=mpmax, seed=seed, indices=indices, weapon=weapon, extra_items=extra_items)
    if cells:
        set_cells(m, cells)
    P = HERO
    if hero_px != (100, 100):
        m.poke(P + 0x114, hero_px[0] << 16); m.poke(P + 0x118, hero_px[1] << 16)
    m.poke(P + 0x8C, hero_dir)
    m.poke(P + 0x94, hero_dir)
    m.poke(P + 0x98, 0)
    return m, STATS


def spawn_global(m, sprite, x_px, y_px, z=0, direction=0):
    m.poke(SP + 0x10, y_px << 16)
    m.poke(SP + 0x14, z)
    m.poke(SP + 0x18, direction)
    return m.call(0x80039F58, HERO, 0, sprite, x_px << 16) & M32


def add_target(m, x_px, y_px, hp=1000, resp_sheet=None):
    """a stationary ClassA target: global sprite 25 with a balance sheet (real 0x80044550 of the given sprite-table index, default: its own) and a large HP"""
    p = spawn_global(m, 25, x_px, y_px)
    assert p
    m.poke(p + 0x6C, m.peek(p + 0x6C) | 1)
    m.poke(p + 0x14, hp)
    m.poke(p + 0x18, hp)
    if resp_sheet is not None:
        m.poke(p + 0x1C4, m.call(0x80044550, resp_sheet) & M32)
    return p


def slot_of(p):
    return (p - HERO) // M.STRIDE


class Run:
    """a run with a pad script and per-tick observers; keeps a compact log of CHANGES"""

    def __init__(self, m, holds):
        self.m = m
        self.holds = holds
        self.pad = W.Pad()
        self.t = 0
        self.log = []
        self.js = []

    def step(self, n=1, pre=None, post=None):
        for _ in range(n):
            self.t += 1
            h = self.holds(self.t) if callable(self.holds) else self.holds.get(self.t, 0)
            self.m.tick_no = self.t
            j0 = len(self.m.journal)
            if pre:
                pre(self.m, self.t)
            W.tick(self.m, self.pad, h, check_pad=False)
            self.js.extend((self.t,) + tuple(j[1:]) for j in self.m.journal[j0:] if not j[1].startswith('pass_'))
            if post:
                post(self.m, self.t)


def e_snap(m, p):
    return dict(st=m.peeks(p + 0x10), ty=m.peeks(p + 0x68), fl=m.peek(p + 0x6C), t24=m.peeks(p + 0x24), c28=m.peek(p + 0x28), ta=m.peeks(p + 0x88), ca=m.peeks(p + 0x90), dr=m.peeks(p + 0x8C),
                aend=m.peeks(p + 0xAC), gr=m.peeks(p + 0x144), adj=m.peeks(p + 0x13C), hitc=m.peeks(p + 0x220), touch=m.peek(p + 0x224), hp=m.peeks(p + 0x14),
                x=m.peeks(p + 0x114), y=m.peeks(p + 0x118), z=m.peeks(p + 0x11C), fx=m.peeks(p + 0xC4), fy=m.peeks(p + 0xC8), fz=m.peeks(p + 0xB8), box=m.peek(p + 0x1D4), sup=m.peek(p + 0x12C),
                b274=m.peeks(p + 0x274), b278=m.peeks(p + 0x278), inv=m.peeks(p + 0x1D0), blink=m.peeks(p + 0x1CC))


def px(v):
    return v / 65536.0
