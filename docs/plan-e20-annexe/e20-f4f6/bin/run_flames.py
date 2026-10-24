"""run_flames.py - the three map-309 records of GLOBAL sprite 12 (charged fire ball), placed by the REAL record loader 0x8003A1B8 on the real cells of map 309.
A program (bytecode index 135 -> table A[7] -> offset 76: [0x1A 2][0x63 0x80 0xF0 0x00][0xFF]) is emulated by a stub (harness, documented): SetAnim 2 ; clear flags 0x00F0 on self.
Usage: python -B run_flames.py
"""
import sys
import os
import json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fw
import world as W
import c1world as C
import pworld as P
from machine import M32
from run_arrows import save, OUT

D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
m309 = json.load(open(D + '/map_309.json', encoding='utf-8-sig'))
TILES = m309['Map']['MapTiles']
WID = m309['Map']['Width']
RECS = [e for e in m309['SpriteInfo']['Entities']['Entities'] if e and (e['SpriteDirection'] & 0x80) == 0 and e['SpriteTableIndex'] == 12 and e['IsEnabled']]


def cells(col, row):
    t = TILES[row * WID + col]
    return (t['Height'], t['Slope'], t['Walkability'] & 0xFF, t['GroundProperty'])


def make(emulate_a=True, n=80, seed=0x12345678):
    spec = dict(item=None, n=n, holds={}, after=0, hero_px=(960, 730), direction=24, cells=cells, targets=(), seed=seed, types=(12,))
    state = {}

    def init(m):
        # entity records of the map block: 0x80126E70 +8 -> records, +0x14 count (page 0 = global block 0x80126EC0 is installed by pworld for types (12,))
        recs = b''.join(C.entity_record_bytes(e, e['XPos'], e['YPos'], e['Height']) for e in RECS)
        m.poke_bytes(C.ENT_REC, recs)
        m.poke(C.MAPBLK + 8, C.ENT_REC)
        m.poke(C.MAPBLK + 0xC, C.SPR_PTRS)
        m.poke(C.MAPBLK + 0x10, 0)
        m.poke(C.MAPBLK + 0x14, len(RECS))
        slots = []
        for i in range(len(RECS)):
            p = m.call(0x8003A1B8, 0, i, 0, limit=5_000_000) & M32
            assert p, 'record %d refused' % i
            slots.append((p - W.ENT0) // W.STRIDE)
        state['slots'] = slots
        # restore A-letter bytecode emulation: stub of 0x8004205C for these slots
        base = P.install_inert_except  # (already installed by setup; we replace the bytecode-runner stub)

        def bytecode(m2):
            a0 = m2.r[4]
            slot = (a0 - W.ENT0) // W.STRIDE
            letter = m2.peek(a0 + 0x228)
            m2.journal.append((m2.tick_no, 'handler', [slot, letter, 0x8004205C]))
            if emulate_a and letter == 0 and slot in state['slots']:
                m2.poke(a0 + 0x88, 2)                                # 0x1A 0x02: TargetAnimationId = 2
                m2.poke(a0 + 0x6C, m2.peek(a0 + 0x6C) & ~0x00F0 & M32)   # 0x63 0x80 0xF0 0x00: self, clear low-16 flags 0x00F0
            m2.r[2] = 0
        m.hooks[0x8004205C] = bytecode
        state['rec_words'] = [[m.peek(W.ENT0 + W.STRIDE * s + 0x4C + 4 * k) for k in range(6)] for s in slots]
        state['codes70'] = [[m.peek(W.ENT0 + W.STRIDE * s + 0x70 + 4 * k) for k in range(6)] for s in slots]
        state['status0'] = [m.peek(W.ENT0 + W.STRIDE * s + 0x10) for s in slots]
        state['pos0'] = [[m.peeks(W.ENT0 + W.STRIDE * s + o) / 65536 for o in (0x114, 0x118, 0x11C)] for s in slots]
    spec['init'] = [init]
    return spec, state


def main():
    spec, state = make()
    res = fw.run(spec)
    print('slots', state['slots'], 'program words +0x4C..:', state['rec_words'], 'codes +0x70..:', state['codes70'], 'status after init', state['status0'], 'pos', state['pos0'])
    cols = ['status', 'anim_t', 'anim_c', 'dir_t', 'cnt1c', 'px', 'py', 'pz', 'fx', 'fy', 'fz', 'adj_13C', 'coll_140', 'ground_144', 'terr_138', 'slope_18C', 'reset_AC', 'hitc', 'touch_slot', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'hbx', 'hbz', 'bw', 'bd', 'bh']
    save('flames309', res, state['slots'], rel=1, title='map 309 records 6,7,8: global sprite 12 through 0x8003A1B8 on the real cells; A program emulated', cols=cols)
    return res, state


if __name__ == '__main__':
    main()
