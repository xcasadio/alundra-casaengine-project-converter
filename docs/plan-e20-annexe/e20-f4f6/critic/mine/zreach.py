import sys, os
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20f4f6/critic/bin')
import run_arrows as RA
import fw, world as W, c1world as C
SQ = W.SQUARE
def one(item, tall, label):
    C.map14().record['Header']['Ubuff'][13] = tall
    spec = dict(item=item, n=75, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    hit = None; dead = None; S = None
    for r in res['rows']:
        a = r['slots'].get(2)
        if a is not None and S is None: S = r['tick']
        if a is not None and a['hitc'] and hit is None: hit = r['tick'] - S
        if S is not None and a is None and dead is None and r['tick'] > S: dead = r['tick'] - S
    print(label, 'targetSizeZ', tall, 'hit at S+', hit, 'arrow gone at S+', dead)
for t in (8, 15, 16, 17, 18):
    one(5, t, 'hunter(3)')
for t in (8, 15):
    one(6, t, 'william normal(4)')
