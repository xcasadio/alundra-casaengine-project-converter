"""rec_tick.py - E20.e0a4 discovery, binary lens (scratch only, never in the repository).

What a RECORD-born entity (the real record spawner 0x8003A1B8 -> InitializeEntity 0x80039D04 -> first switch 0x80038AB4) does in the tick of its birth and the ticks after,
by birth site and slot, on the f-projectiles machine of the E20 oracles (pworld.py, byte copies in repo/docs/plan-e20-annexe/e20-e0a5/oracle/tools, extracted from HEAD).
Real code: the map-event pass 0x8003C67C, the slot pass 0x80038634, the events pass 0x800386D0 (MovePlayer, loop 1, loop 2), the counters pass 0x80038998, the list pass 0x800384F4,
the animation pass 0x80038E18, the physics pass 0x80038364, the hit pass 0x80039300, the record spawner and InitializeEntity, and the REAL opcode handlers of the interpreter:
0x2D -> 0x8003DCC4, 0x8A -> 0x80040284, 0x8B -> 0x8004033C (opcode table 0x80098FAC, dispatch 0x800422D4-0x800422F4).
Replaced: the body of the bytecode runner 0x8004205C (a stub in every E20 oracle); here, for the ONE call that the scenario names (the spawner's program C in loop 2 at 0x8003890C,
or the hero's map-event program at 0x8003C790), the stub calls the real opcode handler with a planted bytecode, exactly as the dispatch would (a0 = running entity, a2 = cursor);
every other call stays the journalling no-op.  Every other handler is the pworld inert stub (except the real projectile handlers of pworld.REAL).
Nothing here knows an expected value of the DLL; every number is read from RAM after the real code ran.

usage:  python -B rec_tick.py            -> out/rec_tick.json (one row per label), out/rec_tick.txt (summary)
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'repo', 'docs', 'plan-e20-annexe', 'e20-e0a5', 'oracle', 'tools')
sys.path.insert(0, TOOLS)
import trace_kinds as K
import world as W
import c1world as C
from machine import M32

ENT0, STRIDE = W.ENT0, W.STRIDE
HERO = ENT0
UPD, UPDN = 0x80127B28, 0x80127D28
COL, COLN = 0x80127C28, 0x80127D2C
DRW, DRWN = 0x80134250, 0x8013435C
COUNT = 0x80134360
CTAB = 0x800C4F34            # C handler table (0x80098F4C[2])
FAKE_C = 0x8008FF00
MAPEV = 0x8013C688           # 64 map events, 0x48 bytes each (0x8003C67C)
RECT = 0x801D8200
BYTECODE = 0x801D8000
CURSOR = 0x801D8100
OPS = {0x2D: 0x8003DCC4, 0x8A: 0x80040284, 0x8B: 0x8004033C}
PASSES = [(0x8003C67C, 'mapev')] + list(K.PASSES)      # one frame of 0x8002E058: map events, then the tick block 0x8003B388 (slots ... hits)
OUT = os.path.join(HERE, 'out')


def ent(i):
    return ENT0 + STRIDE * i


def slot_of(p):
    return (p - ENT0) // STRIDE if ENT0 <= p < ENT0 + 64 * STRIDE else None


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def lst(m, base, cnt):
    return [m.peek(base + 4 * i) for i in range(m.peek(cnt))]


def make_free(m, i, trig):
    """the template of 0x80134368 (zeros, +0x48 = -1) with the slot index, and a chosen stale trigger +0x228"""
    m.zero(ent(i), 0x294)
    m.poke(ent(i), i)
    m.poke(ent(i) + 0x48, 0xFFFFFFFF)
    m.poke(ent(i) + 0x228, trig & M32)


def make_fill(m, i, status=5):
    make_free(m, i, -1)
    m.poke(ent(i) + 0x10, status)


F = [('status', 0x10, 0), ('trig', 0x228, 1), ('c1c', 0x1C, 1), ('cnt_A8', 0xA8, 1), ('end_AC', 0xAC, 1), ('loops_B0', 0xB0, 1), ('adir', 0x98, 1),
     ('anim_t', 0x88, 1), ('dir_t', 0x8C, 1), ('anim_c', 0x90, 1), ('dir_c', 0x94, 1), ('hdr', 0x9C, 0), ('loop', 0xA0, 0), ('next', 0xA4, 0),
     ('animflags_B4', 0xB4, 0), ('box1d4', 0x1D4, 0), ('atk1c8', 0x1C8, 0), ('flags6C', 0x6C, 0),
     ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1), ('modx', 0x1D8, 1), ('mody', 0x1DC, 1), ('modz', 0x1E0, 1),
     ('hbx', 0x1FC, 1), ('hby', 0x200, 1), ('hbz', 0x204, 1), ('bw', 0x214, 1), ('bd', 0x218, 1), ('bh', 0x21C, 1),
     ('prog4c', 0x4C, 1), ('prog54', 0x54, 1), ('cA', 0x70, 0), ('cC', 0x78, 0), ('rec44', 0x44, 0), ('ref48', 0x48, 1), ('parent', 0xC, 0), ('serial', 0x4, 0)]


def snap(m, e):
    d = {}
    for n, off, sg in F:
        d[n] = m.peeks(e + off) if sg else m.peek(e + off)
    d['frame'] = ((d['next'] - d['loop']) // 5 - 1) if d['loop'] and d['next'] >= d['loop'] else None
    d['in_update'] = e in lst(m, UPD, UPDN)
    d['in_collision'] = e in lst(m, COL, COLN)
    d['in_draw'] = e in lst(m, DRW, DRWN)
    d['count'] = m.peeks(COUNT)
    d['parent_slot'] = slot_of(d['parent']) if d['parent'] else None
    return d


class Exp:
    """one row.  scenario: 'loop2' (the spawner's program C in loop 2 runs the opcode, or a native C handler with native=True), 'mapev' (the hero's map-event program runs the opcode
    in the map-event pass before the slot pass), 'load' (0x8003A1B8(0, idx, 0) before tick 1, what 0x8003B308 does)."""

    def __init__(self, label, scenario='loop2', op=0x2D, spr=27, S=1, spawner_slot=1, filler=(), free_stale=None, dead_before=(), native=False, rec_A=0,
                 switches=(), n=34, note='', force_col=False):
        self.label, self.scenario, self.op, self.spr, self.S, self.n, self.note = label, scenario, op, spr, S, n + S, note
        self.native = native
        self.force_col = force_col
        self.switches = list(switches)
        K.SPR = spr
        self.m = m = K.world()
        self.tick, self.ps = 0, 'setup'
        self.new = None
        self.armed = False
        self.fired = False
        self.creation = None            # snapshot right after the first switch (0x80039EB0)
        self.creation_end = None        # snapshot at 0x80039F38 (after the world box 0x80038064 at 0x80039F30, before the loot roll)
        self.after_op = None            # snapshot right after the opcode handler returned (0x8A / 0x8B write the position after the creation)
        self.stale = None               # +0x228 and +0x1C of the slot just before InitializeEntity wrote anything (0x80039D04 entry)
        self.turns = []                 # loop-2 program turns of the new entity: (tick, trigger, status at the turn, kind)
        self.interp = []                # calls of the bytecode runner: (tick, slot, trigger)
        self.anim_calls, self.phys_visits, self.hit_visits = [], [], []
        self.log = []                   # (tick, pass, snapshot of the new entity)
        self.count_at_loop1 = {}
        self.spawner = None
        if rec_A:
            m.poke_bytes(C.ENT_REC + 0xA, bytes([rec_A]))
        for i in range(1, 64):
            make_free(m, i, 0)
        for i in range(0x40 * 0x48 // 4):
            m.poke(MAPEV + 4 * i, 0)
        if scenario == 'loop2':
            for i in range(1, spawner_slot):
                make_fill(m, i, 5)
            arrow = K.P_spawn_arrow(m)
            assert slot_of(arrow) == spawner_slot, (slot_of(arrow), spawner_slot)
            self.spawner = arrow
            m.poke(arrow + 0x10, 2)
            m.poke(arrow + 0x6C, 0)
            m.poke(arrow + 0x80, 0)
            if native:
                m.poke(arrow + 0x54, 0)
                m.poke(arrow + 0x78, 200)
                m.poke(CTAB + 4 * 200, FAKE_C)
                m.hooks[FAKE_C] = self._native_c
            else:
                m.poke(arrow + 0x54, 1)          # program C is a bytecode program: loop 2 calls 0x8004205C(spawner, 2) at 0x8003890C
                m.poke(arrow + 0x78, 0)
            for i in range(1, spawner_slot):
                if i not in filler:
                    make_free(m, i, 0)
        for i in filler:
            make_fill(m, i)
        for i, t in (free_stale or {}).items():
            make_free(m, i, t)
        for i in dead_before:
            make_fill(m, i, status=4)
        m.hooks[0x8004205C] = self._interp
        self.pc = {0x80039D04: self._init_entry, 0x80039EB0: self._after_switch, 0x80039F38: self._after_box, 0x800388F4: self._turn, 0x80038E4C: self._anim,
                   0x8003839C: self._phys, 0x80039364: self._hit}
        orig = m.exec1

        def exec1(pc, w):
            f = self.pc.get(pc)
            if f:
                f(pc)
            return orig(pc, w)
        m.exec1 = exec1

    # ---- PC hooks --------------------------------------------------------------------------------------------------------------------------------------------------------------------
    def _init_entry(self, pc):
        if self.armed and self.new is None:
            e = self.m.r[4] & M32
            self.stale = dict(slot=slot_of(e), trig228=self.m.peeks(e + 0x228), c1c=self.m.peeks(e + 0x1C), status=self.m.peeks(e + 0x10), count=self.m.peeks(COUNT))

    def _after_switch(self, pc):
        e = self.m.r[16] & M32
        if self.armed and self.new is None:
            self.new = e
            self.creation = dict(tick=self.tick, ps=self.ps, **snap(self.m, e))

    def _after_box(self, pc):
        e = self.m.r[16] & M32
        if e == self.new and self.creation_end is None:
            self.creation_end = dict(tick=self.tick, ps=self.ps, **snap(self.m, e))
            if self.force_col:                          # PROBE ONLY (not a real sprite): make the entity eligible for the collision list (+0x6C bit 0x80, +0xB4 bit 0x80 clear, +0x28 = 0)
                self.m.poke(e + 0x6C, self.m.peek(e + 0x6C) | 0x80)
                self.m.poke(e + 0xB4, self.m.peek(e + 0xB4) & ~0x80 & M32)

    def _turn(self, pc):
        e = self.m.r[17] & M32
        if e == self.new:
            trig = s32(self.m.r[5])
            prog = self.m.peeks(e + 0x4C + 4 * trig) if 0 <= trig < 6 else None
            self.turns.append((self.tick, trig, self.m.peek(e + 0x10), 'bytecode' if (prog or 0) & 0x7F else 'native'))

    def _anim(self, pc):
        if (self.m.r[4] & M32) == self.new:
            self.anim_calls.append(self.tick)

    def _phys(self, pc):
        if (self.m.r[6] & M32) == self.new:
            self.phys_visits.append(self.tick)

    def _hit(self, pc):
        if (self.m.r[17] & M32) == self.new:
            self.hit_visits.append(self.tick)

    # ---- the spawn ---------------------------------------------------------------------------------------------------------------------------------------------------------------
    def _bytecode(self):
        op = self.op
        if op == 0x2D:
            code = [0x2D, 0]
        elif op == 0x8A:
            x, y, z = 150, 120, 4                         # absolute X, Y, Z (16-bit words of px), written after the creation
            code = [0x8A, 0, x & 0xFF, x >> 8, y & 0xFF, y >> 8, z & 0xFF, z >> 8]
        else:
            dx, dy, dz = 20, 10, 0                        # selector 0x80 = the running entity itself (0x80023C30[0] = 0x8003CA2C), then the offsets
            code = [0x8B, 0x80, 0, dx & 0xFF, dx >> 8, dy & 0xFF, dy >> 8, dz & 0xFF, dz >> 8]
        self.m.poke_bytes(BYTECODE, bytes(code) + bytes(16 - len(code)))
        self.m.poke(CURSOR, BYTECODE)

    def _interp(self, mm):
        a0 = mm.r[4] & M32
        a1 = s32(mm.r[5])
        self.interp.append((self.tick, slot_of(a0), a1))
        want = (self.scenario == 'loop2' and not self.native and a0 == self.spawner and a1 == 2) or (self.scenario == 'mapev' and a0 == HERO and a1 == 1)
        if want and not self.fired and self.tick == self.S:
            self.fired = True
            self._bytecode()
            sp_ = (mm.r[29] - 0x800) & ~7
            self.ps = 'events-loop2' if self.scenario == 'loop2' else 'mapev'
            self.armed = True
            self.op_ret = C.side_call(mm, OPS[self.op], (a0, a0, CURSOR, 0), sp_)
            if self.new:
                self.after_op = dict(tick=self.tick, ps=self.ps, ret=self.op_ret, **snap(mm, self.new))
        mm.r[2] = 0

    def _native_c(self, mm):
        a0 = mm.r[4] & M32
        if a0 == self.spawner and not self.fired and self.tick == self.S:
            self.fired = True
            sp_ = (mm.r[29] - 0x800) & ~7
            self.ps = 'events-loop2'
            self.armed = True
            C.side_call(mm, 0x8003A1B8, (a0, 0, 1), sp_)
            if self.new:
                self.after_op = dict(tick=self.tick, ps=self.ps, ret=None, **snap(mm, self.new))
        mm.r[2] = 0

    # ---- run ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
    def run(self):
        m = self.m
        if self.scenario == 'load':
            self.tick, self.ps = 0, 'load'
            self.armed = True
            p = m.call(0x8003A1B8, 0, 0, 0, limit=5_000_000) & M32
            assert p, 'record spawner refused'
            self.fired = True
        if self.scenario == 'hero':                      # the hero arrival 0x80031974 as world.place_hero runs it: real InitializeEntity(slot 0, no record), then status 2 (0x800319FC)
            self.tick, self.ps = 0, 'load'
            self.armed = True
            W.place_hero(m, 100, 100, direction=24)
            self.fired = True
        pad = W.Pad()
        for t in range(1, self.n + 1):
            self.tick = t
            m.tick_no = t
            for (k, anim, *rest) in self.switches:
                if k >= 1 and self.new and t == self.creation['tick'] + k:
                    m.poke(self.new + 0x88, anim)
                    if rest:
                        m.poke(self.new + 0x8C, rest[0])
            pad.update(0)
            W.write_pad(m, pad)
            if self.scenario == 'mapev' and t == self.S:
                m.poke(RECT, 0xFFFF0000)                    # rect bytes 0, 0, 255, 255: the hero's tile is inside
                m.poke(MAPEV + 4, RECT)
                m.poke(MAPEV + 8, 1)                        # program code 1 (& 0x7F != 0): the event runs 0x8004205C(hero, 1)
                m.poke(MAPEV + 0xC, HERO)
            for a, nm in PASSES:
                self.ps = nm
                m.call(a, limit=5_000_000)
                if nm == 'mapev' and self.scenario == 'mapev' and t == self.S:
                    for i in range(0x48 // 4):
                        m.poke(MAPEV + 4 * i, 0)
                if nm == 'slots':
                    self.count_at_loop1[t] = m.peeks(COUNT)
                if nm == 'events':
                    for (k, anim, *rest) in self.switches:
                        if k == 0 and self.new and t == self.creation['tick']:
                            m.poke(self.new + 0x88, anim)
                            if rest:
                                m.poke(self.new + 0x8C, rest[0])
                if self.new:
                    self.log.append((t, nm, snap(m, self.new)))
        return self

    # ---- the row -----------------------------------------------------------------------------------------------------------------------------------------------------------------
    def row(self):
        S = self.creation['tick'] if self.creation else None
        by = {}
        for t, nm, d in self.log:
            by.setdefault(t, {})[nm] = d
        ticks = []
        for t in sorted(by):
            ticks.append(dict(tick=t, k=t - S if S is not None else None, passes=by[t]))

        def first(pred, names=None):
            for t in sorted(by):
                for _, nm in PASSES:
                    if (names is None or nm in names) and nm in by[t] and pred(by[t][nm]):
                        return [t, nm]
            return None
        c0 = self.creation_end['c1c'] if self.creation_end else None
        a_turns = [x for x in self.turns if x[1] == 0]
        r = dict(label=self.label, scenario=self.scenario, op=('0x%02X' % self.op) if self.scenario != 'load' else None, native=self.native, sprite=self.spr, note=self.note,
                 S=S, birth_pass=self.creation['ps'] if self.creation else None, new_slot=slot_of(self.new) if self.new else None,
                 spawner_slot=slot_of(self.spawner) if self.spawner else None, stale_before_creation=self.stale,
                 count_after_slot_pass=self.count_at_loop1, creation=self.creation, creation_end=self.creation_end, after_op=self.after_op,
                 program_turns=self.turns, a_turns=a_turns, interp_calls=[x for x in self.interp if x[1] == slot_of(self.new)] if self.new else [],
                 anim_calls=self.anim_calls, physics_visits=self.phys_visits, hit_visits=self.hit_visits,
                 first=dict(status2=first(lambda d: d['status'] == 2), in_update=first(lambda d: d['in_update']), in_collision=first(lambda d: d['in_collision']),
                            in_draw=first(lambda d: d['in_draw']), c1c_counted=first(lambda d: c0 is not None and d['c1c'] != c0, ('counters',)),
                            a_turn=[a_turns[0][0], 'events-loop2'] if a_turns else None, anim_call=self.anim_calls[0] if self.anim_calls else None,
                            physics=self.phys_visits[0] if self.phys_visits else None, hits=self.hit_visits[0] if self.hit_visits else None),
                 switches=self.switches, ticks=ticks)
        return r


def clock_keys(r):
    """per tick, the state after the hit pass (the keys of birth_clock.json)"""
    out = []
    for x in r['ticks']:
        d = x['passes'].get('hits')
        if d is None:
            continue
        out.append(dict(k=x['k'], tick=x['tick'], call_no=sum(1 for t in r['anim_calls'] if t <= x['tick']), status=d['status'], cnt1c=d['c1c'], end_AC=d['end_AC'], loops_B0=d['loops_B0'], frame=d['frame'], cnt_A8=d['cnt_A8'],
                        anim_c=d['anim_c'], dir_c=d['dir_c'], adir=d['adir'], px=d['px'], py=d['py'], pz=d['pz'], hbx=d['hbx'], hby=d['hby'], hbz=d['hbz'],
                        bw=d['bw'], bd=d['bd'], bh=d['bh'], box1d4=d['box1d4']))
    return out


def experiments():
    E = []
    # ---- record births by a program of another entity (loop 2 of the events pass), the V6 H configurations, opcode 0x2D, the wave sprite (27) -------------------------------------
    E.append(Exp('REC-H1', op=0x2D, spawner_slot=1, note='spawner slot 1, nothing else: new slot 2 above the counter (1), stale trigger = template 0'))
    E.append(Exp('REC-H1-8A', op=0x8A, spawner_slot=1, note='as REC-H1, opcode 0x8A (position written after the creation)'))
    E.append(Exp('REC-H1-8B', op=0x8B, spawner_slot=1, note='as REC-H1, opcode 0x8B (position = spawner + offset, written after the creation)'))
    E.append(Exp('REC-H1-8A-COL', op=0x8A, spawner_slot=1, force_col=True, note='PROBE: as REC-H1-8A with +0x6C|0x80 and +0xB4&~0x80 forced at the creation: is a status-1 entity in the collision list at S?'))
    E.append(Exp('REC-H1-NAT', native=True, spawner_slot=1, note='as REC-H1, a native C handler calls 0x8003A1B8 (what C[95], C[98], C[102] do)'))
    E.append(Exp('REC-H1-SA', op=0x2D, spawner_slot=1, rec_A=1, note='as REC-H1, the record gives a bytecode program A (+0x4C = 1)'))
    E.append(Exp('REC-H2', op=0x2D, spawner_slot=1, filler=[4], free_stale={2: -1}, note='slot 2 free with stale trigger -1, slot 4 occupied (counter 4): new slot 2 <= counter'))
    E.append(Exp('REC-H3', op=0x2D, spawner_slot=3, filler=[2], note='spawner slot 3, slot 1 free (template 0 but <= counter: loop 1 writes -1): new slot 1 < spawner'))
    E.append(Exp('REC-H5', op=0x2D, spawner_slot=1, free_stale={2: -1}, note='slot 2 free with stale trigger -1, counter 1: new slot 2 above the counter'))
    E.append(Exp('REC-H6', op=0x2D, spawner_slot=1, dead_before=[2, 3], note='slots 2, 3 died last tick (status 4): the slot pass frees them (template 0): new slot 2'))
    E.append(Exp('REC-H6-8A', op=0x8A, spawner_slot=1, dead_before=[2, 3], note='as REC-H6 with 0x8A'))
    # the arrow (3) as the record sprite, for the V6 comparison (A handler, X)
    E.append(Exp('REC-H1-SPR3', op=0x2D, spr=3, spawner_slot=1, note='as REC-H1, record sprite = the arrow (3)'))
    E.append(Exp('REC-H2-SPR3', op=0x2D, spr=3, spawner_slot=1, filler=[4], free_stale={2: -1}, note='as REC-H2, record sprite = the arrow (3)'))
    # birth later in the map (warm-up ticks before S): the same configurations once the table has lived
    E.append(Exp('REC-H1-S20', op=0x2D, S=20, spawner_slot=1, note='as REC-H1, birth at tick 20 (loop 1 has visited slot 2 only if it was <= counter)'))
    # ---- switches inside the window (wave, REC-H1 configuration) ---------------------------------------------------------------------------------------------------------------------
    for lab, sw in (('REC-SW0', [(0, 1)]), ('REC-SW1', [(1, 1)]), ('REC-SW2', [(2, 1)]), ('REC-SW5', [(5, 1)]), ('REC-SW1-2', [(1, 1), (2, 0)]), ('REC-SWD1', [(1, 0, 16)]),
                    ('REC-SWD2', [(2, 0, 16)])):
        E.append(Exp(lab, op=0x2D, spawner_slot=1, switches=sw, note='REC-H1 with a switch %s (k, anim[, dir]) injected like e20-e0a5 SWITCH' % sw))
    for lab, sw in (('REC-H2-SW0', [(0, 1)]), ('REC-H2-SW1', [(1, 1)])):
        E.append(Exp(lab, op=0x2D, spawner_slot=1, filler=[4], free_stale={2: -1}, switches=sw, note='REC-H2 with a switch %s' % sw))
    # ---- map-event program (before the slot pass of tick S) and map load (before tick 1), for comparison ------------------------------------------------------------------------------
    E.append(Exp('REC-ME', scenario='mapev', op=0x2D, S=5, note='the hero runs a map-event program (0x8003C790) that executes 0x2D: birth before the slot pass of tick S'))
    E.append(Exp('REC-ME-8A', scenario='mapev', op=0x8A, S=5, note='as REC-ME with 0x8A'))
    E.append(Exp('REC-ME-SW0', scenario='mapev', op=0x2D, S=5, switches=[(0, 1)], note='REC-ME with animation 1 asked after the events pass of S'))
    E.append(Exp('REC-H2-S20', op=0x2D, S=20, spawner_slot=1, filler=[4], free_stale={2: -1}, note='as REC-H2, birth at tick 20: the free slot 2 (<= counter) was counted by the counters pass while free'))
    E.append(Exp('REC-LOAD', scenario='load', note='0x8003A1B8(0, 0, 0) before tick 1 (0x8003B308)'))
    E.append(Exp('HERO', scenario='hero', note='the hero arrival: InitializeEntity(slot 0, no record) then status 2, before tick 1 (0x80031974 inside the map load 0x8003B24C)'))
    E.append(Exp('REC-LOAD-SW1', scenario='load', switches=[(1, 1)], note='REC-LOAD with animation 1 asked before tick 1 + ... (k = tick - 0)'))
    return E


def summary_line(r):
    f = r['first']
    ck = clock_keys(r)
    S = r['S']
    end = next((x['k'] for x in ck if x['end_AC'] == 1), None)
    st4 = next((x['k'] for x in ck if x['status'] == 4), None)
    cnt = [(x['k'], x['cnt1c']) for x in ck if x['k'] is not None and -1 <= x['k'] <= 3]
    at = {}
    for t, trig, st, kind in r['program_turns']:
        at.setdefault(t - S, []).append('%s(st%d,%s)' % ('ABCDEF'[trig] if 0 <= trig < 6 else trig, st, kind[0]))
    boxchg = []
    for a, b in zip(ck, ck[1:]):
        if any(a[q] != b[q] for q in ('hbx', 'hby', 'hbz', 'bw', 'bd', 'bh')):
            boxchg.append(b['k'])
    rel = lambda v: (v - S) if isinstance(v, int) else (([v[0] - S, v[1]]) if v else None)
    return ('%-12s %-6s %-5s slot %-2s (spawner %s) stale trig %-3s c1c %-3s | S=%-3s birth in %-13s | turns %s | status2 %s upd %s col %s draw %s counters %s anim %s phys %s hits %s | cnt1c %s | end_AC k=%s st4 k=%s | box changes k=%s'
            % (r['label'], r['scenario'], r['op'] or '-', r['new_slot'], r['spawner_slot'], (r['stale_before_creation'] or {}).get('trig228'), (r['stale_before_creation'] or {}).get('c1c'),
               S, r['birth_pass'], dict(sorted((k, v) for k, v in at.items() if k <= 2)), rel(f['status2']), rel(f['in_update']), rel(f['in_collision']), rel(f['in_draw']),
               rel(f['c1c_counted']), rel(f['anim_call']), rel(f['physics']), rel(f['hits']), cnt, end, st4, boxchg[:8]))


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = []
    lines = ['# rec_tick.py: per row, k = tick - S (S = tick of the creation; 0 for REC-LOAD), first pass of participation, program turns of the new entity per k (letter, status at the turn, b/n = bytecode/native)']
    for e in experiments():
        e.run()
        r = e.row()
        r['clock'] = clock_keys(r)
        rows.append(r)
        lines.append(summary_line(r))
        print(lines[-1])
    with open(os.path.join(OUT, 'rec_tick.json'), 'w', newline='\n') as f:
        json.dump(dict(scenario='e0a4 record births', rows=rows), f, indent=None, sort_keys=True, separators=(',', ':'))
        f.write('\n')
    with open(os.path.join(OUT, 'rec_tick.txt'), 'w', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
