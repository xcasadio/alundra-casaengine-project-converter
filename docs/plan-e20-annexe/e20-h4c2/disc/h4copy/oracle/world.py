"""world.py - D1 oracle: the synthetic world (flat field, map-10 constants), the hero's slot, the pad, the tick.

Everything the binary reads is either (a) real code of ALUN_CD.EXE run by the machine, (b) bytes the harness writes here, each documented, or
(c) reported by the machine as an uninitialised read.  No expected value of the annex appears here.

Tick order = 0x8003B388 (decoded in notes.md), with [0x800DC4B8] = 0:
   0x80038634  entity slots (real code)
   0x800386D0  events: MovePlayer 0x80031B50 first (real), then the loop over entities 1.. (none in D1)
   0x80038998  counters (real)
   0x800384F4  lists (real)
   0x80038E18  animation: 0x80038AB4 on every active entity (real, over the hero bank laid out by bank.py)
   0x80038364  physics (real)
   0x80038E84  effects (stub, journalled)
   0x80039300  hits (stub, journalled)
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import json
from machine import Machine, M32, U, s32
import bank

# ---- memory map of the synthetic pieces (all in the 2 MB RAM, away from the binary's own data) ----------------------------
SPR_REC = 0x801A0000          # hero sprite record + its regions (bank.py), about 110 KB
MAPHDR = 0x801D0000           # map header (+4 gravity, +6 ZViscosity, +8 shift)
TILES = 0x801D1000            # tile table base ([0x800DD86C]); cells at +0x604 + row*416 + col*8
STATS = 0x801E1000            # g_playerStats, pointed to by [0x80176318]
EFFECT_SLOT = 0x801E8000
ENT0 = 0x80127D30
STRIDE = 0x294
ROW = 416
PAD = 0x80126F18              # pad state struct: +0x10 hold, +0x12 pressed, +0x14 released, +0x16 pressed-with-repeat

# entity field offsets (docs/plan-e19-h2-annexe/tools/zsim.py F table + this oracle's reads)
F = dict(status=0x10, hp=0x14, hpmax=0x18, ctrl20=0x20, anim_target=0x88, dir_target=0x8C, anim_cur=0x90, dir_cur=0x94, animdir=0x98,
         flags=0x6C, forcez=0xB8, finalz=0xEC, izf=0xF8, animflags=0xB4, animcounter=0xB0, resetflag=0xAC, nextdelay=0xA8,
         forcex=0xC4, forcey=0xC8, posx=0x114, posy=0x118, posz=0x11C, ground=0x144, coll=0x140, terrain=0x138, floor=0x134,
         vramor=0x180, cnt1c=0x1C, touching=0x224, inv=0x1CC, hitcounter=0x220, balance=0x1C4, sprite=0x64)

# pad bits (docs: PS1 pad, MovePlayer reads the high nibble as directions)
UP, RIGHT, DOWN, LEFT = 0x1000, 0x2000, 0x4000, 0x8000
TRIANGLE, CIRCLE, CROSS, SQUARE = 0x10, 0x20, 0x40, 0x80


def ent(i):
    return ENT0 + STRIDE * i


class Pad:
    """Python port of AlundraTickPad.Update (Alundra/Scripts/AlundraTickPad.cs:56-101): hold, pressed, released, press-with-repeat."""
    MAX = 20

    def __init__(self):
        self.hold = self.pressed = self.released = self.byint = 0
        self.over = 0
        self.nfh = 0
        self.interval = 0

    def update(self, state):
        self.pressed = state & (state ^ self.hold)
        self.released = self.hold & (state ^ self.hold)
        if self.hold != state or self.hold == 0:
            self.over = 0; self.nfh = 0; self.byint = self.pressed; self.hold = state
            return
        if self.over == 0:
            if self.nfh < self.MAX:
                self.nfh += 1; self.byint = 0; self.hold = state
                return
            self.over = 1
        else:
            if self.nfh < self.interval:
                self.nfh += 1; self.byint = 0; self.hold = state
                return
        self.nfh = 0; self.byint = state; self.hold = state


def write_pad(m, pad):
    m.poke16(PAD + 0x10, pad.hold)
    m.poke16(PAD + 0x12, pad.pressed)
    m.poke16(PAD + 0x14, pad.released)
    m.poke16(PAD + 0x16, pad.byint)


# Words of the binary's BSS that the passes read before anything writes them.  The PS1 BSS is zero at power on; each entry says what the word is
# (FACT = decoded from the instructions that use it, UNKNOWN = role not decoded) and why zero is the right starting state.
BSS_ZERO = [
    (0x800DC058, 'sign bit set = a screen transition is running (0x8002F138 bgez, 0x80030D44 bgez, 0x80038A20 bgez); 0x8002AB64 clears it with sw zero; zero = no transition'),
    (0x80126FF8, 'pointer to the charge-aura effect entity (0x8002F730: if set, clears its +0x68 and itself); zero = none'),
    (0x80127110, 'pointer to the weapon sheet of a delayed shot (0x8002F774 beqz: nothing pending); MovePlayer clears it at 0x80031BB4 when +0x20 != 0'),
    (0x801EB838, 'g_playerStats + 0xC = equipped armour item id (0x8004E0F8 lh 0xC); InitPlayerStats 0x8004DAC0 does not write it; zero = item 0, not owned, so GetArmor returns -1'),
    (0x80127D2C, 'count of the collidable list (read by 0x80037F28 FloorHeight during InitializeEntity); rewritten every tick by 0x800384F4'),
    (0x80134360, 'last used entity index (read by InitializeEntity 0x80039D24 and by the passes); rewritten every tick by 0x80038634'),
    (0x80134600, 'entity serial counter (InitializeEntity 0x80039D8C: entity +4 = ++counter)'),
    (0x80126FF4, 'sprint-restart counter (written by MovePlayer only)'),
    (0x80126FFC, 'charge counter (written by 0x8002F49C and MovePlayer only)'),
]


def init_bss(m):
    for a, _ in BSS_ZERO:
        m.poke(a, 0)


def build_world(m, gravity=128, visc=4096, cells=None):
    m.zero(ENT0, 64 * STRIDE)                                  # the 64 entity slots: empty (status 0)
    """flat synthetic field: every cell (walkability 0, ground_property 0, slope 0, height byte 0); map constants of the map header."""
    m.poke(0x800E4334, MAPHDR)
    m.poke16(MAPHDR + 4, gravity)
    m.poke16(MAPHDR + 6, visc)
    m.poke8(MAPHDR + 8, 0)                                     # SlideEffectId (map 10: 0)
    m.poke8(MAPHDR + 9, 2)                                     # BalanceLevel (map 10: 2)
    m.poke8(MAPHDR + 0xA, 7)                                   # C (map 10: 7)
    m.poke8(MAPHDR + 0xB, 0)                                   # D (map 10: 0) = the balance threshold read by 0x8002C39C
    m.poke(0x800DD86C, TILES)
    for x in range(0, 1300):                                  # pixel -> column table (zsim.py: min(x // 24, 51))
        m.poke16(0x80127168 + 2 * x, min(x // 24, 51))
    for row in range(60):
        for col in range(52):
            a = TILES + 0x604 + row * ROW + col * 8
            hb, sl, wk, gp = cells(col, row) if cells else (0, 0, 0, 0)
            m.poke16(a, wk | (gp << 8)); m.poke8(a + 2, sl); m.poke8(a + 3, hb)
    m.poke(0x80127000, 0)                                      # boots level (zsim.py)
    m.poke(0x80127164, 0)                                      # zsim.py
    m.poke(0x800DC4B8, 0)                                      # game-state flags: the tick passes run


BALANCE_BIN = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/BALANCE.BIN"
BALANCE_BASE = 0x80143DF8     # 0x80044550 reads the u16 offset table at 0x80143DF8 + 2*index and the record at 0x80143DF8 + offset


def load_balance(m, level_d):
    """the balance blob the binary loads (disc file DATA/BALANCE.BIN, 11169 bytes; its 512 u16 offsets equal the FileOffset of every entry of
    data-extracted/data/BALANCE.BIN.json: checked by check_balance() below), then 0x80044520(level) for real: [0x80143DF0] = level, equipment sheets cleared.
    level_d = the map header byte +0xB (0x8002C39C: lbu 0xB(header); map 10: Info.D = 0)."""
    raw = open(BALANCE_BIN, 'rb').read()
    m.poke_bytes(BALANCE_BASE, raw)
    m.call(0x80044520, level_d)
    return len(raw)


def check_balance():
    import struct
    raw = open(BALANCE_BIN, 'rb').read()
    j = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/BALANCE.BIN.json", encoding='utf-8-sig'))
    e = j['EntriesBySpriteIndex']
    bad = [i for i in range(512) if struct.unpack_from('<H', raw, 2 * i)[0] != e[i]['FileOffset']]
    return len(e), bad


def init_stats(m):
    """g_playerStats and the inventory as the binary's NEW GAME builds them, with real code:
       0x8004DAC0 InitPlayerStats (stats block 0x801EB82C, inventory table at stats+0x12, 128 slots of 4 bytes, [0x8017638C] = 0x63),
       then the loop of 0x80031868-0x80031890: AddItem 0x8004E530(i) for every item i < 0x62 whose property byte 6 (0x800C5F7C + 8*i + 6) has bit 0x80,
       then SetPlayerWeaponId(1) 0x8004E484 (0x80031894).  Returns the list of items added."""
    m.call(0x8004DAC0)
    kit = []
    for i in range(0x62):
        if m.peek(0x800C5F7C + 8 * i + 4) >> 16 & 0x80:
            m.call(0x8004E530, i)
            kit.append(i)
    m.call(0x8004E484, 1)
    return kit


def place_hero(m, x_px, y_px, anim=0, direction=24, hp=10, rec=None):
    """slot 0 as InitializeEntity 0x80039D04 leaves a hero: that function is run for real, over the hero sprite record (bank.py) and the real balance blob
    (0x80044550 real, sprite table index 0 = the hero's record: level 255, size 169, MaxHp 10), no map record (a3 = 0), PosZ argument 0 (the function
    raises it to terrain + 1 = the binary's resting convention).  Then the plan's starting values: status 2 (InitializeEntity leaves 1), HP +0x14 = hp."""
    P = ent(0)
    info = bank.install(m, SPR_REC, SPR_REC, rec)
    m.zero(P, STRIDE)
    sp = 0x801FFF00
    # stack arguments, caller sp+0x10 ..: +0x68 (balance index 0), +0x48 (unused: no record), PosX, PosY, PosZ, TargetAnimationId, TargetDirection, +0x1B0, +0x1B4
    for i, v in enumerate([0, 0, x_px << 16, y_px << 16, 0, anim, direction, 0, 0]):
        m.poke(sp + 0x10 + 4 * i, v)
    m.call(0x80039D04, P, 0, info['record'], 0)
    m.poke(P + F['status'], 2)
    m.poke(P + F['hp'], hp)
    return info


def run_pass(m, addr):
    return m.call(addr, limit=5_000_000)


PASSES = (0x80038634, 0x800386D0, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364, 0x80038E84, 0x80039300)
PAD2 = 0x801E9000     # a second copy of the pad struct, driven by the REAL pad routine 0x8002E250, to cross-check the port every tick


def init_pad2(m):
    """the state 0x8002E34C / 0x8002E30C leave: [+0] = 0x14 (MaxNbFrameHeld), [+4] = 0 (RepeatInterval), counters and words zero (decoded in notes.md)."""
    m.zero(PAD2, 0x40)
    m.poke(PAD2 + 0, 0x14)
    m.poke(PAD2 + 4, 0)


def tick(m, pad, hold, skip=(), check_pad=True):
    """one logic tick: pad words, then the passes of 0x8003B388 in the binary's order (skip = passes left out, for the mutation tests)."""
    pad.update(hold)
    write_pad(m, pad)
    if check_pad:
        m.call(0x8002E250, PAD2, hold)           # the binary's own routine, same input
        got = (m.peek(PAD2 + 0x10) & 0xFFFF, m.peek(PAD2 + 0x12) & 0xFFFF, m.peek(PAD2 + 0x14) & 0xFFFF, m.peek(PAD2 + 0x16) & 0xFFFF)
        want = (pad.hold, pad.pressed, pad.released, pad.byint)
        if got != want:
            raise RuntimeError('pad port differs from the binary routine at hold 0x%04X: binary %s port %s' % (hold, got, want))
    for a in PASSES:
        if a not in skip:
            run_pass(m, a)
