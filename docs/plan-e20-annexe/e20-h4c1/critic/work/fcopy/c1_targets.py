"""c1_targets.py (h4c1 discovery): the earth-1 cast against the REAL Beannoid record of map 14 (inert handlers, pworld machine), one run per target position, hit counts and ticks."""
import json
import sys
import c1_pworld as C1

def run(tpos_px, n=160, hero=(100, 100), direction=24):
    xpos = (tpos_px[0] - 12) // 12
    ypos = (tpos_px[1] - 8) // 8
    assert 12 * xpos + 12 == tpos_px[0] and 8 * ypos + 8 == tpos_px[1], tpos_px
    res = C1.cast(n=n, hero=hero, direction=direction, targets=[(xpos, ypos, 0)])
    rows = res['rows']
    tslot = res['meta']['target_slots'][0]
    hits = []
    for r in rows:
        d = r['slots'].get(tslot)
        if d is None:
            continue
        if d['touch_slot'] is not None:
            a = r['slots'].get(d['touch_slot'], {})
            hits.append(dict(tick=r['tick'], by_slot=d['touch_slot'], by_type=a.get('type68'), by_status=a.get('status'), by_anim=a.get('anim_t'), by_pos=(a.get('px', 0) >> 16, a.get('py', 0) >> 16, a.get('pz')),
                             draws=r['draws'], nfx=len(r['effects'])))
    return dict(target=tpos_px, slot=tslot, hits=hits)

if __name__ == '__main__':
    out = []
    for p in [(132, 104), (108, 136), (180, 104), (108, 56), (60, 104), (132, 136), (204, 104), (84, 104)]:
        r = run(p)
        out.append(r)
        print(p, 'hits', len(r['hits']), [(h['tick'], h['by_slot'], h['by_anim'], h['draws']) for h in r['hits']])
    json.dump(out, open('../c1_targets_pworld.json', 'w'), default=str)
