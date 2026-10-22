"""E19.sm4: memory layout, address -> logical field -> DLL member mapping, world writer and the python models of the
five opcodes (0x72 0x88 0x96 0x97 0xB3) and of the three zero-site ones (0x15 0x66 0x7A), written from the disassembly of
docs/plan-e19-sm-annexe/sm1-sm4/work/listings and of tools/disas.py (copy). Read-only use of ALUN_CD.EXE (France).

Used by gen_vectors.py. The models are only an agreement check: the expected values of a vector are what the REAL handler of
the binary did in the interpreter (temu.py), never what a model says.
"""
import struct

import lib

M32 = 0xFFFFFFFF

ENT0 = 0x80127D30
STRIDE = 0x294
NSLOTS_WRITTEN = 12          # slots 0..11 are written (slots above N are traps that must be ignored)
NADDR = 0x80134360
RES = 0x8013D8D8             # first result word of the 1-based match buffer at 0x8013D8D4
STATE = 0x8013D9E8
CODE = 0x80150000
PCP = 0x80150100
PSTATS_PTR = 0x80176318
PSTATS = 0x80176400
PAD = 0x80126F18
TABLE = 0x80098FAC
STATUS_TABLE = 0x80023D2C
SEARCH_SWITCH = 0x80023C30
STACK_LO, STACK_HI = 0x801F0000, 0x80200000
LO, HI = 0x80100000, 0x80190000   # window diffed before/after (the stack is above it)
SENTINEL = 0x5A5A5A5A

# offset -> (logical field, DLL member, kind)
SLOT_FIELDS = {
    0x0C: ('parentSlot', 'EntityScriptProxy.ParentEntity (Entity?; the pointed slot\'s LogicContextEntity)', 'slotref'),
    0x10: ('status', 'EntityScriptProxy.Status (EntityStatus, 0..5)', 'u32'),
    0x14: ('hp', 'EntityScriptProxy.Hp (int)', 's32'),
    0x18: ('hpMax', 'EntityScriptProxy.HpMax (int)', 's32'),
    0x28: ('hasPlatform', 'EntityScriptProxy.PlatformEntity (Entity?, non-null = true)', 'ptrbool'),
    0x48: ('refId', 'EntityScriptProxy.EntityRefId (int; the hero is -1, WorldProxy.cs:2111)', 's32'),
    0x6C: ('flags', 'EntityScriptProxy.Flags (uint)', 'u32'),
    0x88: ('targetAnimId', 'EntityScriptProxy.TargetAnimationId (uint)', 'u32'),
    0x8C: ('targetDir', 'EntityScriptProxy.TargetDirection (uint)', 'u32'),
    0xB4: ('animFlags', 'EntityScriptProxy.AnimFlags (int)', 's32'),
    0x12C: ('ridingSlot', 'EntityScriptProxy.RidingEntity (Entity?; the pointed slot\'s LogicContextEntity)', 'slotref'),
    0x130: ('xCollSlot', 'EntityScriptProxy.XCollisionEntity (EntityScriptProxy?)', 'slotref'),
    0x26C: ('lastTargetAnimId', 'EntityScriptProxy.LastTargetAnimationId (uint)', 'u32'),
    0x270: ('lastTargetDir', 'EntityScriptProxy.LastTargetDirection (uint)', 'u32'),
}
SLOT_OFF_BY_NAME = {v[0]: k for k, v in SLOT_FIELDS.items()}

