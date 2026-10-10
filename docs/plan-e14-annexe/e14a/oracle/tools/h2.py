"""h2.py - E14.a0b T1 (b): the floor (+0x134) and the four corner heights (+0x158..+0x164) of the Beannoid and Muruta bodies and of the hero next to them, on the REAL cells of maps 14, 115 and 116.

Everything the binary reads is real code or a byte the harness writes here: the cells of data-extracted/data/map_<N>.json are loaded into the emulated memory in place of the flat field of world.py
(`*[0x800DD86C] + 0x604`, 52 x 60 cells of 8 bytes, word 0 = walkability | ground << 8 | slope << 16 | height << 24, the only word the functions read); the bodies are created by the REAL spawner 0x8003A1B8 (InitializeEntity
0x80039D04, which itself runs 0x800370C4 at 0x80039EE0 and 0x80038064 at 0x80039F30) from the real sprite record and the real map record of the body; the hero is the real InitializeEntity on the hero record.

Variants of a row:  0 = the values the creation left;  1 = the end of a physics step at the same position (0x800370C4, its result stored at +0x138 as the callers do at 0x80037DF8, then 0x80038064);
2 = the body moved by (sdx, sdy) pixels, then the same two calls (the hero stays where it was).  The relative position of the pair (the real 0x8007FE20) is read on every row.
Row layout (signed 32-bit): map record variant hdx hdy sdx sdy | E: 17 values | H: 17 values | R: 6 values, with the 17 = posx posy posz modx mody modz w h d tilex tiley terrain floor c0 c1 c2 c3.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import c1world as C
import world as W
import bank
from machine import Machine
import hmodel as H
import h1
from hmodel import s32, M32

DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
RNG = 0x80098708
REL = 0x801EA000
COLTAB = 0x80127168

# map -> (sprite table index, the records that are bodies of that sprite); read from the data, asserted below
MAPS = {
    14: dict(sprite=79, name='Overworld 3,1', kind='Beannoid'),
    115: dict(sprite=91, name="Tarn's Manor (hall)", kind='Muruta'),
    116: dict(sprite=79, name="Tarn's Manor (grate)", kind='Beannoid'),
}
HERO_OFFSETS = ((0, 0), (24, 0), (-24, 0), (0, 16), (0, -16))        # pixels, from the body
SWEEP = tuple((dx, dy) for dx in (-36, -24, -12, 0, 12, 24, 36) for dy in (-24, -16, -8, 0, 8, 16, 24))
HERO_SWEEP_OFFSET = (24, 0)

_JS = {}


def mapjson(n):
    if n not in _JS:
        _JS[n] = json.load(open(DATA + '/map_%d.json' % n, encoding='utf-8-sig'))
    return _JS[n]


def body_records(n):
    d = mapjson(n)
    sp = MAPS[n]['sprite']
    es = d['SpriteInfo']['Entities']['Entities']
    return [i for i, e in enumerate(es) if e is not None and e['SpriteTableIndex'] == sp]


def cell_words(n):
    """the 52 x 60 first words, with the check that the decoded fields compose the word"""
    d = mapjson(n)
    mp = d['Map']
    assert mp['Width'] == 52 and mp['Height'] == 60
    tiles = mp['MapTiles']
    assert len(tiles) == 52 * 60
    words = {}
    for row in range(60):
        for col in range(52):
            t = tiles[row * 52 + col]
            assert t['TileX'] == col and t['TileY'] == row
            w = (t['Flags'] & M32)
            comp = t['Walkability'] | (t['GroundProperty'] << 8) | ((t['Slope'] & 0xFF) << 16) | ((t['Height'] & 0xFF) << 24)
            assert w == comp, (n, col, row, hex(w), hex(comp))
            words[(row, col)] = w
    return words


def build_machine(n):
    """fresh machine: the real cells of map n, the map constants, BSS words, the balance blob at the level D of the map, the new-game kit; no entity yet"""
    d = mapjson(n)
    inf = d['Info']
    words = cell_words(n)
    m = Machine()
    h1.apply_patches(m)

    def cells(col, row):
        w = words[(row, col)]
        return (w >> 24) & 0xFF, (w >> 16) & 0xFF, w & 0xFF, (w >> 8) & 0xFF

    W.build_world(m, gravity=inf['Gravity'], visc=inf['ZViscosity'], cells=cells)
    m.poke8(W.MAPHDR + 8, inf['SlideEffectId'])
    m.poke8(W.MAPHDR + 9, inf['BalanceLevel'])
    m.poke8(W.MAPHDR + 0xA, inf['C'])
    m.poke8(W.MAPHDR + 0xB, inf['D'])
    W.init_bss(m)
    W.load_balance(m, inf['D'])
    W.init_stats(m)
    W.init_pad2(m)
    m.poke(RNG, 0x12345678)
    return m, words


def spawn_bodies(m, n, recs):
    """the real spawner for the records `recs` of map n (the sprite record of the body installed as the per-map sprite table entry); returns the list of entity addresses"""
    d = mapjson(n)
    si = d['SpriteInfo']
    sp = MAPS[n]['sprite']
    rec = si['SpriteRecords'][sp]
    es = si['Entities']['Entities']
    bank.install(m, C.BEAN_REC, C.BEAN_REC, rec)
    tsize = si['Header']['SpriteTableSize'] // 4
    for i in range(tsize):
        m.poke(C.SPR_PTRS + 4 * i, 0)
    m.poke(C.SPR_PTRS + 4 * sp, C.BEAN_REC)
    blob = b''.join(C.entity_record_bytes(es[r], es[r]['XPos'], es[r]['YPos'], es[r]['Height']) for r in recs)
    m.poke_bytes(C.ENT_REC, blob)
    m.poke(C.MAPBLK + 8, C.ENT_REC)
    m.poke(C.MAPBLK + 0xC, C.SPR_PTRS)
    m.poke(C.MAPBLK + 0x10, tsize)
    m.poke(C.MAPBLK + 0x14, len(recs))
    addrs = []
    for i in range(len(recs)):
        p = m.call(0x8003A1B8, 0, i, 1, limit=5_000_000) & M32      # a2 = 1: forced spawn (the window test of 0x8003A208-0x8003A25C and the bit 0x40 test are skipped; the Muruta of the hall, SpriteDirection 0x80, are spawned by script)
        assert p != 0, 'the real spawner refused record %d of map %d' % (recs[i], n)
        m.poke(p + W.F['status'], 2)
        addrs.append(p)
    return addrs


def read17(m, e):
    g = m.peeks
    return [g(e + 0x114), g(e + 0x118), g(e + 0x11C), g(e + 0x1E4), g(e + 0x1E8), g(e + 0x1EC), g(e + 0x1F0), g(e + 0x1F4), g(e + 0x1F8),
            g(e + 0x120), g(e + 0x124), g(e + 0x138), g(e + 0x134), g(e + 0x158), g(e + 0x15C), g(e + 0x160), g(e + 0x164)]


def phys_end(m, e):
    """the end of a physics step for one entity: 0x800370C4 (corners, result = terrain), stored at +0x138 as 0x80037DF8 does, then 0x80038064 (floor at +0x134, tile, ground)"""
    v = K.call(m, 0x800370C4, e)
    m.poke(e + 0x138, v)
    K.call(m, 0x80038064, e)


def relpos(m, e):
    K.call(m, 0x8007FE20, e, REL)
    return [m.peeks(REL + 4 * i) for i in range(6)]


def model_of(words, v):
    """(terrain, corners) the independent model gives for a 17-value block"""
    posx, posy, posz, mx, my, mz, w, h, d = v[:9]
    return H.ground_model(lambda r, c: words[(r, c)], posx, posy, mx, my, w, h)


def check_block(words, v, floor_is_terrain_plus_one=True):
    """-> list of mismatches between the emulated binary's block and the independent model"""
    terrain, corners = model_of(words, v)
    bad = []
    if v[11] != terrain:
        bad.append('terrain %d model %d' % (v[11], terrain))
    if v[13:17] != corners:
        bad.append('corners %s model %s' % (v[13:17], corners))
    if v[12] != terrain + 1:
        bad.append('floor %d != terrain + 1 (%d)' % (v[12], terrain + 1))
    return bad


