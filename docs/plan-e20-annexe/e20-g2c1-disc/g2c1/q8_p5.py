"""q8_p5.py - P5 (added by this read): the 8 trunks placed on deep water at load (maps 70 recs 9, 10, 11, 12, 21; map 73 recs 11, 12, 36): per tick animation, +0x18C, ground, status.
REAL code (C4 real, record spawner real, REAL cells of the map). Run: python -B -I -X utf8 q8_p5.py"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'work', 'orc'))
import gl
import k0lib as K
gl.CFIELDS.extend([('slope18C', 0x18C, 1)])
R = "D:/development/repo/alundra-casaengine-project-converter"
C4 = 0x80066984
for (n, rec) in ((70, 9), (70, 12), (73, 11), (73, 36), (70, 8)):
    d = json.load(open(R + '/data-extracted/data/map_%d.json' % n, encoding='utf-8-sig'))
    e = d['SpriteInfo']['Entities']['Entities'][rec]
    f_, meta = K.cells_from_export(n)
    m = gl.world(keep={C4}, cells=lambda col, row, f_=f_: f_(col, row), hero_px=(100, 100))
    key = ('map_%d' % n, 117)
    K._CACHE.pop(key, None)
    rec_, ents_, ts_ = K._record('map_%d' % n, 117)
    K._CACHE[key] = (rec_, [dict(x, XMin=0, YMin=0, XMax=255, YMax=255) for x in ents_[:1]] + ents_[1:], ts_)
    slots, info = K.spawn_record_entities(m, 'map_%d' % n, 117, [(e['XPos'], e['YPos'], e['Height'])], rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    s = slots[0]
    pads, nn = gl.pad_script([(6, 0)])
    rows = gl.run(m, nn, pads, crates=[s])
    print('== map %d rec %d sprite 117 at (%.1f,%.1f,z %.1f)' % (n, rec, rows[0]['c'][s]['px'] / 65536, rows[0]['c'][s]['py'] / 65536, rows[0]['c'][s]['pz'] / 65536))
    for r in rows:
        d_ = r['c'][s]
        print('   t%d anim_t=%d anim_c=%d slope18C=%d ground=%d status=%d z=%.1f journal=%s' % (r['tick'], d_['anim_t'], d_['anim_c'], d_['slope18C'], d_['ground'], d_['status'], d_['pz'] / 65536, [x for x in r['journal'] if not x.startswith('stub')]))
