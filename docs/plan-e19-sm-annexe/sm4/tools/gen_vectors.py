"""E19.sm4 value vectors: runs the REAL handlers of ALUN_CD.EXE (France) for 0x72 0x88 0x96 0x97 0xB3 (and 0x7A 0x66 0x15, 0 site)
in the instrumented MIPS interpreter, on hand-picked edge worlds and on seeded random worlds, and writes replayable JSON vectors.

Usage (from a COPY of this folder; it reads the EXE at the path of lib.py):
    python -B gen_vectors.py OUT_DIR [SEED] [N_RANDOM_PER_OPCODE]

Every vector carries: the instruction bytes, the world read, the search result, v0, Result (null = unwritten), every word written (final
value), the set of data bytes read mapped to named fields (an unmapped byte stops the run), and what the DLL gives today (the skip by size).
Two runs with the same arguments must produce byte-identical files (the manifest is the sha256 of each).
"""
import hashlib
import json
import os
import random
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib  # noqa: E402
import temu  # noqa: E402
import sm4model as S  # noqa: E402

SIZES = {0x72: 1, 0x88: 3, 0x96: 3, 0x97: 2, 0xB3: 9, 0x7A: 3, 0x66: 3, 0x15: 1}
LABEL = {0x72: 'Set LastTargetAnim and Direction', 0x88: '(no label)', 0x96: 'Restore HP', 0x97: 'Spend money', 0xB3: 'Set pad buttons',
         0x7A: 'Jump if choice rejected', 0x66: 'Copy context and assign script', 0x15: 'Reset z pos'}


def slot(i, **kw):
    d = S.default_slot(i)
    d.update(kw)
    return d


def hero(**k):
    return slot(0, **k)


def npc(i, **k):
    d = dict(refId=i)
    d.update(k)
    return slot(i, **d)


def world(slots, stats=None, pad=None, state=None):
    st = dict(hp=10, hpMax=10, mp=0, mpMax=4, money=1234)
    st.update(stats or {})
    s = dict(resultWord=S.SENTINEL, cursor=None)
    s.update(state or {})
    return dict(slots=slots, stats=st, pad=pad, state=s)


class Stop(Exception):
    pass


def run_case(m, op, instr, w, logic, owner):
    S.write_world(m, w)
    S.write_code(m, instr)
    base = S.LO & 0x1FFFFFFF
    before = bytes(m.ram[base:base + (S.HI - S.LO)])
    handler = m.rw(S.TABLE + 4 * op)
    m.rlog = set()
    m.wlog = set()
    m.calls = []
    v0 = m.call(handler, S.ent_addr(logic), S.ent_addr(owner), S.PCP, S.STATE)
    after = bytes(m.ram[base:base + (S.HI - S.LO)])
    rl, wl = m.rlog, m.wlog
    m.rlog = m.wlog = None
    if 'DebugMessage(0x8003C840)' in m.calls:
        raise Stop('op %02X reached the fatal debug message (illegal search type or id)' % op)
    changed = {i + base for i in range(len(before)) if before[i] != after[i]}
    return dict(v0=v0, rl=rl, wl=wl, changed=changed, before=before, after=after, calls=list(m.calls), m=m)


