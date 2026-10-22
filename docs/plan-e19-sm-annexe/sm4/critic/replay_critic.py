"""Critic's replay: runs every replayable vector of the preparation's JSON on the REAL handlers (table 0x80098FAC) of ALUN_CD.EXE
in the critic's own interpreter (cmips.py), with the critic's own memory layout/loader. Compares against `expect` and `today`.
Usage: python -B replay_critic.py VECTOR_DIR"""
import json, sys, os
import cmips

ENT0 = 0x80127D30
STRIDE = 0x294
NADDR = 0x80134360
LISTBUF = 0x8013D8D4  # 1-based
STATE = 0x8013D9E8
STATSPTR = 0x80176318
STATS = 0x80176400
PAD = 0x80126F28
CODEA = 0x80150000
CURSOR = 0x80150100
TABLE = 0x80098FAC
WIN = 0x80100000

F = dict(parentSlot=(0x0C, 4, False), status=(0x10, 4, False), hp=(0x14, 4, True), hpMax=(0x18, 4, True), platform=(0x28, 4, False),
         refId=(0x48, 4, True), flags=(0x6C, 4, False), animFlags=(0xB4, 4, True), targetAnimId=(0x88, 4, False),
         targetDir=(0x8C, 4, False), ridingSlot=(0x12C, 4, False), xCollSlot=(0x130, 4, False),
         lastTargetAnimId=(0x26C, 4, False), lastTargetDir=(0x270, 4, False))
STATF = dict(hp=(0, True), hpMax=(2, True), mp=(4, True), mpMax=(6, True), money=(8, True))
PADF = dict(hold=0, pressed=2, released=4, interval=6)


def slot_addr(i):
    return ENT0 + i * STRIDE


def build(m, v):
    w = v['world']
    slots = w['slots']
    base = m.a(WIN)
    m.mem[base:base + 0x90000] = bytes(0x90000)
    N = w['slotCount'] - 1
    m.ww(NADDR, N, log=False)
    for i, s in enumerate(slots):
        ea = slot_addr(i)
        for k, (off, sz, sg) in F.items():
            if k in ('parentSlot', 'ridingSlot', 'xCollSlot'):
                val = slot_addr(s[k]) if s[k] >= 0 else 0
            elif k == 'platform':
                val = 0x80190000 + i * 4 if s['hasPlatform'] else 0
            else:
                val = s[k]
            m.ww(ea + off, val & cmips.M, log=False)
    st = w['stats']
    m.ww(STATSPTR, STATS, log=False)
    for k, (off, sg) in STATF.items():
        m.wh(STATS + off, st[k] & 0xFFFF, log=False)
    if w.get('pad'):
        for k, off in PADF.items():
            m.wh(PAD + off, w['pad'][k] & 0xFFFF, log=False)
    ins = list(v['instruction']) + [0xFF]
    for i, b in enumerate(ins):
        m.wb(CODEA + i, b, log=False)
    m.ww(CURSOR, CODEA, log=False)
    m.ww(STATE + 0x2C, 7, log=False)


def snapshot_window(m):
    b = m.a(WIN)
    return bytes(m.mem[b:b + 0x90000])


def run(m, v):
    build(m, v)
    pre = snapshot_window(m)
    m.stores.clear()
    m.search_returns = []
    m.jal_ret = {}
    op = v['instruction'][0]
    h = m.rw(TABLE + 4 * op)
    m.hooks = {0x8003C914: lambda mm: None, 0x8003C840: lambda mm: None}
    ret = m.call(h, a0=slot_addr(v['logicSlot']), a1=slot_addr(v['ownerSlot']), a2=CURSOR, a3=STATE)
    post = snapshot_window(m)
    return h, ret, pre, post, list(m.stores), list(m.search_returns)


def decode_stores(stores, nslots):
    out = {'slot': set(), 'stats': set(), 'pad': set(), 'state': set(), 'other': set()}
    for addr, sz, val in stores:
        done = False
        for i in range(nslots):
            ea = slot_addr(i)
            for k, (off, s, sg) in F.items():
                if ea + off <= addr < ea + off + 4:
                    out['slot'].add((i, k)); done = True
        if STATS <= addr < STATS + 10:
            for k, (off, sg) in STATF.items():
                if STATS + off <= addr < STATS + off + 2:
                    out['stats'].add(k); done = True
        if PAD <= addr < PAD + 8:
            for k, off in PADF.items():
                if PAD + off <= addr < PAD + off + 2:
                    out['pad'].add(k); done = True
        if STATE <= addr < STATE + 0x100:
            out['state'].add(addr - STATE); done = True
        if 0x8013D8D4 <= addr < STATE:
            done = True  # match buffer (internal)
        if 0x801FE000 <= addr < 0x80200000:
            done = True  # stack
        if not done:
            out['other'].add(addr)
    return out


