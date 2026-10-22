"""E19.sm4: the catalogue of every named test of the slice and the REMOVAL MATRIX derived from it.

Each test declares the code element(s) its success depends on (`needs`) and whether it is red today. Reverting ONE element (the `case`, the
seam, ...) while keeping every test makes red exactly the tests that need it and are red today (a green guard stays green by definition of
"green today"); removing every test while keeping the code makes nothing red (no other test reads these paths, checked by grep in the report).
The unit tests come from named_tests.py (audited against the binary), the rows of the vector theories from counts.json.

Usage: python -B build_catalog.py NAMED_AUDIT.json VECTORS_DIR OUT_DIR
"""
import json
import os
import sys

audit = json.load(open(sys.argv[1], encoding='utf-8'))
vdir = sys.argv[2]
out_dir = sys.argv[3]
counts = json.load(open(os.path.join(vdir, 'counts.json')))['perOpcode']

ELEM = {'R1': 'case0x72', 'R2': 'case0x88', 'R3': 'case0x96', 'R4': 'case0x97', 'R5': 'case0xB3'}
tests = []


def add(tid, cls, name, kind, needs, red, what, count=1, red_count=None):
    tests.append(dict(id=tid, cls=cls, name=name, kind=kind, needs=needs, redToday=red, what=what, count=count,
                      redCount=(count if red else 0) if red_count is None else red_count))


for a in audit:
    needs = [ELEM[a['rule']]]
    if a['id'].startswith('UB3') and a['id'] in ('UB3-3',):
        needs.append('seamOverwrite')
    add(a['id'], a['cls'], a['name'], 'unit', needs, a['redToday'], a['what'])

# DLL-only unit tests (not auditable on the binary)
add('U72-6', 'EntityStateOpcodesTests', 'SetLastTarget_0x72_ATouchProgramEndingWithIt_SurvivesTheResumeOfTheTickProgram', 'unit', ['case0x72'], True,
    'C program resumable (Break); F program [09 0A, 1A 07, 72, FF] interrupts it; the C call that follows restores Target from Last: (7, 10) with the opcode, the pre-F (3, 5) today')
add('U88-10', 'EntityStateOpcodesTests', 'SetStatus_0x88_HeroGroup_WithoutAPlayerEntity_IsEmptyAndDoesNotThrow', 'unit', ['case0x88'], False, 'context without PlayerEntity, byte 1 = 0x81: no match, CodeIndex 3')
add('U88-3b', 'EntityStateOpcodesTests', 'SetStatus_0x88_Index2_DoesNotCallDestroyEntity_NorClearEventTrigger', 'unit', ['case0x88'], True,
    'recording context: DestroyedEntities empty, EventTrigger untouched (kills the structural mutant MS-3); red through the status assertion')
add('U96-14', 'LifeAndMagicOpcodesTests', 'RestoreLife_0x96_HeroGroup_WithoutAPlayerEntity_IsEmptyAndDoesNotThrow', 'unit', ['case0x96'], False, 'no PlayerEntity: nothing written, CodeIndex 3')
add('U96-15', 'LifeAndMagicOpcodesTests', 'RestoreLife_0x96_DoesNotSnapTheHud', 'unit', ['case0x96'], False,
    'HudDirector displayed (10, 20, 0, 2) while the stats maxima are (50, 4): unchanged after 96 81 FF (a Snap would give 50, 50, 4, 4); green today, kills MS-2')
add('U96-16', 'LifeAndMagicOpcodesTests', 'RestoreLife_0x96_ForAHeroThatCarriesItsLife_TheStatsFollowAtTheEndOfMovePlayer', 'unit', ['case0x96'], True,
    'LifePulledFromStats hero 10/50, stats 10/50: entity Hp 50 at once, stats still 10, then PlayerManager.EndOfMovePlayer: stats 50/50')
add('U97-13', 'LifeAndMagicOpcodesTests', 'AddMagic_0x97_DoesNotSnapTheHud', 'unit', ['case0x97'], False, 'same as U96-15 for 97 32; green today, kills MS-2')
add('UB3-6', 'PadWriteOpcodeTests', 'SetPadButtons_0xB3_ThenTheOpcode0x2F_SeesTheForcedWordsInTheSameCall', 'unit', ['case0xB3'], True,
    '[B3 00 10 80 00 00 01 00 02, 2F 00 10 00, FF] -> Result 1; second program [B3 ..., 2F 80 00 01] -> 1 (pressed); [2F 00 20 00] -> 0; today Result 0 (real pad)')
