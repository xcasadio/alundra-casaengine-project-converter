"""Generates the frozen vectors of E19.sm1 (0x60 0x61 0x3C 0x3D): curated vectors (one per named test) and seeded random vectors.
Every vector is run on the REAL handler (TracedEmu), compared with my own python model of the handler, and with the model of the DLL in two montages.
Usage: python -B gen_vectors.py OUTDIR      (writes OUTDIR/sm1-vectors.json and OUTDIR/sm1-vectors.sha256; deterministic: no clock, no environment)"""
import hashlib
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib  # noqa: E402
import sm1lib as L  # noqa: E402
from sm1lib import S, HERO  # noqa: E402

BOX = (10, 12, 20, 22, 3, 5)           # x1 x2 y1 y2 z1 z2
FAR = (50, 50, 50)                      # a tile outside the box BOX (inside the wide box [0..255])
HFAR = (1000, 1000, 1000)               # the hero's tile: outside every box used below, the wide ones included


def vec(vid, title, op, code, n, logic, slots, tags=(), junk=True, anon=()):
    assert len(slots) == n + 1, vid
    assert len(code) == L.SIZE[op], vid
    for a in anon:
        for d in slots:
            for key in ('riding', 'xcoll', 'parent', 'platform'):
                assert d[key] != a, ('a pointer references the anonymous slot', vid)
    return dict(id=vid, title=title, op=op, code=list(code), n=n, logic=logic, slots=slots, tags=list(tags), junk_beyond_n=junk, anon=list(anon))


def code60(t):
    return [0x60, t]


def code61(t):
    return [0x61, t]


def code3(op, box):
    x1, x2, y1, y2, z1, z2 = box
    return [op, x1, x2, y1, y2, z1, z2]


