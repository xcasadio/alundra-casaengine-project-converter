"""c64_lift.py - T0 (g) of E20.g2c1: the barrel of map 113 rec 11 (type 290, header C4) whose A program does 0x41 [C,64]: what does the original do when the hero lifts it and throws it?
REAL code of ALUN_CD.EXE (France) in the g1a/g2c oracle machine: REAL MovePlayer (TryLift / PlayerTryAction / throw), REAL record spawner, REAL physics, REAL C64 (0x8007ADDC) as the C program of the barrel
(harness: the native C code +0x78 poked to 64 after the placement = the 0x41 [C,64] of the A program), inert stubs elsewhere.  Run: python -B -X utf8 c64_lift.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c4_scen as S
import gl
import k0lib as K
C64 = 0x8007ADDC
C4 = S.C4
for label, keep, code, srch in (('C4 (rec 10 of the map 113 and every other barrel)', {C4}, 4, None), ('C64, search 0x8003C954 (group 10 of the entities) finds nothing', {C64}, 64, 0), ('C64, search finds the barrel itself (the hero carries it)', {C64}, 64, 1)):
    mapname, sprite, extra = S.OBJ['tonneau34']
    m = gl.world(keep=keep, cells=None, hero_px=(100, 100))
    S.widen(mapname, sprite)
    c = gl.add_crate(m, 130, 100, mapname=mapname, sprite=sprite)
    e = K.ent(c)
    m.poke(e + 0x78, code)
    if srch is not None:
        # harness: the search 0x8003C954 needs the table of the search groups (0x80126E70), which the oracle machine does not model: replaced by a hook that answers `srch` results, the first one being the barrel
        def search(m2, srch=srch, e=e):
            m2.poke(0x8013D8D8, e)
            m2.r[2] = srch
        m.hooks[0x8003C954] = search
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (88, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    print('== barrel with the native C code %d : %s ; header flags +0x6C=%X' % (code, label, rows[0]['c'][c]['flags6C']))
    prev = None
    for r in rows:
        d = r['c'][c]
        key = (d['status'], d['t24'], d['carrier28'], d['anim_t'], d['flags6C'], d['ground'], d['hitc'])
        if key != prev:
            print('   t%03d status=%d t24=%d carrier28=%X anim_t=%d flags=%X ground=%d hit=%d pos=(%.1f,%.1f,%.1f) hero: anim_t=%d +0x2C=%X' % (
                r['tick'], d['status'], d['t24'], d['carrier28'], d['anim_t'], d['flags6C'], d['ground'], d['hitc'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, r['hero']['anim_t'], r['hero']['carried2C']))
            prev = key
    last = rows[-1]
    d = last['c'][c]
    print('   final t%d: status %d carrier28 %X anim_t %d pos (%.2f,%.2f,%.2f) ground %d ; hero anim_t %d +0x2C %X' % (last['tick'], d['status'], d['carrier28'], d['anim_t'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['ground'], last['hero']['anim_t'], last['hero']['carried2C']))
    for t in (11, 30, 60, 61, 62, 70, 90, 120, 150):
        if t <= len(rows):
            d = rows[t - 1]['c'][c]
            print('     t%03d pos=(%.1f,%.1f,%.1f) carrier28=%X t24=%d anim_t=%d flags=%X adj=%d ground=%d' % (t, d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['carrier28'], d['t24'], d['anim_t'], d['flags6C'], d['adj'], d['ground']))
    jr = [(r['tick'], [x for x in r['journal'] if not x.startswith('stub')]) for r in rows if [x for x in r['journal'] if not x.startswith('stub')]]
    print('   journal (non-stub):', jr[:12])
