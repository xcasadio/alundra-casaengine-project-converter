"""E19.sm4, the three opcodes with 0 site in the corpus: 0x7A (Jump if choice rejected), 0x15 (Reset z pos), 0x66 (Copy context and
assign script). Same method as gen_vectors.py (the REAL handlers in the instrumented interpreter, an unmapped byte is a STOP), with
their own small worlds:
  0x7A  reads Result (+0x2C) and the instruction bytes 1 and 2; writes _34 (+0x34) = the pointer of the instruction + 3.
  0x15  reads the record pointer (+0x44), the record byte 9 (Height), ModZ (+0x1EC); writes PosZ (+0x11C). A null record pointer
        reaches the fatal message 0x8003C840 (BIOS exit): excluded from the vectors, a DLL policy to decide.
  0x66  copies the 14 words of the SHARED state 0x8013D9E8 (whatever state is running) over the OWNER's state (+0x234..+0x26B), then
        owner state cursor = Parameters[0] = pc + signed 16-bit offset (bytes 1, 2); logic.LastTarget{Anim,Dir} = logic.Target{Anim,Dir}.
Pointers (cursor, Parameters[0], _34) are written relative to the instruction (CODE), the way the DLL's CodeIndex reads.

Usage: python -B gen_zero_site.py OUT_DIR
"""
import hashlib
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib  # noqa: E402
import temu  # noqa: E402
import sm4model as S  # noqa: E402

REC = 0x80150200
OFF_REC, OFF_POSZ, OFF_MODZ = 0x44, 0x11C, 0x1EC


def fresh(m):
    base = S.LO & 0x1FFFFFFF
    m.ram[base:base + (S.HI - S.LO)] = bytes(((x * 7 + 3) & 0xFF) for x in range(S.HI - S.LO))
    for i in range(S.NSLOTS_WRITTEN):
        for o in range(0, S.STRIDE, 4):
            m.ww(S.ent_addr(i) + o, 0xA5000000 | (i << 16) | o)
    m.ww(S.NADDR, 1)
    for off in range(0, 0x40, 4):
        m.ww(S.STATE + off, 0xC0DE0000 | off)


def call(m, op, instr, logic, owner):
    for i in range(16):
        m.wb(S.CODE + i, 0)
    for i, x in enumerate(instr):
        m.wb(S.CODE + i, x)
    m.ww(S.PCP, S.CODE)
    base = S.LO & 0x1FFFFFFF
    before = bytes(m.ram[base:base + (S.HI - S.LO)])
    handler = m.rw(S.TABLE + 4 * op)
    m.rlog, m.wlog, m.calls = set(), set(), []
    v0 = m.call(handler, S.ent_addr(logic), S.ent_addr(owner), S.PCP, S.STATE)
    rl, wl = m.rlog, m.wlog
    m.rlog = m.wlog = None
    if 'DebugMessage(0x8003C840)' in m.calls:
        raise SystemExit('STOP: op %02X reached the fatal debug message' % op)
    return v0, rl, wl


def label(ad, op):
    """Extra names for the three opcodes (slot fields outside SLOT_FIELDS)."""
    a = ad | 0x80000000
    if S.ENT0 <= a < S.ENT0 + S.NSLOTS_WRITTEN * S.STRIDE:
        i, off = divmod(a - S.ENT0, S.STRIDE)
        off &= ~3
        if op == 0x15 and off == OFF_REC:
            return 'mapped', 'slot[%d].recordPtr (non-null = RecordHeight has a value)' % i
        if op == 0x15 and off == OFF_POSZ:
            return 'mapped', 'slot[%d].posZ' % i
        if op == 0x15 and off == OFF_MODZ:
            return 'mapped', 'slot[%d].modZ' % i
        if op == 0x66 and 0x234 <= off < 0x26C:
            return 'mapped', 'slot[%d].ownerState.%s' % (i, S.STATE_FIELDS[off - 0x234][0])
    if op == 0x15 and REC <= a < REC + 16:
        return 'mapped', 'record[%d] (record byte 9 = Height)' % (a - REC)
    return S.classify(ad)


