"""exp_spawn2.py - e0-spawn probe, part 2 (scratch only). Same machine as exp_spawn.py, now with the engine's slot initialisation reproduced (index word of every slot, template
word +0x48 = -1 as 0x8003B218-0x8003B21C writes it) and four experiments on WHEN a spawned entity takes part in the tick:
  X1 spawn made inside MovePlayer (before the first loop of the events pass 0x800386D0),
  X2 spawn made by a handler in the second loop (higher slot, then lower slot),
  X3 loot roll at creation (sprite header Contents) with the RNG draws counted,
  X4 SpawnEntityContents 0x80032B90 (the drop) and the item entity C255 for one tick sequence.
Everything printed is read from RAM after the real code ran."""
import os
import sys
import json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import exp_spawn as E
import world as W
import c1world as C
from machine import M32, s32

ent, slot_of, RNG = E.ent, E.slot_of, E.RNG
PASSES = [(0x80038634, 'slots'), (0x800386D0, 'events'), (0x80038998, 'counters'), (0x800384F4, 'lists'), (0x80038E18, 'anim'), (0x80038364, 'physics')]
TEMPLATE = 0x80134368


def init_slots(m):
    """what the engine's init leaves: template (zeros, +0x48 = -1), every slot = template + its index; the hero is re-placed afterwards by the caller"""
    m.zero(TEMPLATE, 0x294)
    m.poke(TEMPLATE + 0x48, 0xFFFFFFFF)
    for i in range(64):
        m.poke(ent(i), i)


def make():
    pr = E.Probe(indices=(3, 4, 7, 27, 2, 1, 236) + tuple(range(0x1E + 36, 0x1E + 87)))
    m = pr.m
    init_slots(m)
    # the hero was placed by Probe before init_slots wrote the index words: restore its index (0) = nothing to do; slot 0 index is 0
    return pr


def brief(m, p, label):
    g = lambda o: m.peeks(p + o)
    return '%s slot %s: status %d trigger %d anim_target %d anim_cur %s AIValues[0]=%d PosZ=%s' % (label, slot_of(p), m.peek(p + 0x10), g(0x228), g(0x88), hex(m.peek(p + 0x90)), s32(m.peek(p + 0x280) & 0xFFFF), g(0x11C))


def run_passes(m, p, names=None, label='new'):
    for a, name in PASSES:
        m.call(a, limit=5_000_000)
        print('   after %-8s: %s' % (name, brief(m, p, label)))


def x1():
    print('\n=== X1  spawn made INSIDE MovePlayer (hooked: the hero does nothing else) at Z = 40 px, then the real tick passes')
    pr = make()
    m = pr.m
    box = {}

    def move_player(mm):
        raw = mm
        box['p'] = C.side_call(raw, 0x80039F58, (ent(0), 0, 3, 150 << 16), (raw.r[29] - 0x800) & ~7) if False else None
    # the hook gets the machine in the middle of the events pass: use a side call with a private stack
    sp_holder = {}

    def hook(mm):
        sp = (mm.r[29] - 0x800) & ~7
        mm.poke(sp + 0x10, 100 << 16)
        mm.poke(sp + 0x14, 40 << 16)
        mm.poke(sp + 0x18, 0x10)
        box['p'] = C.side_call(mm, 0x80039F58, (ent(0), 0, 3, 150 << 16), sp)
        mm.r[2] = 0
    m.hooks[0x80031B50] = hook
    # slots pass first (frees nothing), then events with the hooked MovePlayer
    m.call(0x80038634)
    m.call(0x800386D0, limit=5_000_000)
    p = box['p']
    print('   spawned in slot', slot_of(p), 'status after the events pass:', m.peek(p + 0x10), '(1 = Loaded at creation, 2 = Normal)')
    print('  ', brief(m, p, 'after events'))
    for a, name in PASSES[2:]:
        m.call(a, limit=5_000_000)
        print('   after %-8s: %s' % (name, brief(m, p, 'new')))
    print('   second tick:')
    for a, name in PASSES:
        m.call(a, limit=5_000_000)
    print('  ', brief(m, p, 'after tick 2'))


