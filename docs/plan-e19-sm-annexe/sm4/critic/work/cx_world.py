"""Critic's own world builder + runner of the REAL handlers (own interpreter cx_mips).  Written from scratch."""
import cx_exe as X
from cx_mips import Mem, CPU, s32, s16, M32

ENT0 = 0x80127D30
STRIDE = 0x294
N_ADDR = 0x80134360
BUF = 0x8013D8D4            # 1-based match buffer: entry i at BUF + 4*i
STATE = 0x8013D9E8
RESULT = STATE + 0x2C
STATS_PTR = 0x80176318
STATS = 0x80176400
PAD = 0x80126F18
INS = 0x80150000
CELL = 0x80150100
PLATFORM_PTR = 0x80100123

F = dict(parent=(0x0C, 4), status=(0x10, 4), hp=(0x14, 4), hpMax=(0x18, 4), platform=(0x28, 4), refId=(0x48, 4),
         flags=(0x6C, 4), targetAnimId=(0x88, 4), targetDir=(0x8C, 4), animFlags=(0xB4, 4), riding=(0x12C, 4),
         xcoll=(0x130, 4), lastAnim=(0x26C, 4), lastDir=(0x270, 4))
SIGNED = {'hp', 'hpMax', 'refId', 'animFlags'}
STATF = dict(hp=(0, 2), hpMax=(2, 2), mp=(4, 2), mpMax=(6, 2), money=(8, 2))
PADF = dict(hold=0x10, pressed=0x12, released=0x14, interval=0x16)


def ent(i):
    return ENT0 + i * STRIDE


def ptr(slot):
    return 0 if slot is None or slot < 0 else ent(slot)


# their JSON slot keys -> my field names
JMAP = dict(status='status', hp='hp', hpMax='hpMax', refId='refId', flags='flags', animFlags='animFlags',
            targetAnimId='targetAnimId', targetDir='targetDir', lastTargetAnimId='lastAnim', lastTargetDir='lastDir')


def build(slots, stats, pad=None, ins=(), logic=0, owner=0, trap_slots=0):
    """slots: list of dict in THEIR key naming. returns (mem, args)."""
    m = Mem()
    n = len(slots)
    m.poke(N_ADDR, 4, n - 1)
    for i, s in enumerate(slots):
        a = ent(i)
        for k, f in JMAP.items():
            off, sz = F[f]
            m.poke(a + off, sz, s[k] & M32)
        m.poke(a + F['platform'][0], 4, PLATFORM_PTR if s['hasPlatform'] else 0)
        m.poke(a + F['parent'][0], 4, ptr(s['parentSlot']))
        m.poke(a + F['riding'][0], 4, ptr(s['ridingSlot']))
        m.poke(a + F['xcoll'][0], 4, ptr(s['xCollSlot']))
    m.poke(STATS_PTR, 4, STATS)
    for k, (o, sz) in STATF.items():
        m.poke(STATS + o, sz, stats[k] & 0xFFFF)
    if pad is not None:
        for k, o in PADF.items():
            m.poke(PAD + o, 2, pad[k])
    for i, b in enumerate(ins):
        m.poke(INS + i, 1, b)
    m.poke(CELL, 4, INS)
    m.poke(RESULT, 4, 7)
    return m, (ent(logic), ent(owner), CELL, STATE)


def hooks_noop():
    def noop(c):
        c.r[2] = 1
    return {0x8003C914: noop, 0x8003C840: noop}


def run(op, slots, stats, ins, logic=0, owner=0, pad=None, hooks=None):
    m, args = build(slots, stats, pad, ins, logic, owner)
    cpu = CPU(m, hooks if hooks is not None else hooks_noop())
    ret = cpu.run(X.handler(op), args)
    return m, cpu, s32(ret)


def snapshot(m, nslots):
    """final values of all the fields of interest."""
    out = {'slots': [], 'stats': {}, 'pad': {}}
    for i in range(nslots):
        a = ent(i)
        d = {}
        for f, (off, sz) in F.items():
            d[f] = m.peek(a + off, sz, f in SIGNED)
        out['slots'].append(d)
    for k, (o, sz) in STATF.items():
        out['stats'][k] = m.peek(STATS + o, sz, True)
    for k, o in PADF.items():
        out['pad'][k] = m.peek(PAD + o, 2)
    out['result'] = m.peek(RESULT, 4)
    return out


def written_addresses(m):
    return sorted({a for (a, n, v) in m.writes for a in range(a, a + n)})
