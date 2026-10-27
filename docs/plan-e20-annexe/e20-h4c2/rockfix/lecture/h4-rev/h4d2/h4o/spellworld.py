"""spellworld.py - h4 oracle: the hero with a tome in the d1 world, with the REAL SpawnWarpEntity (0x80039F58) over the global sprite page installed
from data-extracted/data/map_alundra.json (records 0, 16-24, 1, 2 and any extra), the REAL entity passes (events with the native programs A/C/D/E,
animation, physics) and the REAL hit pass 0x80039300.  Stubs (journalled, machine.py): PlaySoundEffect, CreateEffectEntity 0x8003BDD8, CreateAttachedEffectCore
0x8003BE74, the effects pass 0x80038E84.

Nothing here is an expected value: expectations live in the models/checks of the callers."""
import os
import sys
import json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32, M32
import world as W
import oracle as O
import bank

MAPALUNDRA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json"
PAGE = 0x80126EC0            # GLOBAL page struct read by 0x80039B28 when the bank flag is 0: +0xC pointer table, +0x10 count (the map page is 0x80126E70)
PTAB = 0x801EA000            # the pointer table (256 words)
REC_BASE = 0x801BD000        # where the spell records are laid out (after the hero's ~109 KB at 0x801A0000)
STRIDE = 0x294
ENT0 = 0x80127D30

_recs_cache = {}


def sprite_records():
    if not _recs_cache:
        d = json.load(open(MAPALUNDRA, encoding='utf-8-sig'))
        for x in d['SpriteInfo']['SpriteRecords']:
            if x:
                _recs_cache[x['Header']['Sector5Id']] = x
    return _recs_cache


def install_global_page(m, indices):
    """lay out the records of the global sprite indices in RAM and the page table 0x80126E70 (+0xC table, +0x10 count) the real 0x80039B28 reads"""
    recs = sprite_records()
    m.zero(PTAB, 256 * 4)
    addr = REC_BASE
    placed = {}
    for idx in indices:
        info = bank.install(m, addr, addr, recs[idx])
        m.poke(PTAB + 4 * idx, addr)
        placed[idx] = addr
        addr = (info['end'] + 0xFFF) & ~0xFFF
        assert addr < 0x801D0000, hex(addr)
    m.poke(PAGE + 0xC, PTAB)
    m.poke(PAGE + 0x10, 256)
    return placed


def make(item=None, mp=5, mpmax=5, weapon=1, seed=0x12345678, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24), real_hits=True, extra_items=()):
    m, meta = O.setup(seed=seed)
    m.hooks.pop(0x80039F58, None)                       # real SpawnWarpEntity
    if real_hits:
        m.hooks.pop(0x80039300, None)                   # real hit pass
    placed = install_global_page(m, indices)
    m.zero(0x80134368, 0x294)                           # the blank entity template copied over a destroyed slot by the slot pass 0x80038634 (BSS zero)
    STATS = m.peek(0x80176318)
    if item is not None:
        m.call(0x8004E530, item)
        for it in extra_items:
            m.call(0x8004E530, it)
        m.poke16(STATS + 6, mpmax)
        m.poke16(STATS + 4, mp)
        if weapon is not None:
            m.call(0x8004E484, weapon)
        m.poke16(STATS + 0xC, item)
    m.uninit_reads.clear()
    return m, STATS, placed


def ent(i):
    return ENT0 + STRIDE * i


def slots(m):
    out = []
    n = 64
    for i in range(n):
        st = m.peeks(ent(i) + 0x10)
        if st:
            out.append(i)
    return out


def snap_entity(m, i):
    e = ent(i)
    g = m.peeks
    return dict(slot=i, status=g(e + 0x10), type=g(e + 0x68), flags=m.peek(e + 0x6C), hp=g(e + 0x14),
                x=g(e + 0x114), y=g(e + 0x118), z=g(e + 0x11C), fz=g(e + 0xB8), fx=g(e + 0xC4), fy=g(e + 0xC8),
                tanim=g(e + 0x88), canim=g(e + 0x90), tdir=g(e + 0x8C), animend=g(e + 0xAC), ground=g(e + 0x144),
                id=g(e + 4), b274=g(e + 0x274), b278=g(e + 0x278), b27c=g(e + 0x27C), a280=g(e + 0x280), a284=g(e + 0x284),
                a288=g(e + 0x288), a28c=g(e + 0x28C), a290=g(e + 0x290), cc=g(e + 0xCC), d0=g(e + 0xD0), e1cc=g(e + 0x1CC), e1d0=g(e + 0x1D0),
                hitc=g(e + 0x220), touching=m.peek(e + 0x224), curatk=m.peek(e + 0x1C8))


def run_cast(m, STATS, nticks, circle_ticks=(1,), hold_fn=None, on_tick=None):
    pad = W.Pad()
    rows = []
    for t in range(1, nticks + 1):
        hold = hold_fn(t) if hold_fn else (W.CIRCLE if t in circle_ticks else 0)
        m.tick_no = t
        W.tick(m, pad, hold, check_pad=False)
        if on_tick:
            on_tick(m, t)
    return rows
