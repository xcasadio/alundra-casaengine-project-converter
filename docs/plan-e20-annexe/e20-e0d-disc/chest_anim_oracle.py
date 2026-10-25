"""The chest's opening animation on the REAL UpdateAnimation (0x80038AB4) of ALUN_CD.EXE (France), copied MIPS interpreter (the harness of the E14.a oracle-a1 annex:
docs/plan-e14-annexe/e14a/oracle-a1/tools/run_update_animation.py, same entity/sprite-info layout, read from dis_update_animation.txt).
Input: the real frame bytes of the global sprite 30 (data-extracted/data/map_alundra.json): the delay bytes of the animation sets 0, 1 and 2 (the terminator of a chain is the two
bytes 00 <animation>, of a loop the byte 01), the sound byte of the set 2 (51). One call per tick. Output: the tick of the sound, the tick at which TargetAnimationId goes 2 -> 1."""
import sys, json, struct
sys.path.insert(0, 'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/emu')
import emu

UPDATE_ANIMATION = 0x80038AB4
ENT = 0x80180000
SPR = 0x80181000
ANIMSETS = 0x80181100
FRAMES = 0x80181200
COLL = 0x80181600
IMG = 0x80181800
M32 = 0xFFFFFFFF

d = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json', encoding='utf-8'))
rec = d['SpriteInfo']['SpriteRecords'][30]
sets = rec['AnimSets']

def build(m):
    m.ww(SPR + 0, ANIMSETS)
    m.ww(SPR + 4, FRAMES)
    m.ww(SPR + 8, COLL)
    m.ww(SPR + 0xC, IMG)
    # frame bytes: for each anim set / direction at the exported offsets
    for si, a in enumerate(sets):
        base = ANIMSETS + 14 * si
        for k, off in enumerate(a['AnimationOffsets']):
            m.wh(base + 2 * k, off)
        m.wh(base + 0xA, a['IsZForceApplied'])
        m.wb(base + 0xC, a['Sfx'])
        m.wb(base + 0xD, a['Acceleration'])      # the byte +0xD: AnimFlags (the export names it Acceleration)
        for di, an in enumerate(a['PreloadedAnims']):
            off = a['AnimationOffsets'][di]
            p = FRAMES + off
            frames = an['Frames']
            for f in frames[:-1]:
                m.wb(p, f['Delay'])
                m.wh(p + 1, 0xFFFF)
                m.wh(p + 3, 0)
                p += 5
            tail = frames[-1]
            if tail['Delay'] == 1:
                m.wb(p, 1)                  # loop
            else:
                m.wb(p, 0)                  # chain: 00 <animation>
                m.wb(p + 1, tail['TransformIndexLow'])
    m.wb(IMG + 0, 2)
    m.wb(IMG + 1, 0)
    m.ww(ENT + 0x64, SPR)
    m.ww(ENT + 0x1C4, IMG + 0x100)        # balance record pointer: a zero byte at +0xE -> no attack row

def run(direction, ticks=200):
    m = emu.make_machine()
    build(m)
    sounds = []
    m.hooks[0x800490FC] = lambda mc: sounds.append((tick[0], mc.r[4] & M32))
    tick = [0]
    TD = {0: 0, 1: 12, 2: 8, 3: 24}      # TargetDirection -> direction table 0x800237F4 gives 0/1/2/3 (as in the E14 harness)
    # the chest at rest in the set 0, direction 0: settle first
    m.ww(ENT + 0x88, 0)
    m.ww(ENT + 0x90, 0xFFFFFFFF)
    m.ww(ENT + 0x98, 0)
    m.ww(ENT + 0x8C, TD[direction])
    for t in range(8):
        m.call(UPDATE_ANIMATION, ENT)
    sounds.clear()
    # F255 sets TargetAnimationId = 2 at tick 0 (the programs run before the animation pass of the tick)
    m.ww(ENT + 0x88, 2)
    chain_tick = None
    rows = []
    for t in range(ticks):
        tick[0] = t
        m.call(UPDATE_ANIMATION, ENT)
        tgt = m.rw(ENT + 0x88)
        rows.append((t, tgt, m.rw(ENT + 0x90), (m.rw(ENT + 0xA4) - FRAMES), m.rw(ENT + 0xA8)))
        if chain_tick is None and tgt != 2:
            chain_tick = t
    return sounds, chain_tick, rows

if __name__ == '__main__':
    for direction in (0, 1, 2, 3):
        sounds, chain, rows = run(direction)
        cur = [r for r in rows if r[0] in (0, 1, chain, (chain or 0) + 1)]
        print('direction %d: sound calls (tick, id) %s | TargetAnimationId leaves 2 at tick %s -> %s | rows %s' % (direction, sounds, chain, rows[chain][1] if chain is not None else None, cur))
