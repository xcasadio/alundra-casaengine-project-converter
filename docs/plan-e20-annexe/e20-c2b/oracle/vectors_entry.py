"""vectors_entry.py - E20.c2b T0 (c): the vectors of the pure function of the damage entry 0x80031A68, drawn by calling the REAL code of ALUN_CD.EXE (France)
in the MIPS emulator of E20.c2 (hworld.setup_h = the c2 world: hero life from the stats, warm hero sheet and boots, cells).  NEW FILE: nothing of e20-c2/oracle is edited.

One vector = a fresh machine (hworld.setup_h), the inputs poked as DATA (hero state, toucher, cell bits, boots by inventory, sheet byte), ONE call of 0x80031A68 with no tick,
then the return value (v0), the hero life (+0x14) and every byte of the hero entity, of the toucher entity and of the stats block that changed.  Nothing here knows an expected
value: the specification of (e1) to (e4) is read from this output.

Run (Python 3 with capstone, from a COPY of the annex with docs/ laid out as in the repository, always python -B):
    python -B docs/plan-e20-annexe/e20-c2b/oracle/vectors_entry.py [outdir]        # default outdir: <this folder>/out
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
C2 = os.path.normpath(os.path.join(HERE, '..', '..', 'e20-c2', 'oracle'))
sys.path.insert(0, C2)
sys.path.insert(0, os.path.join(C2, 'tools'))
import c1world as C
import hworld as H
import hscen as HS
import scen as S
import world as W
from machine import M32

ENTRY = 0x80031A68

# two inventories added to the dictionary of hworld at import (a copy of the module's data, nothing edited): boots of level 2 / 3 with the silver armour
H.INV['b1b-silver'] = H._inv('b1b-silver', (1, 17, 25, 0x14, 0x1B), None, 'kit + silver armour 0x14 + boots item 0x1B (level 2)')
H.INV['b1c-silver'] = H._inv('b1c-silver', (1, 17, 25, 0x14, 0x1C), None, 'kit + silver armour 0x14 + boots item 0x1C (level 3)')
HERO = H.HERO
TOUCHING = 0x224
SHEET = H.SHEET
BOOTS = H.BOOTS
SB = 255                       # the hero sheet is read as 255 bytes


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def px16(px):
    return int(round(px * 65536)) & M32


def entity_bytes(m, addr):
    base = addr & 0x1FFFFFFF
    return bytes(m.ram[base: base + W.STRIDE])


def diff_bytes(before, after, base_off=0):
    """list of (offset, before byte, after byte) for the bytes that changed"""
    return [(base_off + i, b, a) for i, (b, a) in enumerate(zip(before, after)) if b != a]


def words(diffs):
    """merge changed bytes into u32 words aligned to 4: (offset, before word, after word)"""
    seen = {}
    for off, _b, _a in diffs:
        seen[off & ~3] = 1
    return sorted(seen)


def vector(name, desc, *, inv='kit', hp=10, hpmax=10, toucher=None, attack=None, kind=None, bearing=None, hero_state=None, tou_state=None, bits_180=0,
           dir8c=24, sheet_poke=None, boots_poke=None, hero_px=(200, 100)):
    """toucher: None (no toucher: the cell branch) or 'beannoid' / 'orc' (the real sprite kinds of the oracle; the toucher is the first target);
       attack: (attribute, power) synthetic CurrentAttack of the toucher (hworld.set_attack); bearing: (vx, vy) px = hero - toucher;
       hero_state / tou_state: {offset: value} pokes on the hero / toucher entity (data); bits_180: the value poked at the hero's +0x180;
       dir8c: the hero's +0x8C (the cell branch returns (+0x8C + 0x10) & 0x1F); sheet_poke: {nibble: byte} (sheet byte [nibble + 2]); boots_poke: [0x80127000]."""
    has_t = toucher is not None
    kw = {}
    if toucher == 'orc':
        kw['kind'] = 'map15-orc'
    sp = HS.mk(name, desc, 20, n=0, targets=None if has_t else [], inv=inv, hp=hp, hpmax=hpmax, **kw)
    m, meta = H.setup_h(sp)
    slots = meta['target_slots']
    if has_t:
        t = C.ent(slots[0])
        if attack is not None:
            H.set_attack(attack[0], attack[1])(m, slots[0])
        vx, vy = bearing if bearing is not None else (20, 0)         # default: toucher 20 px east of the hero => hero - toucher = (-20, 0); bearing is hero - toucher
        if bearing is None:
            vx, vy = -20, 0
        hx, hy = hero_px
        m.poke(HERO + 0x114, px16(hx)); m.poke(HERO + 0x118, px16(hy))
        m.poke(t + 0x114, px16(hx - vx)); m.poke(t + 0x118, px16(hy - vy))
        m.poke(HERO + TOUCHING, t)
        for off, v in (tou_state or {}).items():
            m.poke(t + off, v)
    else:
        t = None
        m.poke(HERO + TOUCHING, 0)
    m.poke(HERO + 0x8C, dir8c)
    m.poke(HERO + 0x180, bits_180)
    for off, v in (hero_state or {}).items():
        m.poke(HERO + off, v)
    for nib, b in (sheet_poke or {}).items():
        m.poke_bytes(SHEET + nib + 2, bytes((b,)))
    if boots_poke is not None:
        m.poke(BOOTS, boots_poke)
    pre = dict(hero=entity_bytes(m, HERO), tou=entity_bytes(m, t) if t else b'', sheet=bytes(m.peek(SHEET + i) & 0xFF for i in range(SB)),
               boots=m.peeks(BOOTS))
    sp_stats = m.peek(H.STATS_PTR)
    stats_pre = bytes(m.ram[(sp_stats & 0x1FFFFFFF): (sp_stats & 0x1FFFFFFF) + 64])
    j0 = len(m.journal)
    ret = m.call(ENTRY)
    post_hero = entity_bytes(m, HERO)
    post_tou = entity_bytes(m, t) if t else b''
    stats_post = bytes(m.ram[(sp_stats & 0x1FFFFFFF): (sp_stats & 0x1FFFFFFF) + 64])
    hd = diff_bytes(pre['hero'], post_hero)
    td = diff_bytes(pre['tou'], post_tou)
    sd = diff_bytes(stats_pre, stats_post)
    sheet_post = bytes(m.peek(SHEET + i) & 0xFF for i in range(SB))
    inp = dict(inv=inv, stats_hp=hp, stats_hpmax=hpmax, hero_hp_before=s32(int.from_bytes(pre['hero'][0x14:0x18], 'little')), hero_cc1CC=int.from_bytes(pre['hero'][0x1CC:0x1D0], 'little'),
               hero_1D0=int.from_bytes(pre['hero'][0x1D0:0x1D4], 'little'), hero_180=int.from_bytes(pre['hero'][0x180:0x184], 'little'), hero_8C=dir8c, boots=pre['boots'],
               toucher=toucher, attack=attack, bearing_hero_minus_toucher=bearing if has_t else None, toucher_1CC=(int.from_bytes(pre['tou'][0x1CC:0x1D0], 'little') if has_t else None),
               sheet_poke=sheet_poke, hero_pokes=hero_state, toucher_pokes=tou_state,
               hero_sheet_r=[pre['sheet'][2 + i] for i in range(1, 12)])
    out = dict(ret=s32(ret), hero_hp_after=s32(int.from_bytes(post_hero[0x14:0x18], 'little')), hero_changed_words=[
        (hex(o), '0x%08X' % int.from_bytes(pre['hero'][o:o + 4], 'little'), '0x%08X' % int.from_bytes(post_hero[o:o + 4], 'little')) for o in words(hd)],
        toucher_changed_words=[(hex(o), '0x%08X' % int.from_bytes(pre['tou'][o:o + 4], 'little'), '0x%08X' % int.from_bytes(post_tou[o:o + 4], 'little')) for o in words(td)],
        stats_changed_bytes=[(hex(o), b, a) for o, b, a in sd], sheet_changed=sheet_post != pre['sheet'],
        journal=[C.fmt_effect(j) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] != 'handler'],
        uninit_reads=sorted((hex(pc), hex(a), n, c) for (pc, a, n), c in m.uninit_reads.items()))
    return dict(name=name, desc=desc, inputs=inp, outputs=out)