def x2():
    print('\n=== X2  spawn made by a handler of the second loop of the events pass')
    for label, spawner_slot, free_first in (('higher slot', 1, None), ('lower slot (slot 1 free, spawner in slot 3)', 3, 1)):
        pr = make()
        m = pr.m
        # entities: slots 1..3 are arrows (global 3) made by the real spawn; the spawner gets a fake C handler
        ps = [pr.spawn(ent(0), 0, 3, (150 + 10 * k) << 16, 100 << 16, 40 << 16, 0x10) for k in range(3)]
        for p in ps:
            m.poke(p + 0x10, 2)                               # Normal
        FAKE = 0x8008FF00
        k = 200                                               # an unused C code
        base = 0x800C4F34                                     # table C
        m.poke(base + 4 * k, FAKE)
        sp_ent = ent(spawner_slot)
        m.poke(sp_ent + 0x78, k)                              # C code of the spawner
        m.poke(sp_ent + 0x64, m.peek(sp_ent + 0x64))
        if free_first is not None:
            m.poke(ent(free_first) + 0x10, 4)                 # destroyed; the slot pass of this very tick frees it
        box = {}

        def hook(mm):
            sp = (mm.r[29] - 0x800) & ~7
            mm.poke(sp + 0x10, 100 << 16)
            mm.poke(sp + 0x14, 40 << 16)
            mm.poke(sp + 0x18, 0x10)
            box['p'] = C.side_call(mm, 0x80039F58, (ent(0), 0, 4, 190 << 16), sp)
            box['trig_at_spawn'] = mm.peeks(box['p'] + 0x228)
            mm.r[2] = 0
        m.hooks[FAKE] = hook
        m.hooks[0x80031B50] = lambda mm: None                 # no MovePlayer
        print('  ---', label)
        m.call(0x80038634)
        m.call(0x800386D0, limit=5_000_000)
        p = box['p']
        print('   spawn went to slot', slot_of(p), '; trigger word right after InitializeEntity (template) =', box['trig_at_spawn'])
        print('  ', brief(m, p, 'after events'))
        for a, name in PASSES[2:]:
            m.call(a, limit=5_000_000)
        print('  ', brief(m, p, 'after the whole tick 1'))
        for a, name in PASSES:
            m.call(a, limit=5_000_000)
        print('  ', brief(m, p, 'after tick 2'))


def x3():
    print('\n=== X3  loot roll at creation: a MAP-table sprite whose header Contents = 252 (Beannoide of map 14) and 251 (Muruta of map 115); and a global sprite with Contents 0')
    pr = make()
    m = pr.m
    # per-map table block: install the two records of the maps as map sprites (a1 = 1)
    d14 = json.load(open(E.DATA + '/map_14.json', encoding='utf-8-sig'))['SpriteInfo']['SpriteRecords'][79]
    d115 = json.load(open(E.DATA + '/map_115.json', encoding='utf-8-sig'))['SpriteInfo']['SpriteRecords'][91]
    import bank
    MP = 0x801EE800
    a14, a115 = 0x801FA000, 0x801FC000
    bank.install(m, a14, a14, d14)
    bank.install(m, a115, a115, d115)
    m.poke(E.MT + 0xC, MP)
    m.poke(E.MT + 0x10, 257)
    for k in range(257):
        m.poke(MP + 4 * k, 0xFFFFFFFF)
    m.poke(MP + 4 * 79, a14)
    m.poke(MP + 4 * 91, a115)
    for name, idx, ismap in (('global arrow (Contents 0)', 3, 0), ('map-14 Beannoide idx 79 (Contents %d)' % d14['Header']['Contents'], 79, 1), ('map-115 Muruta idx 91 (Contents %d)' % d115['Header']['Contents'], 91, 1)):
        before = m.peek(RNG)
        p = pr.spawn(ent(0), ismap, idx, 150 << 16, 100 << 16, 0, 0x10)
        after = m.peek(RNG)
        mult = 0x7D2B89DD; add = 0xE06A02E7
        n = 0
        s = before
        while s != after and n < 6:
            s = (s * mult + add) & M32
            n += 1
        print('  %-48s slot %s loot item +0x3C = %3d  flag +0x40 = %d  id +0x68 = 0x%X  RNG draws = %d' % (name, slot_of(p), m.peek(p + 0x3C), m.peek(p + 0x40), m.peek(p + 0x68), n if s == after else -1))


if __name__ == '__main__':
    which = sys.argv[1:] or ['x1', 'x2', 'x3']
    for w in which:
        globals()[w]()


