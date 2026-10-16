"""k1.py - E20.c1b task K1: the three equipment records 0x80146E00 and the 255-byte hero sheet 0x80127008, produced by the REAL UpdateItemEffectState 0x800307E8
on inventories built with the REAL new-game code (InitPlayerStats, AddItem, SetPlayerWeaponId, SetCurrentItemId).

For every inventory: the real output, the independent model (kmodel.py) and their comparison.  Then the dependency analysis: which input bytes each output byte depends on,
measured by perturbing one input byte at a time (record bytes of the blob, inventory counts, weapon slot, equipped item) and re-running the real function.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import kmodel as M
import world as W
import c1world as C

LEVEL_D = 0                 # map 14 Info.D = 0 (asserted by c1world.setup against map_14.json)


def fresh(inv):
    m, meta = K.setup_inv(inv)
    W.write_pad(m, W.Pad())                      # 0x80126F28 (pad hold word) is read by the regeneration gate; the tick loop writes it every tick, here it is zeroed
    m.uninit_reads.clear()
    return m, meta


def outputs(m):
    """everything 0x800307E8 leaves that K1 looks at"""
    return dict(records=[(m.peek(K.EQUIP + 8 * k), m.peek(K.EQUIP + 8 * k + 4)) for k in range(3)], sheet=K.read_sheet(m), boots=m.peeks(K.BOOTS))


def hero_record_bytes(m):
    """the record the hero's +0x1C4 points at, as the disc file holds it (offset = pointer - blob base)"""
    p = m.peek(K.HERO + 0x1C4)
    off = p - K.BLOB
    return M.blob()[off:off + 255], off


def case(inv):
    m, meta = fresh(inv)
    pre = dict(stats=K.hero_stats(m), owned={n: K.owned_count(m, n) for n in sorted(set(inv['owned']) | {1, 17, 25})},
               weapon_item_before=K.call(m, 0x8004E030) & M.M32)
    ret = K.call(m, 0x800307E8)
    out = outputs(m)
    post = dict(stats=K.hero_stats(m), weapon_item=K.call(m, 0x8004E030) & M.M32)
    recs = K.read_records(m)
    # ---- model
    mi = M.Inventory(inv['owned'], inv['weapon'], inv['item'])
    mrecs = M.model_records(mi, LEVEL_D)
    if mrecs[0]:
        base = M.blob_slice(mrecs[0], 255)
    else:
        base, _ = hero_record_bytes(m)
    msheet = M.model_sheet(mrecs, base)
    mrec_words = [((K.BLOB + r[0]) if r else 0, r[1] if r else 0) for r in mrecs]
    # ---- second call: 0x8004E0F8 rewrote stats.ItemId with the best item of its slot, so a second call can pick another accessory record
    ret_b = K.call(m, 0x800307E8)
    out_b = outputs(m)
    stats_b = K.hero_stats(m)
    mi.advance()
    mrecs_b = M.model_records(mi, LEVEL_D)
    base_b = M.blob_slice(mrecs_b[0], 255) if mrecs_b[0] else hero_record_bytes(m)[0]
    mwords_b = [((K.BLOB + r[0]) if r else 0, r[1] if r else 0) for r in mrecs_b]
    second_equal = (mwords_b == out_b['records']) and (M.model_sheet(mrecs_b, base_b) == out_b['sheet'])
    mi = M.Inventory(inv['owned'], inv['weapon'], inv['item'])
    checks = dict(
        records_equal=(mrec_words == out['records']),
        sheet_equal=(msheet == out['sheet']),
        boots_equal=(M.model_boots(mi) == out['boots']),
        weapon_equal=(mi.current_weapon() & M.M32) == post['weapon_item'], second_equal=second_equal, kit_loop=(K.kit_by_real_loop(m) == list(K.KIT_ITEMS)))
    res = dict(inv=inv['name'], desc=inv['desc'], pre=pre, post=post, ret=ret & M.M32, records=recs, record_words=out['records'], sheet=out['sheet'].hex(), boots=out['boots'],
               aux=K.read_aux(m), model_records=mrec_words, model_sheet=msheet.hex(), checks=checks, uninit=sorted((hex(pc), hex(a), n) for (pc, a, n) in m.uninit_reads),
               hero_rec_base=('record [0]' if mrecs[0] else 'hero +0x1C4 record'), ok=all(checks.values()) and not m.uninit_reads, second=dict(record_words=out_b['records'], item_id=stats_b['item_id']))
    return res, m


