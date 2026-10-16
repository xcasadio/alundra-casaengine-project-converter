"""trace_kinds.py - bin agent of the E20.e0a5 discovery (scratch copy, never run in place).

The same animation (global sprite 27, the sword wave, animation 0: 13 frames of 2 calls, then the end command) born in five different ways on the f-projectiles machine (pworld.py),
every pass run by the real code of ALUN_CD.EXE (France): which pass sees the entity first, and when does the first call of the animation pass (0x80038E18 -> 0x80038AB4) happen
relative to the creation (the creation itself runs the first switch inside InitializeEntity).  Every number is read from RAM; nothing here knows an expected value.

Scenarios (usage: python -B trace_kinds.py <name>):
  load    record-born at map load: the real record spawner 0x8003A1B8(0, idx, 0) before tick 1 (what 0x8003B308 does)
  mapev   record-born by the map-event pass: 0x8003A1B8(hero, idx, 1) called before the slot pass of tick S (what opcode 0x2D / 0x8A / 0x8B does when the program is a map-event program run by 0x8003C67C)
  loop2   record-born by an entity program in loop 2 of the events pass: 0x8003A1B8(spawner, idx, 1) from a C handler of the entity in slot 1 (what opcode 0x2D / 0x8A / 0x8B does for an entity program)
  loop2w  SpawnWarpEntity (sprite index 27) from the same C handler (a native handler: the interface-born case of the DLL when the spawner is not MovePlayer)
  move    SpawnWarpEntity from MovePlayer (hooked) in the events pass, before loop 1 (the hero's shot, the DLL's SpawnEntityBySpriteIndex in MovePlayer)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C

from machine import M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
SPR = int(os.environ.get('SPR', '27'))
MAPBLK = C.MAPBLK
ENT_REC = C.ENT_REC
CTAB = 0x800C4F34
FAKE_C = 0x8008FF00
PASSES = [(0x80038634, 'slots'), (0x800386D0, 'events'), (0x80038998, 'counters'), (0x800384F4, 'lists'), (0x80038E18, 'anim'), (0x80038364, 'physics'), (0x80038E84, 'effects'), (0x80039300, 'hits')]
UPD, UPDN = 0x80127B28, 0x80127D28


def ent(i):
    return ENT0 + STRIDE * i


def lst(m, base, cnt):
    return [m.peek(base + 4 * i) for i in range(m.peek(cnt))]


def rec_bytes(sprite, direction_byte, hero_tile=(4, 8), x=10, y=10, h=0):
    # XMin YMin XMax YMax IsEnabled SpriteDirection SpriteTableIndex XPos YPos Height A..F _10 Contents
    return bytes([0, 0, 255, 255, 1, direction_byte, sprite, x, y, h, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])


def world():
    m, meta = P.setup(item=None, hero_px=(100, 100), direction=24, types=tuple(sorted(set(P.PROJECTILE_TYPES) | {SPR})))
    m.poke_bytes(ENT_REC, rec_bytes(SPR, 0x40 | 3, h=2))                  # entity record 0: global page (bit 0x80 clear), flag 0x40, direction index 3 = 0x18
    m.poke(MAPBLK + 8, ENT_REC)
    m.poke(MAPBLK + 0xC, C.SPR_PTRS)
    m.poke(MAPBLK + 0x10, 4)
    m.poke(MAPBLK + 0x14, 1)
    return m


FIELDS = [('status', 0x10, 1), ('c1c', 0x1C, 1), ('adir', 0x98, 1), ('hdr', 0x9C, 0), ('loop', 0xA0, 0), ('next', 0xA4, 0), ('cnt', 0xA8, 1), ('end', 0xAC, 1), ('b0', 0xB0, 1),
          ('animflags', 0xB4, 0), ('b8', 0xB8, 1), ('f8', 0xF8, 1), ('w1FC', 0x1FC, 1), ('box', 0x1D4, 0), ('trig', 0x228, 1), ('serial', 0x4, 1), ('parent', 0xC, 0)]


def snap(m, e):
    d = {}
    for n, off, sg in FIELDS:
        d[n] = m.peeks(e + off) if sg else m.peek(e + off)
    d['frame'] = ((d['next'] - d['loop']) // 5 - 1) if d['loop'] and d['next'] >= d['loop'] else None
    return d


class Run:
    def __init__(self, m):
        self.m = m
        self.e = None
        self.log = []                   # (tick, pass, snap)
        self.creation = None
        self.created_tick = None
        self.created_pass = None
        self.tick = 0
        self.ps = '-'
        self.hooks = {}
        orig = m.exec1

        def exec1(pc, w):
            f = self.hooks.get(pc)
            if f:
                f(pc)
            return orig(pc, w)
        m.exec1 = exec1
        # the first switch happens at 0x80039EA8: record the state right after it returns (0x80039EAC is its delay slot; use 0x80039EB0)
        self.hooks[0x80039EB0] = self.after_first_switch

    def after_first_switch(self, pc):
        m = self.m
        # s0 of InitializeEntity = the entity
        e = m.r[16]
        if self.e is None or e == self.e:
            self.e = e
            self.creation = (self.tick, self.ps, snap(m, e))
            self.created_tick, self.created_pass = self.tick, self.ps

    def run(self, nticks, before=None, first_tick=1):
        m = self.m
        pad = W.Pad()
        for t in range(first_tick, first_tick + nticks):
            self.tick = t
            m.tick_no = t
            if before:
                before(self, t)
            pad.update(0)
            W.write_pad(m, pad)
            for a, nm in PASSES:
                self.ps = nm
                m.call(a, limit=5_000_000)
                if self.e is not None:
                    d = snap(m, self.e)
                    d['inupd'] = self.e in lst(m, UPD, UPDN)
                    self.log.append((t, nm, d))


def table(run, t0, t1):
    out = []
    out.append('creation (state right after the first switch, inside InitializeEntity): tick %s pass %s : %s' % (run.created_tick, run.created_pass, run.creation[2] if run.creation else None))
    out.append('%-5s | %-8s %-3s %-4s | %-5s %-6s %-4s %-3s | %-9s' % ('tick', 'status@ev', 'c1c', 'inUpd', 'frame', 'cnt', 'end', 'adir', 'worldX@phys'))
    by = {}
    for t, nm, d in run.log:
        by.setdefault(t, {})[nm] = d
    for t in range(t0, t1 + 1):
        if t not in by:
            continue
        r = by[t]
        ev, co, li, an, ph = r['events'], r['counters'], r['lists'], r['anim'], r['physics']
        out.append('%-5d | st=%d      %-3d %-4s | %-5s %-11d %-4d %-3d | %.1f | b8(forceZ)=%d f8=%d' % (t, ev['status'], co['c1c'], li['inupd'], an['frame'], an['cnt'], an['end'], an['adir'], ph['w1FC'] / 65536.0, ph['b8'], an['f8']))
    return '\n'.join(out)


def scenario(name):
    m = world()
    run = Run(m)
    S = 20                                              # birth tick of the scenarios that spawn at a chosen tick
    if name == 'load':
        run.tick, run.ps = 0, 'load'
        p = m.call(0x8003A1B8, 0, 0, 0, limit=5_000_000) & M32
        assert p, 'record spawner refused'
        t0 = 1
        n = 34
        before = None
    elif name == 'mapev':
        def before(r, t):
            if t == S:
                r.ps = 'mapevent'
                p = m.call(0x8003A1B8, ent(0), 0, 1, limit=5_000_000) & M32
                assert p
        t0, n = S, 34
    elif name in ('loop2', 'loop2w'):
        # the spawner: an arrow (global sprite 3) in slot 1 with a C handler = our hook, status 2

        arrow = P_spawn_arrow(m)
        assert (arrow - ENT0) // STRIDE == 1, arrow
        run.e = None
        run.creation = None
        m.poke(arrow + 0x10, 2)
        m.poke(arrow + 0x78, 200)
        m.poke(arrow + 0x80, 0)
        m.poke(arrow + 0x6C, 0)
        m.poke(CTAB + 4 * 200, FAKE_C)
        fired = {'n': 0}

        def hook(mm):
            if (mm.r[4] & M32) == arrow and fired['n'] == 0 and run.tick == S:
                fired['n'] = 1
                sp_ = (mm.r[29] - 0x800) & ~7
                run.ps = 'events-loop2'
                if name == 'loop2':
                    C.side_call(mm, 0x8003A1B8, (arrow, 0, 1), sp_)
                else:
                    mm.poke(sp_ + 0x10, 100 << 16)
                    mm.poke(sp_ + 0x14, 100 << 16)
                    mm.poke(sp_ + 0x18, 0x10 + 8)
                    C.side_call(mm, 0x80039F58, (arrow, 0, SPR, 100 << 16), sp_)
            mm.r[2] = 0
        m.hooks[FAKE_C] = hook
        before = None
        t0, n = S, 34
    elif name == 'move':
        def mp(mm):
            if run.tick == S:
                sp_ = (mm.r[29] - 0x800) & ~7
                mm.poke(sp_ + 0x10, 100 << 16)
                mm.poke(sp_ + 0x14, 100 << 16)
                mm.poke(sp_ + 0x18, 0x18)
                run.ps = 'events-moveplayer'
                C.side_call(mm, 0x80039F58, (ent(0), 0, SPR, 100 << 16), sp_)
            mm.r[2] = 0
        m.hooks[0x80031B50] = mp
        before = None
        t0, n = S, 34
    else:
        raise SystemExit('unknown scenario')
    run.run(n, before=before if name in ('mapev',) else None, first_tick=1 if name == 'load' else 1)
    return m, run, t0


def P_spawn_arrow(m):
    sp = 0x801FF000
    m.poke(sp + 0x10, 120 << 16)
    m.poke(sp + 0x14, 100 << 16)
    m.poke(sp + 0x18, 0x10)
    return C.side_call(m, 0x80039F58, (ent(0), 0, 3, 120 << 16), sp)


if __name__ == '__main__':
    name = sys.argv[1]
    m, run, t0 = scenario(name)
    txt = '# scenario %s\n' % name + table(run, t0 - 1 if name != 'load' else 1, t0 + 32)
    print(txt)
    open(os.path.join(HERE, '..', 'out', 'kinds_%s%s.txt' % (name, '' if SPR == 27 else '_spr%d' % SPR)), 'w').write(txt + '\n')