def curated():
    V = []
    h = lambda **kw: HERO(tile=HFAR, **kw)    # noqa: E731
    plate = lambda **kw: S(status=2, ref=25, flags=0x60C0, **kw)                   # noqa: E731  a plate (sprite 108 flags)
    block = lambda ref=5, **kw: S(status=kw.pop('status', 2), ref=ref, flags=0x332384, **kw)   # noqa: E731  a transportable object

    # ---------------------------------------------------------------- 0x60: an entity of the group rests on me
    V.append(vec('v60-c01', '0x60 a member of the raw-id group rides the logic entity', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(riding=1)], ['rides']))
    V.append(vec('v60-c02', '0x60 the member rides nothing', 0x60, code60(5), 2, 1, [h(), plate(), block(riding=None)], ['rides']))
    V.append(vec('v60-c03', '0x60 the member rides another entity', 0x60, code60(5), 3, 1,
                 [h(), plate(), block(riding=3), S(status=2, ref=9, flags=0x60C0)], ['rides']))
    V.append(vec('v60-c04a', '0x60 an empty group (no entity of that raw id) although another entity rides the logic entity', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(ref=6, riding=1)], ['empty']))
    V.append(vec('v60-c04b', '0x60 a group of one entity alone with the hero, nobody rides', 0x60, code60(5), 1, 0, [h(), block()], ['empty', 'hero-logic']))
    V.append(vec('v60-c05a', '0x60 the reference is Destroyed (status 0): the raw-id group is empty, a rider rides it', 0x60, code60(5), 2, 1,
                 [h(), S(status=0, ref=25, flags=0x60C0), block(riding=1)], ['reference-status']))
    V.append(vec('v60-c05b', '0x60 the reference is FlagToDestroy (status 4): the raw-id group is empty', 0x60, code60(5), 2, 1,
                 [h(), S(status=4, ref=25, flags=0x60C0), block(riding=1)], ['reference-status']))
    V.append(vec('v60-c05c', '0x60 the reference is Loaded (status 1): the group is searched', 0x60, code60(5), 2, 1,
                 [h(), S(status=1, ref=25, flags=0x60C0), block(riding=1)], ['reference-status']))
    V.append(vec('v60-c05d', '0x60 the reference is Deactivated (status 3): the group is searched', 0x60, code60(5), 2, 1,
                 [h(), S(status=3, ref=25, flags=0x60C0), block(riding=1)], ['reference-status']))
    V.append(vec('v60-c06a', '0x60 the rider is FlagToDestroy: a raw-id candidate is not status-tested', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(status=4, riding=1)], ['candidate-status']))
    V.append(vec('v60-c06b', '0x60 the rider is Destroyed (status 0): not status-tested either', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(status=0, riding=1)], ['candidate-status']))
    V.append(vec('v60-c06c', '0x60 the rider is Loaded (status 1)', 0x60, code60(5), 2, 1, [h(), plate(), block(status=1, riding=1)], ['candidate-status']))
    V.append(vec('v60-c07', '0x60 the rider is blocked by an entity (+0x20 non-null): still a rider, 0x3F would refuse it', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(riding=1, blocked=True)], ['blocked']))
    V.append(vec('v60-c08a', '0x60 two members, only the second rides', 0x60, code60(5), 3, 1,
                 [h(), plate(), block(), block(riding=1)], ['any-member']))
    V.append(vec('v60-c08b', '0x60 two members, only the first rides', 0x60, code60(5), 3, 1,
                 [h(), plate(), block(riding=1), block()], ['any-member']))
    V.append(vec('v60-c08c', '0x60 three members, none rides', 0x60, code60(5), 4, 1,
                 [h(), plate(), block(), block(), block()], ['any-member']))
    V.append(vec('v60-c09a', '0x60 0x81 the hero rides the logic entity (136 C[5] shape)', 0x60, code60(0x81), 1, 1,
                 [h(riding=1), S(status=2, ref=40, flags=0x32080)], ['hero-group']))
    V.append(vec('v60-c09b', '0x60 0x81 the hero rides nothing', 0x60, code60(0x81), 1, 1, [h(), S(status=2, ref=40, flags=0x32080)], ['hero-group']))
    V.append(vec('v60-c09c', '0x60 0x81 the hero rides another entity', 0x60, code60(0x81), 2, 1,
                 [h(riding=2), S(status=2, ref=40, flags=0x32080), S(status=2, ref=41, flags=0x32080)], ['hero-group']))
    V.append(vec('v60-c09d', '0x60 0x81 another entity rides the logic entity but the group is the hero only', 0x60, code60(0x81), 2, 1,
                 [h(), S(status=2, ref=40, flags=0x32080), block(riding=1)], ['hero-group']))
    V.append(vec('v60-c10a', '0x60 the candidate rides the OWNER (the hero), not the logic entity', 0x60, code60(5), 2, 2,
                 [h(), block(riding=0), plate()], ['logic-not-owner']))
    V.append(vec('v60-c10b', '0x60 the candidate rides the LOGIC entity (a plate that is not the hero)', 0x60, code60(5), 2, 2,
                 [h(), block(riding=2), plate()], ['logic-not-owner']))
    V.append(vec('v60-c11', '0x60 the riding pointer points at nothing in the world (a foreign non-null reference)', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(riding='foreign')], ['foreign']))
    V.append(vec('v60-c12', '0x60 0x82 (all entities, the hero included): the hero rides the logic entity', 0x60, code60(0x82), 2, 1,
                 [h(riding=1), plate(), block()], ['group-all']))
    V.append(vec('v60-c13a', '0x60 0x83 (all but the hero): only the hero rides the logic entity', 0x60, code60(0x83), 2, 1,
                 [h(riding=1), plate(), block()], ['group-all']))
    V.append(vec('v60-c13b', '0x60 0x83 (all but the hero): another entity rides the logic entity', 0x60, code60(0x83), 2, 1,
                 [h(), plate(), block(riding=1)], ['group-all']))
    V.append(vec('v60-c14a', '0x60 the raw id is compared on 32 bits: EntityRefId 0x105 does not match 5', 0x60, code60(5), 2, 1,
                 [h(), plate(), block(ref=0x105, riding=1)], ['ref-width']))
    V.append(vec('v60-c14b', '0x60 control of c14a: EntityRefId 5 matches', 0x60, code60(5), 2, 1, [h(), plate(), block(ref=5, riding=1)], ['ref-width']))
    V.append(vec('v60-c15', '0x60 raw id 0: the hero (EntityRefId -1) is never a candidate even when he rides the logic entity', 0x60, code60(0), 2, 2,
                 [h(riding=2), S(status=2, ref=0, flags=0x60C0), plate()], ['hero-ref']))
    V.append(vec('v60-c16a', '0x60 0x80 the group is the logic entity itself: it rides itself (degenerate)', 0x60, code60(0x80), 1, 1,
                 [h(), plate(riding=1)], ['self-group']))
    V.append(vec('v60-c16b', '0x60 0x80 the group is the logic entity itself, it rides nothing', 0x60, code60(0x80), 1, 1, [h(), plate()], ['self-group']))
    V.append(vec('v60-c17', '0x60 the logic entity is the hero (a map-event program B): an object rides the hero', 0x60, code60(5), 2, 0,
                 [h(), plate(), block(riding=0)], ['hero-logic']))
    V.append(vec('v60-c18a', '0x60 0x86 (entities riding the reference) passes the group of the search through', 0x60, code60(0x86), 2, 1,
                 [h(), plate(), block(riding=1)], ['pass-through']))
    V.append(vec('v60-c18b', '0x60 0x84 (entities on the ground, Collidable, no PlatformEntity) passes through', 0x60, code60(0x84), 2, 1,
                 [h(), plate(), block(riding=1)], ['pass-through']))
    V.append(vec('v60-c21', '0x60 ten slots (N = 9): only the last one rides the logic entity', 0x60, code60(5), 9, 1,
                 [h(), plate()] + [block(ref=6) for _ in range(7)] + [block(ref=5, riding=1)], ['last']))
    V.append(vec('v60-a01', '0x60 a bare logic entity without LogicContextEntity (anonymous slot 1) and a member that rides nothing: 0 (null == null must not match)', 0x60, code60(5), 2, 1,
                 [h(), plate(), block()], ['anonymous'], anon=[1]))
    V.append(vec('v61-a01', '0x61 a member without LogicContextEntity (anonymous slot 1) and a logic entity that rides nothing: 0 (null == null must not match)', 0x61, code61(0), 2, 2,
                 [h(), S(status=2, ref=0, flags=0x3A180), S(status=2, ref=1, flags=0x230180)], ['anonymous'], anon=[1]))
    V.append(vec('v60-c19','0x60 0x84 a member without the Collidable bit is not in the group, although it rides the logic entity', 0x60, code60(0x84), 2, 1,
                 [h(), plate(), S(status=2, ref=5, flags=0x100, riding=1)], ['pass-through']))

    # ---------------------------------------------------------------- 0x61: I rest on an entity of the group
    item = lambda ref=1, **kw: S(status=kw.pop('status', 2), ref=ref, flags=0x230180, **kw)      # noqa: E731  398 B[1] shape
    lurvy = lambda ref=0, **kw: S(status=kw.pop('status', 2), ref=ref, flags=0x3A180, **kw)         # noqa: E731
    V.append(vec('v61-c01', '0x61 0 the logic entity (an item, a 0x43 target) rides record 0 (398 B[1] shape)', 0x61, code61(0), 2, 2,
                 [h(), lurvy(), item(riding=1)], ['rides']))
    V.append(vec('v61-c02', '0x61 0 the logic entity rides nothing: Result 0 (null reference guard)', 0x61, code61(0), 2, 2, [h(), lurvy(), item()], ['rides']))
    V.append(vec('v61-c03', '0x61 0 the logic entity rides an entity outside the group', 0x61, code61(0), 3, 2,
                 [h(), lurvy(), item(riding=3), S(status=2, ref=7, flags=0x3A180)], ['rides']))
    V.append(vec('v61-c04', '0x61 0 an empty group (no record 0) although the logic entity rides something', 0x61, code61(0), 2, 2,
                 [h(), S(status=2, ref=3, flags=0x3A180), item(riding=1)], ['empty']))
    V.append(vec('v61-c05a', '0x61 0 the member rides the logic entity, the reverse relation: 0', 0x61, code61(0), 2, 2,
                 [h(), lurvy(riding=2), item()], ['swap']))
    V.append(vec('v60-c20', '0x60 0 on the same world as v61-c05a: the member rides the logic entity: 1', 0x60, code60(0), 2, 2,
                 [h(), lurvy(riding=2), item()], ['swap']))
    V.append(vec('v61-c06a', '0x61 0x81 the logic entity rides the hero (77 C[14] shape)', 0x61, code61(0x81), 1, 1, [h(), S(status=2, ref=20, flags=0x301E2, riding=0)], ['hero-group']))
    V.append(vec('v61-c06b', '0x61 0x81 the logic entity rides nothing', 0x61, code61(0x81), 1, 1, [h(), S(status=2, ref=20, flags=0x301E2)], ['hero-group']))
    V.append(vec('v61-c06c', '0x61 0x81 the hero rides the logic entity, not the reverse', 0x61, code61(0x81), 1, 1, [h(riding=1), S(status=2, ref=20, flags=0x301E2)], ['hero-group']))
    V.append(vec('v61-c07a', '0x61 13 the logic entity is the hero (a program B, 159 B[5] shape): he rides record 13', 0x61, code61(13), 2, 0,
                 [h(riding=1), S(status=2, ref=13, flags=0x20C0), S(status=2, ref=14, flags=0x20C0)], ['hero-logic']))
    V.append(vec('v61-c07b', '0x61 13 the hero rides nothing', 0x61, code61(13), 2, 0, [h(), S(status=2, ref=13, flags=0x20C0), S(status=2, ref=14, flags=0x20C0)], ['hero-logic']))
    V.append(vec('v61-c07c', '0x61 13 the hero rides another record', 0x61, code61(13), 2, 0, [h(riding=2), S(status=2, ref=13, flags=0x20C0), S(status=2, ref=14, flags=0x20C0)], ['hero-logic']))
    V.append(vec('v61-c09a', '0x61 the carrier is FlagToDestroy: a raw-id candidate is not status-tested', 0x61, code61(0), 2, 2,
                 [h(), lurvy(status=4), item(riding=1)], ['candidate-status']))
    V.append(vec('v61-c09b', '0x61 the carrier is Destroyed (status 0)', 0x61, code61(0), 2, 2, [h(), lurvy(status=0), item(riding=1)], ['candidate-status']))
    V.append(vec('v61-c09c', '0x61 the carrier is Loaded (status 1)', 0x61, code61(0), 2, 2, [h(), lurvy(status=1), item(riding=1)], ['candidate-status']))
    V.append(vec('v61-c10a', '0x61 the logic entity is Destroyed (status 0): the raw-id group is empty', 0x61, code61(0), 2, 2,
                 [h(), lurvy(), item(status=0, riding=1)], ['reference-status']))
    V.append(vec('v61-c10b', '0x61 the logic entity is FlagToDestroy (status 4)', 0x61, code61(0), 2, 2, [h(), lurvy(), item(status=4, riding=1)], ['reference-status']))
    V.append(vec('v61-c10c', '0x61 the logic entity is Loaded (status 1)', 0x61, code61(0), 2, 2, [h(), lurvy(), item(status=1, riding=1)], ['reference-status']))
    V.append(vec('v61-c11', '0x61 the carrier pointer points at nothing known', 0x61, code61(0), 2, 2, [h(), lurvy(), item(riding='foreign')], ['foreign']))
    V.append(vec('v61-c12a', '0x61 two members, the logic entity rides the second', 0x61, code61(0), 3, 3, [h(), lurvy(), lurvy(), item(riding=2)], ['any-member']))
    V.append(vec('v61-c12b', '0x61 two members, the logic entity rides the first', 0x61, code61(0), 3, 3, [h(), lurvy(), lurvy(), item(riding=1)], ['any-member']))
    V.append(vec('v61-c12c', '0x61 two members, the logic entity rides neither', 0x61, code61(0), 3, 3, [h(), lurvy(), lurvy(), item()], ['any-member']))
    V.append(vec('v61-c13a', '0x61 0x82 (all entities, hero included): the logic entity rides the hero', 0x61, code61(0x82), 2, 1,
                 [h(), item(riding=0), S(status=2, ref=7, flags=0x3A180)], ['group-all']))
    V.append(vec('v61-c13b', '0x61 0x83 (all but the hero): the logic entity rides only the hero', 0x61, code61(0x83), 2, 1,
                 [h(), item(riding=0), S(status=2, ref=7, flags=0x3A180)], ['group-all']))
    V.append(vec('v61-c14', '0x61 the raw id is compared on 32 bits: EntityRefId 0x100 does not match 0', 0x61, code61(0), 2, 2,
                 [h(), lurvy(ref=0x100), item(riding=1)], ['ref-width']))
    V.append(vec('v61-c15', '0x61 0x80 the group is the logic entity itself: it rides itself (degenerate)', 0x61, code61(0x80), 1, 1, [h(), item(riding=1)], ['self-group']))
    V.append(vec('v61-c16', '0x61 0x85 passes the group of the search through (the entity the reference rides)', 0x61, code61(0x85), 2, 2,
                 [h(), lurvy(), item(riding=1)], ['pass-through']))

    # ---------------------------------------------------------------- 0x3D: any entity in the inclusive box
    inbox = (11, 21, 4)
    def z(vid, title, op, code, n, logic, slots, tags=()):          # shorthand
        V.append(vec(vid, title, op, code, n, logic, slots, tags))
    for op in (0x3D, 0x3C):
        nm = '%02X' % op
        pfx = 'v%s-' % nm.lower()
        ok = (lambda t, **kw: S(status=kw.pop('status', 2), ref=kw.pop('ref', 30), flags=kw.pop('flags', 0x80), tile=t, **kw))   # noqa: E731
        z(pfx + 'c01', '0x%s an entity inside the box' % nm, op, code3(op, BOX), 2, 1, [h(), S(status=2, ref=1, flags=0x80), ok(inbox)], ['inside'])
        z(pfx + 'c02', '0x%s the lower corner (10,20,3) is inside' % nm, op, code3(op, BOX), 1, 1, [h(), ok((10, 20, 3))], ['corner'])
        z(pfx + 'c03', '0x%s the upper corner (12,22,5) is inside' % nm, op, code3(op, BOX), 1, 1, [h(), ok((12, 22, 5))], ['corner'])
        for k, (t, label, expect) in enumerate([
                ((9, 21, 4), 'x one below', 0), ((13, 21, 4), 'x one above', 0), ((11, 19, 4), 'y one below', 0), ((11, 23, 4), 'y one above', 0),
                ((11, 21, 2), 'z one below', 0), ((11, 21, 6), 'z one above', 0)]):
            z(pfx + 'c04%s' % 'abcdef'[k], '0x%s %s the box: outside' % (nm, label), op, code3(op, BOX), 1, 1, [h(), ok(t)], ['face'])
        for k, (t, label) in enumerate([((10, 21, 4), 'x low face'), ((12, 21, 4), 'x high face'), ((11, 20, 4), 'y low face'),
                                        ((11, 22, 4), 'y high face'), ((11, 21, 3), 'z low face'), ((11, 21, 5), 'z high face')]):
            z(pfx + 'c05%s' % 'abcdef'[k], '0x%s the %s is inside' % (nm, label), op, code3(op, BOX), 1, 1, [h(), ok(t)], ['face'])
        z(pfx + 'c06', '0x%s an inverted box (min above max) holds nobody' % nm, op, code3(op, (12, 10, 22, 20, 5, 3)), 1, 1, [h(), ok(inbox)], ['inverted'])
        hi = (0x80, 0xFF, 0x80, 0xFF, 0x80, 0xFF)
        z(pfx + 'c07a', '0x%s operands are unsigned bytes: (0x90,0xA0,0xFF) is inside [0x80..0xFF]' % nm, op, code3(op, hi), 1, 1, [h(), ok((0x90, 0xA0, 0xFF))], ['unsigned'])
        z(pfx + 'c07b', '0x%s operands are unsigned bytes: (5,5,5) is outside [0x80..0xFF]' % nm, op, code3(op, hi), 1, 1, [h(), ok((5, 5, 5))], ['unsigned'])
        z(pfx + 'c07c', '0x%s operands are unsigned bytes: (0x80,0x80,0x80) the low corner is inside' % nm, op, code3(op, hi), 1, 1, [h(), ok((0x80, 0x80, 0x80))], ['unsigned'])
        z(pfx + 'c07d', '0x%s operands are unsigned bytes: x 0x7F is just below' % nm, op, code3(op, hi), 1, 1, [h(), ok((0x7F, 0x90, 0x90))], ['unsigned'])
        wide = (0, 255, 0, 255, 0, 255)
        z(pfx + 'c08a', '0x%s a negative TileX is outside [0..255]' % nm, op, code3(op, wide), 1, 1, [h(), ok((-1, 5, 5))], ['wrap'])
        z(pfx + 'c08b', '0x%s a negative TileY is outside [0..255]' % nm, op, code3(op, wide), 1, 1, [h(), ok((5, -1, 5))], ['wrap'])
        z(pfx + 'c08c', '0x%s a negative TileZ is outside [0..255]' % nm, op, code3(op, wide), 1, 1, [h(), ok((5, 5, -1))], ['wrap'])
        z(pfx + 'c09a', '0x%s TileX 256 is outside [0..255] (no byte truncation)' % nm, op, code3(op, wide), 1, 1, [h(), ok((256, 5, 5))], ['wrap'])
        z(pfx + 'c09b', '0x%s TileY 261 is outside [0..255]' % nm, op, code3(op, wide), 1, 1, [h(), ok((5, 261, 5))], ['wrap'])
        z(pfx + 'c09c', '0x%s TileZ 0x10000 is outside [0..255]' % nm, op, code3(op, wide), 1, 1, [h(), ok((5, 5, 0x10000))], ['wrap'])
        z(pfx + 'c10a', '0x%s TileX INT_MAX is outside' % nm, op, code3(op, wide), 1, 1, [h(), ok((0x7FFFFFFF, 5, 5))], ['wrap'])
        z(pfx + 'c10b', '0x%s TileX INT_MIN is outside' % nm, op, code3(op, wide), 1, 1, [h(), ok((-0x80000000, 5, 5))], ['wrap'])
        z(pfx + 'c10c', '0x%s the box [0,0,0,0,0,0] holds tile (0,0,0)' % nm, op, code3(op, (0, 0, 0, 0, 0, 0)), 1, 1, [h(), ok((0, 0, 0))], ['wrap'])
        for st, label, expect in [(0, 'Destroyed', 0), (1, 'Loaded', 1), (2, 'Normal', 1), (3, 'Deactivated', 1), (4, 'FlagToDestroy', 0)]:
            z(pfx + 'c11%d' % st, '0x%s the only entity in the box is %s (status %d)' % (nm, label, st), op, code3(op, BOX), 1, 1, [h(), ok(inbox, status=st)], ['status'])
        z(pfx + 'c12', '0x%s the hero is the only one inside' % nm, op, code3(op, BOX), 2, 1, [HERO(tile=inbox), S(status=2, ref=1, tile=FAR), S(status=2, ref=2, tile=FAR)], ['hero'])
        z(pfx + 'c13', '0x%s only the LAST entity is inside' % nm, op, code3(op, BOX), 4, 1,
          [h(), ok(FAR), ok(FAR), ok(FAR), ok(inbox)], ['last'])
        z(pfx + 'c14', '0x%s only the first entity after the hero is inside' % nm, op, code3(op, BOX), 4, 1,
          [h(), ok(inbox), ok(FAR), ok(FAR), ok(FAR)], ['first'])
        z(pfx + 'c15', '0x%s nobody is inside' % nm, op, code3(op, BOX), 3, 1, [h(), ok(FAR), ok((9, 21, 4)), ok((11, 21, 6))], ['nobody'])
        z(pfx + 'c16', '0x%s the LOGIC entity is outside, another entity is inside' % nm, op, code3(op, BOX), 3, 2,
          [h(), ok(FAR), ok(FAR), ok(inbox)], ['logic'])
        z(pfx + 'c17', '0x%s the LOGIC entity is the only one inside' % nm, op, code3(op, BOX), 2, 2, [h(), ok(FAR), ok(inbox)], ['logic'])
        axes = (10, 10, 20, 20, 30, 30)
        z(pfx + 'c18a', '0x%s axes in order: tile (10,20,30) is inside the degenerate box' % nm, op, code3(op, axes), 1, 1, [h(), ok((10, 20, 30))], ['axes'])
        z(pfx + 'c18b', '0x%s axes: tile (20,10,30) (x and y swapped) is outside' % nm, op, code3(op, axes), 1, 1, [h(), ok((20, 10, 30))], ['axes'])
        z(pfx + 'c18c', '0x%s axes: tile (10,30,20) (y and z swapped) is outside' % nm, op, code3(op, axes), 1, 1, [h(), ok((10, 30, 20))], ['axes'])
        real = (43, 43, 48, 51, 12, 12)
        for k, (t, expect) in enumerate([((43, 48, 12), 1), ((43, 51, 12), 1), ((43, 52, 12), 0), ((42, 50, 12), 0), ((43, 50, 13), 0), ((43, 47, 12), 0)]):
            z(pfx + 'c19%s' % 'abcdef'[k], '0x%s the box of 226 B[3] @247 (43,48..51,12), tile %s' % (nm, t), op, code3(op, real), 1, 1, [h(), ok(t)], ['real-box'])
        z(pfx + 'c20', '0x%s several entities inside, the hero too' % nm, op, code3(op, BOX), 4, 1,
          [HERO(tile=inbox), ok(inbox), ok(inbox), ok(FAR), ok(inbox)], ['several'])
        z(pfx + 'c21', '0x%s a hole (Destroyed slot) inside the box between live ones' % nm, op, code3(op, BOX), 3, 1,
          [h(), ok(FAR), ok(inbox, status=0), ok(FAR)], ['status'])
        z(pfx + 'c22', '0x%s ten slots (N = 9): only the last one is inside' % nm, op, code3(op, BOX), 9, 1,
          [h()] + [ok(FAR) for _ in range(8)] + [ok(inbox)], ['last'])
    # 0x3C only: the three conditions
    b3c = code3(0x3C, BOX)
    ib = (11, 21, 4)
    z = lambda vid, title, slots, n=1, logic=1, tags=(): V.append(vec(vid, title, 0x3C, b3c, n, logic, slots, tags))   # noqa: E731
    z('v3c-d01', '0x3C Collidable set, AnimFlags clear, no PlatformEntity: eligible', [h(), S(status=2, ref=1, flags=0x80, anim=0, tile=ib)], tags=['eligibility'])
    z('v3c-d02a', '0x3C the Collidable bit clear (flags 0x100): not eligible', [h(), S(status=2, ref=1, flags=0x100, anim=0, tile=ib)], tags=['eligibility'])
    z('v3c-d02b', '0x3C no flag at all', [h(), S(status=2, ref=1, flags=0, anim=0, tile=ib)], tags=['eligibility'])
    z('v3c-d03', '0x3C AnimFlags 0x80 (NoEntityCollision) set: not eligible', [h(), S(status=2, ref=1, flags=0x80, anim=0x80, tile=ib)], tags=['eligibility'])
    z('v3c-d04a', '0x3C a PlatformEntity (a foreign reference): not eligible', [h(), S(status=2, ref=1, flags=0x80, anim=0, tile=ib, platform='foreign')], tags=['eligibility'])
    z('v3c-d04b', '0x3C a PlatformEntity (another slot): not eligible', [h(), S(status=2, ref=1, flags=0x80, anim=0, tile=ib, platform=2), S(status=2, ref=2, flags=0x80, tile=FAR)], n=2, tags=['eligibility'])
    z('v3c-d05', "0x3C the decompilation's reading (Collidable clear, NoEntityCollision set, no platform) is NOT eligible in the binary",
      [h(), S(status=2, ref=1, flags=0, anim=0x80, tile=ib)], tags=['eligibility', 'decompilation'])
    z('v3c-d06a', '0x3C every flag bit but 0x80 is irrelevant: flags 0xFFFFFFFF, AnimFlags 0x7F: eligible', [h(), S(status=2, ref=1, flags=0xFFFFFFFF, anim=0x7F, tile=ib)], tags=['eligibility'])
    z('v3c-d06b', '0x3C flags 0xFFFFFF7F (only 0x80 clear): not eligible', [h(), S(status=2, ref=1, flags=0xFFFFFF7F, anim=0, tile=ib)], tags=['eligibility'])
    z('v3c-d06c', '0x3C AnimFlags 0xFFFFFF7F (only 0x80 clear): eligible', [h(), S(status=2, ref=1, flags=0x80, anim=0xFFFFFF7F, tile=ib)], tags=['eligibility'])
    z('v3c-d07a', '0x3C the hero with his own flags 0x3118C inside: eligible', [HERO(tile=ib), S(status=2, ref=1, flags=0x80, tile=FAR)], tags=['hero', 'eligibility'])
    z('v3c-d07b', '0x3C the hero without the Collidable bit inside: not eligible', [HERO(tile=ib, flags=0x3118C & ~0x80), S(status=2, ref=1, flags=0x80, tile=FAR)], tags=['hero', 'eligibility'])
    z('v3c-d08', '0x3C the first entity inside is ineligible, the second is eligible: 1',
      [h(), S(status=2, ref=1, flags=0x100, tile=ib), S(status=2, ref=2, flags=0x80, tile=ib)], n=2, tags=['eligibility'])
    z('v3c-d09', '0x3C the eligible entity is outside the box, the ineligible one inside: 0',
      [h(), S(status=2, ref=1, flags=0x80, tile=FAR), S(status=2, ref=2, flags=0x100, tile=ib)], n=2, tags=['eligibility'])
    V.append(vec('v3d-d10', '0x3D the world of v3c-d02a: the ineligible entity inside counts for 0x3D (1)', 0x3D, code3(0x3D, BOX), 1, 1,
                 [h(), S(status=2, ref=1, flags=0x100, anim=0, tile=ib)], ['eligibility']))
    return V