STATE_FIELDS = {
    0x00: ('cursor', 'EventProgramState.CodeIndex (the binary keeps a pointer; the DLL an index into Codes)'),
    0x04: ('params[0]', 'EventProgramState.Parameters[0]'),
    0x08: ('params[1]', 'EventProgramState.Parameters[1]'),
    0x0C: ('params[2]', 'EventProgramState.Parameters[2]'),
    0x10: ('params[3]', 'EventProgramState.Parameters[3]'),
    0x14: ('params[4]', 'EventProgramState.Parameters[4]'),
    0x18: ('params[5]', 'EventProgramState.Parameters[5]'),
    0x1C: ('params[6]', 'EventProgramState.Parameters[6]'),
    0x20: ('params[7]', 'EventProgramState.Parameters[7]'),
    0x24: ('params[8]', 'EventProgramState.Parameters[8]'),
    0x28: ('params[9]', 'EventProgramState.Parameters[9]'),
    0x2C: ('result', 'EventProgramState.Result'),
    0x30: ('_30', 'EventProgramState._30'),
    0x34: ('_34', 'EventProgramState._34'),
}
STATS_FIELDS = {0: ('hp', 'PlayerStats.Hp (short)'), 2: ('hpMax', 'PlayerStats.HpMax (short)'), 4: ('mp', 'PlayerStats.Mp (short)'),
                6: ('mpMax', 'PlayerStats.MpMax (short)'), 8: ('money', 'PlayerStats.Money (short)')}
PAD_FIELDS = {0x10: ('hold', 'TickPad.ButtonsHold'), 0x12: ('pressed', 'TickPad.ButtonsJustPressed'),
              0x14: ('released', 'TickPad.ButtonsReleased'), 0x16: ('interval', 'TickPad.ButtonsJustPressedByInterval')}


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


def ent_addr(i):
    return ENT0 + i * STRIDE


def slot_of(addr):
    """Entity address -> slot index, or -1 for a null pointer. A pointer that is neither is a STOP of the generator."""
    if addr == 0:
        return -1
    d = addr - ENT0
    if d < 0 or d % STRIDE != 0 or d // STRIDE >= NSLOTS_WRITTEN:
        raise ValueError('pointer %08x is not an entity slot' % addr)
    return d // STRIDE


def default_slot(i):
    return dict(status=2, hp=10, hpMax=10, refId=-1 if i == 0 else 0, flags=0, animFlags=0, hasPlatform=False, parentSlot=-1,
                ridingSlot=-1, xCollSlot=-1, targetAnimId=0, targetDir=0, lastTargetAnimId=0, lastTargetDir=0)


def pointer_word(slot):
    return 0 if slot < 0 else ent_addr(slot)


def write_world(m, w):
    """Writes the world `w` (see gen_vectors.py) into the machine. Every word of the window is first patterned so that a read or a
    write outside the mapped fields shows up (as an unmapped address in the logs or as an unexpected diff)."""
    base = LO & 0x1FFFFFFF
    pattern = bytes(((x * 7 + 3) & 0xFF) for x in range(HI - LO))
    m.ram[base:base + (HI - LO)] = pattern
    n = len(w['slots'])
    m.ww(NADDR, n - 1)
    for i in range(NSLOTS_WRITTEN):
        for o in range(0, STRIDE, 4):
            m.ww(ent_addr(i) + o, 0xA5000000 | (i << 16) | o)
    # slots above N: traps (active, matching ids/pointers) that a correct loop never reaches
    for i in range(n, NSLOTS_WRITTEN):
        d = default_slot(i)
        d.update(status=2, hp=1, hpMax=1000, refId=3, flags=0x80, ridingSlot=1, xCollSlot=1, parentSlot=0, hasPlatform=True)
        _put_slot(m, i, d)
    for i, d in enumerate(w['slots']):
        _put_slot(m, i, d)
    for j in range(0x48):
        m.ww(RES - 4 + 4 * j, 0xDEADBEEF)
    for off in range(0, 0x40, 4):
        m.ww(STATE + off, 0xC0DE0000 | off)
    st = w.get('state', {})
    m.ww(STATE + 0x2C, st.get('resultWord', SENTINEL) & M32)
    for k in range(10):
        if 'params' in st and st['params'][k] is not None:
            m.ww(STATE + 4 + 4 * k, st['params'][k] & M32)
    for name, off in (('_30', 0x30), ('_34', 0x34), ('cursor', 0x00)):
        if st.get(name) is not None:
            m.ww(STATE + off, st[name] & M32)
    m.ww(PSTATS_PTR, PSTATS)
    p = w['stats']
    for off, (name, _) in STATS_FIELDS.items():
        m.wh(PSTATS + off, p[name] & 0xFFFF)
    for off in range(0, 0x20, 4):
        m.ww(PAD + off, 0xB0B00000 | off)
    pd = w.get('pad')
    if pd:
        for off, (name, _) in PAD_FIELDS.items():
            m.wh(PAD + off, pd[name] & 0xFFFF)