def decode_writes(m, wl):
    """Final values of the mapped fields the handler wrote (any byte of a field written => the field)."""
    out = {}
    unmapped = []
    for ad in sorted(wl):
        a = ad | 0x80000000
        cat, nm = S.classify(ad)
        if cat == 'internal':
            continue
        if cat == 'UNMAPPED':
            unmapped.append(nm)
            continue
        if nm.startswith('slot['):
            i = int(nm[5:nm.index(']')])
            field = nm[nm.index('.') + 1:]
            if field.startswith('ownerState'):
                out[nm] = ('ownerState', i, field, a & ~3)
                continue
            off = S.SLOT_OFF_BY_NAME[field]
            kind = S.SLOT_FIELDS[off][2]
            word = struct.unpack_from('<I', m.ram, (S.ent_addr(i) + off) & 0x1FFFFFFF)[0]
            if kind == 'u32':
                val = word
            elif kind == 's32':
                val = S.s32(word)
            elif kind == 'slotref':
                val = S.slot_of(word)
            else:
                val = word != 0
            out[nm] = ('slot', i, field, val)
        elif nm.startswith('stats.'):
            field = nm[6:]
            off = [k for k, v in S.STATS_FIELDS.items() if v[0] == field][0]
            half = struct.unpack_from('<h', m.ram, (S.PSTATS + off) & 0x1FFFFFFF)[0]
            out[nm] = ('stats', field, half)
        elif nm.startswith('pad.'):
            field = nm[4:]
            offs = [k for k, v in S.PAD_FIELDS.items() if v[0] == field]
            if not offs:
                out[nm] = ('pad', field, None)
                continue
            half = struct.unpack_from('<H', m.ram, (S.PAD + offs[0]) & 0x1FFFFFFF)[0]
            out[nm] = ('pad', field, half)
        elif nm.startswith('state.'):
            field = nm[6:]
            off = [k for k, v in S.STATE_FIELDS.items() if v[0] == field][0]
            word = struct.unpack_from('<I', m.ram, (S.STATE + off) & 0x1FFFFFFF)[0]
            out[nm] = ('state', field, word)
        else:
            unmapped.append(nm)
    return out, unmapped


def world_json(w):
    return dict(slotCount=len(w['slots']), slots=[dict(sorted(s.items())) for s in w['slots']], stats=dict(sorted(w['stats'].items())),
                pad=w['pad'], stateBefore={k: v for k, v in w['state'].items() if k not in ('resultWord', 'cursor') and v is not None})


def make_vector(m, op, vid, kind, note, instr, w, logic, owner, size_table, extra=None):
    r = run_case(m, op, instr, w, logic, owner)
    b = list(instr) + [0] * (12 - len(instr))
    exp = S.model(op, w, logic, owner, b) if op in (0x72, 0x88, 0x96, 0x97, 0xB3, 0x7A) else None
    wr, unmapped_w = decode_writes(r['m'], r['wl'])
    rnames = S.names(r['rl'])
    unmapped_r = sorted(rnames.get('UNMAPPED', []))
    if unmapped_r or unmapped_w:
        raise Stop('vector %s: UNMAPPED reads %s writes %s' % (vid, unmapped_r, unmapped_w))
    res_word = struct.unpack_from('<I', r['m'].ram, (S.STATE + 0x2C) & 0x1FFFFFFF)[0]
    result_written = any(((a | 0x80000000) & ~3) == S.STATE + 0x2C for a in r['wl'])
    result = S.s32(res_word) if result_written else None
    got_slot = {(v[1], v[2]): v[3] for k, v in wr.items() if v[0] == 'slot'}
    got_stats = {v[1]: v[2] for k, v in wr.items() if v[0] == 'stats'}
    got_pad = {v[1]: v[2] for k, v in wr.items() if v[0] == 'pad' and v[2] is not None}
    # agreement with the python model (a second, independent description of the same disassembly)
    if exp is not None:
        problems = []
        if r['v0'] != exp.v0 and r['v0'] != exp.v0 & S.M32:
            problems.append('v0 %d != %d' % (r['v0'], exp.v0))
        if exp.result is None and result_written:
            problems.append('Result written but the model writes none')
        if exp.result is not None and result != exp.result:
            problems.append('Result %s != %s' % (result, exp.result))
        norm = {}
        for k, v in exp.slot.items():
            kind_ = S.SLOT_FIELDS[S.SLOT_OFF_BY_NAME[k[1]]][2]
            norm[k] = S.s32(v) if kind_ == 's32' else v & S.M32
        if got_slot != norm:
            problems.append('slot writes %s != model %s' % (got_slot, norm))
        if got_stats != {k: S.s16(v) for k, v in exp.stats.items()}:
            problems.append('stats writes %s != model %s' % (got_stats, exp.stats))
        if got_pad != {k: v & 0xFFFF for k, v in exp.pad.items()}:
            problems.append('pad writes %s != model %s' % (got_pad, exp.pad))
        if problems:
            raise Stop('vector %s disagrees with the model: %s' % (vid, '; '.join(problems)))
    matched = None
    if op in (0x88, 0x96):
        matched = S.search(w, logic, instr[1])
    slot_writes = sorted((dict(slot=k[0], field=k[1], value=v) for k, v in got_slot.items()), key=lambda d: (d['slot'], d['field']))
    stats_writes = sorted((dict(field=k, value=v) for k, v in got_stats.items()), key=lambda d: d['field'])
    pad_writes = sorted((dict(field=k, value=v) for k, v in got_pad.items()), key=lambda d: d['field'])
    state_writes = sorted((dict(field=v[1], value=v[2]) for k, v in wr.items() if v[0] == 'state' and v[1] != 'result'), key=lambda d: d['field'])
    reads = {k: sorted(v) for k, v in rnames.items()}
    # a vector is red today when the real handler CHANGES some observable value (the DLL skips the opcode by its table size and writes nothing)
    changes = []
    for (si, fld), val in got_slot.items():
        b4 = w['slots'][si][fld]
        if b4 != val:
            changes.append('slot[%d].%s' % (si, fld))
    for fld, val in got_stats.items():
        if S.s16(w['stats'][fld]) != val:
            changes.append('stats.' + fld)
    for fld, val in got_pad.items():
        if (w['pad'] or {}).get(fld) != val:
            changes.append('pad.' + fld)
    if state_writes:
        changes.append('state')
    if result_written:
        changes.append('state.result')
    vec = dict(
        id=vid, opcode=op, opcodeHex='0x%02X' % op, kind=kind, note=note,
        instruction=list(instr), size=SIZES[op], logicSlot=logic, ownerSlot=owner,
        world=world_json(w),
        expect=dict(returned=r['v0'], result=result, resultWritten=result_written, slotWrites=slot_writes, statsWrites=stats_writes,
                    padWrites=pad_writes, stateWrites=state_writes, matchedSlots=matched, unchangedElsewhere=True),
        reads=reads,
        calls=r['calls'],
        today=dict(returned=size_table, writes='none (the interpreter skips the opcode by its table size)', redToday=bool(changes) or r['v0'] != size_table, changedFields=changes),
    )
    if extra:
        vec.update(extra)
    return vec