def random_vectors(op, count, seed):
    rng = random.Random(seed)
    V = []
    for k in range(count):
        n = rng.randint(0, 7)
        slots = []
        for i in range(n + 1):
            st = rng.choice([0, 1, 2, 2, 2, 3, 3, 4]) if i else rng.choice([2, 2, 2, 3, 1])
            ref = -1 if i == 0 else rng.choice([0, 1, 2, 3, 4, 5, 5, 6, 7, 0x10, 0x7F, 0x80, 0xFF, 0x105])
            fl = rng.choice([0, 0x80, 0x80, 0x100, 0x4100, 0x180, 0x1, 0xFFFFFF7F, 0xFFFFFFFF, 0x3118C, 0x332384, 0x60C0])
            an = rng.choice([0, 0, 0, 0x80, 0x40, 0xC0, 0x7F, 0xFFFFFF7F])
            tile = [rng.choice([0, 1, 2, 5, 10, 11, 12, 13, 20, 50, 255, 256, -1, 0x7FFFFFFF, -0x80000000, rng.randint(0, 25)]) for _ in range(3)]

            def pick(p_null=0.4, p_foreign=0.1):
                r = rng.random()
                if r < p_null:
                    return None
                if r < p_null + p_foreign:
                    return 'foreign'
                return rng.randint(0, n)
            slots.append(S(status=st, ref=ref, flags=fl, anim=an, tile=tile, riding=pick(), xcoll=pick(0.5, 0.05), parent=pick(0.5, 0.05),
                           platform=(None if rng.random() < 0.75 else (pick(0.0, 0.5))), blocked=rng.random() < 0.15))
        logic = rng.randint(0, n)
        if op in (0x60, 0x61):
            t = rng.choice([rng.randint(0, 8), 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x81, 0x80 + rng.randint(0, 11), rng.randint(0, 255) & 0x7F])
            # plant a positive with probability 0.5: make the relation hold for one member of the group
            if rng.random() < 0.5:
                vv = dict(op=op, code=[op, t], n=n, logic=logic, slots=slots)
                grp = L.model_search(vv, t)
                if grp:
                    c = rng.choice(grp)
                    if op == 0x60:
                        slots[c]['riding'] = logic
                    else:
                        slots[logic]['riding'] = c
            code = [op, t]
        else:
            lo = [rng.randint(0, 14) for _ in range(3)]
            hi = [l + rng.choice([0, 0, 1, 2, 5, 200]) for l in lo]
            if rng.random() < 0.1:
                lo[0], hi[0] = hi[0], lo[0]
            hi = [min(h, 255) for h in hi]
            lo = [min(l, 255) for l in lo]
            code = [op, lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]]
            if rng.random() < 0.15:
                code = [op] + [rng.choice([0x80, 0xFF, 0x7F, 0]) for _ in range(6)]
            if rng.random() < 0.55:
                # plant a tile inside the box on a random slot
                j = rng.randint(0, n)
                for ax in range(3):
                    a, b = code[1 + 2 * ax], code[2 + 2 * ax]
                    slots[j]['tile'][ax] = rng.randint(min(a, b), max(a, b)) if a <= b else a
        V.append(vec('v%02x-r%03d' % (op, k), 'random %d of 0x%02X (seed %d)' % (k, op, seed), op, code, n, logic, slots, ['random']))
    return V


