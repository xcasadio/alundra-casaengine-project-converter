"""lib392.py - disc-e20g3: the REAL geometry of map 392 on the REAL code of ALUN_CD.EXE (France) in the E20 oracle machine (copied under ./g1a/orc, never edited).

Reads (read-only): data-extracted/data/map_392.json (cells = Map.MapTiles, entity records = SpriteInfo.Entities, sprite record 36 = the crate).
Writes only inside this folder.  Nothing here knows an expected value.

World: 52 x 60 cells of 24 x 16 px, the cell bytes written exactly as world.build_world does (+0 walkability | ground property << 8, +2 slope, +3 height byte); map constants
Gravity 128, ZViscosity 4096, SlideEffectId 0, BalanceLevel 2, C 7, D 0 are the constants build_world writes and ARE the ones of map 392 (checked below by assert).
Hero: REAL InitializeEntity (world.place_hero), REAL MovePlayer from the events pass.  Crates: the REAL map-record spawner 0x8003A1B8 over the 12 REAL records (or a subset);
C23 0x8006B848 and E2 0x8007ED30 are REAL, every other sprite handler is an inert journalling stub (k0lib.make_world).
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'g1a', 'orc')
sys.path.insert(0, ORC)
sys.path.insert(0, os.path.join(ORC, 'oracle'))
import gl                      # noqa: E402
import k0lib as K              # noqa: E402
import world as W              # noqa: E402
import ana                     # noqa: E402
from machine import M32, s32   # noqa: E402

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
gl.OUT = OUT

DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
_D = json.load(open(DATA + '/map_392.json', encoding='utf-8-sig'))
INFO = _D['Info']
assert (INFO['Gravity'], INFO['ZViscosity'], INFO['SlideEffectId'], INFO['BalanceLevel'], INFO['C'], INFO['D']) == (128, 4096, 0, 2, 7, 0)
CELLS = {(c['TileX'], c['TileY']): c for c in _D['Map']['MapTiles']}
RECORDS = [e for e in _D['SpriteInfo']['Entities']['Entities'] if e is not None]     # 12 records, record order = file order
assert len(RECORDS) == 12 and all(e['SpriteTableIndex'] == 36 for e in RECORDS)
UP, RIGHT, DOWN, LEFT = gl.UP, gl.RIGHT, gl.DOWN, gl.LEFT
CROSS, SQUARE, CIRCLE, TRI = gl.CROSS, gl.SQUARE, gl.CIRCLE, gl.TRI


def cells_spec(col, row):
    c = CELLS[(col, row)]
    return dict(h=c['Height'], slope=c['Slope'], wk=c['Walkability'], gp=c['GroundProperty'])


def rec_px(i):
    """position the spawner gives (0x8003A2B0-0x8003A2E4, c1world.target_px): X = 12 * XPos + 12, Y = 8 * YPos + 8"""
    e = RECORDS[i]
    return 12 * e['XPos'] + 12, 8 * e['YPos'] + 8, e['Height']


def world392(hero_px=(560, 296), direction=24, rec_ids=(4, 5, 6, 7, 8, 10, 11), seed=0x12345678, keep=None, cells=cells_spec):
    """returns (m, slots) where slots[rec_id] = the slot index of the crate the real spawner made from record rec_id.  rec_ids in record order."""
    m = gl.world(cells=cells, hero_px=hero_px, direction=direction, seed=seed, keep=keep)
    pos = [(RECORDS[i]['XPos'], RECORDS[i]['YPos'], RECORDS[i]['Height']) for i in rec_ids]
    slots, info = K.spawn_record_entities(m, 'map_392', 36, pos)
    return m, dict(zip(rec_ids, slots))


def pad_seq(seq):
    """seq = list of (n_ticks, hold) -> dict tick -> hold"""
    return gl.pad_script(seq)


def px(v):
    return v / 65536.0
