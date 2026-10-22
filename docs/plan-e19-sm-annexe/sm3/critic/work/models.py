"""Critic-own models (from scratch).
World = dict(n_last, slots=[dict(status, ref_id, flags, anim_flags, force_z, target_anim, platform, riding, xcoll, parent)], ref).
bin_*: literal reading of the disassembly (slots 0..N). dll_*: shape of EntitySearchService.cs + rules R0..R6 of the report."""


def act(s):
    return s['status'] in (1, 2, 3)


def bin_search(w, ref, t):
    S = w['slots']
    N = w['n_last']
    rng_all = [i for i in range(0, N + 1) if i < len(S)]
    rng_nh = [i for i in range(1, N + 1) if i < len(S)]
    if t & 0x80 == 0:
        if not act(S[ref]):
            return []
        return [i for i in rng_nh if (S[i]['ref_id'] & 0xFFFFFFFF) == t]
    f = t & 0x7F
    if f == 0:
        return [ref]
    if f == 1:
        return [0]
    if f == 2:
        return [i for i in rng_all if act(S[i])]
    if f == 3:
        return [i for i in rng_nh if act(S[i])]
    if f == 4:
        return [i for i in rng_all if act(S[ref]) and S[i]['flags'] & 0x80 and not S[i]['anim_flags'] & 0x80 and S[i]['platform'] is None]
    if f == 5:
        return [i for i in rng_nh if act(S[i]) and S[ref]['riding'] == i]
    if f == 6:
        return [i for i in rng_nh if act(S[i]) and S[i]['riding'] == ref]
    if f == 7:
        return [i for i in rng_nh if act(S[i]) and S[ref]['xcoll'] == i]
    if f == 8:
        return [i for i in rng_nh if act(S[i]) and S[i]['xcoll'] == ref]
    if f == 9:
        return [i for i in rng_nh if act(S[i]) and S[i]['parent'] == ref]
    if f == 10:
        return [i for i in rng_nh if act(S[i]) and S[ref]['parent'] == i]
    if f == 11:
        return [i for i in rng_nh if act(S[i]) and S[i]['platform'] is not None]
    raise ValueError("fatal")


def sg(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x


def handlers(search, w, ref, instr, grid=None):
    """Result for instr using the given search function; semantic of each opcode as read in the disassembly"""
    S = w['slots']
    op = instr[0]
    if op == 0xA9:
        g = search(w, ref, instr[1])
        x = S[ref]['xcoll']
        return int(any(x == i for i in g))
    if op == 0xAA:
        g = search(w, ref, instr[1])
        return int(any(S[i]['xcoll'] == ref for i in g))
    if op == 0xB2:
        g1 = search(w, ref, instr[1])
        if not g1:
            return 0
        g2 = search(w, ref, instr[2])
        return int(any(a == b for a in g1 for b in g2))
    if op in (0xB4, 0xB5, 0xB6):
        g = search(w, ref, instr[1])
        if not g:
            return 0
        f = sg(S[g[0]]['force_z'])
        return int({0xB4: f > 0, 0xB5: f < 0, 0xB6: f == 0}[op])
    if op == 0xB7:
        g = search(w, ref, instr[1])
        return int(any((S[i]['target_anim'] & 0xFFFFFFFF) == instr[2] for i in g))
    raise ValueError(hex(op))


def nine_e(instr, grid):
    _, x, y, wd, h, mn = instr
    cnt = 0
    for r in range(h):
        for c in range(wd):
            idx = (y + r) * 52 + x + c
            cnt += 1 if (grid[idx] & 2) else 0
    return int(cnt >= mn)


def dll_search(w, ref, t):
    """candidate list = non-free slots in slot order (hero is slot 0 = IsPlayer)"""
    S = w['slots']
    N = w['n_last']
    sp = [i for i in range(0, N + 1) if i < len(S) and S[i]['status'] != 0]

    def isp(i):
        return i == 0
    if t & 0x80 == 0:
        return [c for c in sp if S[c]['ref_id'] == t] if act(S[ref]) else []
    f = t & 0x7F
    if f == 0:
        return [ref]
    if f == 1:
        return [0] if 0 in sp else []
    if f == 2:
        return [c for c in sp if act(S[c])]
    if f == 3:
        return [c for c in sp if act(S[c]) and not isp(c)]
    if f == 4:
        return [c for c in sp if act(S[ref]) and S[c]['flags'] & 0x80 and not S[c]['anim_flags'] & 0x80 and S[c]['platform'] is None]
    if f == 5:
        return [c for c in sp if not isp(c) and act(S[c]) and S[ref]['riding'] == c]
    if f == 6:
        return [c for c in sp if not isp(c) and act(S[c]) and S[c]['riding'] == ref]
    if f == 7:
        return [c for c in sp if not isp(c) and act(S[c]) and S[ref]['xcoll'] == c]
    if f == 8:
        return [c for c in sp if not isp(c) and act(S[c]) and S[c]['xcoll'] == ref]
    if f == 9:
        return [c for c in sp if not isp(c) and act(S[c]) and S[c]['parent'] == ref]
    if f == 10:
        return [c for c in sp if not isp(c) and act(S[c]) and S[ref]['parent'] == c]
    if f == 11:
        return [c for c in sp if not isp(c) and act(S[c]) and S[c]['platform'] is not None]
    return []