def main():
    outdir = sys.argv[1]
    os.makedirs(outdir, exist_ok=True)
    allv = curated()
    for op, seed in ((0x60, 6001), (0x61, 6101), (0x3C, 3601), (0x3D, 3701)):
        allv.extend(random_vectors(op, 300, seed))
    ids = [v['id'] for v in allv]
    assert len(ids) == len(set(ids)), 'duplicate vector ids: %s' % sorted({i for i in ids if ids.count(i) > 1})
    m = L.make_machine()
    out = []
    stats = dict(model_vs_binary=0, dll_prod_vs_binary=0, dll_ctx_vs_binary=0)
    ctx_diff = []
    for v in allv:
        jr = random.Random(int(hashlib.sha1(v['id'].encode()).hexdigest()[:8], 16))
        b = L.run_binary(m, v, jr)
        mv0, mres = L.model_binary(v)
        assert b['v0'] == mv0 == L.SIZE[v['op']], (v['id'], b['v0'], mv0)
        assert b['result'] == mres, ('model_binary differs from the real handler', v['id'], b['result'], mres)
        stats['model_vs_binary'] += 1
        assert b['result'] in (0, 1), v['id']
        res = {}
        for name, hero_in_list in (('prod', True), ('ctx', False)):
            mt = L.Montage(v, hero_in_list=hero_in_list, anon=v['anon'])
            size, r = L.dll_case(mt, v['op'], v['code'])
            res[name] = (size, r)
        assert res['prod'] == (L.SIZE[v['op']], b['result']), ('DLL model (production montage) differs from the binary', v['id'], res['prod'], b['result'])
        stats['dll_prod_vs_binary'] += 1
        if res['ctx'] != res['prod']:
            ctx_diff.append(v['id'])
        else:
            stats['dll_ctx_vs_binary'] += 1
        v = dict(v)
        v['binary'] = dict(v0=b['v0'], result=b['result'], reads=b['reads'], matches=b['matches'])
        v['dll_hero_outside_list_same'] = (res['ctx'] == res['prod'])
        out.append(v)
    doc = dict(
        format='sm1-vectors-1',
        binary=dict(exe='ALUN_CD.EXE (France)', sha1=hashlib.sha1(lib.DATA).hexdigest(), handlers={('0x%02X' % k): ('0x%08X' % a) for k, a in sorted(L.HANDLER.items())},
                    search='0x8003C954', stubs=['0x8003C914 (id validator, result discarded; fatal for an invalid id)', '0x8003C840 (debug printer of an illegal search type)']),
        layout=dict(slot_stride='0x294', slot0='hero 0x80127D30', n_word='0x80134360', fields={k: '0x%X' % o for k, o in sorted(L.OFS.items())}),
        counts=dict(vectors=len(out), curated=sum(1 for v in out if 'random' not in v['tags']), random=sum(1 for v in out if 'random' in v['tags'])),
        vectors=out)
    head = {k: v for k, v in doc.items() if k != 'vectors'}
    lines = [json.dumps(v, sort_keys=True, separators=(',', ':'), ensure_ascii=True) for v in out]
    nl = chr(10)
    text = '{' + nl + '"head":' + json.dumps(head, sort_keys=True, indent=1, ensure_ascii=True) + ',' + nl + '"vectors":[' + nl + (',' + nl).join(lines) + nl + ']' + nl + '}'
    path = os.path.join(outdir, 'sm1-vectors.json')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text + '\n')
    h = hashlib.sha256((text + '\n').encode('utf-8')).hexdigest()
    with open(os.path.join(outdir, 'sm1-vectors.sha256'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(h + '  sm1-vectors.json\n')
    print('vectors', len(out), 'stats', stats, 'ctx_diff', len(ctx_diff))
    print('sha256', h)
    with open(os.path.join(outdir, 'ctx-differences.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(ctx_diff) + '\n')


main()