def stats_snapshot(m):
    p = m.peek(0x80176318)
    return dict(hp=m.peeks(ent(0) + 0x14), hpmax=m.peeks(ent(0) + 0x18), money=m.peek(p + 8) & 0xFFFF, mp=m.peek(p + 4) & 0xFFFF, mpmax=m.peek(p + 6) & 0xFFFF)


def x4(item=69, at_hero=False, ticks=700, verbose_ticks=40, hp_before=None, mpmax=None):
    print('\n=== X4  drop of item %d by the real 0x80032B90 and the real C255 handler 0x8007C174 (%s), %d ticks' % (item, 'on the hero' if at_hero else 'away from the hero', ticks))
    pr = make()
    m = pr.m
    journal = []
    for a in (0x800450F0, 0x8004507C, 0x80049B7C, 0x8005A9E0):
        m.hooks[a] = (lambda aa: (lambda mm: (journal.append((mm.tick_no, hex(aa), [mm.r[4] & M32, mm.r[5] & M32]), None)[1], setattr_r2(mm))))(a)
    m.hooks[0x80031B50] = lambda mm: None
    if mpmax is not None:
        sp = m.peek(0x80176318)
        m.poke16(sp + 6, mpmax)
    if hp_before is not None:
        m.poke(ent(0) + 0x14, hp_before)
    x, y = (100, 100) if at_hero else (150, 100)
    dying = pr.spawn(ent(0), 0, 3, x << 16, y << 16, 0, 0x10)
    m.poke(dying + 0x10, 2)
    m.poke(dying + 0x3C, item)
    m.poke(dying + 0x40, 0x0123 if False else 0)
    s0 = stats_snapshot(m)
    n_before = sum(1 for i in range(1, 64) if m.peek(ent(i) + 0x10))
    ret = m.call(0x80032B90, dying, limit=5_000_000)
    p = None
    for i in range(1, 64):
        if i != slot_of(dying) and m.peek(ent(i) + 0x10) == 1:
            p = ent(i)
    print('  0x80032B90 returned', ret, '; the item entity is in slot', slot_of(p) if p else None, '; sound journal:', [j for j in m.journal if j[1] == 'sound'])
    if not p:
        return
    g = lambda o: m.peeks(p + o)
    print('  fields: id68=%d parent=%s ForceZ(+0xB8)=0x%X +0x274=%d flags6c=%s +0x278=%d +0x27C=%d +0x280=%d +0x284=0x%X pos=(%d,%d,%d) hdr codes=%s' % (g(0x68), slot_of(m.peek(p + 0xC)), g(0xB8), g(0x274), hex(m.peek(p + 0x6C)), g(0x278), g(0x27C), g(0x280), m.peek(p + 0x284), g(0x114) >> 16, g(0x118) >> 16, g(0x11C), [m.peek(p + 0x70 + 4 * i) for i in range(6)]))
    seen = []
    last = None
    for t in range(1, ticks + 1):
        m.tick_no = t
        for a, name in PASSES:
            m.call(a, limit=5_000_000)
        if m.peek(p + 0x10) == 0 or m.peek(p + 0x10) == 4:
            seen.append((t, 'status', m.peek(p + 0x10)))
            if m.peek(p + 0x10) == 4:
                print('  tick %d: item status 4 (destroyed); stats %s' % (t, stats_snapshot(m)))
                break
        cur = (g(0xB8), g(0x27C), g(0x278), g(0x284), g(0x1CC), g(0x144))
        if t <= verbose_ticks or cur[1] != (last[1] if last else None) or cur[3] != (last[3] if last else None) or cur[4] != (last[4] if last else None) and (cur[2] in (0x78, 0x77)):
            print('  tick %3d: PosZ=%8d ForceZ=%8d onGround=%d ItemState=%d lifetime=%d bounce284=0x%X blink1CC=%d status=%d' % (t, g(0x11C), g(0xB8), g(0x144), g(0x27C), g(0x278), m.peek(p + 0x284), g(0x1CC), m.peek(p + 0x10)))
        last = cur
    print('  journal of the message helpers:', journal[:6])
    print('  stats before', s0, 'after', stats_snapshot(m), 'sounds:', [j[2] for j in m.journal if j[1] == 'sound'])


def setattr_r2(mm):
    mm.r[2] = 0
