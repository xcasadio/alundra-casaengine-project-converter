"""v8.py - E20.e0b2 T0, V8 by the LOAD tile of the hero (revision 1 of the plan, point 1).

The real map-load record loop of ALUN_CD.EXE (France) (0x8003B2EC-0x8003B354, the REAL spawner 0x8003A1B8 for i = 0..127, stop at the first record whose first word is 0) is run on the MIPS machine
by `p8_loadmap.load_map` (byte copy of the critic's probe), for (map, tile) pairs taken from the ArcSpecs of Alundra.Tests, where the tile is the one the hero of the DLL occupies DURING the load
loop: `AdoptPlayerPawn` places the hero BEFORE the loop, at the centre of the New Game tile (AlundraGameState.CameraTileX/Y = (33, 59)) for an arc without arrival, or at the arrival record's
position ((PosX >> 16) / 24, (PosY >> 16) / 16) for an arc with an arrival; ArcRun writes the tile of its ArcSpec (HeroTileX/Y) only AFTER InitializeWithWorld. The hero tile is what the spawn-zone gate
of the binary (0x8003A1F4-0x8003A25C) reads (+0x120 / +0x124 of slot 0).

Output: out/v8.json = { pairs: [...], reproduction: [...] }. Each pair gives: map, zone and world names, the load tile, how the DLL test reaches it (the arc specs that use it), the records called, the
entities created in order (record index, slot, sprite number, item, +0x40, draws), the number of draws, the final RNG word (seed 0xB017C93D = AlundraRandom.Reset()).
`reproduction` re-runs the three pairs of the critic (14 at (8, 6) -> D5F4D64A, 390 and 392 at (0, 0) -> BBE29AC9) and checks them: the harness reproduces the discovery, with no acceptance value for the DLL.
Nothing here knows an expected value of the DLL.
"""
import sys
import os
import json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
sys.path.insert(0, HERE)
import p8_loadmap as P

NEWGAME = (33, 59)