# ======================================================================================== edge cases
def edges_72():
    out = []
    out.append(('e01', 'hero as logic: copies (anim, dir) to Last*', bytes([0x72]), world([hero(targetAnimId=5, targetDir=9, lastTargetAnimId=1, lastTargetDir=2), npc(1)]), 0, 0))
    out.append(('e02', 'an NPC as logic (a B program after 0x43), owner = the hero: acts on the LOGIC entity, never on the owner',
                bytes([0x72]), world([hero(targetAnimId=7, targetDir=7), npc(1, targetAnimId=3, targetDir=21, lastTargetAnimId=99, lastTargetDir=98)]), 1, 0))
    out.append(('e03', 'max uint32 values are copied verbatim', bytes([0x72]), world([hero(), npc(1, targetAnimId=0xFFFFFFFF, targetDir=0xFFFFFFFF, lastTargetAnimId=0, lastTargetDir=0)]), 1, 1))
    out.append(('e04', 'already equal: still a write (same value)', bytes([0x72]), world([hero(), npc(1, targetAnimId=4, targetDir=4, lastTargetAnimId=4, lastTargetDir=4)]), 1, 1))
    out.append(('e05', 'zero Target over a non-zero Last (the write is not skipped for zero)', bytes([0x72]), world([hero(), npc(1, targetAnimId=0, targetDir=0, lastTargetAnimId=0x41, lastTargetDir=0x1F)]), 1, 1))
    out.append(('e06', 'a Destroyed (status 0) logic entity is still written: no status guard', bytes([0x72]), world([hero(), npc(1, status=0, refId=-1, targetAnimId=8, targetDir=2)]), 1, 1))
    out.append(('e07', 'a FlagToDestroy (status 4) logic entity is still written', bytes([0x72]), world([hero(), npc(1, status=4, targetAnimId=8, targetDir=2)]), 1, 1))
    return out


