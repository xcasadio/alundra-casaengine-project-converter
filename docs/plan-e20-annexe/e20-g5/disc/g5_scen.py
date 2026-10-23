"""g5_scen.py - whole-pipeline replays (REAL MovePlayer, REAL map-record spawner, REAL C76, REAL physics passes, inert stubs elsewhere) of the pushable pillar (map 26 sprite 174).
Harness writes: the pad words, the cell field, the placement of the pillar (as gl.add_crate), a journalling hook for StopSoundEffect 0x80049634, nothing else.
Run: python -B -X utf8 g5_scen.py [scenario ...]   (no -I: capstone lives in the user site-packages)"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
import gl
import ana
import k0lib as K
from machine import M32

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
gl.CFIELDS.extend([('cnt274', 0x274, 1), ('t68', 0x68, 1), ('speedF4', 0xF4, 1), ('curdir94', 0x94, 1), ('tfx', 0xBC, 1), ('tfy', 0xC0, 1), ('fx2', 0xC4, 1), ('fy2', 0xC8, 1),
                   ('w1F0', 0x1F0, 1), ('h1F4', 0x1F4, 1), ('d1F8', 0x1F8, 1), ('modx', 0x1D8, 1), ('mody', 0x1DC, 1)])
gl.HFIELDS.extend([('w1F0', 0x1F0, 1), ('h1F4', 0x1F4, 1), ('d1F8', 0x1F8, 1), ('modx', 0x1D8, 1), ('mody', 0x1DC, 1), ('adirB', 0x98, 1)])
C76 = 0x8007C024
NL = chr(10)


def install_stop_hook(m):
    def stop(m2):
        m2.journal.append((m2.tick_no, 'stopsnd%d' % (m2.r[4] & 0xFFFF), [m2.r[4]]))
    m.hooks[0x80049634] = stop


def widen(mapname, sprite, which=0):
    key = (mapname, sprite)
    if key not in K._CACHE:
        K._CACHE[key] = K._record(mapname, sprite)
    rec_, ents_, ts_ = K._CACHE[key]
    tmpl = ents_[which]
    K._CACHE[key] = (rec_, [dict(tmpl, XMin=0, YMin=0, XMax=255, YMax=255)] + ents_[1:], ts_)


def pillar_world(cells=None, hero_px=(100, 100), direction=24, pil_px=(130, 100), height=0, mapname='map_26', sprite=174):
    m = gl.world(keep={C76}, cells=cells, hero_px=hero_px, direction=direction)
    install_stop_hook(m)
    widen(mapname, sprite)
    c = gl.add_crate(m, pil_px[0], pil_px[1], mapname=mapname, sprite=sprite, height=height)
    return m, c


def real_cells(mapno):
    """the real MapTiles of data-extracted/data/map_<n>.json as a gl cells callable (col,row)->dict(h,slope,wk,gp)"""
    d = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_%d.json' % mapno, encoding='utf-8-sig'))
    W_, H_ = d['Map']['Width'], d['Map']['Height']
    grid = {}
    for t in d['Map']['MapTiles']:
        grid[(t['TileX'], t['TileY'])] = (t['Height'], t['Slope'], t['Walkability'], t['GroundProperty'])

    def f(col, row):
        v = grid.get((col, row))
        if v is None:
            return dict(wk=0x40)
        return dict(h=v[0], slope=v[1], wk=v[2], gp=v[3])
    return f


SCEN = {}


def scen(f):
    SCEN[f.__name__] = f
    return f


COLS = ('status', 'anim_t', 'dir_t', 'cnt274', 'px', 'py', 'pz', 'fx', 'fy', 'ground', 'adj', 'xcoll', 'support')


def row_line(r, c, extra=''):
    d = r['c'][c]
    h = r['hero']
    j = [x for x in r['journal'] if not x.startswith('stub')]
    return 't%03d pad=%04X | H anim=%d px=%.3f py=%.3f pz=%.3f fx=%d adj=%d xcoll=%s | P anim=%d dir=%d cnt=%d px=%.3f py=%.3f pz=%.3f fx=%d fy=%d adj=%d gnd=%d%s %s' % (
        r['tick'], r['hold'], h['anim_t'], h['px'] / 65536, h['py'] / 65536, h['pz'] / 65536, h['fx'], h['adj'], 'PIL' if h['xcoll'] == K.ent(c) else ('%X' % h['xcoll'] if h['xcoll'] else '0'),
        d['anim_t'], d['dir_t'], d['cnt274'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['fx'], d['fy'], d['adj'], d['ground'], extra, ('| J ' + ' '.join(j)) if j else '')


def run_push(hold, hero_px, pil_px, direction, n, release_at=None, cells=None, height=0, mapname='map_26', sprite=174, press_from=1):
    m, c = pillar_world(cells=cells, hero_px=hero_px, direction=direction, pil_px=pil_px, height=height, mapname=mapname, sprite=sprite)
    seq = [(press_from - 1, 0)] if press_from > 1 else []
    if release_at:
        seq += [(release_at - press_from + 1, hold), (n - release_at, 0)]
    else:
        seq += [(n - press_from + 1, hold)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    return m, c, rows


def summarize(label, rows, c, hero_start_dir=None):
    contact = ana.first_tick(rows, lambda r: r['hero']['xcoll'] == K.ent(c))
    anim1 = ana.first_tick(rows, lambda r: r['c'][c]['anim_t'] == 1)
    sound = ana.first_tick(rows, lambda r: any(x == 'snd(25)' for x in r['journal']))
    mv = ana.first_tick(rows, lambda r: abs(r['c'][c]['px'] - rows[0]['c'][c]['px']) > 0 or abs(r['c'][c]['py'] - rows[0]['c'][c]['py']) > 0)
    last = rows[-1]
    p0 = rows[0]['c'][c]
    return '%s: first hero.xcoll==pillar t%s ; counter reaches 31 / first anim1 t%s ; first snd(25) t%s ; first pillar displacement t%s ; end: pillar (%.3f,%.3f) moved (%.3f,%.3f) anim %d dir %d cnt %d ; hero (%.3f,%.3f)' % (
        label, contact, anim1, sound, mv, last['c'][c]['px'] / 65536, last['c'][c]['py'] / 65536, (last['c'][c]['px'] - p0['px']) / 65536, (last['c'][c]['py'] - p0['py']) / 65536,
        last['c'][c]['anim_t'], last['c'][c]['dir_t'], last['c'][c]['cnt274'], last['hero']['px'] / 65536, last['hero']['py'] / 65536)


@scen
def p1_flat_east():
    """P1: hero (100,100) walks RIGHT into the pillar at (130,100) on flat ground, 200 ticks"""
    out = []
    m, c, rows = run_push(gl.RIGHT, (100, 100), (130, 100), 24, 200)
    out.append(summarize('P1 flat east', rows, c))
    open(os.path.join(OUT, 'p1_flat_east.txt'), 'w').write('\n'.join(row_line(r, c) for r in rows if r['tick'] <= 200) + '\n')
    return out



def brief(rows, c, ticks):
    out = []
    for r in rows:
        if r['tick'] in ticks:
            out.append(row_line(r, c))
    return out


@scen
def p2_directions():
    """P2: the four sides. hero walks into the pillar (130,100) from the west / east / north / south (hero direction set to the walk), 120 ticks"""
    out = []
    cases = [('west->east (RIGHT, dir 24)', gl.RIGHT, (100, 100), 24), ('east->west (LEFT, dir 8)', gl.LEFT, (160, 100), 8),
             ('north->south (DOWN, dir 0)', gl.DOWN, (130, 70), 0), ('south->north (UP, dir 16)', gl.UP, (130, 130), 16)]
    for label, hold, hp, d in cases:
        m, c, rows = run_push(hold, hp, (130, 100), d, 120)
        out.append(summarize('P2 ' + label, rows, c))
        open(os.path.join(OUT, 'p2_%s.txt' % label.split()[0].replace('->', '_')), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


@scen
def p3_release():
    """P3: hero presses RIGHT until t80 then lets go (pad 0); then presses again at t110: the counter, the stop and the 31-tick delay"""
    out = []
    m, c = pillar_world(hero_px=(100, 100), direction=24, pil_px=(130, 100))
    seq = [(80, gl.RIGHT), (30, 0), (90, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    out.append(summarize('P3 release at t80, press again at t111', rows, c))
    prev = None
    for r in rows:
        d = r['c'][c]
        key = (d['anim_t'], d['dir_t'], d['cnt274'] == 0, r['hero']['xcoll'] == K.ent(c), d['fx'] != 0)
        if key != prev:
            out.append(row_line(r, c))
            prev = key
    open(os.path.join(OUT, 'p3_release.txt'), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


@scen
def p4_wall():
    """P4: wall (walkability 0x40) in column W east of the pillar; the pillar stops against it, the hero goes on pressing 150 ticks: does anim/sound go on?"""
    out = []
    for col in (7, 8, 9):
        cells = lambda cc, rr, col=col: dict(wk=0x40) if cc == col else None
        m, c, rows = run_push(gl.RIGHT, (100, 100), (130, 100), 24, 150, cells=cells)
        out.append(summarize('P4 wall column %d (x %d..%d)' % (col, col * 24, col * 24 + 23), rows, c))
        out.append('   last rows: ' + ' || '.join(row_line(r, c) for r in rows if r['tick'] in (149, 150)))
        n_snd = sum(1 for r in rows if r['tick'] > 100 for x in r['journal'] if x == 'snd(25)')
        out.append('   snd(25) in t101..150: %d ; pillar anim at t150 %d ; hero.xcoll==pillar at t150: %s' % (n_snd, rows[-1]['c'][c]['anim_t'], rows[-1]['hero']['xcoll'] == K.ent(c)))
        open(os.path.join(OUT, 'p4_wall_col%d.txt' % col), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


@scen
def p5_offset_and_diagonal():
    """P5: hero y offset (dy) against the pillar (130,100) pressing RIGHT; and a diagonal press (RIGHT|DOWN)"""
    out = []
    for dy in (-14, -12, -8, -4, 0, 4, 8, 12, 14):
        m, c, rows = run_push(gl.RIGHT, (100, 100 + dy), (130, 100), 24, 120)
        out.append(summarize('P5 dy=%+d RIGHT' % dy, rows, c))
    for label, hold, hp in (('RIGHT|DOWN from (100,88)', gl.RIGHT | gl.DOWN, (100, 88)), ('RIGHT|UP from (100,112)', gl.RIGHT | gl.UP, (100, 112))):
        m, c, rows = run_push(hold, hp, (130, 100), 24, 120)
        out.append(summarize('P5 ' + label, rows, c))
        open(os.path.join(OUT, 'p5_diag_%s.txt' % label.split()[0].replace('|', '_')), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


@scen
def p6_real_map26():
    """P6: the real cell field of map 26 and the real placement of record 29 (XPos 26, YPos 52 -> pixel (324, 424)); the hero starts at the save's tile (12,26) = pixel (300, 424) and pushes RIGHT, then other sides"""
    out = []
    cells = real_cells(26)
    for label, hold, hp, d in (('from the west (the save tile (12,26)), RIGHT', gl.RIGHT, (300, 424), 24), ('from the east, LEFT', gl.LEFT, (350, 424), 8),
                               ('from the north, DOWN', gl.DOWN, (324, 396), 0), ('from the south, UP', gl.UP, (324, 452), 16)):
        m, c, rows = run_push(hold, hp, (324, 424), d, 200, cells=cells, mapname='map_26', sprite=174)
        out.append(summarize('P6 map26 ' + label, rows, c))
        open(os.path.join(OUT, 'p6_map26_%s.txt' % label.split(',')[-1].strip()), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out



@scen
def p7_freeze_after_f_start():
    """P7: the binary's F-slot start (0x80042150) zeroes the hero's ForceStepY/X +0xD8/+0xD4 and ForceY/X +0xC8/+0xC4; harness write of those four words at tick 30 while the hero walks RIGHT in the open:
    does MovePlayer + the physics pass restore the walk while the key stays down? (free ground, no pillar)"""
    out = []
    m = gl.world(keep=set(), hero_px=(100, 100), direction=24)
    seq = [(100, gl.RIGHT)]
    pads, nn = gl.pad_script(seq)

    def pre(m_, t):
        if t == 30:
            e = K.ent(0)
            for off in (0xD8, 0xD4, 0xC8, 0xC4):
                m_.poke(e + off, 0)
    rows = gl.run(m, nn, pads, crates=[], pre=pre)
    for r in rows:
        if r['tick'] in (28, 29, 30, 31, 32, 33, 40, 60, 100):
            h = r['hero']
            out.append('t%03d hero px=%.3f fx=%d izf=%d anim=%d adir=%d' % (r['tick'], h['px'] / 65536, h['fx'], h['izf'], h['anim_t'], h['adir']))
    out.append('final hero x %.3f (walking would give ~%.1f)' % (rows[-1]['hero']['px'] / 65536, 100 + 2.4375 * 98))
    return out



def add_other(m, xpx, ypx, mapname, sprite, rec_addr, ent_addr, ptr_addr):
    widen(mapname, sprite)
    return gl.add_crate(m, xpx, ypx, mapname=mapname, sprite=sprite, rec_addr=rec_addr, ent_addr=ent_addr, ptr_addr=ptr_addr)


@scen
def p8_onto_plate_and_switch():
    """P8: the pillar (130,100) pushed east into (a) a plate of the puzzle rooms (map 27 sprite 108, 48x32x16 slab, Collidable) placed at x=190, (b) a stepping switch (map 26 sprite 45, 'Interrupteur (pietiner)', Collidable)
    placed at x=190: is the pillar blocked, does it ride on top, what is its support (+0x12C)?"""
    out = []
    for label, mapname, sprite, addrs in (('plate 108', 'map_27', 108, (0x801F0000, 0x801F8000, 0x801F9000)), ('switch 45', 'map_26', 45, (0x801DC000, 0x801E4000, 0x801E5000))):
        m, c = pillar_world(hero_px=(100, 100), direction=24, pil_px=(130, 100))
        p = add_other(m, 190, 100, mapname, sprite, *addrs)
        pe = K.ent(p)
        seq = [(220, gl.RIGHT)]
        pads, nn = gl.pad_script(seq)
        rows = gl.run(m, nn, pads, crates=[c, p])
        last = rows[-1]
        fl = m.peek(pe + 0x6C)
        out.append('P8 %s: flags6C=%X size(w,h,d)=(%.2f,%.2f,%.2f) pz=%.2f ; end: pillar (%.3f,%.3f,%.3f) support==%s ground %d adj %d anim %d ; hero (%.3f) xcoll==%s' % (
            label, fl, m.peeks(pe + 0x1F0) / 65536, m.peeks(pe + 0x1F4) / 65536, m.peeks(pe + 0x1F8) / 65536, last['c'][p]['pz'] / 65536,
            last['c'][c]['px'] / 65536, last['c'][c]['py'] / 65536, last['c'][c]['pz'] / 65536, 'PLATE' if last['c'][c]['support'] == pe else '%X' % last['c'][c]['support'],
            last['c'][c]['ground'], last['c'][c]['adj'], last['c'][c]['anim_t'], last['hero']['px'] / 65536, 'PLATE' if last['hero']['xcoll'] == pe else ('PIL' if last['hero']['xcoll'] == K.ent(c) else '%X' % last['hero']['xcoll'])))
        open(os.path.join(OUT, 'p8_%s.txt' % label.replace(' ', '_')), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


@scen
def p9_two_pillars_in_line():
    """P9: two pillars in line (130 and 156 px): the hero pushes the first, which touches the second: does the second move, and what does the first one do?"""
    out = []
    m, c = pillar_world(hero_px=(100, 100), direction=24, pil_px=(130, 100))
    p = add_other(m, 156, 100, 'map_26', 174, 0x801F0000, 0x801F8000, 0x801F9000)
    # the second pillar needs its own C76 hook: the handler table keeps the real C76 for every entity of type 0x1AE
    pads, nn = gl.pad_script([(220, gl.RIGHT)])
    rows = gl.run(m, nn, pads, crates=[c, p])
    for t in (36, 40, 60, 100, 160, 220):
        r = rows[t - 1]
        out.append('t%d first (%.2f,anim %d,cnt %d) second (%.2f,anim %d,cnt %d,xcoll==first %s)' % (t, r['c'][c]['px'] / 65536, r['c'][c]['anim_t'], r['c'][c]['cnt274'], r['c'][p]['px'] / 65536, r['c'][p]['anim_t'], r['c'][p]['cnt274'], r['c'][p]['xcoll'] == K.ent(c)))
    open(os.path.join(OUT, 'p9_two_pillars.txt'), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out



@scen
def p10_ledge_drop():
    """P10: ledge. Cells of columns 0..7 are 2 height units high (32 px), columns 8+ are at 0: the pillar (130,100) and the hero stand on the high side; pushed east the pillar leaves the ledge at x ~ 202 (its box
    passes the column 8 line at x=192): does it fall (Gravity bit 0x100), where does it land, and does the hero follow (fall too / get blocked)?"""
    out = []
    cells = lambda cc, rr: dict(h=2) if cc < 8 else dict(h=0)
    m, c, rows = run_push(gl.RIGHT, (100, 100), (130, 100), 24, 260, cells=cells)
    out.append(summarize('P10 ledge', rows, c))
    prev = None
    for r in rows:
        d = r['c'][c]
        key = (d['ground'], d['adj'], d['anim_t'])
        if key != prev or r['tick'] % 40 == 0:
            out.append('t%03d pillar (%.2f,%.2f,z %.2f) ground %d adj %d anim %d | hero (%.2f, z %.2f, ground %d)' % (r['tick'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536, d['ground'], d['adj'], d['anim_t'], r['hero']['px'] / 65536, r['hero']['pz'] / 65536, r['hero']['ground']))
            prev = key
    open(os.path.join(OUT, 'p10_ledge.txt'), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out



@scen
def p11_pit_fill_map347():
    """P11: the real cells of map 347 (flat plateau of height 3 = 48 px with 1-cell pits of height 1 = 16 px) and the real placement of record 16 (204, 232): the hero, east of the pillar, pushes it west along the row 14
    toward the pit of the cell (5,14) [x 120..143], then east toward the pit (11,14) [x 264..287]: does the pillar fall into the pit, at which height does it rest, can the hero then walk over it?"""
    out = []
    cells = real_cells(347)
    for label, hold, hp, d in (('west to the pit (5,14)', gl.LEFT, (240, 232), 8), ('east to the pit (11,14)', gl.RIGHT, (168, 232), 24)):
        m, c, rows = run_push(hold, hp, (204, 232), d, 330, cells=cells, mapname='map_26', sprite=174)
        out.append(summarize('P11 ' + label, rows, c))
        prev = None
        for r in rows:
            d_ = r['c'][c]
            key = (d_['ground'], d_['adj'], d_['anim_t'])
            if key != prev and r['tick'] > 36:
                out.append('   t%03d pillar (%.2f,%.2f,z %.2f) ground %d adj %d anim %d | hero (%.2f, z %.2f, ground %d, support==pillar %s)' % (
                    r['tick'], d_['px'] / 65536, d_['py'] / 65536, d_['pz'] / 65536, d_['ground'], d_['adj'], d_['anim_t'], r['hero']['px'] / 65536, r['hero']['pz'] / 65536, r['hero']['ground'], r['hero']['support'] == K.ent(c)))
                prev = key
        open(os.path.join(OUT, 'p11_%s.txt' % label.split()[0]), 'w').write(NL.join(row_line(r, c) for r in rows) + NL)
    return out


if __name__ == '__main__':
    names = [a for a in sys.argv[1:] if a in SCEN] or list(SCEN)
    for nme in names:
        print('==', nme)
        for line in SCEN[nme]():
            print(line)