def build():
    V = []
    # ---- toucher branch: the damage per armour x stats against the Orc (attribute 0x02, power 4) -------------------------------------------------------------------
    for inv in ('kit', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet'):
        for hp, hpmax in ((12, 12), (5, 12), (3, 3), (1, 12)):
            V.append(vector('T-orc-%s-%d-%d' % (inv, hp, hpmax), 'toucher = Orc of map 15 (attribute 0x02, power 4), inventory %s, stats %d/%d' % (inv, hp, hpmax),
                            inv=inv, hp=hp, hpmax=hpmax, toucher='orc'))
    for inv in ('kit', 'leather'):
        V.append(vector('T-syn-%s' % inv, 'toucher = Beannoid whose CurrentAttack is the synthetic pair (0x02, 4), inventory %s, stats 12/12' % inv, inv=inv, hp=12, hpmax=12,
                        toucher='beannoid', attack=(0x02, 4)))
    V.append(vector('T-beannoid-real', 'toucher = the real Beannoid of map 14 (its CurrentAttack as left by the spawner), kit, 10/10', toucher='beannoid'))
    # ---- the toucher with a non-zero +0x1CC (e1): the branch tests neither the toucher's nor the hero's +0x1CC / +0x1D0 / the boots / the state -----------------------------
    for tv in (0x1, 0x40, 0x77, 0x78):
        V.append(vector('T-toucher1CC-%02X' % tv, 'toucher with +0x1CC = 0x%X (its own invulnerability counter), hero normal, Orc attack, kit 12/12' % tv, toucher='orc', hp=12, hpmax=12, tou_state={0x1CC: tv}))
    for hv in (0x1, 0x77, 0x78):
        V.append(vector('T-hero1CC-%02X' % hv, 'HERO +0x1CC = 0x%X with a toucher set: the branch does not test it, Orc attack, kit 12/12' % hv, toucher='orc', hp=12, hpmax=12, hero_state={0x1CC: hv}))
    V.append(vector('T-hero1D0', 'HERO +0x1D0 = 25 with a toucher set (not tested), Orc, kit 12/12', toucher='orc', hp=12, hpmax=12, hero_state={0x1D0: 25}))
    V.append(vector('T-both', 'toucher +0x1CC = 0x78 AND hero +0x1CC = 0x78 AND hero +0x1D0 = 25, Orc, kit 12/12', toucher='orc', hp=12, hpmax=12, tou_state={0x1CC: 0x78}, hero_state={0x1CC: 0x78, 0x1D0: 25}))
    V.append(vector('T-boots3', 'boots level 3 with a toucher (boots are not tested in this branch), Orc, kit', inv='b1c', toucher='orc', hp=12, hpmax=12))
    V.append(vector('T-bits180', 'hero +0x180 = 0x180 (cell bits) with a toucher set: the toucher branch wins (no cell cost), Orc, kit 12/12', toucher='orc', hp=12, hpmax=12, bits_180=0x180))
    # ---- responses 0x80 / 0xC0 / 0x40 and attribute 0 (e2) --------------------------------------------------------------------------------------------------------------
    V.append(vector('T-charm-resp80', 'Charm of Nava worn (response index 6 = 0x80), toucher attack attribute 0x06 power 4, stats 10/10: the direction is returned, the life is unchanged', inv='charm',
                    toucher='beannoid', attack=(0x06, 4)))
    V.append(vector('T-nocharm-attr6', 'same attack (0x06, 4), kit (no charm): damage as an ordinary attribute', toucher='beannoid', attack=(0x06, 4)))
    V.append(vector('T-resp-C0', 'sheet byte of nibble 2 poked to 0xC0 (harness write of data), Orc attack (0x02, 4): unchanged life, direction returned', toucher='orc', hp=12, hpmax=12, sheet_poke={2: 0xC0}))
    V.append(vector('T-resp-40', 'sheet byte of nibble 2 poked to 0x40 (harness write of data), Orc attack (0x02, 4): life 0, direction returned', toucher='orc', hp=12, hpmax=12, sheet_poke={2: 0x40}))
    V.append(vector('T-resp-80', 'sheet byte of nibble 2 poked to 0x80 (harness write of data), Orc attack (0x02, 4): unchanged life, direction returned', toucher='orc', hp=12, hpmax=12, sheet_poke={2: 0x80}))
    V.append(vector('T-attr0', 'toucher attack attribute 0 (power 4): the life is unchanged, direction returned', toucher='beannoid', attack=(0x00, 4)))
    V.append(vector('T-lethal-1hp', 'stats 1/10 hit by the Orc: life 0, the return is the direction (not -1)', toucher='orc', hp=1, hpmax=10))
    # ---- the bearing (e3) ------------------------------------------------------------------------------------------------------------------------------------------
    for lab, vx, vy in (('E', 14, 0), ('W', -14, 0), ('S', 0, 10), ('N', 0, -10), ('SE', 10, 8), ('SW', -10, 8), ('NE', 10, -8), ('NW', -10, -8), ('ENE', 16, -3), ('WSW', -16, 3),
                        ('SSE', 3, 12), ('NNW', -3, -12), ('E1', 1, 0), ('W1', -1, 0), ('S1', 0, 1), ('N1', 0, -1), ('SE1', 1, 1), ('NW1', -1, -1), ('same-0-0', 0, 0)):
        V.append(vector('B-%s' % lab, 'bearing hero - toucher = (%d, %d) px, Orc, kit 12/12' % (vx, vy), toucher='orc', hp=12, hpmax=12, bearing=(vx, vy)))
    # (0, 0) at sub-pixel offsets
    # ---- the cell branch: cost x boots (e1), the armour ignored, the floor ---------------------------------------------------------------------------------------------
    for word in (0x80, 0x100, 0x180):
        for inv in ('kit', 'b1a', 'b1b', 'b1c'):
            V.append(vector('H-%03X-%s' % (word, inv), 'no toucher, hero +0x180 = 0x%X, inventory %s (boots level %d), stats 10/10, +0x8C = 24' % (word, inv, {'kit': 0, 'b1a': 1, 'b1b': 2, 'b1c': 3}[inv]), inv=inv, bits_180=word))
    for inv in ('kit', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet', 'charm'):
        V.append(vector('H-armour-%s' % inv, 'no toucher, cell cost 3 (0x180), inventory %s: the armour is ignored, stats 10/10' % inv, inv=inv, bits_180=0x180))
    for word in (0x80, 0x180):
        V.append(vector('H-boots2-silver-%03X' % word, 'no toucher, cell 0x%X, boots level 2 AND silver armour: blessed (boots < 3), the armour is ignored' % word, inv='b1b-silver', bits_180=word))
        V.append(vector('H-boots3-silver-%03X' % word, 'no toucher, cell 0x%X, boots level 3 AND silver armour: spared' % word, inv='b1c-silver', bits_180=word))
    for hp, word in ((2, 0x180), (1, 0x80), (3, 0x180), (0, 0x180), (1, 0x180)):
        V.append(vector('H-floor-%d-%03X' % (hp, word), 'no toucher, cell 0x%X, stats %d/10: the floor is 0 (signed)' % (word, hp), hp=hp, bits_180=word))
    for d in (0, 5, 8, 16, 24, 31):
        V.append(vector('H-dir-%d' % d, 'no toucher, cell 0x80, +0x8C = %d: the return is (+0x8C + 0x10) & 0x1F' % d, bits_180=0x80, dir8c=d))
    for hv in (0x1, 0x77, 0x78):
        V.append(vector('H-hero1CC-%02X' % hv, 'no toucher, cell 0x180, hero +0x1CC = 0x%X: -1, nothing written' % hv, bits_180=0x180, hero_state={0x1CC: hv}))
    # ---- nothing (e4) -----------------------------------------------------------------------------------------------------------------------------------------------
    V.append(vector('N-nothing', 'no toucher, hero +0x180 = 0: -1, nothing written', bits_180=0))
    V.append(vector('N-other-bits', 'no toucher, hero +0x180 = 0x7F | 0x800 (no 0x180 bit): -1, nothing written', bits_180=0x87F))
    V.append(vector('N-bit80-only-water', 'no toucher, hero +0x180 = 0x800 (deep water, no hazard bits): -1', bits_180=0x800))
    V.append(vector('N-bit100-set', 'no toucher, hero +0x180 = 0x100 (n = 2): cost 2', bits_180=0x100))
    return V


def render(V):
    L = ['# vectors of the damage entry 0x80031A68 (E20.c2b T0 (c)), %d vectors, all computed by calling the real code of ALUN_CD.EXE (France) in the MIPS emulator; one call, no tick' % len(V), '']
    for v in V:
        i, o = v['inputs'], v['outputs']
        L.append('## %s: %s' % (v['name'], v['desc']))
        L.append('inputs : inventory %s, stats %s/%s, hero HP %s, hero +0x1CC %d, +0x1D0 %d, +0x180 0x%X, +0x8C %d, boots %d, toucher %s attack %s bearing %s toucher+0x1CC %s, sheet r1..r11 %s' % (
            i['inv'], i['stats_hp'], i['stats_hpmax'], i['hero_hp_before'], i['hero_cc1CC'], i['hero_1D0'], i['hero_180'], i['hero_8C'], i['boots'], i['toucher'], i['attack'],
            i['bearing_hero_minus_toucher'], i['toucher_1CC'], ' '.join('%02X' % b for b in i['hero_sheet_r'])))
        L.append('outputs: return %d, hero HP %d -> %d, hero words changed %s, toucher words changed %s, stats bytes changed %s, sheet changed %s, journal %s, unwritten reads %s' % (
            o['ret'], i['hero_hp_before'], o['hero_hp_after'], o['hero_changed_words'] or 'none', o['toucher_changed_words'] or 'none', o['stats_changed_bytes'] or 'none', o['sheet_changed'],
            o['journal'] or 'none', o['uninit_reads'] or 'none'))
        L.append('')
    return '\n'.join(L)


def crosscheck(V):
    """the direct vectors against the stored tick-level oracle of E20.c2 (out/A2, A4, B1, B2 json, read only): the same inputs must give the same life and direction"""
    by = {v['name']: v for v in V}
    out = []
    ok_all = True

    def runs(name):
        return {r['spec']['name']: r for r in json.load(open(os.path.join(C2, 'out', name + '.json'), encoding='utf-8'))['runs']}
    a2 = runs('A2')
    for inv in ('kit', 'leather', 'ancient', 'silver', 'noarmour', 'bracelet'):
        for hp, mx in ((12, 12), (5, 12), (3, 3)):
            r = a2['A2-%s-%d-%d' % (inv, hp, mx)]
            want = r['hero']['hp'][2]
            got = by['T-orc-%s-%d-%d' % (inv, hp, mx)]['outputs']['hero_hp_after']
            ok = want == got
            ok_all &= ok
            out.append('A2 %-9s %2d/%2d: tick-level oracle HP after the hurt %d, direct call %d  %s' % (inv, hp, mx, want, got, 'equal' if ok else 'DIFFERENT'))
    a4 = runs('A4')
    for lab in ('E', 'W', 'S', 'N', 'SE', 'SW', 'NE', 'NW', 'ENE', 'WSW', 'SSE', 'NNW'):
        want = a4['A4-' + lab]['hero']['dir_target'][2]
        got = by['B-' + lab]['outputs']['ret']
        ok = want == got
        ok_all &= ok
        out.append('A4 %-3s: tick-level oracle +0x8C after the hurt %d, direct call return %d  %s' % (lab, want, got, 'equal' if ok else 'DIFFERENT'))
    b1 = runs('B1')
    for word in (0x80, 0x100, 0x180):
        for inv in ('kit', 'b1a', 'b1b', 'b1c'):
            hp = b1['B1-%03X-%s' % (word, inv)]['hero']['hp']
            drops = [hp[i] - hp[i - 1] for i in range(1, len(hp)) if hp[i] != hp[i - 1]]
            want = -drops[0] if drops else 0
            v = by['H-%03X-%s' % (word, inv)]['outputs']
            got = by['H-%03X-%s' % (word, inv)]['inputs']['hero_hp_before'] - v['hero_hp_after']
            ok = want == got
            ok_all &= ok
            out.append('B1 word 0x%03X %-3s: tick-level oracle first loss %d, direct call %d (return %d)  %s' % (word, inv, want, got, v['ret'], 'equal' if ok else 'DIFFERENT'))
    b2 = runs('B2')
    for nm, key in (('B2-hp2-cost3', 'H-floor-2-180'), ('B2-hp1-cost1', 'H-floor-1-080'), ('B2-hp3-cost3', 'H-floor-3-180')):
        want = [x for x in b2[nm]['hero']['hp'] if x != b2[nm]['hero']['hp'][0]][:1]
        got = by[key]['outputs']['hero_hp_after']
        ok = (want == [got])
        ok_all &= ok
        out.append('B2 %s: tick-level oracle first life after a hurt %s, direct call %d  %s' % (nm, want, got, 'equal' if ok else 'DIFFERENT'))
    out.append('VERDICT: %s' % ('every direct vector equals the tick-level oracle of E20.c2' if ok_all else 'DIFFERENCES'))
    return out, ok_all


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out')
    os.makedirs(outdir, exist_ok=True)
    V = build()
    with open(os.path.join(outdir, 'vectors_entry.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(render(V) + '\n')
    with open(os.path.join(outdir, 'vectors_entry.json'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(dict(entry='0x%08X' % ENTRY, vectors=V), sort_keys=True, separators=(',', ':'), default=str) + '\n')
    cc, ok = crosscheck(V)
    with open(os.path.join(outdir, 'vectors_entry-crosscheck.txt'), 'w', encoding='utf-8', newline=chr(10)) as f:
        f.write(chr(10).join(cc) + chr(10))
    print('%d vectors written to %s; cross-check with the tick-level oracle: %s' % (len(V), outdir, 'equal' if ok else 'DIFFERENT'))


if __name__ == '__main__':
    main()