def edges_88():
    out = []
    base = lambda: [hero(), npc(1, status=2), npc(2, status=3), npc(3, status=1)]
    out.append(('e01', 'self (0x80), index 0 -> Normal(2): the reference was Deactivated', bytes([0x88, 0x80, 0]), world([hero(), npc(1, status=3)]), 1, 0))
    out.append(('e02', 'self (0x80), index 1 -> Deactivated(3)', bytes([0x88, 0x80, 1]), world([hero(), npc(1, status=2)]), 1, 0))
    out.append(('e03', 'self (0x80), index 2 -> FlagToDestroy(4) (map 442 E[1] @1312 shape)', bytes([0x88, 0x80, 2]), world([hero(), npc(1, status=3)]), 1, 0))
    out.append(('e04', 'hero (0x81), index 2: the hero too is set to 4 (no guard)', bytes([0x88, 0x81, 2]), world([hero(), npc(1)]), 1, 1))
    out.append(('e05', 'all active (0x82, hero included), index 0: statuses 1..3 become 2, the others (0, 4, 5) stay',
                bytes([0x88, 0x82, 0]), world([hero(status=3), npc(1, status=1), npc(2, status=3), npc(3, status=4), npc(4, status=0, refId=-1), npc(5, status=5), npc(6, status=2)]), 1, 0))
    out.append(('e06', 'all active but the hero (0x83), index 1', bytes([0x88, 0x83, 1]), world([hero(status=2), npc(1, status=2), npc(2, status=2)]), 1, 0))
    out.append(('e07', 'empty group: no collidable with a platform (0x8B), nothing is written', bytes([0x88, 0x8B, 2]), world([hero(), npc(1), npc(2)]), 1, 0))
    out.append(('e08', 'only the hero in the world (N = 0), group 3 (everyone but the hero): empty', bytes([0x88, 0x83, 2]), world([hero()]), 0, 0))
    out.append(('e09', 'only the hero (N = 0), raw id 5: the raw loop starts at slot 1, blez N: empty', bytes([0x88, 0x05, 2]), world([hero()]), 0, 0))
    out.append(('e10', 'raw id 7, reference active: every slot 1..N with that id is written, the candidate status is not tested (a corpse at status 4 is revived to 2)',
                bytes([0x88, 0x07, 0]), world([hero(), npc(1, refId=7, status=4), npc(2, refId=7, status=0), npc(3, refId=6), npc(4, refId=7, status=2)]), 3, 0))
    out.append(('e11', 'raw id 7 with an INACTIVE reference (status 4): the reference guard returns an empty group', bytes([0x88, 0x07, 0]),
                world([hero(), npc(1, refId=7, status=2), npc(2, status=4, refId=2)]), 2, 0))
    out.append(('e12', 'raw id 0: slot 0 (the hero, id -1) is never scanned; a slot with id 0 is', bytes([0x88, 0x00, 1]), world([hero(), npc(1, refId=0), npc(2, refId=2)]), 2, 0))
    out.append(('e13', 'riding group (0x85) with a NULL riding pointer on the reference: empty', bytes([0x88, 0x85, 1]), world([hero(), npc(1), npc(2)]), 1, 0))
    out.append(('e14', 'riding group (0x85) with the reference riding slot 2: only slot 2 (active) is written', bytes([0x88, 0x85, 1]),
                world([hero(), npc(1, ridingSlot=2), npc(2), npc(3)]), 1, 0))
    out.append(('e15', 'ridden-by group (0x86): slots whose riding pointer is the reference', bytes([0x88, 0x86, 2]),
                world([hero(), npc(1), npc(2, ridingSlot=1), npc(3, ridingSlot=1, status=4), npc(4, ridingSlot=2)]), 1, 0))
    out.append(('e16', 'parent group (0x89): slots whose parent is the reference; null parents never match', bytes([0x88, 0x89, 0]),
                world([hero(), npc(1), npc(2, parentSlot=1, status=3), npc(3, parentSlot=-1, status=3), npc(4, parentSlot=1, status=3)]), 1, 0))
    out.append(('e23', 'parent-of group (0x8A): the entity that the parent pointer of the reference designates (slot 2) only', bytes([0x88, 0x8A, 0]),
                world([hero(), npc(1, parentSlot=2), npc(2, status=3), npc(3, status=3)]), 1, 0))
    out.append(('e17', 'the slots above N are traps that a correct loop never reaches (raw id 3)', bytes([0x88, 0x03, 2]), world([hero(), npc(1, refId=3), npc(2, refId=2)]), 1, 0))
    out.append(('e18', 'index 3 = first word after the 3-word table (the ASCII "No.1"): out of the table', bytes([0x88, 0x80, 3]), world([hero(), npc(1)]), 1, 0))
    out.append(('e19', 'index 4: out of the table', bytes([0x88, 0x80, 4]), world([hero(), npc(1)]), 1, 0))
    out.append(('e20', 'index 5: out of the table', bytes([0x88, 0x80, 5]), world([hero(), npc(1)]), 1, 0))
    out.append(('e21', 'index 255: out of the table', bytes([0x88, 0x80, 255]), world([hero(), npc(1)]), 1, 0))
    out.append(('e22', 'index 2, empty-looking world but collidable group (0x84): flags 0x80 set, anim bit 0x80 clear, no platform', bytes([0x88, 0x84, 1]),
                world([hero(flags=0x80), npc(1, flags=0x80), npc(2, flags=0x80, animFlags=0x80), npc(3, flags=0, animFlags=0), npc(4, flags=0x80, hasPlatform=True)]), 1, 0))
    return out