def fmt_case(r):
    L = ['== K1 %s: %s' % (r['inv'], r['desc']),
         '   before the call: stats WeaponId %d ItemId %d; GetItemIdFromCurrentWeapon (0x8004E030) = %s; owned counts %s' % (
             r['pre']['stats']['weapon_id'], r['pre']['stats']['item_id'], sx(r['pre']['weapon_item_before']), r['pre']['owned']),
         '   after  the call: stats WeaponId %d ItemId %d (0x8004E0F8 rewrites ItemId with the best item of its slot); equipped weapon item %s; v0 = 0x%08X' % (
             r['post']['stats']['weapon_id'], r['post']['stats']['item_id'], sx(r['post']['weapon']) if 'weapon' in r['post'] else sx(r['post']['weapon_item']), r['ret'])]
    for rec in r['records']:
        w = r['record_words'][rec['k']]
        if not rec['ptr']:
            L.append('   record [%d] at 0x%08X: pointer 0x%08X (null), item word %d' % (rec['k'], K.EQUIP + 8 * rec['k'], w[0], w[1]))
        else:
            L.append('   record [%d] at 0x%08X: pointer 0x%08X (blob + %d), item word %d (= item %d + 0x1E); level %d size %d MaxHp %d; responses %s; AttackCount %d %s' % (
                rec['k'], K.EQUIP + 8 * rec['k'], rec['ptr'], rec['blob_offset'], rec['item_word'], rec['item_word'] - 0x1E, rec['level'], rec['size'], rec['max_hp'],
                ' '.join('%02X' % x for x in rec['responses']), rec['attack_count'], rec['attacks']))
    sh = bytes.fromhex(r['sheet'])
    L.append('   hero sheet 0x%08X (255 bytes), base = %s; header %s; responses [3..13] = %s; AttackCount [14] = %d' % (
        K.SHEET, r['hero_rec_base'], ' '.join('%02X' % x for x in sh[:3]), ' '.join('%02X' % x for x in sh[3:14]), sh[14]))
    for i in range(0, 255, 16):
        L.append('      +%03d: %s' % (i, ' '.join('%02X' % x for x in sh[i:i + 16])))
    L.append('   boots level [0x80127000] = %d; regeneration counters [0x80126FE8..] = %s' % (r['boots'], r['aux']['regen']))
    sec = r['second']
    L.append('   second call: stats.ItemId is now %d; record words %s%s; equal to the model (which applies the ItemId rewrite): %s' % (
        sec['item_id'], sec['record_words'], ' (SAME as the first call)' if sec['record_words'] == r['record_words'] else ' (DIFFERENT from the first call)', eq(r['checks']['second_equal'])))
    L.append('   model (kmodel.py, independent): records %s, sheet %s, boots %s, equipped weapon %s; unwritten-byte reads: %s  =>  %s' % (
        eq(r['checks']['records_equal']), eq(r['checks']['sheet_equal']), eq(r['checks']['boots_equal']), eq(r['checks']['weapon_equal']), r['uninit'] or 'none', 'OK' if r['ok'] else 'FAIL'))
    return L


def eq(b):
    return 'EQUAL' if b else 'DIFFERENT'


def sx(v):
    return '%d' % (v - (1 << 32) if v & 0x80000000 else v)


# ---- dependency analysis ---------------------------------------------------------------------------------------------------
MASKS = (0x01, 0x40, 0x80)