def names(addrs, op):
    out = {}
    for ad in sorted(addrs):
        cat, nm = label(ad, op)
        out.setdefault(cat, set()).add(nm)
    return {k: sorted(v) for k, v in out.items()}


def word(m, a):
    return struct.unpack_from('<I', m.ram, a & 0x1FFFFFFF)[0]


def vec_7a(m):
    out = []
    cases = [
        ('e01', 'Result 0, offset +16: jumps, _34 = pc + 3', 0, [0x7A, 0x10, 0x00]),
        ('e02', 'Result 1: no jump, size 3, nothing written', 1, [0x7A, 0x10, 0x00]),
        ('e03', 'Result 0, offset -8 (0xFFF8)', 0, [0x7A, 0xF8, 0xFF]),
        ('e04', 'Result 0, offset 0x7FFF', 0, [0x7A, 0xFF, 0x7F]),
        ('e05', 'Result 0, offset 0x8000 (-32768)', 0, [0x7A, 0x00, 0x80]),
        ('e06', 'Result 0, offset 0: jumps by 0 (a loop on itself), _34 = pc + 3', 0, [0x7A, 0x00, 0x00]),
        ('e07', 'Result 0x80000000 (non-zero, negative): no jump', 0x80000000, [0x7A, 0x10, 0x00]),
        ('e08', 'Result 255: no jump', 255, [0x7A, 0x10, 0x00]),
    ]
    for vid, note, res, instr in cases:
        fresh(m)
        m.ww(S.STATE + 0x2C, res & S.M32)
        m.ww(S.STATE + 0x34, 0xABCD0000)
        v0, rl, wl = call(m, 0x7A, instr, 0, 0)
        wrote = sorted({label(a, 0x7A)[1] for a in wl if label(a, 0x7A)[0] == 'mapped'})
        _34 = word(m, S.STATE + 0x34)
        out.append(dict(id='op7A/' + vid, note=note, instruction=instr, resultBefore=S.s32(res), returned=S.s32(v0),
                        stateWrites=([dict(field='_34', value=_34 - S.CODE, relativeToInstruction=True)] if 'state._34' in wrote else []),
                        reads=names(rl, 0x7A), today=dict(returned=3, writes='none (skipped by size)', redToday=(S.s32(v0) != 3 or 'state._34' in wrote))))
    return out


def vec_15(m):
    out = []
    cases = [
        ('e01', 'Height 4, ModZ 0: PosZ = 32 << 16', 4, 0),
        ('e02', 'Height 4, ModZ 8: (32 - 8) << 16', 4, 8),
        ('e03', 'Height 0, ModZ 0: PosZ 0', 0, 0),
        ('e04', 'Height 0, ModZ 5: negative PosZ ((0 - 5) << 16)', 0, 5),
        ('e05', 'Height 255, ModZ 0: 2040 << 16', 255, 0),
        ('e06', 'Height 255, ModZ -3 (0xFFFFFFFD)', 255, -3),
        ('e07', 'Height 1, ModZ 0x7FFFFFFF: 32-bit wrap of the 16.16 shift', 1, 0x7FFFFFFF),
        ('e08', 'Height 16, ModZ 128: zero', 16, 128),
    ]
    for vid, note, height, modz in cases:
        fresh(m)
        for i in range(16):
            m.wb(REC + i, 0x70 + i)
        m.wb(REC + 9, height)
        m.ww(S.ent_addr(1) + OFF_REC, REC)
        m.ww(S.ent_addr(1) + OFF_MODZ, modz & S.M32)
        m.ww(S.ent_addr(1) + OFF_POSZ, 0x11111111)
        v0, rl, wl = call(m, 0x15, [0x15], 1, 0)
        posz = S.s32(word(m, S.ent_addr(1) + OFF_POSZ))
        wrote = sorted({label(a, 0x15)[1] for a in wl if label(a, 0x15)[0] == 'mapped'})
        assert wrote == ['slot[1].posZ'], wrote
        out.append(dict(id='op15/' + vid, note=note, instruction=[0x15], logicSlot=1, recordHeight=height, modZ=S.s32(modz & S.M32), posZBefore=0x11111111,
                        returned=v0, slotWrites=[dict(slot=1, field='posZ', value=posz)], resultWritten=False, reads=names(rl, 0x15),
                        frameNote='binary frame; the DLL frame is one raw unit lower (ADR-0026) - INCONNU until the port reads RecordHeight / ModZ',
                        today=dict(returned=1, writes='none (skipped by size)', redToday=posz != 0x11111111)))
    return out


