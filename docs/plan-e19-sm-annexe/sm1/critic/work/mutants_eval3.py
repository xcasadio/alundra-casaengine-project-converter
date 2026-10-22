"""Critic: recompute the mutant kill sets of the preparation on MY model, with the montages EXACTLY as the README states them
(prod: owner = the logic proxy, LogicEntity null; prod-owner-hero: owner = hero with LogicEntity = the logic proxy; anon: bare proxies;
'prod+ctx': both the production shape and the shape of the existing fake Context). A test is red under a mutant when ANY of its
vectors gives a Result different from the frozen binary Result. Size mutants are red under every test (every test asserts CodeIndex == size)."""
import sys, json, collections
sys.path.insert(0, 'work')
import mymodel as M

VEC = sys.argv[1]
TESTS = sys.argv[2]
THEIRS = sys.argv[3]

vecs = {}
for line in open(VEC, encoding='utf-8'):
    line = line.strip().rstrip(',')
    if line.startswith('{"anon"'):
        v = json.loads(line)
        vecs[v['id']] = v


def s32(x):
    x &= 0xFFFFFFFF
    return x - 2**32 if x & 0x80000000 else x


def world(v):
    proxies = []
    for i, s in enumerate(v['slots']):
        ctx = None if i in v['anon'] else 'auto'
        proxies.append(M.P(i, s['status'], s['ref'], s['flags'], s32(s['anim']), [s32(t) for t in s['tile']], blocked=s['blocked'], ctx=ctx, is_player=(i == 0)))
    fp = M.P(99, 2, 77, 0, 0, [0, 0, 0], ctx='F')
    for i, s in enumerate(v['slots']):
        p = proxies[i]
        def idty(x):
            if x is None:
                return None
            if x == 'foreign':
                return 'FOREIGN'
            return proxies[x].ctx
        p.riding = idty(s['riding'])
        p.parent = idty(s['parent'])
        p.platform = idty(s['platform'])
        x = s['xcoll']
        p.xcoll = None if x is None else (fp if x == 'foreign' else proxies[x])
    return proxies


def run_mut(op, code, proxies, logic_idx, owner, hero_in_list, mut):
    player = proxies[0]
    spawned = proxies if hero_in_list else proxies[1:]
    entity = proxies[logic_idx]
    ref = owner if mut == 'owner_instead_of_logic' else entity
    size = 2 if op in (0x60, 0x61) else 7
    result = 'unchanged'
    if op in (0x60, 0x61):
        o = op
        if mut == 'swap_60_61':
            o = 0x61 if op == 0x60 else 0x60
        pl = None if mut == 'no_player_in_search' else player
        group = M.search(ref, code[1], spawned, pl)
        if mut == 'first_only':
            group = group[:1]
        if mut == 'last_only':
            group = group[-1:]
        carrier = ref.ctx
        res = 0
        extra_ok = lambda m_: True
        if mut == 'member_active':
            extra_ok = lambda m_: m_.status in (2, 3)
        elif mut == 'member_not_blocked':
            extra_ok = lambda m_: not m_.blocked
        elif mut == 'logic_active':
            extra_ok = lambda m_: ref.status in (2, 3)
        elif mut == 'logic_not_blocked':
            extra_ok = lambda m_: not ref.blocked
        elif mut == 'member_loaded_normal_deact':
            extra_ok = lambda m_: m_.status in (1, 2, 3)
        elif mut == 'logic_loaded_normal_deact':
            extra_ok = lambda m_: ref.status in (1, 2, 3)
        if o == 0x60:
            for m in group:
                if not extra_ok(m):
                    continue
                if mut == 'reuse_IsRidingTheLogicEntity':
                    ok = m.status in (2, 3) and not m.blocked and m.riding == carrier
                elif mut == 'no_null_guard':
                    ok = m.riding == carrier
                elif mut == 'platform_instead_of_riding':
                    ok = m.platform is not None and m.platform == carrier
                else:
                    ok = m.riding is not None and m.riding == carrier
                if ok:
                    res = 1
                    break
        else:
            for m in group:
                if not extra_ok(m):
                    continue
                if mut == 'no_null_guard':
                    ok = ref.riding == m.ctx
                elif mut == 'platform_instead_of_riding':
                    ok = ref.platform is not None and ref.platform == m.ctx
                else:
                    ok = ref.riding is not None and ref.riding == m.ctx
                if ok:
                    res = 1
                    break
        if mut == 'empty_group_keeps_result' and not group:
            res = 'unchanged'
        if mut == 'empty_group_one' and not group:
            res = 1
        if mut == 'result_always_one':
            res = 1
        if mut == 'size_one':
            size = 1
        if mut == 'size_three':
            size = 3
        return size, res
    # zone
    b = list(code)
    if mut == 'signed_operands':
        b = [x - 256 if x >= 128 else x for x in b]
    ground = (op == 0x3C)
    if mut == 'zone_3c_equals_3d':
        ground = False
    if mut == 'zone_3d_equals_3c':
        ground = True
    cands = ([] if mut == 'spawned_only' else [player]) + list(spawned)
    if mut == 'skip_first_spawned':
        cands = [player] + list(spawned)[1:]
    if mut == 'skip_last':
        cands = cands[:-1]
    if mut == 'logic_only':
        cands = [entity]
    res = 0
    for c in cands:
        if mut == 'hero_status_bypass' and c is player:
            pass
        elif mut == 'no_status_filter':
            pass
        elif mut == 'active_only':
            if c.status not in (2, 3):
                continue
        elif not c.live:
            continue
        if ground and mut == 'hero_bypass_eligibility' and c is player:
            pass
        elif ground:
            if mut == 'inverted_3c':
                el = (not (c.flags & 0x80)) and bool(c.anim & 0x80) and c.platform is None
            elif mut == '3c_no_collidable':
                el = (c.anim & 0x80) == 0 and c.platform is None
            elif mut == '3c_no_anim':
                el = (c.flags & 0x80) != 0 and c.platform is None
            elif mut == '3c_no_platform':
                el = (c.flags & 0x80) != 0 and (c.anim & 0x80) == 0
            elif mut == '3c_flags_equal':
                el = c.flags == 0x80 and (c.anim & 0x80) == 0 and c.platform is None
            elif mut == '3c_anim_nonzero':
                el = (c.flags & 0x80) != 0 and c.anim == 0 and c.platform is None
            else:
                el = M.eligible(c)
            if not el:
                continue
        x, y, z = c.tile
        if mut == 'axes_xy_swapped':
            x, y = y, x
        if mut == 'axes_yz_swapped':
            y, z = z, y
        if mut == 'byte_truncated_tile':
            x, y, z = x & 0xFF, y & 0xFF, z & 0xFF
        if mut == 'unsigned_tile':
            x, y, z = x & 0xFFFFFFFF, y & 0xFFFFFFFF, z & 0xFFFFFFFF
            b = [t & 0xFFFFFFFF for t in b]
        lo_ok = (lambda lo, t: lo < t) if mut == 'lower_exclusive' else (lambda lo, t: lo <= t)
        hi_ok = (lambda hi, t: t < hi) if mut == 'upper_exclusive' else (lambda hi, t: t <= hi)
        zok = True if mut == 'z_ignored' else (lo_ok(b[5], z) and hi_ok(b[6], z))
        if lo_ok(b[1], x) and hi_ok(b[2], x) and lo_ok(b[3], y) and hi_ok(b[4], y) and zok:
            res = 1
            break
    if mut == 'result_only_on_true':
        if res == 0:
            res = 'unchanged'
    if mut == 'size_six':
        size = 6
    if mut == 'size_eight':
        size = 8
    return size, res


