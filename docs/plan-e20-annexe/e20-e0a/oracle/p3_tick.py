"""p3_tick.py - E20.e0a binary semantics, probe P3 (scratch only): WHAT A FRESH ENTITY RUNS IN ITS BIRTH TICK, by spawn site and slot.
Real code: SpawnWarpEntity 0x80039F58 and everything it calls, the slot pass 0x80038634, the events pass 0x800386D0 (loop 1 + loop 2), the counters pass 0x80038998,
the list pass 0x800384F4, the animation pass 0x80038E18, the physics pass 0x80038364, and (loop-1 case) the real DestroyEntity 0x8003A59C + the real drop 0x80032B90.
Stubs: MovePlayer 0x80031B50 (a hook that does the spawn or nothing), the C handler of the 'spawner' (a hook that does the spawn), the A handler 0x8006174C (counts calls),
effects 0x8003BDD8 / 0x8003BE74, sound 0x800490FC, passes 0x80038E84 and 0x80039300 (machine.py stubs).
Occupied filler slots are status 5 (loop 1: trigger -1; not in the lists; counted by the slot pass).
Everything printed is read from RAM after the real code ran."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import exp_spawn as E
import exp_spawn2 as E2
import c1world as C
from machine import M32, s32

ent, slot_of, RNG = E.ent, E.slot_of, E.RNG
TEMPLATE = 0x80134368
A0 = 0x8006174C           # A[0] handler (sprite 3's A code is 0)
A255 = 0x80061BD4         # A[255] handler (item sprites)
FAKE_C = 0x8008FF00
CTAB = 0x800C4F34
UPD, UPDN = 0x80127B28, 0x80127D28
COL, COLN = 0x80127C28, 0x80127D2C
DRW, DRWN = 0x80134250, 0x8013435C


def lst(m, base, cnt):
    return [m.peek(base + 4 * i) for i in range(m.peek(cnt))]


def make_free(m, i, trig):
    m.zero(ent(i), 0x294)
    m.poke(ent(i), i)
    m.poke(ent(i) + 0x48, 0xFFFFFFFF)
    m.poke(ent(i) + 0x228, trig & M32)


def make_fill(m, i, status=5):
    make_free(m, i, -1)
    m.poke(ent(i) + 0x10, status)


class Case:
    def __init__(self, title):
        self.title = title
        self.pr = E2.make()
        self.m = self.pr.m
        self.m.hooks[0x80031B50] = lambda mm: None               # MovePlayer: nothing unless the case replaces it
        self.counts = {}
        self.box = {}
        for a in (A0, A255):
            self.m.hooks[a] = self._counter(a)
        for i in range(1, 64):                                   # clean table: every slot free, trigger 0 (template) unless the case says otherwise
            make_free(self.m, i, 0)

    def _counter(self, addr):
        def f(mm):
            p = mm.r[4] & M32
            self.counts[p] = self.counts.get(p, 0) + 1
            mm.r[2] = 0
        return f

    def spawn_in_hook(self, mm, idx=3, label='new'):
        sp = (mm.r[29] - 0x800) & ~7
        mm.poke(sp + 0x10, 100 << 16)
        mm.poke(sp + 0x14, 40 << 16)
        mm.poke(sp + 0x18, 0x10)
        p = C.side_call(mm, 0x80039F58, (ent(0), 0, idx, 190 << 16), sp)
        self.box[label] = p
        if p:
            self.box[label + '_trig_at_spawn'] = mm.peeks(p + 0x228)
            self.box[label + '_serial'] = mm.peek(p + 4)
            mm.poke(p + 0xC4, 3 << 16)                           # ForceX = 3 px: shows whether the physics pass moves it in the birth tick
        mm.r[2] = 0
        return p

    def role_spawner(self, slot, c_code=200):
        """a status-2 entity with a C handler = hook that spawns"""
        m = self.m
        p = ent(slot)
        # real spawn of an arrow then overwritten to be an inert 'spawner'
        m.poke(CTAB + 4 * c_code, FAKE_C)
        m.hooks[FAKE_C] = lambda mm: self.spawn_in_hook(mm) if (mm.r[4] & M32) == p else None
        return p

    def tick(self, name, extra=None):
        m = self.m
        out = {}
        PASS = [(0x80038634, 'slots'), (0x800386D0, 'events'), (0x80038998, 'counters'), (0x800384F4, 'lists'), (0x80038E18, 'anim'), (0x80038364, 'physics')]
        for a, nm in PASS:
            m.call(a, limit=5_000_000)
            p = self.box.get('new')
            if p:
                out[nm] = dict(status=m.peek(p + 0x10), trig=m.peeks(p + 0x228), c1c=m.peeks(p + 0x1C), delay=m.peeks(p + 0xA8), posx=m.peeks(p + 0x114) >> 16,
                               inupd=p in lst(m, UPD, UPDN), incol=p in lst(m, COL, COLN), indraw=p in lst(m, DRW, DRWN), a_runs=self.counts.get(p, 0), count=m.peeks(0x80134360))
                if extra:
                    extra(nm, out[nm])
        return out


def show(case, t1, t2):
    p = case.box.get('new')
    print('\n=== %s' % case.title)
    if not p:
        print('  no spawn happened'); return
    print('  new entity in slot %d ; trigger word at spawn (before loop 1/2 see it) = %s ; A-handler runs in tick 1 = %d, in tick 2 = %d' % (
        slot_of(p), case.box.get('new_trig_at_spawn'), t1['physics']['a_runs'], t2['physics']['a_runs'] - t1['physics']['a_runs']))
    for nm in ('events', 'counters', 'lists', 'anim', 'physics'):
        d = t1[nm]
        print('   tick1 after %-8s status=%d trigger=%2d +0x1C=%d delay(+0xA8)=%d posX=%d inUpdateList=%s inCollisionList=%s inDrawList=%s count=%d' % (
            nm, d['status'], d['trig'], d['c1c'], d['delay'], d['posx'], d['inupd'], d['incol'], d['indraw'], d['count']))
    d = t2['physics']
    print('   tick2 end        status=%d trigger=%2d +0x1C=%d delay=%d posX=%d inUpdateList=%s inDrawList=%s' % (d['status'], d['trig'], d['c1c'], d['delay'], d['posx'], d['inupd'], d['indraw']))


def run(case):
    t1 = case.tick('t1')
    t2 = case.tick('t2')
    show(case, t1, t2)


def case_moveplayer(title, filler, free_stale=None, upto=0):
    """spawn made inside MovePlayer; 'filler' = slots occupied by inert entities (status 5) BEFORE the tick; free_stale = {slot: trigger} for free slots"""
    c = Case(title)
    m = c.m
    for i in filler:
        make_fill(m, i)
    for i, t in (free_stale or {}).items():
        make_free(m, i, t)
    m.hooks[0x80031B50] = lambda mm: c.spawn_in_hook(mm, label='new')
    # only the first MovePlayer call spawns (tick 1)
    first = {'done': False}
    inner = m.hooks[0x80031B50]

    def once(mm):
        if not first['done']:
            first['done'] = True
            inner(mm)
    m.hooks[0x80031B50] = once
    return c


def case_loop2(title, spawner_slot, filler, free_stale=None, dead_before=()):
    """spawn made by the C handler of an entity of slot 'spawner_slot' (second loop of the events pass)"""
    c = Case(title)
    m = c.m
    for i in filler:
        make_fill(m, i)
    # the spawner: real arrow spawn lands in the first free slot, so occupy below it first, then free them again
    for i in range(1, spawner_slot):
        if m.peek(ent(i) + 0x10) == 0:
            make_fill(m, i, status=5)
    sp = c.pr.spawn(ent(0), 0, 3, 120 << 16, 100 << 16, 0, 0x10)
    assert slot_of(sp) == spawner_slot, (slot_of(sp), spawner_slot)
    m.poke(sp + 0x10, 2)
    m.poke(sp + 0x78, 200)                                       # C code 200 -> table C[200] = FAKE
    m.poke(sp + 0x80, 0)                                         # no E handler
    m.poke(sp + 0x6C, 0)                                         # no flags that turn it into status 3
    for i in range(1, spawner_slot):                              # restore the temporary fillers to free (trigger 0 = template) unless the case keeps them
        if i not in filler:
            make_free(m, i, 0)
    for i, t in (free_stale or {}).items():
        make_free(m, i, t)
    m.poke(CTAB + 4 * 200, FAKE_C)
    fired = {'n': 0}

    def hook(mm):
        if (mm.r[4] & M32) == sp and fired['n'] == 0:
            fired['n'] = 1
            c.spawn_in_hook(mm, label='new')
        mm.r[2] = 0
    m.hooks[FAKE_C] = hook
    for i in dead_before:                                         # entities that died last tick (status 4): the slot pass of this tick frees them
        make_fill(m, i, status=4)
    return c


def case_loop1(title, dying_slot, filler, free_stale=None):
    """spawn made in loop 1 of the events pass: slot 'dying_slot' holds an entity that auto-destroys (flags bit 20 with +0x18C == 4) and drops a money item (real DestroyEntity + real drop)"""
    c = Case(title)
    m = c.m
    for i in filler:
        make_fill(m, i)
    for i in range(1, dying_slot):
        if m.peek(ent(i) + 0x10) == 0:
            make_fill(m, i, status=5)
    dp = c.pr.spawn(ent(0), 0, 3, 120 << 16, 100 << 16, 0, 0x10)
    assert slot_of(dp) == dying_slot, (slot_of(dp), dying_slot)
    m.poke(dp + 0x10, 2)
    m.poke(dp + 0x6C, m.peek(dp + 0x6C) | 0x100000)
    m.poke(dp + 0x18C, 4)
    m.poke(dp + 0x3C, 69)                                        # loot item 69 (money): the real drop 0x80032B90 spawns sprite 0x1E+69 = 99
    for i in range(1, dying_slot):
        if i not in filler:
            make_free(m, i, 0)
    for i, t in (free_stale or {}).items():
        make_free(m, i, t)
    # the drop's sound/message helpers are stubbed like in exp_spawn2.x4
    def setr2(mm):
        mm.r[2] = 0
    for a in (0x800450F0, 0x8004507C, 0x80049B7C, 0x8005A9E0):
        m.hooks[a] = setr2
    # find the new item entity after the events pass: the slot that is status 1/2 and id 99
    orig_tick = c.tick

    def tick(name, extra=None):
        out = orig_tick(name, extra)
        return out
    # tell the case where to look: the 'new' entity is the entity with id +0x68 == 99; set after the events pass by a pass wrapper
    real_events = 0x800386D0

    def watch(mm):
        pass
    c.dying = dp

    def find_new():
        for i in range(1, 64):
            if m.peek(ent(i) + 0x68) == 99 and m.peek(ent(i) + 0x10) in (1, 2):
                return ent(i)
        return None
    c.find_new = find_new
    return c


def run_loop1(c):
    m = c.m
    PASS = [(0x80038634, 'slots'), (0x800386D0, 'events'), (0x80038998, 'counters'), (0x800384F4, 'lists'), (0x80038E18, 'anim'), (0x80038364, 'physics')]
    res = []
    for t in (1, 2):
        out = {}
        for a, nm in PASS:
            m.call(a, limit=5_000_000)
            if nm == 'events':
                c.box['new'] = c.find_new()
                # trigger at spawn is not observable after the pass; report the final
            p = c.box.get('new')
            if p:
                out[nm] = dict(status=m.peek(p + 0x10), trig=m.peeks(p + 0x228), c1c=m.peeks(p + 0x1C), delay=m.peeks(p + 0xA8), posx=m.peeks(p + 0x114) >> 16,
                               inupd=p in lst(m, UPD, UPDN), incol=p in lst(m, COL, COLN), indraw=p in lst(m, DRW, DRWN), a_runs=c.counts.get(p, 0), count=m.peeks(0x80134360))
        res.append(out)
    if not c.box.get('new'):
        print('\n=== %s\n  no item appeared (drop returned 0?)' % c.title); return
    show(c, res[0], res[1])
    print('  dying entity (slot %d) status after tick 1 = %d' % (slot_of(c.dying), m.peek(c.dying + 0x10)))


def main():
    # ---- MovePlayer spawns ---------------------------------------------------------------------------------------------------------------------------------
    run(case_moveplayer('M1  MovePlayer spawn, table empty (hero only), new slot 1 > count 0', filler=[]))
    run(case_moveplayer('M2  MovePlayer spawn, slots 2,3 occupied (status 5), slot 1 free (trigger 0): new slot 1 < count 3', filler=[2, 3]))
    run(case_moveplayer('M3  MovePlayer spawn, slot 1 occupied, slot 2 free: new slot 2 > count 1', filler=[1]))
    # ---- loop-2 spawns ------------------------------------------------------------------------------------------------------------------------------------
    run(case_loop2('H1  loop-2 spawn, spawner in slot 1, no other entity: new slot 2 > spawner, > count (template trigger 0 in the free slot)', 1, filler=[]))
    run(case_loop2('H2  loop-2 spawn, spawner in slot 1, slot 2 free with STALE trigger -1, slot 4 occupied (count 4): new slot 2 > spawner, <= count', 1, filler=[4], free_stale={2: -1}))
    run(case_loop2('H3  loop-2 spawn, spawner in slot 3, slot 1 free: new slot 1 < spawner', 3, filler=[2]))
    run(case_loop2('H4  loop-2 spawn, spawner in slot 1, slot 2 free, slot 3 free, nothing above: new slot 2 (template trigger 0) - SAME as H1 but with a dead neighbour', 1, filler=[], dead_before=[]))
    run(case_loop2('H5  loop-2 spawn, spawner in slot 1, slot 2 free with STALE trigger -1 (loop 1 visited it in an earlier tick), count = 1 (nothing above)', 1, filler=[], free_stale={2: -1}))
    run(case_loop2('H6  loop-2 spawn, spawner in slot 1, slots 2,3 hold entities that died last tick (status 4): they are freed by the slot pass; first free slot = 2', 1, filler=[], dead_before=[2, 3]))
    # ---- loop-1 spawns (real DestroyEntity + real drop) -----------------------------------------------------------------------------------------------------
    for title, dslot, fill, stale in (
            ('L1  loop-1 spawn: dying entity in slot 1, nothing else: item lands in slot 2 (> dying slot, > count)', 1, [], None),
            ('L2  loop-1 spawn: dying entity in slot 3, slot 1 free: item lands in slot 1 (< dying slot)', 3, [2], None),
            ('L3  loop-1 spawn: dying entity in slot 1, slot 2 free (template 0), slot 4 occupied (count 4): item lands in slot 2 (loop 1 will still visit it)', 1, [4], None)):
        c = case_loop1(title, dslot, fill, stale)
        run_loop1(c)


if __name__ == '__main__':
    main()


def case_mapevent(title, filler, dead=()):
    """spawn made by the map-event pass 0x8003C67C, i.e. BEFORE the slot pass 0x80038634 of the tick (the real map-event opcodes use the record spawner 0x8003A1B8; here the record-less
    SpawnWarpEntity is called at the same moment to show what the passes do with a status-1 entity that exists before the slot pass).  'dead' = slots with status 4 (died last tick)."""
    c = Case(title)
    m = c.m
    for i in filler:
        make_fill(m, i)
    for i in dead:
        make_fill(m, i, status=4)
    p = c.pr.spawn(ent(0), 0, 3, 190 << 16, 100 << 16, 40 << 16, 0x10)
    c.box['new'] = p
    c.box['new_trig_at_spawn'] = m.peeks(p + 0x228)
    m.poke(p + 0xC4, 3 << 16)
    return c


if __name__ == '__main__' and len(sys.argv) > 1 and sys.argv[1] == 'map':
    pass
