"""flame_probe.py - E20.f3 T0 (recette of f3b, O-F3-5): does the "flame" of map 14 (entity record 17, global sprite 238, map cell (14, 28), Height 18) attack in its animation 0, with which
attack-attribute nibble, and can the hero be in reach?  Real code of ALUN_CD.EXE (France) on the f-projectiles machine (pworld.py): the flame is born by the REAL map-record spawner
0x8003A1B8 from the record of map_14.json; the sprite record is the global one (map_alundra.json, SpriteRecords[238]); the real hit pass runs; the flame's own programs are the inert journalling
stubs of the machine (program D34, its touch program, is NOT executed here - the animation set is forced where the question needs it, and said so in the output).
The ground is flat at height byte 9 (144 px), so that the hero stands at the flame's Z (the spawner puts the flame at Height 18 * 8 = 144 px; both rawZ 9437185).

usage (repository root):  python -B docs/plan-e20-annexe/e20-f3/oracle/flame_probe.py      writes out/flame_probe.json and out/flame_probe.txt
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import pworld as P
import world as W
import c1world as C
from machine import M32

OUT = os.path.join(HERE, 'out')
FLAME_TYPE = 238
RECORD = 17


def build(hero_px, anim_set, cells_height=9):
    m, meta = P.setup(item=None, hero_px=hero_px, direction=16, targets=(), cells=lambda c, r: (cells_height, 0, 0, 0), types=P.PROJECTILE_TYPES + (FLAME_TYPE,))
    d = json.load(open(C.DATA + '/map_14.json', encoding='utf-8-sig'))
    ent = d['SpriteInfo']['Entities']['Entities'][RECORD]
    assert ent['SpriteTableIndex'] == FLAME_TYPE
    m.poke_bytes(C.ENT_REC, C.entity_record_bytes(ent, ent['XPos'], ent['YPos'], ent['Height']))
    tsize = d['SpriteInfo']['Header']['SpriteTableSize'] // 4
    for i in range(tsize):
        m.poke(C.SPR_PTRS + 4 * i, 0)
    m.poke(C.MAPBLK + 8, C.ENT_REC)
    m.poke(C.MAPBLK + 0xC, C.SPR_PTRS)
    m.poke(C.MAPBLK + 0x10, tsize)
    m.poke(C.MAPBLK + 0x14, 1)
    p = m.call(0x8003A1B8, 0, 0, 0, limit=5_000_000) & M32
    assert p, 'the real spawner refused the flame'
    slot = (p - P.ENT0) // P.STRIDE
    m.poke(p + 0x10, 2)
    if anim_set:
        m.poke(p + 0x88, anim_set)           # forced animation set (stands for the effect of program D34, not executed here)
    return m, slot, ent


def run(hero_px, anim_set, n=130):
    m, slot, ent = build(hero_px, anim_set)
    pad = W.Pad()
    rows = []
    for t in range(1, n + 1):
        m.tick_no = t
        j0 = len(m.journal)
        W.tick(m, pad, 0)
        f = P.snap(m, slot) if m.peek(P.ENT0 + P.STRIDE * slot + 0x10) else None
        h = P.snap(m, 0)
        ev = [P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_')]
        rows.append(dict(tick=t, flame=None if f is None else dict(atk_attr=f['atk_attr'], atk_pow=f['atk_pow'], anim_t=f['anim_t'], hbx=f['hbx'] / 65536, hby=f['hby'] / 65536, hbz=f['hbz'] / 65536,
                                                                  bw=f['bw'], bd=f['bd'], bh=f['bh'], px=f['px'] / 65536, py=f['py'] / 65536, pz=f['pz']),
                         hero=dict(c1d0=h['c1d0'], hp=h['hp'], pz=h['pz'], px=h['px'] / 65536, py=h['py'] / 65536), fx=[e for e in ev if e.startswith('fx')], rng=m.peek(C.RNG) & M32))
    return rows, slot, ent


def first_hit(rows):
    for r in rows:
        if r['hero']['c1d0'] == 25:
            return r
    return None


def main():
    out = {}
    d = json.load(open(C.DATA + '/map_alundra.json', encoding='utf-8-sig'))
    hdr = d['SpriteInfo']['SpriteRecords'][FLAME_TYPE]['Header']
    bal = json.load(open(os.path.join(HERE, '..', '..', '..', '..', 'alundra-project', 'Data', 'balance.json'), encoding='utf-8-sig'))['EntriesBySpriteIndex'][FLAME_TYPE]
    out['sprite'] = dict(index=FLAME_TYPE, programs=dict(A=hdr['ProgramLoad'], C=hdr['ProgramTick'], D=hdr['ProgramTouch'], E=hdr['ProgramDeactivate'], F=hdr['ProgramInteract']),
                         more_flags=hdr['MoreFlags'], body=dict(offset=[hdr['OffsetX'], hdr['OffsetY'], hdr['OffsetZ']], size=[hdr['SizeX'], hdr['SizeY'], hdr['SizeZ']]), break_effect=hdr['BreakEffect'])
    out['balance'] = dict(max_hp=bal['MaxHp'], attacks=bal['Attacks'], damage_responses=bal['DamageResponses'])
    rows0, slot, ent = run((180, 232), 0)
    rows1, _, _ = run((180, 232), 1, n=60)
    out['entity_record'] = ent
    out['flame_slot'] = slot
    out['anim0_hero_on_flame'] = dict(first_hit=first_hit(rows0), hit_ticks=[r['tick'] for r in rows0 if r['hero']['c1d0'] == 25], rows=rows0[:3] + rows0[118:124])
    out['anim1_forced_hero_on_flame'] = dict(first_hit=first_hit(rows1), hit_ticks=[r['tick'] for r in rows1 if r['hero']['c1d0'] == 25], flame_gone_at=next((r['tick'] for r in rows1 if r['flame'] is None), None), rows=rows1[:3] + rows1[44:49])
    # reach: where can the hero stand and be hit at the first tick (same Z as the flame), animation 0 and the forced animation set 1
    reach = []
    for dy in (-40, -30, -20, -10, 0, 10, 20, 30, 40):
        line = []
        for dx in (-40, -30, -20, -10, 0, 10, 20, 30, 40):
            cell = []
            for a in (0, 1):
                rr, _, _ = run((180 + dx, 232 + dy), a, n=2)
                h = first_hit(rr)
                cell.append(dict(anim=a, hit=bool(h), attr=(rr[0]['flame'] or {}).get('atk_attr'), fx=h['fx'] if h else []))
            line.append(dict(dx=dx, dy=dy, cells=cell))
        reach.append(line)
    out['reach_first_tick'] = reach
    attr0 = sorted(set(r['flame']['atk_attr'] for r in rows0 if r['flame']))
    attr1 = sorted(set(r['flame']['atk_attr'] for r in rows1 if r['flame']))
    fx0 = [e for r in rows0 for e in r['fx'] if e.startswith('fxattach')]
    fx1 = [e for r in rows1 for e in r['fx'] if e.startswith('fxattach')]
    out['answer'] = dict(
        attacks_in_animation_0=bool(attr0 and attr0 != [None]), animation_0_attribute_values=attr0, animation_0_effect_attachments=fx0, animation_0_hero_hit_ticks=out['anim0_hero_on_flame']['hit_ticks'],
        animation_1_forced_attribute_values=attr1, animation_1_forced_effect_attachments=fx1[:3],
        hero_can_be_in_reach=any(c['hit'] for line in reach for c in [x for cc in line for x in cc['cells']] if c['anim'] == 0),
        verdict=('the flame attacks in its animation 0 with the nibble %s and hits the hero standing at its Z and in its 42x26 box (effect attached: %s); the nibble 6 (effect 4) belongs to the animation set 1 '
                 '(attack index 0 of the balance record); whether its touch program D34 (not run by this oracle, not decoded here) selects that set is NOT established: animation 0 gives no effect, so the recette of f3b '
                 'can only count on effect 4 on the hero if D34 selects the set 1, otherwise it moves to another of the 85 maps (a "no" blocks nothing)'
                 % (attr0, fx0 or 'none')))
    os.makedirs(OUT, exist_ok=True)
    json.dump(out, open(os.path.join(OUT, 'flame_probe.json'), 'w'), indent=1, sort_keys=True)
    lines = ['# flame probe (map 14, entity record 17, sprite 238, map cell (14,28), Height 18)',
             'programs %s ; body offset/size %s ; balance attacks %s' % (out['sprite']['programs'], (out['sprite']['body']['offset'], out['sprite']['body']['size']), out['balance']['attacks']),
             'animation 0: attack attribute values over %d ticks %s ; hero hit ticks %s ; effect attachments %s' % (len(rows0), attr0, out['anim0_hero_on_flame']['hit_ticks'], fx0),
             'animation set 1 (forced, D34 not run): attribute values %s ; hero hit ticks %s ; effect attachments %s ; flame gone at tick %s' % (attr1, out['anim1_forced_hero_on_flame']['hit_ticks'], fx1[:3], out['anim1_forced_hero_on_flame']['flame_gone_at']),
             'reach at the first tick (hero at flame + (dx,dy) px, same Z): ("a0"/"a1" = animation 0 / set 1; "." = no hit)']
    for line in reach:
        lines.append('  dy=%+4d  ' % line[0]['dy'] + ' '.join('%s%s' % ('a0' if c['cells'][0]['hit'] else '..', 'a1' if c['cells'][1]['hit'] else '..') for c in line))
    lines.append('  dx columns: ' + ' '.join('%+4d' % c['dx'] for c in reach[0]))
    lines.append('verdict: ' + out['answer']['verdict'])
    open(os.path.join(OUT, 'flame_probe.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
