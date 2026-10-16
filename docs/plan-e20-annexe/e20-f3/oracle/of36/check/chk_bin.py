"""chk_bin.py - independent re-run (checker) of O-F3-6 on the E20.f3 MIPS machine.

Written from scratch (does NOT import the probe's fxlib/run_hit_effect). Reuses only the machine library (machine.py, world.py, pworld.py, c1world.py) by path.
REAL code: hit pass 0x80039300, effect creators 0x8003BE74/0x8003BDD8 (+ 0x8003B9C4, 0x8003BB14, 0x8003BFE8), entity effects pass 0x80038E84, slot pass 0x8003C410
(-> 0x8003BBDC animation step, 0x8003C284 position step). Frame order = 0x8002E058: [0x8003B388 = eight passes] then 0x8003C410.

usage: python -B chk_bin.py
"""
import sys
import json
sys.dont_write_bytecode = True
TOOLS = r"D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-f3/oracle/tools"
sys.path.insert(0, TOOLS)
sys.path.append(r'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
import pworld as P      # noqa: E402
import world as W       # noqa: E402

DATAS = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN"
MAPJ = P.MAP_ALUNDRA
SLOT0 = 0x80138608
BANK_RAM = 0x80153460
FXPTR = 0x80152800
BLOCK = 0x80126EC0
ATK = 0x801F8000
HERO = P.ENT0
BEAN = P.ENT0 + P.STRIDE


def sa(i):
    return SLOT0 + 0x80 * i


def build():
    m, meta = P.setup(item=None, hero_px=(100, 100), direction=24, targets=[(12, 12, 0)], seed=0x2468ACE0)
    for a in (0x8003BDD8, 0x8003BE74, 0x80038E84):
        assert a in m.hooks, hex(a)
        del m.hooks[a]
    # the global effect tables (29), laid out exactly where the JSON says (SpriteInfoMemoryAddress), reached through the block the real lookup 0x8003B9FC reads
    m.poke_bytes(BANK_RAM, open(DATAS, 'rb').read(0x9000))
    tabs = json.load(open(MAPJ, encoding='utf-8-sig'))['SpriteInfo']['SpriteEffectRecords']
    for i, t in enumerate(tabs):
        assert t['EffectId'] == 0x8000 + i, (i, t['EffectId'])
        assert 0x80000000 + t['SpriteInfoMemoryAddress'] - BANK_RAM == t['BinOffset']
        m.poke(FXPTR + 4 * i, 0x80000000 + t['SpriteInfoMemoryAddress'])
    m.poke(BLOCK + 0x1C, FXPTR)
    m.poke(BLOCK + 0x20, len(tabs))
    # free all 128 slots (what the map load 0x8003C1A4 does with sw zero 0x68(slot)); the template 0x8013C608 stays untouched (BSS)
    for i in range(128):
        m.poke(sa(i) + 0x68, 0)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    return m


def slot_state(m, i):
    a = sa(i)
    return dict(status=m.peek(a + 0x68), mode=m.peek(a + 0x34), id=m.peek(a + 0x6E) & 0xFF, anchor=m.peek(a + 0x38), cnt=m.peek(a + 0x7C) & 0xFF,
                done=m.peek(a + 0x7D) & 0xFF, seqcur=m.peek(a + 0x71) & 0xFF, fptr=m.peek(a + 0xC), bptr=m.peek(a + 8), sptr=m.peek(a + 0x74), depthoff=m.peek(a + 0x60))


def inject(m, attacker, target, nib, resp):
    saved = {off: m.peek(attacker + off) for off in (0x1D4, 0x1C8, 0x6C, 0x1FC, 0x200, 0x204)}
    m.poke8(ATK, nib if nib else 0x80)
    m.poke8(ATK + 1, 1)
    m.poke16(ATK + 2, 0)
    m.poke(attacker + 0x1D4, 1)
    m.poke(attacker + 0x1C8, ATK)
    if attacker == HERO:
        m.poke(attacker + 0x6C, m.peek(attacker + 0x6C) | 4)
    for src, dst in ((0x1D8, 0x1FC), (0x1DC, 0x200), (0x1E0, 0x204)):
        m.poke(attacker + dst, m.peek(target + src))
    m.poke(attacker + 0x220, 0)
    for off in (0x1D0, 0x1CC, 0x224):
        m.poke(target + off, 0)
    sheet = m.peek(target + 0x1C4)
    m.poke8(sheet + 2 + nib, resp)
    return saved


def run(who, nib, T, N, resp=0x10, slot_first=False):
    """who: 'hero' (hero hits Beannoid) or 'bean' (Beannoid hits hero, as the DLL test).  Returns rows (one per tick, state at the END of the tick)."""
    m = build()
    pad = W.Pad()
    attacker, target = (HERO, BEAN) if who == 'hero' else (BEAN, HERO)
    wanted = {6: 4, 10: 4, 7: 5, 9: 5}.get(nib)
    rows = []
    tracked = None
    sparks = []
    for t in range(1, T + N + 1):
        m.tick_no = t
        m.poke(0x80126E6C, 0x8011CB60)      # 0x8002E0F0-0x8002E0F8: display list pointer
        m.poke(0x8011CE60, 0)               # 0x8002E104: display count
        if slot_first:
            W.run_pass(m, 0x8003C410)       # COUNTERFACTUAL (executor's assumption): slot pass BEFORE the world tick
        pad.update(0)
        W.write_pad(m, pad)
        for a in W.PASSES:
            saved = None
            before = None
            if a == 0x80039300 and t == T:
                saved = inject(m, attacker, target, nib, resp)
                before = {i for i in range(128) if m.peek(sa(i) + 0x68) != 0}
            W.run_pass(m, a)
            if a == 0x80039300 and t == T:
                for off, v in saved.items():
                    m.poke(attacker + off, v)
                new = [i for i in range(128) if m.peek(sa(i) + 0x68) != 0 and i not in before]
                for i in new:
                    s = slot_state(m, i)
                    if s['id'] == wanted and s['anchor'] == target and tracked is None:
                        tracked = i
                        tracked_born = dict(s)
                    if s['id'] == 9:
                        sparks.append(i)
        if not slot_first:
            W.run_pass(m, 0x8003C410)       # 0x8002E110
        row = dict(t=t, d=t - T, hitc=m.peeks(attacker + 0x220))
        n = m.peek(0x8011CE60)
        lst = [m.peek(0x8011CB60 + 4 * k) for k in range(n)]
        row['sparks'] = {i: (m.peek(sa(i) + 0x68), (sa(i) + 0xC) in lst) for i in sparks}
        if tracked is not None:
            s = slot_state(m, tracked)
            s['drawn'] = (sa(tracked) + 0xC) in lst
            row['s'] = s
        rows.append(row)
    return rows, tracked, (tracked_born if tracked is not None else None), sparks


def summarize(rows, T, label):
    tr = [r for r in rows if 's' in r]
    if not tr:
        return '%s: no effect slot tracked' % label
    act = [r['d'] for r in tr if r['s']['status'] == 2]
    drawn = [r['d'] for r in tr if r['s']['drawn']]
    done = [r['d'] for r in tr if r['s']['done'] == 1]
    firstabs = [r['d'] for r in tr if r['d'] >= 0 and r['s']['status'] == 0]
    return '%s: end-of-tick active d=%d..%d (first absent d=%s) ; drawn d=%d..%d (%d ticks) ; done flag first at d=%s' % (
        label, act[0], act[-1], firstabs[0] if firstabs else None, drawn[0], drawn[-1], len(drawn), done[0] if done else None)


if __name__ == '__main__':
    out = []
    for who in ('hero', 'bean'):
        for nib in (6, 10, 7, 9):
            for T in (5, 8, 13):
                rows, tracked, born, sparks = run(who, nib, T, 110)
                line = summarize(rows, T, '%s hits, nib=%d T=%d' % (who, nib, T))
                sp = {i: [r['d'] for r in rows if r['d'] >= 0 and r['sparks'][i][0] == 0][:1] for i in sparks}
                line += ' ; sparks first-absent d=%s' % sp
                print(line)
                out.append(line)
                if T == 8 and who == 'bean' and nib in (6, 7):
                    print('   born: mode=%d id=%d status=%d anchor_is_target=%s depthoff=%d seqcur=0x%02X cnt=%d done=%d' % (
                        born['mode'], born['id'], born['status'], born['anchor'] == (BEAN if who == 'hero' else HERO), born['depthoff'], born['seqcur'], born['cnt'], born['done']))
    # control: nibble 1 (no effect), immune response
    rows, tracked, born, sparks = run('bean', 1, 8, 20)
    print('control nib1: tracked=%s sparks=%s' % (tracked, sparks))
    rows, tracked, born, sparks = run('bean', 6, 8, 20, resp=0x80)
    print('control nib6 immune(0x80): tracked=%s sparks=%s hitc@T=%s' % (tracked, sparks, [r['hitc'] for r in rows if r['d'] == 0]))
    # counterfactual: slot pass before the world tick
    for nib in (6, 7):
        rows, tracked, born, sparks = run('bean', nib, 8, 110, slot_first=True)
        print(summarize(rows, 8, 'COUNTERFACTUAL slot pass first, nib=%d' % nib))
    json.dump(out, open(r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/o-f3-6/checker/chk_bin_summary.json', 'w'), indent=1)