def run_map(n, engine, light=None):
    """-> (rows, problems, stats).  light (the mutation probe): a tuple of the records to run, with one hero place and the whole sweep"""
    K.use_engine('emu')
    m, words = build_machine(n)
    recs = body_records(n)
    hero_offsets = ((24, 0),) if light else HERO_OFFSETS
    sweep = SWEEP
    if light:
        recs = [r for r in recs if r in light]
    bodies = spawn_bodies(m, n, recs)
    m.uninit_reads.clear()
    rows = []
    problems = []
    creation = {}
    for r, e in zip(recs, bodies):
        creation[r] = read17(m, e)
    cnt = dict(rows=0, bodies=len(recs))
    K.use_engine(engine)
    for r, e in zip(recs, bodies):
        v0 = creation[r]
        bx, by = v0[0] >> 16, v0[1] >> 16
        for hdx, hdy in hero_offsets:
            hx, hy = bx + hdx, by + hdy
            if not (0 <= hx < 1248 and 0 <= hy < 960):
                continue
            K.use_engine('emu')
            W.place_hero(m, hx, hy)
            K.use_engine(engine)
            hero = W.ent(0)
            hc = read17(m, hero)
            # variant 0: the values the creation left (body: read after the spawner; hero: after the real InitializeEntity)
            rows.append(([n, r, 0, hdx, hdy, 0, 0], v0, hc, relpos(m, e)))
            for who, blk in (('body', v0), ('hero', hc)):
                for msg in check_block(words, blk):
                    problems.append('map %d record %d variant 0 %s: %s' % (n, r, who, msg))
            # variant 1: the end of a physics step at the same position
            phys_end(m, e)
            phys_end(m, hero)
            ev, hv = read17(m, e), read17(m, hero)
            rows.append(([n, r, 1, hdx, hdy, 0, 0], ev, hv, relpos(m, e)))
            for who, blk in (('body', ev), ('hero', hv)):
                for msg in check_block(words, blk):
                    problems.append('map %d record %d variant 1 %s (hero offset %d,%d): %s' % (n, r, who, hdx, hdy, msg))
        # variant 2: the sweep of the body around its place, the hero beside it
        K.use_engine('emu')
        hx, hy = bx + HERO_SWEEP_OFFSET[0], by + HERO_SWEEP_OFFSET[1]
        W.place_hero(m, hx, hy)
        K.use_engine(engine)
        hero = W.ent(0)
        phys_end(m, hero)
        for sdx, sdy in sweep:
            nx, ny = bx + sdx, by + sdy
            if not (0 <= nx < 1230 and 0 <= ny < 940):
                continue
            m.poke(e + 0x114, v0[0] + (sdx << 16))
            m.poke(e + 0x118, v0[1] + (sdy << 16))
            phys_end(m, e)
            ev, hv = read17(m, e), read17(m, hero)
            rows.append(([n, r, 2, HERO_SWEEP_OFFSET[0], HERO_SWEEP_OFFSET[1], sdx, sdy], ev, hv, relpos(m, e)))
            for msg in check_block(words, ev):
                problems.append('map %d record %d variant 2 sweep (%d,%d): %s' % (n, r, sdx, sdy, msg))
        m.poke(e + 0x114, v0[0])
        m.poke(e + 0x118, v0[1])
        phys_end(m, e)
    unread = sorted(((hex(pc), hex(a), sz), c) for (pc, a, sz), c in m.uninit_reads.items())
    K.use_engine('emu')
    return rows, problems, dict(uninit=unread, bodies=len(recs), cov=m.cov, words=words)