MUT60 = ['member_active', 'member_not_blocked', 'logic_active', 'logic_not_blocked', 'member_loaded_normal_deact', 'logic_loaded_normal_deact', 'swap_60_61', 'no_player_in_search', 'first_only', 'last_only', 'owner_instead_of_logic', 'reuse_IsRidingTheLogicEntity', 'no_null_guard',
         'platform_instead_of_riding', 'empty_group_keeps_result', 'empty_group_one', 'result_always_one', 'size_one', 'size_three']
MUTZ = ['skip_first_spawned', 'hero_status_bypass', 'hero_bypass_eligibility', 'zone_3c_equals_3d', 'zone_3d_equals_3c', 'signed_operands', 'spawned_only', 'skip_last', 'logic_only', 'no_status_filter', 'active_only', 'inverted_3c',
        '3c_no_collidable', '3c_no_anim', '3c_no_platform', '3c_flags_equal', '3c_anim_nonzero', 'axes_xy_swapped', 'axes_yz_swapped', 'upper_exclusive',
        'lower_exclusive', 'z_ignored', 'byte_truncated_tile', 'unsigned_tile', 'size_six', 'size_eight', 'result_only_on_true']

rows = [l.rstrip('\n').split('\t') for l in open(TESTS, encoding='utf-8')][1:]
kills = collections.defaultdict(list)
for r in rows:
    tid, cls, meth, ops, montage, vids = r[0], r[1], r[2], r[3], r[4], r[5]
    if vids == '-':
        continue
    for mut in MUT60 + MUTZ:
        killed = False
        for vid in vids.split():
            v = vecs[vid]
            if mut in MUT60 and v['op'] not in (0x60, 0x61):
                continue
            if mut in MUTZ and v['op'] not in (0x3C, 0x3D):
                continue
            hero_modes = (True,) if montage in ('prod', 'anon', 'prod-owner-hero') else (True, False)
            for hil in hero_modes:
                px = world(v)
                owner = px[0] if montage == 'prod-owner-hero' else px[v['logic']]
                size, res = run_mut(v['op'], v['code'], px, v['logic'], owner, hil, mut)
                exp_size = 2 if v['op'] in (0x60, 0x61) else 7
                exp = v['binary']['result']
                if size != exp_size or res != exp:
                    killed = True
        if killed:
            kills[mut].append(tid)

theirs = {}
for l in open(THEIRS, encoding='utf-8').read().splitlines()[1:]:
    m, ops, k = l.split('\t')
    theirs[m] = set(k.split()) if not k.startswith('NONE') else set()
out = []
for mut in MUT60 + MUTZ:
    a = set(kills[mut])
    b = theirs.get(mut, None)
    if b is None:
        print('NEW MUTANT', mut, 'killed by', sorted(a)); continue
    if a != b:
        out.append((mut, sorted(b - a), sorted(a - b)))
print("mutants with a different kill set (mut, only-theirs, only-mine):")
for o in out:
    print(" ", o)
print("mutants never killed by my evaluation:", [m for m in MUT60 + MUTZ if not kills[m]])
json.dump({m: kills[m] for m in MUT60 + MUTZ}, open('notes/my_mutant_kills.json', 'w'), indent=1)