def vec_66(m):
    out = []
    cases = [
        ('e01', 'offset +20, owner = logic = the hero: state copied, cursor and Parameters[0] = pc + 20', 20, 0, 0),
        ('e02', 'offset -4', S.s16(0xFFFC), 0, 0),
        ('e03', 'logic (slot 1) differs from the owner (slot 0): LastTarget of the LOGIC entity, the state copy goes to the OWNER', 6, 1, 0),
        ('e04', 'offset 0x7FFF', 0x7FFF, 0, 0),
        ('e05', 'offset 0', 0, 0, 0),
    ]
    for vid, note, off, logic, owner in cases:
        fresh(m)
        shared = [0x1000, 0x2000, 11, 12, 13, 14, 15, 16, 17, 18, 19, 1, 0x30303030, 0x34343434]
        # words: cursor, params[0..9], result, _30, _34
        for k, v in enumerate(shared):
            m.ww(S.STATE + 4 * k, v)
        m.ww(S.ent_addr(logic) + 0x88, 0x55)
        m.ww(S.ent_addr(logic) + 0x8C, 0x66)
        instr = [0x66, off & 0xFF, (off >> 8) & 0xFF]
        v0, rl, wl = call(m, 0x66, instr, logic, owner)
        st = {S.STATE_FIELDS[4 * k][0]: word(m, S.ent_addr(owner) + 0x234 + 4 * k) for k in range(14)}
        for f in ('cursor', 'params[0]'):
            st[f] = S.s32(st[f] - S.CODE)
        for f in st:
            if f not in ('cursor', 'params[0]'):
                st[f] = S.s32(st[f])
        wrote = sorted({label(a, 0x66)[1] for a in wl if label(a, 0x66)[0] == 'mapped'})
        out.append(dict(id='op66/' + vid, note=note, instruction=instr, logicSlot=logic, ownerSlot=owner, sharedStateBefore=dict(zip([S.STATE_FIELDS[4 * k][0] for k in range(14)], shared)),
                        returned=v0, ownerStateAfter=st, lastTargetAfter=dict(anim=word(m, S.ent_addr(logic) + 0x26C), dir=word(m, S.ent_addr(logic) + 0x270)),
                        writeSet=wrote, reads=names(rl, 0x66),
                        mappingNote='cursor and params[0] are relative to the instruction (CODE); the source of the copy is the SHARED state 0x8013D9E8 whatever state runs (a3 is not read)',
                        today=dict(returned=3, writes='none (skipped by size)', redToday=True)))
    return out


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    m = temu.make_machine()
    manifest = {}
    for op, f in ((0x7A, vec_7a), (0x15, vec_15), (0x66, vec_66)):
        vs = f(m)
        doc = dict(opcode=op, opcodeHex='0x%02X' % op, handler='0x%08X' % lib.u32(S.TABLE + 4 * op), sites=0, vectors=vs)
        text = json.dumps(doc, indent=1, sort_keys=True) + '\n'
        name = 'op%02X.json' % op
        with open(os.path.join(out_dir, name), 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(text)
        manifest[name] = hashlib.sha256(text.encode()).hexdigest()
        unm = [v['id'] for v in vs if 'UNMAPPED' in v['reads']]
        print('op %02X: %d vectors, unmapped reads in %s' % (op, len(vs), unm or 'none'))
    with open(os.path.join(out_dir, 'MANIFEST.sha256'), 'w', encoding='utf-8', newline='\n') as fh:
        for k in sorted(manifest):
            fh.write('%s  %s\n' % (manifest[k], k))


if __name__ == '__main__':
    main()
