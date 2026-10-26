"""scen_zone335.py - E20.h3a T0 (c) (w2): the explosion hit zone against the target of the TEST, the record 335 (the Beannoid of the map 14: page record 79 of data/map_14.json,
the sprite type 79 + 0x100 = 335 that SpawnWarpEntity builds for a map page), instead of the global sprite 253 of critic/run/scen_zone.py.  Same method: a bomb is born with the real
SpawnWarpEntity at (300, 300), its fuse shortened to 2 (status 3 at tick 4), the target is born at (300 + dx, 300 + dy, TZ) as a map-page entity; the hero stays at (100, 100) (183 px or more
from the bomb).  Columns: first tick at which the target is touched (D1 ran: +0x88 = 5, or its life went down), its life and status at the end, the number of ticks its life changed.
usage: python -B runpy.py scen_zone335.py [scan|grid|one DX DY]   (from a copy of critic/run with the a362 runpy.py)"""
import sys
import json
import spellworld as S
import world as W
import bank
from machine import s32, M32
from scen_lib import *
from scen_zone import spawn, BX, BY

MAP14 = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_14.json"
MAPPAGE = 0x80126E70
PTAB2 = 0x801EA800
REC79 = 0x801C8000
IDX = 79
H0 = W.ent(0)


def install_map_record(m):
    d = json.load(open(MAP14, encoding='utf-8-sig'))
    rec = d['SpriteInfo']['SpriteRecords'][IDX]
    assert rec['Header']['Sector5Id'] == IDX
    m.zero(PTAB2, 256 * 4)
    info = bank.install(m, REC79, REC79, rec)
    assert info['end'] < 0x801D0000, hex(info['end'])
    m.poke(PTAB2 + 4 * IDX, REC79)
    m.poke(MAPPAGE + 0xC, PTAB2)
    m.poke(MAPPAGE + 0x10, 256)
    return rec['Header']


class Rigw:
    def __init__(self):
        self.m, self.STATS = mk(None, indices=(1, 2))
        self.hdr = install_map_record(self.m)
        self.snap = (bytes(self.m.ram), bytes(self.m.init))

    def restore(self):
        self.m.ram[:] = self.snap[0]; self.m.init[:] = self.snap[1]; self.m.journal.clear()
        self.m.hi = self.m.lo = 0


def trial(rw, dx, dy, tz=0, nticks=64, bz=0, fuse=2, verbose=False):
    rw.restore()
    m = rw.m
    pad = W.Pad()
    b = spawn(m, H0, 0, 2, BX, BY, bz)
    t = spawn(m, H0, 1, IDX, BX + dx, BY + dy, tz)
    assert b and t
    assert m.peeks(t + 0x68) == 335, m.peeks(t + 0x68)
    m.poke(b + 0x274, fuse)
    hp0 = m.peeks(t + 0x14)
    first = None; changes = 0; last = hp0; when = None
    if verbose:
        print('target slot %#x type %d hp %d box size at birth' % (t, m.peeks(t + 0x68), hp0))
    for k in range(1, nticks + 1):
        m.tick_no = k
        W.tick(m, pad, 0, check_pad=False)
        hp = m.peeks(t + 0x14)
        if hp != last:
            changes += 1; last = hp
        if first is None and (m.peeks(t + 0x88) == 5 or hp != hp0):
            first = k
        if m.peeks(b + 0x10) == 3 and when is None:
            when = k
        if verbose:
            print('t%3d bomb st %d anim %d | target st %d hp %d tanim %d hit %d touching %#x x %.2f y %.2f' % (k, m.peeks(b + 0x10), m.peeks(b + 0x88), m.peeks(t + 0x10), hp, m.peeks(t + 0x88), m.peeks(t + 0x220), m.peek(t + 0x224), m.peeks(t + 0x114) / 65536, m.peeks(t + 0x118) / 65536))
    return dict(first=first, hp0=hp0, hp_end=m.peeks(t + 0x14), status_end=m.peeks(t + 0x10), changes=changes, bomb3=when)


if __name__ == '__main__':
    rw = Rigw()
    h = rw.hdr
    print('target: map 14 page record %d, size (%d, %d, %d) offset (%d, %d, %d), MoreFlags %#x, programs L/T/H/D/I %d/%d/%d/%d/%d' % (
        IDX, h['SizeX'], h['SizeY'], h['SizeZ'], h['OffsetX'], h['OffsetY'], h['OffsetZ'], h['MoreFlags'], h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract']))
    mode = sys.argv[1] if len(sys.argv) > 1 else 'scan'
    if mode == 'one':
        print(trial(rw, float(sys.argv[2]), float(sys.argv[3]), verbose=True))
        sys.exit(0)
    if mode == 'grid':
        print('grid of offsets (dx, dy) -> first tick, life at the end, changes of life:')
        for dy in (-23, -22, 0, 22, 23):
            for dx in (-34, -33, 0, 33, 34):
                r = trial(rw, dx, dy)
                print('  (%4d, %4d) -> first %s, life %d -> %d, changes %d' % (dx, dy, r['first'], r['hp0'], r['hp_end'], r['changes']))
        sys.exit(0)
    print('baseline centre:', trial(rw, 0, 0))
    print('X scan (dy = 0):')
    for dx in list(range(-40, -28)) + list(range(28, 42)):
        print('  dx %4d -> %s' % (dx, trial(rw, dx, 0)))
    print('Y scan (dx = 0):')
    for dy in list(range(-30, -16)) + list(range(16, 30)):
        print('  dy %4d -> %s' % (dy, trial(rw, 0, dy)))
