"""fxlib.py - O-F3-6 harness library (scratch, read-only use of the repository).

Builds, on the E20.f3 MIPS machine (docs/plan-e20-annexe/e20-f3/oracle/tools: pworld.py, world.py, machine.py, emu.py, ... imported by path, never copied, never modified, no .pyc written):
  * the REAL effect creators 0x8003BDD8 / 0x8003BE74 (+ 0x8003B9C4 GetFree, 0x8003BB14 Init) : their stubs are REMOVED;
  * the REAL entity-effects pass 0x80038E84 (stub removed);
  * the REAL effect-slot pass 0x8003C410 (-> animation step 0x8003BBDC, position step 0x8003C284, free of a finished slot), called after the eight passes of 0x8003B388 as
    the frame function 0x8002E058 does (jal 0x8003B388 at 0x8002E108, jal 0x8003C410 at 0x8002E110);
  * the REAL hit pass 0x80039300;
  * the effect tables of the global bank (29 tables) laid out in RAM from the disc file DATA/DATAS.BIN (container offset 0 at RAM 0x80153460: BinOffset + 1389664 =
    SpriteInfoMemoryAddress, checked against data-extracted/data/map_alundra.json for the 29 tables), reached through the block 0x80126EC0 (+0x1C pointer table, +0x20 count)
    that the REAL lookup 0x8003B9FC reads for selector 0.
Nothing here knows an expected value.
"""
import os
import sys
import json

sys.dont_write_bytecode = True
TOOLS = r"D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-f3/oracle/tools"
sys.path.insert(0, TOOLS)
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')

import pworld as P          # noqa: E402
import world as W           # noqa: E402
import c1world as C         # noqa: E402
from machine import Machine, s32, M32   # noqa: E402

DATAS_BIN = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN"
MAP_ALUNDRA = P.MAP_ALUNDRA

# ---- the effect slot table of the binary -------------------------------------------------------------------------------------------
SLOT0 = 0x80138608       # 128 slots of 0x80 bytes (0x8003B9C4: lw 0x68(v1) ; addiu 0x80 ; slti 0x80)
NSLOT = 128
TEMPLATE = 0x8013C608    # the 0x80-byte template 0x8003BB14 copies over a slot (BSS, never written by the binary: see scan in the report)
DISPLAY_LIST = 0x8011CB60   # [0x80126E6C] at the start of every frame (0x8002E0F0-0x8002E0F8)
DISPLAY_COUNT = 0x8011CE60  # counted entries (0x8002E104: sw zero)
DISPLAY_PTR = 0x80126E6C
GLOBAL_BLOCK = 0x80126EC0
FX_PTRS = 0x80152800      # pointer table of the global effect tables, 29 entries (harness memory, free in the machine)
BANK_RAM = 0x80153460     # DATAS.BIN offset 0 (BinOffset + 1389664 = SpriteInfoMemoryAddress)
BANK_BYTES = 0x9000
ZERO_RECORDS = 0x801E7000   # map effect record array [0x80126E88]: first word 0 = no record (0x8003C1A4 loop ends at once)

SLOT_FIELDS = [   # name, offset, kind
    ('status', 0x68, 'w'), ('mode', 0x34, 'w'), ('anchor', 0x38, 'w'), ('id', 0x6E, 'b'), ('sel', 0x6C, 'b'),
    ('seq_target', 0x70, 'b'), ('seq_cur', 0x71, 'b'), ('counter', 0x7C, 'b'), ('done', 0x7D, 'b'),
    ('bank_ptr', 0x08, 'w'), ('frame_ptr', 0x0C, 'w'), ('seq_ptr', 0x74, 'w'), ('seq_loop_ptr', 0x78, 'w'),
    ('x', 0x3C, 'w'), ('y', 0x40, 'w'), ('z', 0x44, 'w'), ('depth_off', 0x60, 'w'), ('depth_key', 0x64, 'w'),
]


def slot_addr(i):
    return SLOT0 + 0x80 * i


def load_bank(m):
    """lay the DATAS.BIN container out in RAM and publish the 29 global effect tables through the block the real lookup reads"""
    raw = open(DATAS_BIN, 'rb').read(BANK_BYTES)
    m.poke_bytes(BANK_RAM, raw)
    d = json.load(open(MAP_ALUNDRA, encoding='utf-8-sig'))
    tabs = d['SpriteInfo']['SpriteEffectRecords']
    info = []
    n = 0
    for i, t in enumerate(tabs):
        ram = 0x80000000 + t['SpriteInfoMemoryAddress']
        assert ram - BANK_RAM == t['BinOffset'], (i, hex(ram), t['BinOffset'])     # the layout assumption, checked for every table
        m.poke(FX_PTRS + 4 * i, ram)
        info.append(dict(index=i, ram=ram, bin_offset=t['BinOffset'], effect_id=t['EffectId'], animation_count=t['AnimationCount']))
        n += 1
    m.poke(GLOBAL_BLOCK + 0x1C, FX_PTRS)
    m.poke(GLOBAL_BLOCK + 0x20, n)
    return info


def build(target_xy=(12, 12, 0), hero_px=(100, 100), seed=0x2468ACE0, real_38e84=True, load_effects=True):
    """E20.f3 world (hero + one inert Beannoid of map 14) with the real effect code and the real hit pass"""
    m, meta = P.setup(item=None, hero_px=hero_px, direction=24, targets=[target_xy], seed=seed)
    for a in (0x8003BDD8, 0x8003BE74):
        m.hooks.pop(a, None)
    if real_38e84:
        m.hooks.pop(0x80038E84, None)
    # keep the harness-level journal of the stubs empty : there are none left on the effect path
    meta['bank'] = load_bank(m) if load_effects else None
    # map load: the REAL 0x8003C1A4 clears the status word of the 128 slots (sw zero 0x68(slot)) and walks the map effect records ([0x80126E88]); a map with no record
    m.poke(ZERO_RECORDS, 0)
    m.poke(0x80126E88, ZERO_RECORDS)
    m.call(0x8003C1A4, limit=5_000_000)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    return m, meta


def slot_read(m, i):
    a = slot_addr(i)
    d = {}
    for name, off, kind in SLOT_FIELDS:
        if kind == 'b':
            d[name] = m.peek(a + off) & 0xFF
        else:
            d[name] = m.peek(a + off)
    return d


def active_slots(m):
    return [i for i in range(NSLOT) if m.peek(slot_addr(i) + 0x68) != 0]


def display_entries(m):
    n = m.peek(DISPLAY_COUNT)
    p = m.peek(DISPLAY_PTR)
    lst = [m.peek(DISPLAY_LIST + 4 * k) for k in range((p - DISPLAY_LIST) // 4)]
    return n, p, lst


def frame_ordinal(m, d):
    """ordinal (0-based) of the frame header the slot has read last = (seq_ptr - first header)/3 - 1, and the u16 'image set word' of that header (frame_ptr = bank + 2*word + 2)"""
    if d['bank_ptr'] == 0 or d['frame_ptr'] == 0:
        return None, None
    word = (d['frame_ptr'] - 2 - d['bank_ptr']) // 2
    first = d['bank_ptr'] + (m.peek(d['bank_ptr']) & 0xFFFF)          # sequence 0: u16 offset at bank + 0
    ordinal = (d['seq_ptr'] - first) // 3 - 1
    return ordinal, word


def pad_tick_prefix(m, pad, hold=0):
    pad.update(hold)
    W.write_pad(m, pad)
    m.call(0x8002E250, W.PAD2, hold)


def passes_before_hit():
    return [a for a in W.PASSES if a != 0x80039300]