add('UB3-7', 'PadWriteOpcodeTests', 'SetPadButtons_0xB3_LeavesTheCountersAndTheStickAlone', 'unit', ['case0xB3'], True,
    'after 3 Update(0x1000): NumberOfFrameHold 2; B3 -> hold 0x1000 still, counters 2/0/0, GameState.LastPadState (stick 0.5) unchanged')
add('S-1', 'TickPadTests', 'Overwrite_WritesTheFourWordsAndKeepsTheCounters', 'seam', ['seamOverwrite'], False, 'Update x3 then Overwrite(0x1234, 0x5678, 0x9ABC, 0xDEF0): words set, NumberOfFrameHold 2, IsOverThanMaxNbFrameHeld 0, RepeatInterval 0')
add('S-2', 'TickPadTests', 'Overwrite_ThenUpdate_ComputesTheEdgesFromTheForcedHold', 'seam', ['seamOverwrite'], False, 'Overwrite(0x1000,0,0,0) then Update(0): pressed 0, released 0x1000, hold 0, counters reset')
add('S-3', 'TickPadTests', 'Overwrite_ThenUpdateOfTheSameHold_KeepsCountingFromTheCounters', 'seam', ['seamOverwrite'], False, 'Overwrite(0x1000,...) after 3 Update(0x1000), then Update(0x1000): NumberOfFrameHold 3, interval 0')

# vector replay theories
for op, el in (('72', 'case0x72'), ('88', 'case0x88'), ('96', 'case0x96'), ('97', 'case0x97'), ('B3', 'case0xB3')):
    c = counts[op]
    needs = [el] + (['seamOverwrite'] if op == 'B3' else [])
    add('V-' + op, 'ActionOpcodeVectorTests', 'TheVectorsOfTheOpcode_%s_ReplayOnTheDll' % op, 'vector', needs, c['redToday'] > 0, '%d replayable rows' % c['replayable'],
        count=c['replayable'], red_count=c['redToday'])
add('V-OOT', 'ActionOpcodeVectorTests', 'TheOutOfTableVectorsOf_0x88_WriteNothing', 'vector', ['case0x88'], False, '22 rows with an index above 2: statuses unchanged', count=22, red_count=0)
add('V-MANIFEST', 'ActionOpcodeVectorTests', 'TheVectorFilesMatchTheirManifest', 'vector', [], False, 'sha256 of every file against MANIFEST.sha256 (drift guard)')

# real bytes
add('RB-SHAPES', 'ActionOpcodeSiteTests', 'TheOperandsOfTheNinetySitesAreInsideTheCoveredShapes', 'realbytes', [], False,
    '90 sites in 50 maps: bytes at (map, pc) equal opcode+operands of the list; 0x96 byte 1 in {0x80,0x81}; 0x97 in {0x32,0x0A}; 0x88 index in {0,2}; 0xB3 words = 00 10 00 00 00 00 00 00; export drift guard')
add('RB-SAVEROOMS', 'ActionOpcodeSiteTests', 'TheNineteenSaveRoomsHaveTheSameProgramBytes', 'realbytes', [], False,
    'maps 452..470: B[2] entry 116 / B[3] entry 224; bytes 96 80 32 @204 and 97 32 @330 in each (export drift guard); the 36 sites of the 18 other rooms are covered by this pin and by the shape', count=19, red_count=0)
RB = [('RB-452-B2', 'case0x96', 'bytes 96 80 32 @204 of Save Room (Coal Mine)-452 B[2]; hero 5/40 -> 40'),
      ('RB-452-B3', 'case0x97', 'bytes 97 32 @330 B[3]; Mp 1/4 -> 4, money 500 untouched'),
      ('RB-394-C3', 'case0x96,case0x97', 'bytes 96 80 32 97 0A @749..753 (logic = hero after the 0x42 @592): Hp full, Mp +10 clamped to 4'),
      ('RB-475-B2', 'case0x96,case0x97', 'bytes 96 81 32 97 32 @502..506 (logic = hero after the 0x42 @139)'),
      ('RB-22-C6', 'case0x96', 'bytes 96 81 FF @863 of Lars Crypt 22 C[6]'),
      ('RB-306-E14', 'case0x88', 'bytes 88 80 00 @1110 of Meia Dream-306 E[14]: Deactivated -> Normal'),
      ('RB-442-E1', 'case0x88', 'bytes 88 80 02 @1312 of Meia Dream-442 E[1]: -> FlagToDestroy'),
      ('RB-444-D15', 'case0x72', 'bytes 2B 09 10 72 @1299..1302 of Torla (inner)-444 D[15]: ClassA cleared, Target dir 16, Last = Target'),
      ('RB-140-D2', 'case0x72', 'bytes 27 0A BD DC 01 72 @2032..2037 of Cave-140 D[2]: Last = Target and differs from the sentinel 0x77'),
      ('RB-161-B1', 'case0xB3', 'bytes B3 00 10 00 00 00 00 00 00 @54 of Fairy cave (Stenia)-161 B[1]: TickPad hold 0x1000, others 0')]
