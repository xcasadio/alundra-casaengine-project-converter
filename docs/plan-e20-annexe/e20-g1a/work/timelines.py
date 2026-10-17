"""timelines.py - iterates UpdatePlayerCarriedEntity (REAL code vs model) for the gesture of the lift (mode 0) and the swallowing in a lock (mode 1). Usage: python -B timelines.py"""
import g1a_models as M
from h import World, Ent, ENT0, STRIDE, M32, s32
w = World(); m = w.m
SHEET = 0x80141000
def run(mode_seq, off, attack=1, n=40):
    hero = Ent(); hero.set32(M.POSX, 100 << 16); hero.set32(M.POSY, 100 << 16); hero.set32(M.POSZ, 0)
    obj = Ent(); obj.set32(M.CARRIER, ENT0); obj.set32(M.SHEET, SHEET)
    m.wb(SHEET + 0xE, attack)
    ox, oy, oz = off
    obj.set32(M.POSX, (100 + ox) << 16 & M32); obj.set32(M.POSY, (100 + oy) << 16 & M32); obj.set32(M.POSZ, oz << 16 & M32)
    hero.set32(M.CARRIED, ENT0 + 3 * STRIDE)
    hero.set32(M.OFFX, ox << 16 & M32); hero.set32(M.OFFY, oy << 16 & M32); hero.set32(M.OFFZ, oz << 16 & M32)     # the offsets TryLift / the last tick left
    w.put(0, hero); w.put(3, obj)
    m.ww(M.CTR, 0)
    mh, mo = hero.copy(), obj.copy(); mem = {'ctr': 0}
    rows = []
    destroyed = None
    for t in range(1, n + 1):
        w.call(0x8002F884, mode_seq(t))
        # MoveEntity of the carried object places it at hero + offset (the physics pass): emulate it with the model for the next tick's position
        M.upce(mode_seq(t), mh, mo if mh.u32(M.CARRIED) else None, attack, mem, [])
        rh, ro = w.get(0), w.get(3)
        assert bytes(rh.raw) == bytes(mh.raw) and bytes(ro.raw) == bytes(mo.raw) and m.rw(M.CTR) == mem['ctr'], 'real != model at tick %d' % t
        # the physics pass (MoveEntity 0x80037E34, carried branch): position := hero + offset
        for e_ in (ro, mo):
            e_.set32(M.POSX, (rh.u32(M.POSX) + rh.u32(M.OFFX)) & M32); e_.set32(M.POSY, (rh.u32(M.POSY) + rh.u32(M.OFFY)) & M32); e_.set32(M.POSZ, (rh.u32(M.POSZ) + rh.u32(M.OFFZ)) & M32)
        w.put(3, ro); mo.raw = bytearray(ro.raw)
        if ro.u32(M.STATUS) == 4 and destroyed is None:
            destroyed = t
        rows.append((t, rh.s32(M.OFFX) / 65536, rh.s32(M.OFFY) / 65536, rh.s32(M.OFFZ) / 65536))
    return rows, destroyed
if __name__ == '__main__':
    print('MODE 0 (every free tick): offsets (x, y, z) in px after tick t, obj at the oracle geometry')
    for name, off in (('east  (+21, 0, 0)', (21, 0, 0)), ('west  (-21, 0, 0)', (-21, 0, 0)), ('north (0, -15, 0)', (0, -15, 0)), ('south (0, +15, 0)', (0, 15, 0)), ('on the head (0,0,32)', (0, 0, 32))):
        rows, _ = run(lambda t: 0, off)
        xs = [r for r in rows if r[0] in (1, 11, 12, 13, 21, 32, 33)]
        first_x = next((r[0] for r in rows if abs(r[1]) < abs(off[0]) or abs(r[2]) < abs(off[1])), None)
        print('  %-22s first tick where X/Y moves: %s ; ticks to (0,0,32): %s ; samples %s' % (name, first_x, next((r[0] for r in rows if r[1:] == (0.0, 0.0, 32.0)), None), [(r[0], r[1], r[2], r[3]) for r in xs]))
    print('MODE 1 (locked control, every tick): the object at (0,0,32) over the head')
    for att in (1, 0):
        rows, d = run(lambda t: 1, (0, 0, 32), attack=att, n=60)
        print('  AttackCount=%d: destroyed (status 4) at call number %s ; offsets sampled %s' % (att, d, [(r[0], r[3]) for r in rows if r[0] in (5, 6, 7, 21, 22, 23)]))
    print('MODE 2 (hurt, slope 4, death) once, then MODE 0: offsets stay what they were, +0x24 = -1 set')