def edges_96():
    out = []
    pl = dict(hp=33, hpMax=44)
    out.append(('e01', 'the shape of 31 sites: 96 81 FF on the hero, Hp 10 / HpMax 50 -> 50 (amount 129, byte 2 ignored)', bytes([0x96, 0x81, 0xFF]),
                world([hero(hp=10, hpMax=50), npc(1)], stats=pl), 1, 0))
    out.append(('e02', 'the save-room shape: 96 80 32 on the reference (the hero in a B program), Hp 1 / HpMax 20 -> 20', bytes([0x96, 0x80, 0x32]),
                world([hero(hp=1, hpMax=20), npc(1)], stats=pl), 0, 0))
    out.append(('e03', 'byte 2 is never read: same world, byte 2 = 0x00 gives the same result', bytes([0x96, 0x80, 0x00]),
                world([hero(hp=1, hpMax=20), npc(1)], stats=pl), 0, 0))
    out.append(('e04', 'sum below HpMax: no clamp (raw id 20 heals by 20)', bytes([0x96, 0x14, 0x32]),
                world([hero(), npc(1, refId=0x14, hp=3, hpMax=50), npc(2, refId=0x14, hp=45, hpMax=50), npc(3, refId=5, hp=1, hpMax=50)]), 3, 0))
    out.append(('e05', 'sum equals HpMax exactly: written, unchanged value', bytes([0x96, 0x14, 0]),
                world([hero(), npc(1, refId=0x14, hp=30, hpMax=50)]), 1, 0))
    out.append(('e06', 'Hp already above HpMax: the formula LOWERS it to HpMax', bytes([0x96, 0x81, 0]), world([hero(hp=120, hpMax=10)]), 0, 0))
    out.append(('e07', 'negative Hp stays negative when the sum is below HpMax (no floor)', bytes([0x96, 0x14, 0]),
                world([hero(), npc(1, refId=0x14, hp=-200, hpMax=50)]), 1, 0))
    out.append(('e08', 'negative Hp whose sum goes positive and above HpMax: clamped', bytes([0x96, 0x81, 0]), world([hero(hp=-5, hpMax=50)]), 0, 0))
    out.append(('e09', '32-bit wrap: Hp = 0x7FFFFFFF + 129 wraps NEGATIVE and is written as such (HpMax < wrapped is false)', bytes([0x96, 0x81, 0]),
                world([hero(hp=0x7FFFFFFF, hpMax=50)]), 0, 0))
    out.append(('e10', 'negative HpMax: Hp := HpMax (negative)', bytes([0x96, 0x81, 0]), world([hero(hp=5, hpMax=-1)]), 0, 0))
    out.append(('e11', 'HpMax = 0 and Hp = 0 (a bare proxy): stays 0', bytes([0x96, 0x80, 0x32]), world([hero(hp=0, hpMax=0)]), 0, 0))
    out.append(('e12', 'a corpse (status 4, Hp 0) found by raw id is healed: no life test', bytes([0x96, 0x09, 0]),
                world([hero(), npc(1, refId=9, status=4, hp=0, hpMax=30)]), 0, 0))
    out.append(('e13', 'empty group (0x8B): nothing is written', bytes([0x96, 0x8B, 0]), world([hero(hp=1, hpMax=50), npc(1, hp=1, hpMax=50)]), 1, 0))
    out.append(('e14', 'all active (0x82, hero included): each its own Hp and HpMax', bytes([0x96, 0x82, 0]),
                world([hero(hp=1, hpMax=10), npc(1, hp=5, hpMax=200), npc(2, status=4, hp=1, hpMax=10), npc(3, status=3, hp=130, hpMax=140)]), 1, 0))
    out.append(('e15', 'all but the hero (0x83), amount 131', bytes([0x96, 0x83, 0]),
                world([hero(hp=1, hpMax=500), npc(1, hp=1, hpMax=500), npc(2, hp=1, hpMax=500)]), 1, 0))
    out.append(('e16', 'a stats mirror that differs from the entity: the stats are NOT written by the opcode (only the entity is)', bytes([0x96, 0x81, 0]),
                world([hero(hp=2, hpMax=30)], stats=dict(hp=7, hpMax=9)), 0, 0))
    out.append(('e17', 'hero as raw id: the raw loop starts at slot 1, the hero (id -1) is never matched by id 255... and 0x7F amount 127', bytes([0x96, 0x7F, 0]),
                world([hero(hp=1, hpMax=500), npc(1, refId=0x7F, hp=1, hpMax=500)]), 1, 0))
    out.append(('e18', 'null xcoll on the reference (0x87): empty', bytes([0x96, 0x87, 0]), world([hero(), npc(1), npc(2)]), 1, 0))
    out.append(('e19', 'xcoll group (0x87) with the reference blocked by slot 2', bytes([0x96, 0x87, 0]), world([hero(), npc(1, xCollSlot=2, hp=0, hpMax=9), npc(2, hp=0, hpMax=9)]), 1, 0))
    return out


