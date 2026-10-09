"""k3.py - E20.c1b task K3: the opcode handlers 0x86, 0x87, 0x95 and 0x71, called for real on a synthetic program state.

PROGRAM STATE LAYOUT (decoded from the dispatcher, the program runner 0x8004205C, loop at 0x80042280-0x80042344):
    owner entity            s4 = a0 of the runner (an entity slot, 0x294 bytes)
    self entity pointer     [owner + 0x230]   -> s1, the handler's a0 ("the current entity"; InitializeEntity leaves it pointing at the entity itself)
    state struct            s3 = owner + 0x234 (for letters B and C; 0x800420DC / 0x80042108)
    program counter cell    s2 = s3 + 4 = owner + 0x238, a pointer to the opcode byte; the loop reads the opcode with lbu 0(pc)
    Result word             s3 + 0x2C = owner + 0x260 (opcode 0x71 writes it: `sw v0, 0x2c(a3)`; 0x87 and 0x95 write 0 or 1; 0x86 does not write it)
    handler call            (0x800422C0-0x800422F4)  a0 = self entity, a1 = owner, a2 = &pc cell, a3 = state struct; address = word at 0x80098FAC + 4 * opcode;
                            the returned v0 is the size of the instruction: the loop adds it to the pc cell (0x80042330-0x80042340), `sw zero, 8(s3)` before; opcode 0xFF ends the program
    search 0x8003C954(a0 = self, a1 = id): id < 0x80 -> every entity slot 1..[0x80134360] with status 1..3 and +0x48 (map record index) == id (the id must be an enabled map record, 0x80039BD0),
                            collected in the list at 0x8013D8D8 (the handlers walk it from the last entry to the first); 0x80 -> a0; 0x81 -> slot 0; 0x82 -> every entity of status 1..3, slot 0 included

Every case is run twice on a copy of the machine: (a) the handler called directly with the registers above, (b) the REAL program runner 0x8004205C on the program [opcode bytes..., 0xFF]
(the harness only removes the inert-target stub that c1world installed on 0x8004205C).  The two must leave the same state.  The expected value is the independent model's (kmodel.py).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import kmodel as M
import world as W
import c1world as C
import lib

PROG = K.SCRATCH + 0x400
STATE_OFF, PC_OFF, RESULT_OFF, SELF_OFF = 0x234, 0x238, 0x260, 0x230
SENTINEL = 0xAAAA5555
HANDLER = {op: lib.u32(0x80098FAC + 4 * op) for op in (0x71, 0x86, 0x87, 0x95)}
NIBBLE_BYTES = [lib.DATA[0x800 + 0x80098FA4 - 0x80020000 + i] for i in range(12)]      # 0x80098FA4 .. 0x80098FAF


def build_world():
    """kit inventory; hero at (212, 96); T (252,96) and T2 (264,104) in the reach of the swing; A (348,96) with a synthetic nibble-4 attack (attr 0x04, power 2) and the HitsA bit, armed just
       before tick 5; V (360,96) in front of A; U (252,168) far from everything.  Five ticks, Square at tick 1: at the end of tick 5 the hero touches T and T2 (sword, attr 0x81) and A touches V."""
    inv = K.INV['kit']
    m, meta = K.make_world(inv, a_attack=K.syn(2))
    pad = W.Pad()
    for t in range(1, 6):
        if t == 5:
            K.arm_a(m)
        m.tick_no = t
        K.tick(m, pad, 0x80 if t == 1 else 0)
    ents = K.read_entities(m)
    ok = (ents[1]['touching_slot'] == 0 and ents[2]['touching_slot'] == 0 and ents[4]['touching_slot'] == 3 and ents[0]['hit_counter'] == 2 and ents[3]['hit_counter'] == 1
          and ents[3]['attr'] == 0x04 and ents[5]['touching_slot'] is None)
    m.hooks.pop(0x8004205C, None)          # the inert-target stub: from here on the real program runner may be called
    m.uninit_reads.clear()
    return m, ents, ok


def prog_bytes(*b):
    return bytes(b) + b'\xff'


def region(m):
    return bytes(m.ram[(C.ENT0 & 0x1FFFFFFF): (C.ENT0 & 0x1FFFFFFF) + 6 * C.STRIDE])


def diff_region(a, b, skip_slot=None):
    out = []
    for i in range(len(a)):
        if a[i] != b[i]:
            slot, off = divmod(i, C.STRIDE)
            if slot == skip_slot and (STATE_OFF <= off < STATE_OFF + 0x40 or 0x22C <= off < 0x230):
                continue                # the program state struct itself, and the runner's own bookkeeping word owner+0x22C (sw s1, 0x22c(s4) = the letter)
            out.append((slot, off))
    return out


def group_diff(d):
    """(slot, offset) list -> {slot: [offsets rounded to the word]}"""
    g = {}
    for s, o in d:
        g.setdefault(s, set()).add(o & ~3)
    return {s: sorted(v) for s, v in g.items()}


def run_case(m, owner, self_slot, program, mutate=None, probe=None):
    """-> dict(direct=..., runner=..., equal=bool).  Both runs start from the same machine state (restored in between)."""
    op = program[0]
    sb = C.Sandbox(m)
    before = region(m)
    out = {}
    for mode in ('direct', 'runner'):
        sb.restore()
        if mutate:
            mutate(m)
        o = K.ent(owner)
        m.poke_bytes(PROG, program)
        m.poke(o + SELF_OFF, K.ent(self_slot))
        m.poke(o + STATE_OFF, PROG)
        m.poke(o + PC_OFF, PROG)
        m.poke(o + RESULT_OFF, SENTINEL)
        pre = region(m)
        if mode == 'direct':
            v0 = K.call(m, HANDLER[op], K.ent(self_slot), o, o + PC_OFF, o + STATE_OFF) & M.M32
            size = v0
            pc_after = m.peek(o + PC_OFF)
        else:
            K.call(m, 0x8004205C, o, 1)
            pc_after = m.peek(o + PC_OFF)
            size = pc_after - PROG
            v0 = None
        post = region(m)
        res = m.peek(o + RESULT_OFF)
        d = diff_region(pre, post, skip_slot=owner)
        out[mode] = dict(size=size, v0=v0, result=res, result_written=(res != SENTINEL), changed=group_diff(d), changed_raw=d, pc_delta=pc_after - PROG,
                         uninit=sorted((hex(pc), hex(a), n) for (pc, a, n) in m.uninit_reads if (pc, a, n) not in sb.uninit))
        out[mode]['values'] = {(s, off): m.peek(C.ENT0 + C.STRIDE * s + off) for s, off in out[mode]['changed_raw']}
        out[mode]['probe'] = probe(m) if probe else None
    sb.restore()
    a, b = out['direct'], out['runner']
    out['equal'] = (a['size'] == b['size'] and a['result'] == b['result'] and a['changed_raw'] == b['changed_raw'] and a['values'] == b['values'])
    return out


def model_entities(ents):
    return ents


def fmt_changed(r):
    d = r['direct']
    if not d['changed']:
        return 'no entity field written'
    return 'writes ' + ', '.join('%s+0x%X=0x%X' % (K.E_NAMES.get(s, s), o, d['values'][(s, o)]) for s, o in d['changed_raw'])


RESULTS = {}               # label -> result (for the mutation tests)


def line(label, r, expect, got, extra=''):
    d = r['direct']
    RESULTS[label] = got
    return '   %-70s size %-2d Result %s  %s  | expected %-12s got %-12s %s  runner==direct %s%s' % (
        label, d['size'], ('0x%X' % d['result']) if d['result_written'] else 'untouched', fmt_changed(r), expect, got, 'OK' if expect == got else 'MISMATCH', 'yes' if r['equal'] else 'NO', extra)


# ---- 0x71 ----------------------------------------------------------------------------------------------------------------------------
def case_71(m, ents, lines, verdicts):
    lines.append('== K3 0x71 [0x71]: Result = HitCounter (+0x220) of the self entity (handler 0x%08X)' % HANDLER[0x71])
    for s in range(6):
        r = run_case(m, owner=K.SLOT['T'], self_slot=s, program=prog_bytes(0x71))
        want = ents[s]['hit_counter']
        got = r['direct']['result'] if r['direct']['result_written'] else None
        ok = (want == got) and r['equal'] and r['direct']['size'] == 1 and not r['direct']['changed'] and not r['direct']['uninit']
        verdicts.append(ok)
        lines.append(line('self = %s (HitCounter in memory %d)' % (K.E_NAMES[s], ents[s]['hit_counter']), r, want, got))


# ---- 0x86 ----------------------------------------------------------------------------------------------------------------------------
def poke_u(slot, off, v):
    def f(m):
        m.poke(K.ent(slot) + off, v)
    return f


def chain(*fs):
    def f(m):
        for g in fs:
            g(m)
    return f


def case_86(m, ents, lines, verdicts):
    lines.append('== K3 0x86 [0x86, search, lo, hi]: +0x1CC := lo | hi << 8 on every match (handler 0x%08X); Result untouched (sentinel 0x%X kept)' % (HANDLER[0x86], SENTINEL))
    T = K.SLOT['T']
    A = K.SLOT['A']
    # (label, owner, self, program, harness change before the call, value written)
    cases = [
        ('search record 0 (T), value 0x012C', T, T, [0x86, 0, 0x2C, 0x01], None, 0x012C),
        ('search record 0 shared by T and U (U.+0x48 := 0 by the harness), value 0x1234', T, T, [0x86, 0, 0x34, 0x12], poke_u(5, 0x48, 0), 0x1234),
        ('search record 5 (enabled map record, no entity): no match', T, T, [0x86, 5, 0x01, 0x00], None, 0x0001),
        ('SET not add: T.+0x1CC preset 0x0100, value 0x0002', T, T, [0x86, 0, 0x02, 0x00], poke_u(1, 0x1CC, 0x100), 0x0002),
        ('maximum value 0xFFFF', T, T, [0x86, 0, 0xFF, 0xFF], None, 0xFFFF),
        ('search 0x80 (self) with owner A, self T', A, T, [0x86, 0x80, 0x05, 0x00], None, 0x0005),
        ('search 0x81 (slot 0, the hero)', T, T, [0x86, 0x81, 0x07, 0x00], None, 0x0007),
        ('search 0x82 (every active entity, slot 0 included)', T, T, [0x86, 0x82, 0x09, 0x00], None, 0x0009),
        ('value 0 clears: T.+0x1CC preset 0x0055', T, T, [0x86, 0, 0x00, 0x00], poke_u(1, 0x1CC, 0x55), 0x0000),
    ]
    for label, owner, self_slot, prog, mut, val in cases:
        sb = C.Sandbox(m)
        if mut:
            mut(m)
        pre = K.read_entities(m)
        sb.restore()
        matched = M.model_search(pre, self_slot, 0, prog[1], True)
        exp_after = {s: pre[s]['c1cc'] for s in range(6)}
        for s in matched:
            exp_after[s] = val
        r = run_case(m, owner=owner, self_slot=self_slot, program=prog_bytes(*prog), mutate=mut, probe=lambda mm: {s: mm.peek(K.ent(s) + 0x1CC) for s in range(6)})
        after = r['direct']['probe']
        RESULTS[label] = dict(after)
        ok = (after == exp_after) and (r['runner']['probe'] == after) and r['equal'] and r['direct']['size'] == 4 and (not r['direct']['result_written']) and not r['direct']['uninit']
        ok &= all((o & ~3) == 0x1CC for s, o in r['direct']['changed_raw'])           # nothing but +0x1CC may be written
        verdicts.append(ok)
        lines.append('   %-76s size %d Result %-9s +0x1CC after %s (matched %s) | expected %s  %s  runner==direct %s' % (
            label, r['direct']['size'], 'untouched' if not r['direct']['result_written'] else '0x%X' % r['direct']['result'],
            ' '.join('%s=%X' % (K.E_NAMES[s], after[s]) for s in range(6)), [K.E_NAMES[s] for s in matched],
            ' '.join('%s=%X' % (K.E_NAMES[s], exp_after[s]) for s in range(6)), 'OK' if ok else 'MISMATCH', 'yes' if r['equal'] else 'NO'))


# ---- 0x87 ----------------------------------------------------------------------------------------------------------------------------
def case_87(m, ents, lines, verdicts):
    lines.append('== K3 0x87 [0x87, search, typeIdx]: Result 1 iff a match is touched by an attacker whose frame box and attack are set and attack attribute nibble == byte[0x80098FA4 + typeIdx] (handler 0x%08X)' % HANDLER[0x87])
    lines.append('   bytes 0x80098FA4..0x80098FAF: %s  (indices 0..7 = the nibble table; 8..11 are the first bytes of the handler table 0x80098FAC)' % ' '.join('%02X' % b for b in NIBBLE_BYTES))
    lines.append('   state: T and T2 touched by the hero (attr 0x%02X), V touched by A (attr 0x%02X), U untouched' % (ents[0]['attr'], ents[3]['attr']))

    def one(label, search, tidx, mut=None, owner=1, self_slot=1):
        sb = C.Sandbox(m)
        if mut:
            mut(m)
        pre = K.read_entities(m)
        sb.restore()
        matched = M.model_search(pre, self_slot, 0, search, True)
        want = M.model_0x87(pre, matched, NIBBLE_BYTES[tidx])
        r = run_case(m, owner=owner, self_slot=self_slot, program=prog_bytes(0x87, search, tidx), mutate=mut)
        got = r['direct']['result'] if r['direct']['result_written'] else None
        ok = (want == got) and r['equal'] and r['direct']['size'] == 3 and not r['direct']['changed'] and not r['direct']['uninit']
        verdicts.append(ok)
        lines.append(line(label, r, want, got))
        return got
    rows = {}
    for tidx in range(8):
        rows[('T', tidx)] = one('T (hero sword, nibble 1) typeIdx %d -> nibble %d' % (tidx, NIBBLE_BYTES[tidx]), 0, tidx)
    lines.append('   => T (touched by the hero\'s sword): Result 1 for typeIdx %s only' % [t for t in range(8) if rows[('T', t)] == 1])
    one('T2 (hero sword) typeIdx 0', 1, 0)
    one('V (A, nibble 4) typeIdx 3 -> nibble 4', 3, 3)
    one('V (A, nibble 4) typeIdx 0 -> nibble 1', 3, 0)
    one('U (untouched) typeIdx 0', 4, 0)
    one('U (untouched) typeIdx 3', 4, 3)
    one('search 0x80 (self = T, the lever case) typeIdx 0', 0x80, 0)
    one('search 0x80 (self = T) typeIdx 3', 0x80, 3)
    one('search 0x80 (self = V) typeIdx 3', 0x80, 3, self_slot=4)
    one('record 0 shared by T and U (U.+0x48 := 0): typeIdx 0', 0, 0, poke_u(5, 0x48, 0))
    one('record 3 shared by V and U (U.+0x48 := 3): typeIdx 3', 3, 3, poke_u(5, 0x48, 3))
    one('record 5 (no entity): typeIdx 0', 5, 0)
    # degraded attackers: the hero as the attacker of T
    one('T touched by the hero but hero.+0x1D4 (frame box) := 0: typeIdx 0', 0, 0, poke_u(0, 0x1D4, 0))
    one('T touched by the hero but hero.+0x1C8 (attack) := 0: typeIdx 0', 0, 0, poke_u(0, 0x1C8, 0))
    one('T touched by the hero but the attack attribute byte := 0: typeIdx 0', 0, 0, chain(lambda mm: mm.poke8(K.SYN_ATTACKS + 12, 0), poke_u(0, 0x1C8, K.SYN_ATTACKS + 12)))
    one('T.+0x224 (TouchingEntity) := 0: typeIdx 0', 0, 0, poke_u(1, 0x224, 0))
    # indices 8..11 read past the 8-byte table
    for tidx in (8, 9, 10, 11):
        one('V (A, nibble 4) typeIdx %d -> byte 0x%02X' % (tidx, NIBBLE_BYTES[tidx]), 3, tidx)
    for tidx in (2, 8, 9, 10, 11):
        one('V with A attack := synthetic attr 0x03 power 2 (nibble 3): typeIdx %d -> byte 0x%02X' % (tidx, NIBBLE_BYTES[tidx]), 3, tidx, poke_u(3, 0x1C8, K.syn(3)))
    for tidx in (4, 5):
        one('V with A attack := synthetic attr 0x05 (nibble 5): typeIdx %d -> byte 0x%02X' % (tidx, NIBBLE_BYTES[tidx]), 3, tidx,
            chain(lambda mm: mm.poke8(K.SYN_ATTACKS + 14, 5), lambda mm: mm.poke8(K.SYN_ATTACKS + 15, 2), poke_u(3, 0x1C8, K.SYN_ATTACKS + 14)))


# ---- 0x95 ----------------------------------------------------------------------------------------------------------------------------
UX, UY = 3 << 19, 1 << 20          # 24 px and 16 px in 16.16


def rect_prog(x, y, z, wx, hy, mask, tidx, b6=0):
    return prog_bytes(0x95, x, y, z, wx, hy, b6, mask & 0xFF, (mask >> 8) & 0xFF, tidx)


def rect_model(x, y, z, wx, hy):
    return dict(ox=x * UX, oy=y * UY, oz=z * UY, sx=wx * UX, sy=hy * UY)


def case_95(m, ents, lines, verdicts):
    H = ents[0]
    lines.append('== K3 0x95 [0x95, x, y, z, wx, hy, _, maskLo, maskHi, typeIdx] (handler 0x%08X): origin (24x, 16y, 16z) px, size X and Z = 24 wx px, Y = 16 hy px; any attacker slot 0..N with status 2/3, +0x20 == 0, frame box, attack with non-zero attribute,'
                 ' Flags & mask != 0, attribute nibble == byte[0x80098FA4 + typeIdx], whose HitBox overlaps the rectangle' % HANDLER[0x95])
    lines.append('   hero at the end of tick 5: swing HitBox (raw 16.16) X %d Y %d Z %d = (%.5f, %.5f, %.5f) px, attacker sizes %s (stored (n << 16) - 1), Flags 0x%X, attack attr 0x%02X nibble %d; other attackers: T/T2/V Flags 0x%X attr 0x%02X, A Flags 0x%X attr 0x%02X'
                 % (H['hitbox'][0], H['hitbox'][1], H['hitbox'][2], H['hitbox'][0] / 65536, H['hitbox'][1] / 65536, H['hitbox'][2] / 65536, H['asize'], H['flags'], H['attr'], H['attr'] & 0xF,
                    ents[1]['flags'], ents[1]['attr'], ents[3]['flags'], ents[3]['attr']))
    count_slot = m.peeks(0x80134360)
    hx, hy, hz = H['hitbox']

    def one(label, x, y, z, wx, hh, mask, tidx, mut=None, owner=1, self_slot=1, quiet=False, b6=0):
        sb = C.Sandbox(m)
        if mut:
            mut(m)
        pre = K.read_entities(m)
        cnt = m.peeks(0x80134360)
        sb.restore()
        want = M.model_0x95(pre, cnt, rect_model(x, y, z, wx, hh), mask, NIBBLE_BYTES[tidx])
        r = run_case(m, owner=owner, self_slot=self_slot, program=rect_prog(x, y, z, wx, hh, mask, tidx, b6), mutate=mut)
        got = r['direct']['result'] if r['direct']['result_written'] else None
        ok = (want == got) and r['equal'] and r['direct']['size'] == 10 and not r['direct']['changed'] and not r['direct']['uninit']
        verdicts.append(ok)
        if not quiet:
            lines.append(line(label, r, want, got))
        return got
    HERO_MASK = 0x0008            # bit 3: the hero has it (Flags 0x3118C), the Beannoids (0x103) and A (0x107) do not
    NONE_MASK = 0x0040            # no entity has bit 6
    xu = hx // UX
    yu = hy // UY
    xe = (hx + H['asize'][0] + 1) // UX              # unit holding the end of the box
    ye = (hy + H['asize'][1] + 1) // UY
    lines.append('   rectangles in units around the hero box: x unit %d (box starts in it), box ends in x unit %d, y unit %d / %d' % (xu, xe, yu, ye))
    one('IN: rect (%d,%d,0) 2 x 2 units around the hero box, hero-only mask 0x%04X, typeIdx 0 (sword)' % (xu, yu, HERO_MASK), xu, yu, 0, 2, 2, HERO_MASK, 0)
    one('same rect, typeIdx 3 (nibble 4: no attacker of that kind there)', xu, yu, 0, 2, 2, HERO_MASK, 3)
    one('same rect, mask 0x%04X (no entity has the bit)' % NONE_MASK, xu, yu, 0, 2, 2, NONE_MASK, 0)
    one('same rect, mask 0 (early exit at 0x80040818)', xu, yu, 0, 2, 2, 0, 0)
    one('OUT in X, rectangle entirely right of the box: x unit %d' % (xe + 1), xe + 1, yu, 0, 2, 2, HERO_MASK, 0)
    one('OUT in X, rectangle entirely left of the box: x unit 0', 0, yu, 0, 2, 2, HERO_MASK, 0)
    one('OUT in Y, rectangle below the box: y unit %d' % (ye + 1), xu, ye + 1, 0, 2, 2, HERO_MASK, 0)
    one('OUT in Y, rectangle above the box: y unit 0, 1 unit high', xu, 0, 0, 2, 1, HERO_MASK, 0)
    one('OUT in Z, rectangle from z unit 3 (48 px): the box (z 0..15 px) is below it', xu, yu, 3, 2, 2, HERO_MASK, 0)
    one('all-bits mask 0xFFFF, same rect (T, T2 attack boxes qualify as well), typeIdx 0', xu, yu, 0, 2, 2, 0xFFFF, 0)
    one('rect around A (x unit %d, y unit %d), mask 0x0004, typeIdx 3 (A is nibble 4)' % (ents[3]['hitbox'][0] // UX, ents[3]['hitbox'][1] // UY), ents[3]['hitbox'][0] // UX, ents[3]['hitbox'][1] // UY, 0, 2, 2, 0x0004, 3)
    one('same rect, typeIdx 0 (sword nibble): A is nibble 4, V (flags 0x103 & 0x0004 = 0) does not qualify', ents[3]['hitbox'][0] // UX, ents[3]['hitbox'][1] // UY, 0, 2, 2, 0x0004, 0)
    one('same rect, mask 0x0100, typeIdx 0 (V and A share bit 8; V attr 0x01)', ents[3]['hitbox'][0] // UX, ents[3]['hitbox'][1] // UY, 0, 2, 2, 0x0100, 0)
    # exact boundaries: the hero's HitBox is moved by the harness one raw unit across the edge of a rectangle (the handler code is the real one, the geometry is synthetic)
    lines.append('   boundaries (hero HitBox written by the harness; rect x,y,z = (10,6,0) 1 x 1 units: X [%d, %d), Y [%d, %d), Z [0, %d) raw):' % (10 * UX, 11 * UX, 6 * UY, 7 * UY, UX))
    ox, oy, oz = 10 * UX, 6 * UY, 0
    asz = H['asize']
    sx, sy = UX, UY
    for ax, (o, size, asize, tag) in enumerate(((ox, sx, asz[0], 'X'), (oy, sy, asz[1], 'Y'), (oz, sx, asz[2], 'Z'))):
        for dlt, name in ((0, 'd = 0 (inside)'), (size - 1, 'd = size - 1 (last inside)'), (size, 'd = size (first outside)'), (-asize, 'd = -asize (last inside on the negative side)'), (-asize - 1, 'd = -asize - 1 (first outside)')):
            def mut(mm, ax=ax, o=o, dlt=dlt):
                # hero HitBox on the axis `ax` := rect origin + dlt, the two other axes inside the rectangle
                base = [10 * UX + 100, 6 * UY + 100, 100]
                base[ax] = o + dlt
                for k in range(3):
                    mm.poke(K.ent(0) + 0x1FC + 4 * k, base[k] & M.M32)
            one('hero box %s axis: %s, rect (10,6,0,1,1)' % (tag, name), 10, 6, 0, 1, 1, HERO_MASK, 0, mut)
    # indices 8..11
    lines.append('   typeIdx 8..11 read the bytes 0x80098FAC.. = %s (first bytes of the handler table); the attribute nibble is at most 15 so only 0x03 (idx 10) can match' % ' '.join('%02X' % b for b in NIBBLE_BYTES[8:12]))
    ax_, ay_ = ents[3]['hitbox'][0] // UX, ents[3]['hitbox'][1] // UY
    a3 = poke_u(3, 0x1C8, K.syn(3))                    # A attack := attr 0x03 power 2 (nibble 3)
    for tidx in (2, 3, 8, 9, 10, 11):
        one('rect around A, mask 0x0004, A attack nibble 3: typeIdx %d -> byte 0x%02X' % (tidx, NIBBLE_BYTES[tidx]), ax_, ay_, 0, 2, 2, 0x0004, tidx, a3)
    for tidx in (8, 9, 10, 11):
        one('rect around A, mask 0x0004, A attack nibble 4 (real): typeIdx %d' % tidx, ax_, ay_, 0, 2, 2, 0x0004, tidx)
    # attacker disqualifications
    for lab, mut in (('hero status 1 (not 2/3)', poke_u(0, 0x10, 1)), ('hero +0x20 := 1', poke_u(0, 0x20, 1)), ('hero frame box +0x1D4 := 0', poke_u(0, 0x1D4, 0)),
                     ('hero attack +0x1C8 := 0', poke_u(0, 0x1C8, 0)), ('hero attack attribute byte := 0', chain(lambda mm: mm.poke8(K.SYN_ATTACKS + 12, 0), poke_u(0, 0x1C8, K.SYN_ATTACKS + 12)))):
        one('IN rect, hero-only mask, %s' % lab, xu, yu, 0, 2, 2, HERO_MASK, 0, mut)
    # the scan covers slots 0..[0x80134360] inclusive: lower the count to 0 (only the hero) and to the slot before A
    one('IN rect around A, mask 0x0004, typeIdx 3, [0x80134360] := 2 (A is slot 3: not scanned)', ax_, ay_, 0, 2, 2, 0x0004, 3, lambda mm: mm.poke(0x80134360, 2))
    one('IN rect around A, mask 0x0004, typeIdx 3, [0x80134360] := 3 (A scanned: the bound is inclusive)', ax_, ay_, 0, 2, 2, 0x0004, 3, lambda mm: mm.poke(0x80134360, 3))
    one('IN rect, program byte 6 := 0x55 (the unused byte)', xu, yu, 0, 2, 2, HERO_MASK, 0, None, b6=0x55)


def run():
    RESULTS.clear()
    lines = []
    verdicts = []
    m, ents, ok = build_world()
    lines.append('== K3 world: kit inventory, 5 ticks, Square at tick 1, A armed before tick 5; slots: hero 0, T 1 (map record 0), T2 2 (1), A 3 (2), V 4 (3), U 5 (4), record 5 unspawned')
    lines.append('   at the end of tick 5: ' + '; '.join('%s: Touching=%s HitCounter=%d Flags=0x%X attr=%s hp=%d +0x1CC=%d' % (
        e['name'], e['touching_slot'], e['hit_counter'], e['flags'], ('0x%02X' % e['attr']) if e['attr'] is not None else '-', e['hp'], e['c1cc']) for e in ents.values()))
    lines.append('   world as designed (T,T2 touched by the hero, V touched by A, hero HitCounter 2, A HitCounter 1): %s' % ('yes' if ok else 'NO'))
    verdicts.append(ok)
    lines.append('   program state: owner entity (default T) state struct = owner + 0x%X, pc cell = owner + 0x%X (-> program at 0x%08X), Result word = owner + 0x%X, self pointer = owner + 0x%X;'
                 ' handler addresses from the table 0x80098FAC: %s' % (STATE_OFF, PC_OFF, PROG, RESULT_OFF, SELF_OFF, ', '.join('0x%02X -> 0x%08X' % (k, v) for k, v in sorted(HANDLER.items()))))
    case_71(m, ents, lines, verdicts)
    case_86(m, ents, lines, verdicts)
    case_87(m, ents, lines, verdicts)
    case_95(m, ents, lines, verdicts)
    return lines, all(verdicts), len(verdicts), m


if __name__ == '__main__':
    ls, ok, n, m = run()
    print('\n'.join(ls))
    print('K3', 'PASS' if ok else 'FAIL', n, 'checks')