def text_of(n, rows):
    lines = ['# map %d (%s): bodies of sprite %d, cells of data-extracted/data/map_%d.json; each row = map record variant hero_dx hero_dy sweep_dx sweep_dy | E(17) | H(17) | R(6)' % (n, MAPS[n]['name'], MAPS[n]['sprite'], n),
             '# E and H = posx posy posz modx mody modz w h d tilex tiley terrain floor c0 c1 c2 c3 (raw 16.16 words); R = |dx| |dy| |dz| dx dy dz of the real 0x8007FE20 (body, hero)']
    for head, e, h, r in rows:
        lines.append(' '.join(str(x) for x in head) + ' | ' + ' '.join(str(x) for x in e) + ' | ' + ' '.join(str(x) for x in h) + ' | ' + ' '.join(str(x) for x in r))
    return '\n'.join(lines) + '\n'


def run_all():
    """both interpreters on every map; the texts must be identical; returns (summary lines, ok, texts by map)"""
    lines = []
    ok = True
    texts = {}
    covs = set()
    for n in sorted(MAPS):
        rows, problems, st = run_map(n, 'emu')
        rows2, problems2, st2 = run_map(n, 'mips')
        t1, t2 = text_of(n, rows), text_of(n, rows2)
        same = t1 == t2
        covs |= st['cov']
        # the formula of section 0.2 of the notes, on the binary's values (the DLL side is checked by the C# tests against these rows)
        floors_enemy = [e[12] - e[11] for _, e, h, r in rows]
        floors_hero = [h[12] - h[11] for _, e, h, r in rows]
        terr_e = sorted({e[11] >> 16 for _, e, h, r in rows})
        terr_h = sorted({h[11] >> 16 for _, e, h, r in rows})
        good = same and not problems and not problems2 and not st['uninit'] and not st2['uninit']
        ok &= good
        texts[n] = t1
        v2 = [x for x in rows if x[0][2] == 2]
        lines.append('map %-3d %-22s %2d bodies   %4d rows (variant 0/1/2: %d/%d/%d)   emulator == independent model (terrain, four corners, floor = terrain + 1): %s   second interpreter text identical: %s   unwritten-byte reads: %d   %s' % (
            n, MAPS[n]['name'], st['bodies'], len(rows), sum(1 for x in rows if x[0][2] == 0), sum(1 for x in rows if x[0][2] == 1), len(v2),
            'yes (%d problems)' % len(problems) if problems else 'yes', 'yes' if same else 'NO', len(st['uninit']) + len(st2['uninit']), 'PASS' if good else 'FAIL'))
        lines.append('        floor - terrain: body {%s}, hero {%s} (raw units);  terrain heights seen (px): body %s, hero %s' % (
            ','.join(str(x) for x in sorted(set(floors_enemy))), ','.join(str(x) for x in sorted(set(floors_hero))), terr_e, terr_h))
        for p in (problems + problems2)[:6]:
            lines.append('        PROBLEM ' + p)
        for u in st['uninit'][:5]:
            lines.append('        UNWRITTEN READ ' + str(u))
    return lines, ok, texts, covs


if __name__ == '__main__':
    ls, ok, _, _ = run_all()
    print('\n'.join(ls))
    print('H2', 'PASS' if ok else 'FAIL')