def edges_97():
    out = []

    def c(vid, note, b1, **stats):
        out.append((vid, note, bytes([0x97, b1]), world([hero(), npc(1)], stats=stats), 0, 0))
    c('e01', 'the save-room shape 97 32: Mp 0 / MpMax 4 -> 4 (clamped; +50)', 0x32, mp=0, mpMax=4, money=777)
    c('e02', 'no clamp: Mp 1, +1 -> 2', 1, mp=1, mpMax=4)
    c('e03', '+0 on a full gauge', 0, mp=4, mpMax=4)
    c('e04', 'MpMax = 0: Mp := 0 (clamped to MpMax)', 0x0A, mp=0, mpMax=0)
    c('e05', 'Mp above MpMax already (4 over 2), +0: lowered to MpMax', 0, mp=4, mpMax=2)
    c('e06', 'negative Mp (-1), +0: floor 0', 0, mp=-1, mpMax=4)
    c('e07', 'negative Mp (-1), +1: sum 0 is written as 0', 1, mp=-1, mpMax=4)
    c('e08', 'negative Mp (-3), +1: sum -2 below 0: floor 0', 1, mp=-3, mpMax=4)
    c('e09', '32-bit sum, no 16-bit wrap: Mp 32767 + 255 = 33022 > MpMax: Mp := MpMax (a (short) cast of the sum would give -32514 -> 0)', 0xFF, mp=0x7FFF, mpMax=4)
    c('e10', 'Mp = -32768, +0: floor 0', 0, mp=-32768, mpMax=4)
    c('e11', 'negative MpMax (-1), Mp 0: Mp := MpMax (-1)', 0, mp=0, mpMax=-1)
    c('e12', 'MpMax = 0x7FFF, Mp 0x7FFF, +255: Mp := MpMax', 0xFF, mp=0x7FFF, mpMax=0x7FFF)
    c('e13', 'money is untouched (random money 9999)', 0x32, mp=1, mpMax=4, money=9999)
    c('e14', 'Mp 3 MpMax 4 +10 (394 C[3] @752 shape)', 0x0A, mp=3, mpMax=4)
    return out