def perturb_records(sample=None):
    """inventory 'all' (the three records non-null): flip one blob byte of one record at a time (record [0]: bytes 0..254, records [1] and [2]: bytes 0..31), re-run the real function.
       sample: restrict to every `sample`-th byte (for the second interpreter run)."""
    inv = K.INV['all']
    m, meta = fresh(inv)
    K.call(m, 0x800307E8)
    base = outputs(m)
    recs = [m.peek(K.EQUIP + 8 * k) for k in range(3)]
    snap = C.Sandbox(m)
    dep = {}                      # (record k, byte j, mask) -> sorted list of changed outputs
    for k, span in ((0, 255), (1, 32), (2, 32)):
        for j in range(span):
            if sample and (j % sample) and not (3 <= j <= 13) and j not in (0, 1, 2, 14):
                continue
            for mask in MASKS:
                a = recs[k] + j
                old = m.peek(a) & 0xFF
                m.poke8(a, old ^ mask)
                K.call(m, 0x800307E8)
                o = outputs(m)
                ch = []
                for i in range(255):
                    if o['sheet'][i] != base['sheet'][i]:
                        ch.append('sheet[%d]' % i)
                for q in range(3):
                    if o['records'][q] != base['records'][q]:
                        ch.append('record[%d]' % q)
                if o['boots'] != base['boots']:
                    ch.append('boots')
                dep[(k, j, mask)] = ch
                snap.restore()
    return base, recs, dep


def summarize_dep(dep, recs, sample=None):
    """structure of the measured dependency: per sheet byte, the set of (record, byte offset) inputs that changed it"""
    by_out = {}
    for (k, j, mask), ch in dep.items():
        for o in ch:
            by_out.setdefault(o, set()).add((k, j))
    L = []
    resp = set('sheet[%d]' % i for i in range(3, 14))
    bad = []
    seen_inputs = {(k, j) for (k, j, mask) in dep}
    for i in range(255):
        o = 'sheet[%d]' % i
        got = by_out.get(o, set())
        if i in range(3, 14):
            want = {(0, i), (1, i), (2, i)}
        else:
            want = {(0, i)}
        # inputs that were not sampled cannot be asserted
        want = {w for w in want if w in seen_inputs}
        if got != want:
            bad.append((o, sorted(got), sorted(want)))
    other = sorted(o for o in by_out if not o.startswith('sheet['))
    L.append('   structure measured (flip of 1 bit pattern in {0x01, 0x40, 0x80} of one blob byte, real function re-run): every sheet byte i outside [3..13] changed only with byte i of record [0] '
             '(the 255-byte copy), every response byte i in [3..13] only with byte i of record [0], [1] and [2]; deviations: %s; record pointers / item words / boots depend on no record byte: %s' % (
                 bad if bad else 'none', 'confirmed' if not other else other))
    # per response index, which flag patterns matter (rec bytes 3 as the representative)
    for k in (0, 1, 2):
        row = []
        for mask in MASKS:
            ch = dep.get((k, 3, mask))
            if ch is not None:
                row.append('xor 0x%02X: %s' % (mask, ','.join(ch) if ch else 'no change'))
        L.append('   record [%d] byte 3 (response of nibble 1): %s' % (k, '; '.join(row)))
    for k in (0, 1, 2):
        row = []
        for j in (0, 1, 2, 14):
            c = []
            for mask in MASKS:
                ch = dep.get((k, j, mask))
                if ch is not None:
                    c.append(','.join(ch) if ch else '-')
            if c:
                row.append('byte %d: %s' % (j, ' | '.join(c)))
        L.append('   record [%d] header bytes (level, size, MaxHp, AttackCount): %s' % (k, '; '.join(row)))
    return L, not bad and not other, bad


