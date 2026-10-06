"""Run the REAL entity animation routine of ALUN_CD.EXE (France), UpdateAnimation 0x80038AB4, over
synthetic sprite-info bytes that reproduce the delay/terminator bytes of bank 127 animset 0 and bank 151
animset 1 (data-extracted map_75.json / map_48.json), one call per game tick (the per-tick caller is
UpdateEntitiesAnimation 0x80038E18, which calls 0x80038AB4 for each active entity).

Layout read from the disassembly (dis_update_animation.txt):
  entity+0x64 sprite info: [0] animset table (14 bytes each: u16 byte offsets down/up/left/right, +0xa lh, +0xc sfx, +0xd flags),
                           [4] frame bytes base, [8] collision base, [0xc] image-set base
  frame = 5 bytes: delay, u16 collision index (0xFFFF none), u16 image-set index (0xFFFF none); loop terminator = 1 byte (0x01)
  entity: +0x88 TargetAnimationId, +0x8c TargetDirection, +0x90 CurrentAnimationId, +0x98 AnimationDirection,
          +0xa0 first frame ptr, +0xa4 current frame ptr, +0xa8 NextFrameDelay, +0xac force-reset flag, +0xb0 AnimCompleteCounter,
          +0x194 SpriteRef.Images ptr, +0x1a8 image count, +0x1c4 balance record ptr
"""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import emu

UPDATE_ANIMATION = 0x80038AB4
ENT = 0x80180000
SPR = 0x80181000
ANIMSETS = 0x80181100
FRAMES = 0x80181200
IMG = 0x80181400
BAL = 0x80181800
TD = {"down": 0, "up": 12, "left": 8, "right": 24}   # TargetDirection -> direction table 0x800237F4 gives 0/1/2/3


def build(m, delays, quads):
    """delays: {dir: first-frame delay byte}; one displayed frame then a 0x01 loop terminator, offsets 0,6,12,18 like the data."""
    m.ww(SPR + 0, ANIMSETS); m.ww(SPR + 4, FRAMES); m.ww(SPR + 8, 0x80181600); m.ww(SPR + 0xC, IMG)
    order = ["down", "up", "left", "right"]
    for i, d in enumerate(order):
        off = 6 * i
        m.wh(ANIMSETS + 2 * i, off)
        m.wb(FRAMES + off + 0, delays[d])
        m.wh(FRAMES + off + 1, 0xFFFF)          # no collision box
        m.wh(FRAMES + off + 3, 0)               # image set index 0
        m.wb(FRAMES + off + 5, 0x01)            # loop terminator
    m.wb(IMG + 0, quads)                         # NumberOfImages
    m.wb(IMG + 1, 0)                             # depth sort value
    m.ww(ENT + 0x64, SPR)
    m.ww(ENT + 0x1C4, BAL)


def run(label, delays, quads, direction, ticks=600, poke_tick=None):
    m = emu.make_machine()
    build(m, delays, quads)
    m.ww(ENT + 0x88, 0)               # TargetAnimationId = animset 0
    m.ww(ENT + 0x90, 0xFFFFFFFF)      # CurrentAnimationId differs -> first call takes the reset path
    m.ww(ENT + 0x98, 0)
    m.ww(ENT + 0x8C, TD[direction])
    rows = []
    for t in range(ticks):
        if poke_tick is not None and t == poke_tick:
            m.ww(ENT + 0xA8, 1)       # stand-in for the 2^32-1 decrements a 0x80 frame needs before the terminator is read
        m.call(UPDATE_ANIMATION, ENT)
        rows.append((t, m.rw(ENT + 0x98), m.rw(ENT + 0xA8), m.rw(ENT + 0xA4) - FRAMES, m.rw(ENT + 0xB0),
                     m.rw(ENT + 0x194), m.rb(ENT + 0x1A8), m.rw(ENT + 0xAC)))
    print(f"--- {label}: direction {direction}, first-frame delay 0x{delays[direction]:02X}, {quads} quad(s), {ticks} ticks"
          + (f", NextFrameDelay poked to 1 before tick {poke_tick}" if poke_tick is not None else ""))
    images_ptrs = sorted({r[5] for r in rows})
    counts = sorted({r[6] for r in rows})
    print(f"    SpriteRef.Images over all ticks: {[hex(p) for p in images_ptrs]} (frame 0 image set = {hex(IMG + 2)}); image count values {counts}")
    print(f"    AnimCompleteCounter at end: {rows[-1][4]}; distinct frame-ptr offsets {sorted({r[3] for r in rows})}")
    for r in rows[:4] + rows[-2:]:
        print(f"    tick {r[0]:4d} dir {r[1]} NextFrameDelay 0x{r[2]:08X} framePtr+{r[3]} counter {r[4]} images {hex(r[5])} n {r[6]} force {r[7]}")
    if poke_tick is not None:
        for r in rows[poke_tick - 1: poke_tick + 3]:
            print(f"    tick {r[0]:4d} dir {r[1]} NextFrameDelay 0x{r[2]:08X} framePtr+{r[3]} counter {r[4]} images {hex(r[5])} n {r[6]}")
    return rows


b127 = {"down": 0x82, "up": 0x82, "left": 0x80, "right": 0x80}
b151 = {"down": 0x80, "up": 0x80, "left": 0x80, "right": 0x8A}
for d in ["left", "right", "down"]:
    run(f"bank127 anim0 {d}", b127, 1, d)
for d in ["down", "left", "up", "right"]:
    run(f"bank151 anim1 {d}", b151, 4, d)
run("bank127 anim0 left (wrap check)", b127, 1, "left", ticks=40, poke_tick=20)
