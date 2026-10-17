"""my392.py - critic of disc-e20g3: an INDEPENDENT harness for map 392 on the REAL code of ALUN_CD.EXE (France), using only the oracle machine copied
from the repository (docs/plan-e20-annexe/e20-g1a/work -> ./g1a, identical to the discovery's copy by diff -rq).  It does NOT import the discovery's lib392.

World: the cells are read here from data-extracted/data/map_392.json (Map.MapTiles: Height, Slope, Walkability, GroundProperty), checked against the exported
tilemap (k0lib.cells_from_export) over the whole map.  ALL 12 crate records are spawned by the REAL map-record spawner 0x8003A1B8 in FILE order (as the
game does at map load), so the slot order of the collidable list is the record order.  Hero: REAL InitializeEntity (world.place_hero), REAL MovePlayer.
Harness writes after creation: the hero's +0x114/+0x118 (start position, raw 16.16), nothing else.  Read-only on every input."""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'g1a', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, os.path.join(ORC, 'oracle'))
import gl                      # noqa: E402
import k0lib as K              # noqa: E402

OUT = os.path.join(HERE, 'out')
gl.OUT = OUT
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
_D = json.load(open(DATA + '/map_392.json', encoding='utf-8-sig'))
INFO = _D['Info']
CONST = (INFO['Gravity'], INFO['ZViscosity'], INFO['SlideEffectId'], INFO['BalanceLevel'], INFO['C'], INFO['D'])
TILES = {(c['TileX'], c['TileY']): c for c in _D['Map']['MapTiles']}
RECS = [e for e in _D['SpriteInfo']['Entities']['Entities'] if e is not None]
UP, RIGHT, DOWN, LEFT = gl.UP, gl.RIGHT, gl.DOWN, gl.LEFT
CROSS, SQUARE = gl.CROSS, gl.SQUARE
U = 65536


def cell(col, row):
    c = TILES.get((col, row))
    if c is None:
        return None
    return dict(h=c['Height'], slope=c['Slope'], wk=c['Walkability'], gp=c['GroundProperty'])


def check_cells():
    f, dims = K.cells_from_export(392)
    diff = 0
    for (col, row), c in TILES.items():
        a = cell(col, row)
        b = f(col, row)
        if a != b:
            diff += 1
    return len(TILES), diff, dims


def world(hero_px, direction, yraw=None, xraw=None, recs=tuple(range(12))):
    """returns (m, slot_of_rec)"""
    m = gl.world(cells=cell, hero_px=hero_px, direction=direction)
    pos = [(RECS[i]['XPos'], RECS[i]['YPos'], RECS[i]['Height']) for i in recs]
    slots, _ = K.spawn_record_entities(m, 'map_392', 36, pos)
    h = K.ent(0)
    if xraw is not None:
        m.poke(h + 0x114, xraw & 0xFFFFFFFF)
    if yraw is not None:
        m.poke(h + 0x118, yraw & 0xFFFFFFFF)
    return m, dict(zip(recs, slots))


def run(m, seq, slots):
    pads, n = gl.pad_script(seq)
    return gl.run(m, n, pads, crates=list(slots.values()))