def perturb_inventory():
    """kit base; for each item n in 0..0x62: AddItem(n); then also SetCurrentItemId(n); then SetPlayerWeaponId(s) for s in -1, 1..6.  Re-run the real function, list which outputs changed,
       and compare every outcome with the model."""
    inv = K.INV['kit']
    m, meta = fresh(inv)
    K.call(m, 0x800307E8)
    base = outputs(m)
    snap = C.Sandbox(m)
    rows = []
    allok = True
    for n in range(0, 0x63):
        snap.restore()
        K.call(m, 0x8004E530, n)
        K.call(m, 0x800307E8)
        o = outputs(m)
        d1 = [q for q in range(3) if o['records'][q] != base['records'][q]]
        sheet1 = o['sheet'] != base['sheet']
        boots1 = o['boots'] != base['boots']
        mi = M.Inventory(list(inv['owned']) + [n], inv['weapon'], inv['item'])
        # ... and equipped as the accessory
        K.call(m, 0x8004E4D8, n)
        K.call(m, 0x800307E8)
        o2 = outputs(m)
        d2 = [q for q in range(3) if o2['records'][q] != base['records'][q]]
        mi2 = M.Inventory(list(inv['owned']) + [n], inv['weapon'], n)
        ok = True
        for mm, oo in ((mi, o), (mi2, o2)):
            mrecs = M.model_records(mm, LEVEL_D)
            words = [((K.BLOB + r[0]) if r else 0, r[1] if r else 0) for r in mrecs]
            base_bytes = M.blob_slice(mrecs[0], 255) if mrecs[0] else hero_record_bytes(m)[0]
            ok &= (words == oo['records']) and (M.model_sheet(mrecs, base_bytes) == oo['sheet']) and (M.model_boots(mm) == oo['boots'])
        allok &= ok
        rows.append(dict(n=n, cat=M.category(n), added=d1, sheet_added=sheet1, boots_added=boots1, equipped=d2, model_ok=ok))
    wrows = []
    for s in (-1, 1, 2, 3, 4, 5, 6):
        snap.restore()
        K.call(m, 0x8004E484, s)
        K.call(m, 0x800307E8)
        o = outputs(m)
        mi = M.Inventory(inv['owned'], s, inv['item'])
        mrecs = M.model_records(mi, LEVEL_D)
        words = [((K.BLOB + r[0]) if r else 0, r[1] if r else 0) for r in mrecs]
        ok = words == o['records']
        allok &= ok
        wrows.append(dict(slot=s, records=[q for q in range(3) if o['records'][q] != base['records'][q]], rec1_word=o['records'][1][1], model_ok=ok))
    return rows, wrows, allok


def fmt_inventory_deps(rows, wrows):
    L = ['   inventory side (kit + one more item n; then n also set as the equipped item):']
    groups = {}
    for r in rows:
        key = (tuple(r['added']), r['sheet_added'], r['boots_added'], tuple(r['equipped']))
        groups.setdefault(key, []).append(r['n'])
    for key in sorted(groups):
        added, sheet, boots, equipped = key
        L.append('      items %s: AddItem changes records %s%s%s; AddItem + SetCurrentItemId changes records %s' % (
            fmt_ranges(groups[key]), list(added) or 'none', ', hero sheet' if sheet else '', ', boots level' if boots else '', list(equipped) or 'none'))
    L.append('   weapon slot side (kit, SetPlayerWeaponId(s)): ' + '; '.join('s=%d -> changes records %s (record [1] item word %d)' % (w['slot'], w['records'] or 'none', w['rec1_word']) for w in wrows))
    return L


def fmt_ranges(v):
    out = []
    i = 0
    while i < len(v):
        j = i
        while j + 1 < len(v) and v[j + 1] == v[j] + 1:
            j += 1
        out.append('%d' % v[i] if i == j else '%d-%d' % (v[i], v[j]))
        i = j + 1
    return ','.join('0x%X' % int(x.split('-')[0]) if '-' not in x else '0x%X-0x%X' % tuple(int(y) for y in x.split('-')) for x in out)


def item_table():
    """data of every item of category 1, 2, 3 (the only ones 0x800307E8 can put in a record), read from the disc BALANCE.BIN at level D = 0 with the EXE category and property tables"""
    L = ['== K1 data: every item that can fill an equipment record (category 1 weapon -> record [1], 2 armour -> record [0], 3 accessory -> record [2]); record = entry item + 0x1E of the disc BALANCE.BIN at level D = 0']
    counts = {}
    for n in range(0x62):
        cat = M.category(n)
        if cat not in (1, 2, 3):
            continue
        off = M.item_record_offset(n, LEVEL_D)
        r = M.blob()[off:off + 0x20]
        cnt = r[0xE]
        counts[cnt] = counts.get(cnt, 0) + 1
        p = M.prop(n)
        resp = r[3:14]
        L.append('   item 0x%02X (%3d) cat %d slot %2d rank %d | record blob+%d level %d size %d MaxHp %d responses %s | AttackCount %d attacks %s' % (
            n, n, cat, p['slot'], p['rank'], off, r[0], r[1], r[2], ('all %02X' % resp[0]) if len(set(resp)) == 1 else ' '.join('%02X' % b for b in resp), cnt,
            [(r[0xF + 2 * i], r[0x10 + 2 * i]) for i in range(cnt)]))
    L.append('   AttackCount distribution over these items: %s (so the attack index rule g + 1 < count ? g + 1 : 0 always selects Attacks[0] for real equipment)' % ', '.join('%d: %d items' % (k, counts[k]) for k in sorted(counts)))
    return L, max(counts) <= 1