def arrival_tile(pos_x, pos_y):
    return ((pos_x >> 16) // 24, (pos_y >> 16) // 16)


# (map, zone, world, tile, how the DLL test reaches the tile, source arcs)
PAIRS = [
    (14, 'Overworld', 'Overworld 3,1-14', NEWGAME, 'arc without arrival: AdoptPlayerPawn places the hero on the New Game tile',
     'AlundraHitArena / E20e0a3a, E20e0a3b, E20e0a5, E20e0k1 (Arc(name): 8, 6 written AFTER the load)'),
    (390, 'The Klark', 'Ship Klark (inner)-390', NEWGAME, 'arc without arrival', 'A0b, A1, A1c (AlundraShipArcTests), RandomReset, HeldDirections (AlundraPrefabArcSupportTests)'),
    (392, 'The Klark', 'Ship Klark (night, inner, break)-392', NEWGAME, 'arc without arrival', 'A5, A5r (AlundraShipCorridorArcTests: 29, 13 written AFTER the load), E19t-392'),
    (389, 'The Klark', 'Ship Klark (beginning)-389', NEWGAME, 'arc without arrival', 'the ship arcs of map 389 (AlundraHeroJumpArcTests and others)'),
    (391, 'The Klark', 'Ship Klark (night, break, Event)-391', NEWGAME, 'arc without arrival', 'A5r end, the 391 arcs (AlundraVisionAndCoastArcTests)'),
    (478, 'Inoa', 'Inoa (Vision Event from Lars and Melzas cutscene)-478', NEWGAME, 'arc without arrival', 'PrefabSelfTest, PrefabBare, PrefabHeroSelfTest (AlundraPrefabArcSupportTests)'),
    (115, "Tarn's Manor", "Tarn's Manor-115", arrival_tile(84 << 16, 120 << 16), 'arc with arrival (84, 120) px, z 1 (LeverArcs.Spec)', 'E20c1b2-A115, E20c1b2-smoke115'),
    (116, "Tarn's Manor", "Tarn's Manor-116", arrival_tile(84 << 16, 328 << 16), 'arc with arrival (84, 328) px, z 0 (LeverArcs.Spec)', 'E20c1b2-B116, E20c1b2-smoke116'),
    (117, "Tarn's Manor", "Tarn's Manor (unused broken)-117", arrival_tile(500 << 16, 192 << 16), 'arc with arrival (500, 192) px, z 1 (LeverArcs.Spec)', 'E20c1b2-C117, E20c1b2-smoke117b'),
    (117, "Tarn's Manor", "Tarn's Manor (unused broken)-117", arrival_tile(72 << 16, 640 << 16), 'arc with arrival (72, 640) px, z 2 (LeverArcs.Spec)', 'E20c1b2-D117, E20c1b2-smoke117l'),
    (44, 'Wendels Nightmare', 'Wendels Nightmare-44', arrival_tile(120 << 16, 440 << 16), 'arc with arrival (120, 440) px, z 8 (LeverArcs.Spec)', 'E20c1b2-smoke44 (and E, map 44 arcs)'),
    (162, 'Inoa', 'Inoa-162', arrival_tile(47972352, 27787264), 'arc with arrival 47972352, 27787264', 'TH4-A20 (AlundraEntityContactArcTests.ArrivalSpec)'),
    (165, 'Inoa', 'Inoa (inner)-165', arrival_tile(19660800, 23592960), 'arc with arrival 19660800, 23592960', 'TH4-A10'),
    (185, 'Inoa', 'Inoa (inner)-185', arrival_tile(60555264, 24641536), 'arc with arrival 60555264, 24641536', 'TH4-A19'),
    (179, 'Inoa', 'Inoa (inner)-179', arrival_tile((17 * 24 + 12) << 16, (7 * 16 + 8) << 16), 'arc with arrival at tile (17, 7), z 1 (day3-after-dream and many)', 'TH4-day3-after-dream, UJ-7, S6c...'),
    (176, 'Inoa', 'Inoa-176', arrival_tile(11796480, 36175872), 'arc with arrival 11796480, 36175872, z 10', 'TH4-A13'),
    (10, 'Overworld', 'Overworld 2,1-10', arrival_tile(16515072, 61341696), 'arc with arrival 16515072, 61341696', 'TH4-A10J, AlundraArcInventoryHelpersTests'),
    (135, 'Church', 'Church-135', arrival_tile(30670848, 54001664), 'arc with arrival 30670848, 54001664, z 1', 'TH4-A17'),
    (178, 'Inoa', 'Inoa (inner)-178', arrival_tile(60555264, 26738688), 'arc with arrival 60555264, 26738688', 'TH4-A18'),
    (167, 'Inoa', 'Inoa (inner)-167', NEWGAME,
     'NO ArcSpec of the suite names map 167 (its only test, RealMap167_OneFullRunnerCallFrom0x5AAt144, loads the program without a world); an ad-hoc ArcSpec without arrival is the DLL way',
     'none (the plan names map 167 for the arc of the 0x5A mode 4: ad-hoc spec of the m2 test)'),
]


def run_pair(mp, tile):
    t, rows, final, printed = P.load_map(mp, tile)
    created = [r for r in rows if r.get('ptr')]
    refused = [r['i'] for r in rows if 'ptr' in r and not r['ptr']]
    called = sum(1 for r in rows if 'ptr' in r)
    stop = next((r['i'] for r in rows if r.get('stop')), None)
    draws = sum(r['draws'] for r in rows if 'draws' in r)
    ents = [dict(record=r['i'], slot=r['slot'], sprite=r['sprite'], item=r['item'], flag40=r['flag'], draws=r['draws']) for r in created]
    return dict(tile=list(t), records_called=called, stopped_at=stop, refused=refused, entities=ents, draws=draws, final_word='%08X' % final)


out = dict(seed='B017C93D', pairs=[], reproduction=[])
for mp, zone, world, tile, how, src in PAIRS:
    r = run_pair(mp, tile)
    r.update(map=mp, zone=zone, world=world, how=how, arcs=src)
    out['pairs'].append(r)
    print('map %3d tile %-9s called %3d entities %2d draws %2d final %s  [%s]' % (mp, tuple(tile), r['records_called'], len(r['entities']), r['draws'], r['final_word'], how[:40]))

REPRO = [(14, (8, 6), 'D5F4D64A', 11, 19), (390, (0, 0), 'BBE29AC9', 12, 16), (392, (0, 0), 'BBE29AC9', 12, 12)]
ok_all = True
for mp, tile, want, want_draws, want_ents in REPRO:
    r = run_pair(mp, tile)
    ok = r['final_word'] == want and r['draws'] == want_draws and len(r['entities']) == want_ents
    ok_all &= ok
    r.update(map=mp, expected_final_word=want, expected_draws=want_draws, expected_entities=want_ents, reproduced=ok)
    out['reproduction'].append(r)
    print('REPRO map %3d tile %s: final %s draws %d entities %d -> %s' % (mp, tile, r['final_word'], r['draws'], len(r['entities']), 'OK' if ok else 'MISMATCH'))
json.dump(out, open(os.path.join(HERE, 'out', 'v8.json'), 'w'), indent=1)
print('reproduction of the critic pairs:', 'PASS' if ok_all else 'FAIL')
sys.exit(0 if ok_all else 1)