def _put_slot(m, i, d):
    a = ent_addr(i)
    for off, (name, _, kind) in SLOT_FIELDS.items():
        v = d[name]
        if kind == 'slotref':
            m.ww(a + off, pointer_word(v))
        elif kind == 'ptrbool':
            m.ww(a + off, ent_addr(1) if v else 0)
        else:
            m.ww(a + off, v & M32)


def write_code(m, instr):
    for i in range(16):
        m.wb(CODE + i, 0)
    for i, x in enumerate(instr):
        m.wb(CODE + i, x)
    m.ww(PCP, CODE)


# -------------------------------------------------------------------------------------------------------- classification
def classify(addr):
    """Physical byte address (as logged) -> (category, name). category in mapped | internal | binary-constant | UNMAPPED."""
    a = addr | 0x80000000
    if CODE <= a < CODE + 16:
        return 'mapped', 'instr[%d]' % (a - CODE)
    if PCP <= a < PCP + 4:
        return 'internal', 'cursor pointer (a2)'
    if STATE <= a < STATE + 0x40:
        off = (a - STATE) & ~3
        if off in STATE_FIELDS:
            return 'mapped', 'state.' + STATE_FIELDS[off][0]
        return 'UNMAPPED', 'state+0x%x' % off
    if ENT0 <= a < ENT0 + NSLOTS_WRITTEN * STRIDE:
        i, off = divmod(a - ENT0, STRIDE)
        off &= ~3
        if off in SLOT_FIELDS:
            return 'mapped', 'slot[%d].%s' % (i, SLOT_FIELDS[off][0])
        if 0x234 <= off < 0x26C:
            return 'mapped', 'slot[%d].ownerState+0x%x' % (i, off - 0x234)
        return 'UNMAPPED', 'slot[%d]+0x%x' % (i, off)
    if NADDR <= a < NADDR + 4:
        return 'mapped', 'slotCount-1 (N)'
    if RES - 4 <= a < RES - 4 + 0x120:
        return 'internal', 'match buffer'
    if PSTATS_PTR <= a < PSTATS_PTR + 4:
        return 'internal', 'player stats pointer'
    if PSTATS <= a < PSTATS + 10:
        return 'mapped', 'stats.' + STATS_FIELDS[(a - PSTATS) & ~1][0]
    if PAD <= a < PAD + 0x20:
        off = (a - PAD) & ~1
        if off in PAD_FIELDS:
            return 'mapped', 'pad.' + PAD_FIELDS[off][0]
        return 'mapped', 'pad.counters+0x%x' % off
    if SEARCH_SWITCH <= a < SEARCH_SWITCH + 48:
        return 'internal', 'search switch table'
    if STATUS_TABLE <= a < STATUS_TABLE + 12:
        return 'binary-constant', 'status table[%d]' % ((a - STATUS_TABLE) // 4)
    if STATUS_TABLE + 12 <= a < STATUS_TABLE + 4 * 256:
        return 'binary-constant', 'status table overrun [%d]' % ((a - STATUS_TABLE) // 4)
    if TABLE <= a < TABLE + 4 * 256:
        return 'internal', 'handler table'
    if STACK_LO <= a < STACK_HI:
        return 'internal', 'stack'
    return 'UNMAPPED', 'addr %08x' % a


def names(addrs):
    out = {}
    for ad in sorted(addrs):
        cat, nm = classify(ad)
        out.setdefault(cat, set()).add(nm)
    return out


# -------------------------------------------------------------------------------------------------------- models
def active(s):
    return 1 <= s['status'] <= 3


def search(w, ref, t):
    """0x8003C954 by my reading: slot indices in buffer order. Group types 12.. are excluded from the vectors (fatal in the binary)."""
    slots = w['slots']
    n = len(slots)
    S = slots[ref]
    if (t & 0x80) == 0:
        if not active(S):
            return []
        return [i for i in range(1, n) if slots[i]['refId'] == s32(t)]
    ft = t & 0x7F
    if ft == 0:
        return [ref]
    if ft == 1:
        return [0]
    if ft == 2:
        return [i for i in range(0, n) if active(slots[i])]
    if ft == 3:
        return [i for i in range(1, n) if active(slots[i])]
    if ft == 4:
        return [i for i in range(0, n)
                if active(S) and (slots[i]['flags'] & 0x80) and not (slots[i]['animFlags'] & 0x80) and not slots[i]['hasPlatform']]
    out = []
    for i in range(1, n):
        c = slots[i]
        if not active(c):
            continue
        if ft == 5 and S['ridingSlot'] == i:
            out.append(i)
        elif ft == 6 and c['ridingSlot'] == ref:
            out.append(i)
        elif ft == 7 and S['xCollSlot'] == i:
            out.append(i)
        elif ft == 8 and c['xCollSlot'] == ref:
            out.append(i)
        elif ft == 9 and c['parentSlot'] == ref:
            out.append(i)
        elif ft == 10 and S['parentSlot'] == i:
            out.append(i)
        elif ft == 11 and c['hasPlatform']:
            out.append(i)
    return out


def status_word(idx):
    return lib.u32(STATUS_TABLE + 4 * idx)


class Exp:
    def __init__(self, v0, result=None, slot=None, stats=None, pad=None, state=None, owner_state=None):
        self.v0 = v0
        self.result = result
        self.slot = slot or {}        # (slot, field) -> value
        self.stats = stats or {}
        self.pad = pad or {}
        self.state = state or {}
        self.owner_state = owner_state or {}


def model(op, w, logic, owner, b):
    slots = w['slots']
    if op == 0x72:
        S = slots[logic]
        return Exp(1, slot={(logic, 'lastTargetAnimId'): S['targetAnimId'], (logic, 'lastTargetDir'): S['targetDir']})
    if op == 0x88:
        ms = search(w, logic, b[1])
        val = status_word(b[2])
        return Exp(3, slot={(i, 'status'): val for i in ms})
    if op == 0x96:
        ms = search(w, logic, b[1])
        wr = {}
        for i in ms:
            hp = s32(slots[i]['hp'] + b[1])
            if s32(slots[i]['hpMax']) < hp:
                hp = s32(slots[i]['hpMax'])
            wr[(i, 'hp')] = hp
        return Exp(3, slot=wr)
    if op == 0x97:
        p = w['stats']
        v = s16(p['mp']) + b[1]
        mpmax = s16(p['mpMax'])
        if mpmax < v:
            new = mpmax
        elif v < 0:
            new = 0
        else:
            new = v
        return Exp(2, stats={'mp': new})
    if op == 0xB3:
        return Exp(9, pad={'hold': b[1] | b[2] << 8, 'pressed': b[3] | b[4] << 8, 'released': b[5] | b[6] << 8, 'interval': b[7] | b[8] << 8})
    if op == 0x7A:
        st = w['state']
        if s32(st['resultWord']) != 0:
            return Exp(3)
        return Exp(s16(b[1] | b[2] << 8), state={'_34': st['cursor'] + 3})
    raise KeyError(op)