def chg(pre, post, m, addr, n):
    o = m.a(addr) - m.a(WIN)
    return pre[o:o + n] != post[o:o + n]


def main(d):
    m = cmips.Machine()
    tot = 0; bad = 0
    problems = []
    stats = {}
    for op in ['72', '88', '96', '97', 'B3']:
        doc = json.load(open(os.path.join(d, 'op%s.json' % op)))
        cnt = 0; red = 0; green = 0
        for v in doc['vectors']:
            if v.get('outOfTable'):
                continue
            tot += 1; cnt += 1
            h, ret, pre, post, stores, sr = run(m, v)
            exp = v['expect']
            n = v['world']['slotCount']
            ok = True
            why = []
            if ret != exp['returned']:
                ok = False; why.append('returned %s != %s' % (ret, exp['returned']))
            dec = decode_stores(stores, n)
            if dec['other']:
                ok = False; why.append('stores elsewhere %s' % sorted(hex(x) for x in dec['other']))
            if dec['state']:
                ok = False; why.append('state stores %s' % sorted(dec['state']))
            expslot = {(w['slot'], w['field']): w['value'] for w in exp['slotWrites']}
            for i, s in enumerate(v['world']['slots']):
                for k, (off, sz, sg) in F.items():
                    after = m.rw(slot_addr(i) + off)
                    if sg:
                        after = cmips.s32(after)
                    if (i, k) in expslot:
                        ev = expslot[(i, k)]
                        if k in ('parentSlot', 'ridingSlot', 'xCollSlot', 'platform'):
                            continue
                        if (ev & cmips.M) != (after & cmips.M):
                            ok = False; why.append('slot%d.%s %s != %s' % (i, k, after, ev))
                    else:
                        if chg(pre, post, m, slot_addr(i) + off, 4):
                            ok = False; why.append('slot%d.%s changed unexpectedly' % (i, k))
            if dec['slot'] != set(expslot.keys()):
                ok = False; why.append('slot store set %s != %s' % (sorted(dec['slot']), sorted(expslot.keys())))
            expst = {w['field']: w['value'] for w in exp['statsWrites']}
            if dec['stats'] != set(expst):
                ok = False; why.append('stats store set %s != %s' % (sorted(dec['stats']), sorted(expst)))
            for k, (off, sg) in STATF.items():
                after = m.rh(STATS + off)
                if sg:
                    after = cmips.s16(after)
                if k in expst and after != expst[k]:
                    ok = False; why.append('stats.%s %s != %s' % (k, after, expst[k]))
            exppad = {w['field']: w['value'] for w in exp['padWrites']}
            if dec['pad'] != set(exppad):
                ok = False; why.append('pad store set %s != %s' % (sorted(dec['pad']), sorted(exppad)))
            for k, off in PADF.items():
                if k in exppad and m.rh(PAD + off) != exppad[k]:
                    ok = False; why.append('pad.%s %s != %s' % (k, m.rh(PAD + off), exppad[k]))
            if exp.get('matchedSlots') is not None and op in ('88', '96'):
                c = sr[0] if sr else 0
                got = [(m.rw(LISTBUF + 4 * j) - ENT0) // STRIDE for j in range(1, c + 1)]
                if got != exp['matchedSlots']:
                    ok = False; why.append('matched %s != %s' % (got, exp['matchedSlots']))
            changed = any(chg(pre, post, m, slot_addr(i) + F[k][0], 4) for (i, k) in expslot) \
                or any(chg(pre, post, m, STATS + STATF[k][0], 2) for k in expst) \
                or any(chg(pre, post, m, PAD + PADF[k], 2) for k in exppad)
            # a store of the same value is not a change: recompute strictly on the whole window outside the stack/buffer/state
            diffs = [i for i in range(0, 0x90000) if pre[i] != post[i]]
            real_changed = any(not (m.a(0x8013D8D4) - m.a(WIN) <= i < m.a(STATE) - m.a(WIN) + 0x100) for i in diffs)
            if real_changed != bool(v['today']['redToday']):
                ok = False; why.append('redToday %s but changed %s' % (v['today']['redToday'], real_changed))
            if real_changed:
                red += 1
            else:
                green += 1
            if not ok:
                bad += 1
                problems.append((v['id'], why))
        stats[op] = (cnt, red, green)
    print('replayed', tot, 'bad', bad)
    print('per opcode (replayed, red, green):', stats)
    for p in problems[:40]:
        print(p)
    return bad


if __name__ == '__main__':
    sys.exit(1 if main(sys.argv[1]) else 0)
