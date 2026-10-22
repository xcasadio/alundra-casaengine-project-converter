"""Own control-flow walk and memo dataflow for the E19.sm2 preparation (read-only; python -B).

Two reach functions:
  reach_walkprogram(...)  = a port of AlundraStoryChainOpcodeAudit.WalkProgram (the DLL test helper at HEAD): both branches of every
                            conditional, 0x78/0x79/0x7B/0x7C call marks, return users 0x7D-0x81, 0x49 restart, 0x4B, 0x40 roots.
  dataflow(...)           = per pc, per axis, the set of abstract states {INIT, SAVED, CLOB} that reach it, with the call/return
                            modelled by the marks (a return user continues at every mark: sound over-approximation).
The clobber sets are given by the caller (derived from the DLL source by derive_clobbers.py), NOT copied from the binary.
"""
import collections, re, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C


def reach_walkprogram(codes, entry):
    count = len(codes)
    seen = {}
    marks = set()
    return_users = set()
    extra_roots = set()
    work = [entry]

    def byte(i):
        return codes[i] if 0 <= i < count else 0

    def s16(lo, hi):
        v = byte(lo) | (byte(hi) << 8)
        return v - 0x10000 if v >= 0x8000 else v

    while True:
        while work:
            pc = work.pop()
            while 0 <= pc < count and pc not in seen:
                op = codes[pc]
                seen[pc] = op
                size = C.SIZES.get(op)
                if size is None or size == 0 or op == 0xFF:
                    break
                jumped = False
                stop = False
                if op == 0x02:
                    pc += s16(pc + 1, pc + 2); jumped = True
                elif op in (0x03, 0x04):
                    work.append(pc + s16(pc + 1, pc + 2))
                elif op in (0x30, 0x31):
                    work.append(pc + s16(pc + 3, pc + 4))
                elif op == 0x74:
                    work.append(pc + s16(pc + 1, pc + 2))
                elif op in (0x57, 0x58):
                    for d in range(4):
                        work.append(pc + s16(pc + 1 + 2 * d, pc + 2 + 2 * d))
                    stop = True
                elif op == 0x78:
                    marks.add(pc + 3); pc += s16(pc + 1, pc + 2); jumped = True
                elif op == 0x79:
                    marks.add(pc + 3); work.append(pc + s16(pc + 1, pc + 2))
                elif op in (0x7B, 0x7C):
                    marks.add(pc + 5); work.append(pc + s16(pc + 3, pc + 4))
                elif op == 0x7D:
                    return_users.add(pc); stop = True
                elif op in (0x7E, 0x7F, 0x80, 0x81):
                    return_users.add(pc)
                elif op == 0x49:
                    pc = entry; jumped = True
                elif op == 0x4B:
                    work.append(entry)
                elif op == 0x40:
                    if byte(pc + 1) in (2, 3, 4, 5):
                        extra_roots.add(('ABCDEF'[byte(pc + 1)], byte(pc + 2) & 0x7F))
                if stop:
                    break
                if not jumped:
                    pc += size
        if return_users:
            fresh = [m for m in marks if m not in seen]
            if fresh:
                work.extend(fresh)
                continue
        break
    return seen, marks, return_users, extra_roots


def successors(codes, entry, pc, marks):
    """Edges for the dataflow (every edge is an explicit pc -> pc; calls and returns included)."""
    op = codes[pc]
    size = C.SIZES.get(op, 1) or 1
    nxt = pc + size
    s16 = lambda lo: C.s16(codes, lo)
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(pc + 1)]
    if op in (0x03, 0x04, 0x74):
        return [nxt, pc + s16(pc + 1)]
    if op in (0x30, 0x31):
        return [nxt, pc + s16(pc + 3)]
    if op == 0x49:
        return [entry]
    if op == 0x4A:
        return [nxt, entry]
    if op == 0x4B:
        return [nxt, entry]
    if op in (0x57, 0x58):
        return [pc + s16(pc + 1 + 2 * d) for d in range(4)]
    if op == 0x78:
        return [pc + s16(pc + 1)]
    if op == 0x79:
        return [nxt, pc + s16(pc + 1)]
    if op in (0x7B, 0x7C):
        return [nxt, pc + s16(pc + 3)]
    if op == 0x7D:
        return sorted(marks)
    if op in (0x7E, 0x7F, 0x80, 0x81):
        return [nxt] + sorted(marks)
    return [nxt]


def dataflow(codes, entry, clob, readers, writer=0x6A):
    """clob: {axis: set(opcodes)}; readers: {opcode: axis}. Returns ({pc: {axis: frozenset(states)}}, seen, marks, ret_users)."""
    seen, marks, ret_users, _ = reach_walkprogram(codes, entry)
    out = {}
    for axis in 'XYZ':
        states = collections.defaultdict(set)
        states[entry].add('INIT')
        work = [entry]
        while work:
            pc = work.pop()
            if pc < 0 or pc >= len(codes):
                continue
            op = codes[pc]
            cur = set(states[pc])
            res = set()
            for st in cur:
                if op == writer:
                    res.add('SAVED')
                elif op in clob[axis]:
                    res.add('CLOB')
                else:
                    res.add(st)
            for q in successors(codes, entry, pc, marks):
                if q < 0 or q >= len(codes):
                    continue
                if not res <= states[q]:
                    states[q] |= res
                    work.append(q)
        for pc, st in states.items():
            if codes[pc] in readers and readers[codes[pc]] == axis:
                out.setdefault(pc, {})[axis] = frozenset(st)
    return out, seen, marks, ret_users
