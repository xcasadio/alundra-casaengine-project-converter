"""Critic-own reachability scan (flow-following, state-free over-approximation) of the 8 sm3 opcodes over the 483 maps.
Edges: every jump opcode of the DLL Dispatch (both branches for conditionals), restarts to the entry, mark-returns to every remembered mark of the program."""
import mycorpus as c
from cfg_stale import s16
TARGET = c.TARGET
JUMP_REL16 = {0x02}
COND16 = {0x03, 0x04}
def succ(codes, pc, entry, marks):
    op = codes[pc]; sz = c.SIZES.get(op, 0)
    if op == 0xFF or sz <= 0: return []
    b = lambda k: codes[pc+k] if pc+k < len(codes) else 0
    j = lambda k: s16(b(k) | (b(k+1) << 8))
    nxt = pc + sz
    if op == 0x02: return [pc + j(1)]
    if op in (0x03, 0x04): return [pc + j(1), nxt]
    if op in (0x30, 0x31): return [pc + j(3), nxt]
    if op in (0x49,): return [entry]
    if op in (0x4A, 0x4B): return [entry, nxt]
    if op in (0x57, 0x58): return [pc + s16(b(1+2*d) | (b(2+2*d) << 8)) for d in range(4)]
    if op == 0x74: return [pc + j(1), nxt]
    if op == 0x78: marks.add(pc+3); return [pc + j(1)]
    if op == 0x79: marks.add(pc+3); return [pc + j(1), nxt]
    if op in (0x7B, 0x7C): marks.add(pc+5); return [pc + j(3), nxt]
    if op in (0x7D,): return list(marks)
    if op in (0x7E, 0x7F): return list(marks) + [nxt]
    if op in (0x80, 0x81): return list(marks) + [nxt]
    return [nxt]
def scan(mid):
    base, d = c.load(mid); codes = d['Codes']; n = len(codes)
    hits = {}
    for s, i, e in c.entries(d):
        if e == 0: continue
        marks = set(); seen = set(); st = [e]
        # two passes so marks found late are used
        for _ in range(2):
            seen = set(); st = [e]
            while st:
                pc = st.pop()
                if pc in seen or pc < 0 or pc >= n: continue
                seen.add(pc)
                for q in succ(codes, pc, e, marks):
                    st.append(q)
            for pc in seen:
                if codes[pc] in TARGET: hits.setdefault((pc, codes[pc], bytes(codes[pc+1:pc+max(c.SIZES.get(codes[pc],1),1)]).hex()), set()).add((s, i))
    return hits
if __name__ == '__main__':
    import collections
    tot = collections.Counter(); sites = {}
    for mid in sorted(int(k) for k in c.idx):
        h = scan(mid)
        for (pc, op, ops), owners in h.items():
            sites[(mid, pc, op, ops)] = owners; tot[op] += 1
    print({hex(k): v for k, v in sorted(tot.items())}, "distinct instrs", len(sites))
    for k in sorted(sites):
        print(k, len(sites[k]))