def edges_B3():
    out = []
    w = lambda pad: world([hero(), npc(1)], pad=pad)
    zero = dict(hold=0, pressed=0, released=0, interval=0)
    out.append(('e01', 'the real site (161 B[1] @54): Up held (0x1000), the three other words 0', bytes([0xB3, 0x00, 0x10, 0, 0, 0, 0, 0, 0]), w(zero), 0, 0))
    out.append(('e02', 'little-endian composition of the four words', bytes([0xB3, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE]), w(zero), 0, 0))
    out.append(('e03', 'all words 0xFFFF', bytes([0xB3] + [0xFF] * 8), w(zero), 0, 0))
    out.append(('e04', 'all words 0 over previous non-zero words: overwritten, not OR-ed', bytes([0xB3] + [0] * 8),
                w(dict(hold=0x1234, pressed=0x00F0, released=0x8001, interval=0xFFFF)), 0, 0))
    out.append(('e05', 'Square (0x80) in hold and pressed', bytes([0xB3, 0x80, 0, 0x80, 0, 0, 0, 0, 0]), w(zero), 0, 0))
    out.append(('e06', 'only the interval word (choice box repeat)', bytes([0xB3, 0, 0, 0, 0, 0, 0, 0x40, 0x00]), w(dict(hold=1, pressed=2, released=3, interval=4)), 0, 0))
    out.append(('e07', 'the owner/logic slot is irrelevant: logic = NPC 1, owner = hero', bytes([0xB3, 0x00, 0x10, 0, 0, 0, 0, 0, 0]), w(zero), 1, 0))
    return out


# ======================================================================================== random cases
def rand_slot(rng, i, nslots):
    d = S.default_slot(i)
    d['status'] = rng.choice([0, 1, 2, 2, 2, 3, 3, 4, 5]) if i else rng.choice([2, 2, 2, 3])
    d['hp'] = rng.choice([0, 1, 5, 10, 30, 50, 51, 120, -1, -5, -200, 0x7FFFFFFF, -0x80000000, rng.randint(-300, 300)])
    d['hpMax'] = rng.choice([0, 1, 5, 10, 30, 50, 51, 60, -1, rng.randint(1, 500)])
    d['refId'] = (rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 0x14, 0x7F]) if i else -1)
    if d['status'] == 0:
        d['refId'] = -1 if i else -1
    d['flags'] = rng.choice([0, 0x80, 0x80, 0x100, 0x4100, 0x180, 0x1, 0x80 | (rng.getrandbits(32) & 0xFFFF0000)])
    d['animFlags'] = rng.choice([0, 0, 0x80, 0x40, 0xC0])
    d['hasPlatform'] = rng.random() < 0.25
    for k in ('parentSlot', 'ridingSlot', 'xCollSlot'):
        d[k] = rng.randrange(nslots) if rng.random() < 0.45 else -1
    d['targetAnimId'] = rng.choice([0, 1, 2, 3, 9, 0x41, 0xFF, rng.getrandbits(32)])
    d['targetDir'] = rng.choice([0, 1, 7, 0x10, 0x1F, rng.getrandbits(32)])
    d['lastTargetAnimId'] = rng.choice([0, 7, 0x1234, rng.getrandbits(32)])
    d['lastTargetDir'] = rng.choice([0, 3, 0x1F, rng.getrandbits(32)])
    return d


def rand_world(rng, op):
    n = rng.randint(1, 8)
    slots = [rand_slot(rng, i, n) for i in range(n)]
    stats = dict(hp=rng.randint(0, 50), hpMax=rng.randint(1, 50), mp=rng.choice([0, 1, 2, 3, 4, -1, 0x7FFF, -32768, rng.randint(-5, 6)]),
                 mpMax=rng.choice([0, 1, 2, 3, 4, 4, -1, 0x7FFF, rng.randint(0, 5)]), money=rng.randint(0, 9999))
    pad = dict(hold=rng.getrandbits(16), pressed=rng.getrandbits(16), released=rng.getrandbits(16), interval=rng.getrandbits(16)) if op == 0xB3 else None
    return world(slots, stats=stats, pad=pad), n


def rand_search_type(rng, amount_ok=False):
    return rng.choice([rng.randint(0, 8), 0x14, 0x7F] + list(range(0x80, 0x8C)) * 2)


