#!/usr/bin/env python3
"""Swept-box check: the driven path of the hero (or of a scripted actor) of each arc against the liftable footprints of its map. Read-only.

A path is a polyline of root positions (x, y) in logical px. The mover box (offsets -10,-7, size 21 x 15; the hero's, Alundra.Tests/AlundraDialogueOpcodesProductionTests.cs:350-351)
is swept along each segment (Chebyshev: the union of the boxes at the end points, expanded over the segment's bounding rectangle, which over-approximates diagonals).
Result per path: the smallest XY gap (px) between the swept rectangle and any liftable footprint of the map (0 = XY overlap; Z is not tested, so a 0 means 'à mesurer'),
and the record that gives it. Every path point cites its source (test file:line or program pc); nothing is simulated.
"""
import json

rows = json.load(open('all_records.json', encoding='utf-8'))

# (arc, map, label, [(x, y), ...], source)
PATHS = [
    ('A5 part 1', 392, 'B1 0x64@50 then 0x1F@68 48 px east', [(720, 216), (768, 216)], 'events 392 @50/@68; AlundraShipCorridorArcTests.cs:97-100 (arc), 392 map program'),
    ('A5r roll', 392, 'roll after 0x11@104 to the end of frame 225', [(776, 216), (770.4, 248.7)], 'AlundraShipCorridorArcTests.cs:121-122 (HeroAt104 50798592/65536=775.1), :233 (50492832, 16298384)'),
    ('A5/A5r final', 392, 'PlaceHero(540,360,64) then Down to the portal (22,23)', [(540, 360), (540, 380)], 'AlundraShipCorridorArcTests.cs:166-167,212-213; portal tile (22,23) y 368..384'),
    ('A1c cabin', 390, 'B2 walks 80 N, 48 W, 80 N, 32 W from tile (44,23)', [(1068, 376), (1068, 296), (1020, 296), (1020, 215), (988, 215)], 'AlundraShipArcTests.cs:201, AlundraCabinWalkTests.cs:21-30,95-99'),
    ('A0b push zone', 390, 'bare arc: hero tile (38,49) then B1 0x1F 80+32 (no controller: probe not consulted)', [(924, 792), (924, 712), (924, 680)], 'AlundraShipArcTests.cs:90 (RealController false)'),
    ('A8 hero', 163, 'hero (960,152) walks 64 px west (0x0B r=64 @190)', [(960, 152), (896, 152)], 'AlundraInoaAwakeningArcTests.cs:62-63 (HeroAt73), :251-252 (HeroAt190/194)'),
    ('A8 Jess', 163, 'Jess (924,136) walks south to the wall at y 297', [(924, 136), (924, 297)], 'AlundraInoaAwakeningArcTests.cs:243-248 (60555264/65536=924)'),
    ('A9 hero idle', 172, 'tile (36,18): hero does not move', [(876, 296)], 'AlundraVisionAndCoastArcTests.cs:430'),
    ('A10 hero idle', 165, 'arrival (300,360)', [(300, 360)], 'AlundraInoaDayOneArcTests.cs:112-115'),
    ('A10 hero B[2]', 165, 'hero (300,360): 52 N, 118 E, 102 N (0x09 dir 16/24/16 then 0x0B @304/@310/@316)', [(300, 360), (300, 308), (418, 308), (418, 206)], 'events 165 B[2] @302-316'),
    ('A10 rec5 C[1]', 165, 'rec5 from (300,428): 96 N, 120 E, 78 N', [(300, 428), (300, 332), (420, 332), (420, 254)], 'events 165 C[1] @682-696 (dir 16, 24, 16)'),
    ('A11 hero', 164, 'PlaceHero(996,120,16)', [(996, 120)], 'AlundraInoaDayOneArcTests.cs:373'),
    ('A11 Septimus', 164, 'rec1 from (1092,120): S, W (to the hero), E 24', [(1092, 120), (1092, 136), (1017, 136), (1041, 136)], 'AlundraInoaDayOneArcTests.cs:326-331; events 164 C[2] @337-386'),
    ('A12/A14 arrival', 179, 'arrival (420,120,16)', [(420, 120)], 'AlundraBergusJumpArcTests.cs:27-28'),
    ('A14 hero walk', 179, 'hero from (420,120) south to (420,332) (0x24@564)', [(420, 120), (420, 332)], 'AlundraDay3SceneArcTests.cs:140-148'),
    ('A18 hero', 178, 'arrival (924,408) then PlaceHero(948,232,16)', [(924, 408), (948, 232)], 'AlundraDay3SceneArcTests.cs:349-351,404'),
    ('A18 Septimus', 178, 'rec15 from (972,344): W 24, W 22, E 48, S 160', [(972, 344), (948, 344), (926, 344), (974, 344), (974, 504)], 'events 178 C[6] @519-703 (dirs 8,8,24,0)'),
    ('T-C61 cart', 61, 'hero (588,648) towards blocks rec32-35 (x 252..588)', [(588, 648), (252, 792)], 'AlundraEntityContactArcTests.cs:493-496; blocks at (588,536),(396,552),(252,696),(276,792)'),
]


def gap_rect(rect, r):
    x0 = r['x'] + r['offx']; x1 = x0 + r['sx']; y0 = r['y'] + r['offy']; y1 = y0 + r['sy']
    gx = max(x0 - rect[1], rect[0] - x1, 0)
    gy = max(y0 - rect[3], rect[2] - y1, 0)
    return max(gx, gy)


def main():
    for name, m, label, pts, src in PATHS:
        lifts = [r for r in rows if r['map'] == m and r['kind'] != 0]
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        rect = (min(xs) - 10, max(xs) + 11, min(ys) - 7, max(ys) + 8)
        best = min(((gap_rect(rect, r), r['idx']) for r in lifts), default=(None, None))
        print(f'{name:<16} map {m:>3}: swept x[{rect[0]:.0f},{rect[1]:.0f}) y[{rect[2]:.0f},{rect[3]:.0f}) min XY gap to a liftable = {best[0]} (rec{best[1]}) :: {label}')


if __name__ == '__main__':
    main()
