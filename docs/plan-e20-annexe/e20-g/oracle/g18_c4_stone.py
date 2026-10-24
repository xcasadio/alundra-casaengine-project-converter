"""g18_c4_stone.py - S18: a C4 object (map 22 sprite 34: stones / iron balls, ProgramTick C4, ProgramDeactivate E2) survives the throw (C4 does not set the 0x30 flags nor clear 0x80) and lands as a solid, rideable, liftable object again.
Sequence: lift at t11, throw east at t61 (moving), the stone lands ~t83 at x~178; the hero walks east, jumps onto the stone, stands on it; then walks off and lifts it a second time.
C4 (0x80066984), E2 and C23 are REAL in the oracle for this script.
Run: python -B -I g18_c4_stone.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import k0lib as K

if __name__ == '__main__':
    out = []
    keep = {0x80066984}
    m = gl.world(keep=keep)
    c = gl.add_crate(m, 130, 100, mapname='map_22', sprite=34)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (30, 0),            # lift, throw (lands ~t83)
           (40, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (40, gl.RIGHT)]                                         # walk east to the stone, jump
    pads, n = gl.pad_script(seq)
    rows = gl.run(m, n, pads, crates=[c])
    land = ana.first_tick(rows, lambda r: r['c'][c]['ground'] == 1 and r['c'][c]['carrier28'] == 0, first=62)
    st = rows[100]['c'][c]
    out.append('stone after the throw: landed at t%s ; at t101: status %d anim %d flags %X pos (%.2f,%.2f,%.2f) ground %d' % (land, st['status'], st['anim_t'], st['flags6C'], st['px'] / 65536, st['py'] / 65536, st['pz'] / 65536, st['ground']))
    sup = ana.first_tick(rows, lambda r: r['hero']['support'] == K.ent(c), first=100)
    out.append('hero support == stone from tick %s ; hero Z on it %s ; hero anim changes %s' % (sup, sorted(set(round(r['hero']['pz'] / 65536, 1) for r in rows if sup and r['tick'] >= sup and r['hero']['support'] == K.ent(c))), ana.changes(rows, 'hero', ['anim_t'], first=100)[:8]))
    txt = gl.table(rows, [c], first=80, last=170, every=3, hcols=['anim_t', 'px', 'pz', 'support', 'xcoll', 'adj'], ccols=['px', 'pz', 'status', 'anim_t', 'flags6C', 'ground'], title='C4 stone thrown then used as a step')
    open(os.path.join(gl.OUT, 'g18-c4-stone.txt'), 'w').write(txt + '\n')
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g18-c4-summary.txt'), 'w').write('\n'.join(out) + '\n')

    # second scenario: the stone thrown against a wall (column 6, walkability 0x40): C4 bounces it (direction + 0x10, effect 9) while it is rising
    cells = lambda col, row: dict(wk=0x40) if col == 6 else None
    m = gl.world(keep=keep, cells=cells)
    c = gl.add_crate(m, 130, 100, mapname='map_22', sprite=34)
    pads, n = gl.pad_script([(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, gl.RIGHT | gl.SQUARE), (60, 0)])
    rows = gl.run(m, n, pads, crates=[c])
    j = [(r['tick'], [x for x in r['journal'] if x.startswith(('fx', 'snd'))]) for r in rows if r['tick'] >= 61 and any(x.startswith(('fx', 'snd')) for x in r['journal'])]
    path = [(r['tick'], round(r['c'][c]['px'] / 65536, 1), round(r['c'][c]['pz'] / 65536, 1), r['c'][c]['dir_t']) for r in rows if 61 <= r['tick'] <= 100 and r['tick'] % 3 == 0]
    s2 = 'stone vs wall: changes %s ; journal(fx/snd) %s ; path (t,x,z,dir) %s ; end status %d flags %X' % (ana.changes(rows, c, ['status', 'anim_t', 'dir_t', 'flags6C'], first=60)[:8], j[:6], path, rows[-1]['c'][c]['status'], rows[-1]['c'][c]['flags6C'])
    print(s2)
    open(os.path.join(gl.OUT, 'g18-c4-summary.txt'), 'a').write(s2 + '\n')