def combo_sweep(seed=20261010, nrandom=4000):
    """the response combination 0x80030B64-0x80030C0C on many (b, A, B) triples: b = byte of record [0] (the copied base), A = byte of record [1], B = byte of record [2], poked into the
       blob bytes the three records point at (inventory 'all'); 11 triples per call of the real function (response indices 1..11 are independent).  Compared with kmodel.combine_response."""
    import random
    inv = K.INV['all']
    m, meta = fresh(inv)
    K.call(m, 0x800307E8)
    recs = [m.peek(K.EQUIP + 8 * k) for k in range(3)]
    grid = [0x00, 0x0C, 0x10, 0x1F, 0x30, 0x40, 0x4C, 0x80, 0xC0]
    triples = [(b, a, c) for b in grid for a in grid for c in grid]
    rnd = random.Random(seed)
    triples += [(rnd.randrange(256), rnd.randrange(256), rnd.randrange(256)) for _ in range(nrandom)]
    while len(triples) % 11:
        triples.append((0x10, 0x10, 0x10))
    bad = []
    hist = {}
    for c0 in range(0, len(triples), 11):
        chunk = triples[c0:c0 + 11]
        for i, (b, a, c) in enumerate(chunk):
            m.poke8(recs[0] + 3 + i, b)
            m.poke8(recs[1] + 3 + i, a)
            m.poke8(recs[2] + 3 + i, c)
        K.call(m, 0x800307E8)
        for i, (b, a, c) in enumerate(chunk):
            got = m.peek(K.SHEET + 3 + i) & 0xFF
            want = M.combine_response(b, a, c)
            hist[got] = hist.get(got, 0) + 1
            if got != want:
                bad.append((b, a, c, got, want))
    return len(triples), bad, hist


def run(sample=None):
    """returns (lines, results dict, ok)"""
    lines = []
    results = {}
    ok = True
    for name, inv in K.INV.items():
        r, m = case(inv)
        results[name] = r
        lines += fmt_case(r)
        lines.append('')
        ok &= r['ok']
    il, iok2 = item_table()
    lines += il
    lines.append('')
    ok &= iok2
    base, recs, dep = perturb_records(sample)
    dl, dok, bad = summarize_dep(dep, recs, sample)
    lines.append('== K1 dependency of the outputs on the input bytes (inventory "all": Holy Sword in slot [1], leather armour in slot [0], ring 0x33 in slot [2])')
    lines += dl
    ok &= dok
    rows, wrows, iok = perturb_inventory()
    lines += fmt_inventory_deps(rows, wrows)
    lines.append('   inventory-side model comparison: %s' % ('every one of the %d AddItem, %d AddItem+SetCurrentItemId and %d SetPlayerWeaponId variants equals the model' % (len(rows), len(rows), len(wrows)) if iok else 'DIFFERENT'))
    ok &= iok
    ntr, cbad, hist = combo_sweep()
    lines.append('   response combination sweep: %d (b, A, B) triples (9 x 9 x 9 grid + 4000 random, seed 20261010), 11 per call of the real function: %s; result values seen: %d distinct (0x80 x %d, 0x40 x %d, 0x1F x %d, 0x00 x %d, 0x10 x %d)' % (
        ntr, 'all equal to kmodel.combine_response' if not cbad else 'DIFFERENT %s' % cbad[:5], len(hist), hist.get(0x80, 0), hist.get(0x40, 0), hist.get(0x1F, 0), hist.get(0, 0), hist.get(0x10, 0)))
    ok &= not cbad
    results['dep'] = dict(structure_ok=dok, deviations=bad, inventory_rows=rows, weapon_rows=wrows,
                          n_runs=len(dep), changed=sorted((k, j, mask, ch) for (k, j, mask), ch in dep.items() if ch))
    return lines, results, ok


if __name__ == '__main__':
    ls, res, ok = run(sample=int(sys.argv[1]) if len(sys.argv) > 1 else None)
    print('\n'.join(ls))
    print('K1', 'PASS' if ok else 'FAIL')
