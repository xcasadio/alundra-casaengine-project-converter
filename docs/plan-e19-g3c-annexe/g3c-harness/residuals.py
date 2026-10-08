import os
import sys

import numpy as np

ANNEX = r'D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-g3c-annexe'
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
sys.path.insert(0, ANNEX)
sys.path.insert(0, os.path.join(ANNEX, '..', 'plan-e19-g2b-annexe'))
import g3c_model as m  # noqa: E402
import g3c_compare as c  # noqa: E402

BG = np.array(m.BG)
expected = c.load_expected()
ns = m.load_walker()

for name in ('aura_t0', 'aura_t2', 'aura_t16'):
    w1, h1, k1, d1 = c.load_dump(os.path.join(OUT, 'dump_%s_k1.bin' % name))
    w3, h3, k3, d3 = c.load_dump(os.path.join(OUT, 'dump_%s_k3.bin' % name))
    sc = [s for s in m.build_scenes() if s['name'] == name][0]
    _, sheet, _ = m.load_map(sc['map_id'])
    quads = m.scene_quads(sc)
    wd, wc = m.walker_drawn(sc, quads, sheet, ns)
    img1 = d1[:, :, :3].astype(int)
    lit1 = (img1 != BG).any(axis=2)
    # B(1) engine capture against the hardware walker: drawn mask (covered pixel with alpha > 0)
    res1 = m.render_scene(c.scene_of(expected, name), 1)
    keep = ~res1['noise']
    only_walker = int((wd & ~lit1 & keep).sum())
    only_engine = int((lit1 & ~wd & keep).sum())
    # the pixels where the engine and the walker drawn masks disagree are those whose blended result equals the background by chance
    print('%s B(1) vs walker: walker drawn=%d engine lit(!=bg)=%d  walker-only=%d (outside noise) engine-only=%d (outside noise); noise px=%d'
          % (name, int(wd.sum()), int(lit1.sum()), only_walker, only_engine, int(res1['noise'].sum())))
    # D-E19-92: the k=3 capture against the k=1 capture enlarged 3x (nearest, the PS1 look)
    up = np.kron(img1, np.ones((3, 3, 1), dtype=int))
    img3 = d3[:, :, :3].astype(int)
    diff = np.abs(img3 - up).max(axis=2)
    lit3 = (img3 != BG).any(axis=2)
    litup = (up != BG).any(axis=2)
    print('%s D-E19-92 x3 vs x1 enlarged 3x: pixels differing=%d of %d (%.2f%%), max|diff|=%d, lit x3=%d lit x1up=%d, lit symmetric diff=%d'
          % (name, int((diff > 0).sum()), diff.size, 100.0 * (diff > 0).sum() / diff.size, int(diff.max()), int(lit3.sum()), int(litup.sum()),
             int((lit3 != litup).sum())))