for tid, el, what in RB:
    add(tid, 'ActionOpcodeSiteTests', 'RealBytes_' + tid[3:].replace('-', '_'), 'realbytes', el.split(','), True, what)

# arcs
ARCS = [('ARC-452', ['case0x96', 'case0x97'], True, 'Save Room (Coal Mine)-452: B[2] ... 96 @204 then B[3] ... 97 @330; Hp 5/40 -> 40, Mp 1/4 -> 4, money untouched; no sm4 opcode in SkippedOrExceeded'),
        ('ARC-161a', ['case0xB3'], True, 'Fairy cave (Stenia)-161: B3 @54 runs; the pad seen by MovePlayer in the same frame is hold 0x1000 / pressed 0 / released 0 (order probe)'),
        ('ARC-161b', ['case0xB3'], True, 'same map, real controller: the loop reaches (B, 67, 0x8B) with the hero in (6..13, 29, 3); the frame after the last B3 shows released 0x1000'),
        ('ARC-306', ['case0x88'], True, 'Meia Dream-306: records 13 and 14 set Deactivated; E runs once, 88 @1110 implemented, status Normal, FF @1113'),
        ('ARC-442', ['case0x88'], True, 'Meia Dream-442: record 0 set Deactivated; E[1] runs once, status 4 at FF @1315, the entity leaves the world at the next tick'),
        ('ARC-40', ['case0x72'], True, 'Nirude Lair-40: record 0 mid C[2], ActiveCollisionEntity = it; at 72 @1153 Last = Target = (1, dir); the next C instruction keeps Target')]
for tid, needs, red, what in ARCS:
    add(tid, 'ActionOpcodeSiteTests', 'Arc_' + tid[4:].replace('-', '_'), 'arc', needs, red, what)

add('P-WIT', 'EventProgramRunnerTests', 'UnknownOpcode_KnownSize_SkipsBySize', 'pin', ['witness'], False,
    'repointed from 0xB3 to 0xB9: [B9, 0, 1A, 4, FF] -> TargetAnimationId 4; green before and after; the old 0xB3 version stays green but empty after the slice')
add('P-LBL', 'EventOpcodeSizeTableTests', 'TheSm4LabelsAreCorrected', 'pin', ['labels'], True, 'Entries[0x88].Name not empty, Entries[0x97].Name has no "money"; red today (empty / "Spend money")')

json.dump(tests, open(os.path.join(out_dir, 'tests-catalog.json'), 'w', encoding='utf-8', newline='\n'), indent=1, sort_keys=True)

# ---- removal matrix
elements = ['case0x72', 'case0x88', 'case0x96', 'case0x97', 'case0xB3', 'witness', 'labels']
matrix = {}
for e in elements:
    hit = [t for t in tests if e in t['needs']]
    red = [t for t in hit if t['redToday']]
    matrix[e] = dict(testsThatNeedIt=[t['id'] for t in hit],
                     redWhenRemoved=[t['id'] for t in red],
                     greenGuardsThatStayGreen=[t['id'] for t in hit if not t['redToday']],
                     redRowsOfTheTheories=sum(t['redCount'] for t in red if t['kind'] == 'vector'),
                     redTestsCount=len(red))
seam = [t for t in tests if 'seamOverwrite' in t['needs']]
matrix['seamOverwrite(compile)'] = dict(testsThatCallIt=[t['id'] for t in seam], note='removing the seam breaks the BUILD of these tests, it does not make them red')
json.dump(matrix, open(os.path.join(out_dir, 'removal-matrix.json'), 'w', encoding='utf-8', newline=chr(10)), indent=1, sort_keys=True)
print('tests in the catalogue:', len(tests), ' unit(audited+extra):', sum(1 for t in tests if t['kind'] == 'unit'))
for e, m in matrix.items():
    if 'redTestsCount' in m:
        print(e, '-> red tests', m['redTestsCount'], '+ red theory rows', m['redRowsOfTheTheories'], m['redWhenRemoved'], 'green guards', m['greenGuardsThatStayGreen'])
    else:
        print(e, m['testsThatCallIt'])
