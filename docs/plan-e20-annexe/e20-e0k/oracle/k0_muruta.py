"""k0_muruta.py - the Muruta of the hall of map 115 (sprite 91, real record, records 24-27 Height 16 = 128 px, records 6-9 Height 20 = 160 px) created by the REAL map-record spawner on the REAL cells of map 115;
the physics pass runs; their programs do not (inert stubs: the C program of the hall waits for the ground with 0x25, it is not run here).  Answers the E14.a1c question "how long is the fall, where do they land".
Run: python -B -I k0_muruta.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import OUT

import json
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
d115 = json.load(open(D + '/map_115.json', encoding='utf-8-sig'))
ENTS = d115['SpriteInfo']['Entities']['Entities']
cellfn, (W_, H_, G_, V_) = K.cells_from_export(115)


def run_one(rec, ticker=None, n=45):
    e = ENTS[rec]
    m = K.make_world(cells=K.cells_fn(cellfn), hero_px=(30, 30))
    slots, info = K.spawn_record_entities(m, 'map_115', 91, [(e['XPos'], e['YPos'], e['Height'])])
    s = slots[0]
    init = K.snap(m, s)
    rows = K.run(m, n, [s], ticker=ticker)
    return m, s, init, rows


if __name__ == '__main__':
    lines = ['map 115: size %dx%d cells, Gravity %d, ZViscosity %d' % (W_, H_, G_, V_)]
    for rec in (24, 25, 26, 27, 6, 7, 8, 9, 12, 13, 14):
        a = run_one(rec)
        b = run_one(rec, ticker=interp2.tick2)
        dd = K.same_rows(a[3], b[3])
        m, s, init, rows = a
        t_land = next((r['tick'] for r in rows if r['slots'][s]['collz_140'] != 0), None)
        t_ground = next((r['tick'] for r in rows if r['slots'][s]['ground_144'] != 0), None)
        fin = rows[-1]['slots'][s]
        lines.append('record %2d (XPos %d YPos %d Height %d): spawn PosZ raw %d (%.4f px) terrain %.2f px ; first IsOnGround tick %s ; first CollidedWithEntityZ tick %s ; final PosZ %.4f px, X %.2f Y %.2f ; interp diff %s' % (
            rec, ENTS[rec]['XPos'], ENTS[rec]['YPos'], ENTS[rec]['Height'], init['pz'], init['pz'] / 65536, init['terr_138'] / 65536, t_ground, t_land, fin['pz'] / 65536, fin['px'] / 65536, fin['py'] / 65536, dd))
        if rec in (24, 6):
            open(os.path.join(OUT, 'muruta-record%d.txt' % rec), 'w').write(K.table(rows, s, ['status', 'fz_B8', 'fnz_EC', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'p180', 'p18C'], first=1, last=40, title='Muruta record %d (map 115) free fall' % rec) + '\n')
    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'muruta-summary.txt'), 'w').write(txt + '\n')
    print(txt)