def random_cases(rng, op, count):
    out = []
    for k in range(count):
        w, n = rand_world(rng, op)
        logic = rng.randrange(n)
        owner = rng.randrange(n)
        if op == 0x72:
            b = bytes([0x72])
        elif op == 0x88:
            b = bytes([0x88, rand_search_type(rng), rng.choice([0, 1, 2, 2, 0, 1, 3, 4, 7, 200])])
        elif op == 0x96:
            b = bytes([0x96, rand_search_type(rng), rng.getrandbits(8)])
        elif op == 0x97:
            b = bytes([0x97, rng.choice([0, 1, 2, 4, 10, 0x32, 0xFF, rng.getrandbits(8)])])
        elif op == 0xB3:
            b = bytes([0xB3] + [rng.getrandbits(8) for _ in range(8)])
        out.append(('r%03d' % (k + 1), 'random world (seeded)', b, w, logic, owner))
    return out


def main():
    out_dir = sys.argv[1]
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261018
    nrand = int(sys.argv[3]) if len(sys.argv) > 3 else 60
    os.makedirs(out_dir, exist_ok=True)
    rng = random.Random(seed)
    m = temu.make_machine()
    builders = {0x72: edges_72, 0x88: edges_88, 0x96: edges_96, 0x97: edges_97, 0xB3: edges_B3}
    manifest = {}
    union_reads = {}
    union_writes = {}
    for op in sorted(builders):
        vectors = []
        cases = [(vid, note, bytes(instr), w, logic, owner, 'edge') for (vid, note, instr, w, logic, owner) in builders[op]()]
        cases += [(vid, note, bytes(instr), w, logic, owner, 'random') for (vid, note, instr, w, logic, owner) in random_cases(random.Random(seed ^ (op << 8)), op, nrand)]
        for vid, note, instr, w, logic, owner, kind in cases:
            extra = None
            if op == 0x88 and instr[2] >= 3:
                extra = dict(outOfTable=True, binaryStatusWord=S.status_word(instr[2]),
                             policy='NOT a replayable value: the binary stores a word read past its 3-word table; the DLL rule (R2-88-3) ignores it and logs once')
            vec = make_vector(m, op, 'op%02X/%s' % (op, vid), kind, note, instr, w, logic, owner, SIZES[op], extra)
            vectors.append(vec)
            for cat, lst in vec['reads'].items():
                union_reads.setdefault(op, {}).setdefault(cat, set()).update(
                    __import__('re').sub(r'slot\[\d+\]', 'slot[*]', x) for x in lst)
            for key in ('slotWrites', 'statsWrites', 'padWrites'):
                for e in vec['expect'][key]:
                    nm = ('slot.%s' % e['field']) if key == 'slotWrites' else ('%s.%s' % (key[:-6], e['field']))
                    union_writes.setdefault(op, set()).add(nm)
            if vec['expect']['resultWritten']:
                union_writes.setdefault(op, set()).add('state.result')
        doc = dict(opcode=op, opcodeHex='0x%02X' % op, label=LABEL[op], size=SIZES[op], handler='0x%08X' % lib.u32(S.TABLE + 4 * op),
                   generator=dict(seed=seed, randomPerOpcode=nrand), vectors=vectors)
        text = json.dumps(doc, indent=1, sort_keys=True) + '\n'
        path = os.path.join(out_dir, 'op%02X.json' % op)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
        manifest['op%02X.json' % op] = hashlib.sha256(text.encode('utf-8')).hexdigest()
        red = sum(1 for v in vectors if v['today']['redToday'])
        print('op %02X: %d vectors (%d edge, %d random), %d red today, %d green guards' %
              (op, len(vectors), sum(1 for v in vectors if v['kind'] == 'edge'), sum(1 for v in vectors if v['kind'] == 'random'), red, len(vectors) - red))
    summary = dict(reads={'0x%02X' % op: {c: sorted(v) for c, v in d.items()} for op, d in union_reads.items()},
                   writes={'0x%02X' % op: sorted(v) for op, v in union_writes.items()})
    text = json.dumps(summary, indent=1, sort_keys=True) + '\n'
    with open(os.path.join(out_dir, 'read-write-sets.json'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    manifest['read-write-sets.json'] = hashlib.sha256(text.encode('utf-8')).hexdigest()
    with open(os.path.join(out_dir, 'MANIFEST.sha256'), 'w', encoding='utf-8', newline='\n') as f:
        for k in sorted(manifest):
            f.write('%s  %s\n' % (manifest[k], k))
    print('manifest written;', len(manifest), 'files')


if __name__ == '__main__':
    main()
