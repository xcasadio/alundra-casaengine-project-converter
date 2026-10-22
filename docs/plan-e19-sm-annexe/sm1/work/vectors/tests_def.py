"""The named tests of E19.sm1 built on the frozen vectors: for each test the vectors it replays, the montage, the start state, the expected values (read from the
vectors, i.e. from the real binary), the value the DLL gives today (the opcode is skipped by its size), and the mutants of sm1lib it kills (computed).
Usage: python -B tests_def.py VECTORS.json OUTDIR"""
import json
import os
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sm1lib as L  # noqa: E402

CLS_RP = 'AlundraRidingPredicateOpcodesTests'
CLS_ZP = 'AlundraZonePredicateOpcodesTests'
CLS_OV = 'AlundraSm1OracleVectorsTests'

# (id, class, method, montage, [vector ids], note)
TESTS = [
    # ---- 0x60
    ('RP-01', CLS_RP, 'RidingOnMe_0x60_AMemberOfTheGroupRidesTheLogicEntity_GivesOne', 'prod', ['v60-c01', 'v60-c08a', 'v60-c08b', 'v60-c16a', 'v60-c18a'], 'any member; self group'),
    ('RP-02', CLS_RP, 'RidingOnMe_0x60_NobodyOfTheGroupRidesTheLogicEntity_GivesZero', 'prod', ['v60-c02', 'v60-c03', 'v60-c04b', 'v60-c08c', 'v60-c11', 'v60-c16b', 'v60-c10a'], 'rides nothing / another / a foreign pointer / the owner'),
    ('RP-03', CLS_RP, 'RidingOnMe_0x60_AnEmptyGroupGivesZero_AndOverwritesAStaleResult', 'prod', ['v60-c04a', 'v60-c04b'], 'Result before 7 and before 1'),
    ('RP-04', CLS_RP, 'RidingOnMe_0x60_TheReferenceStatusGatesTheRawIdGroup', 'prod', ['v60-c05a', 'v60-c05b', 'v60-c05c', 'v60-c05d'], 'status 0, 4 give 0; 1, 3 give 1'),
    ('RP-05', CLS_RP, 'RidingOnMe_0x60_TheRiderStatusAndTheBlockAreNotTested', 'prod', ['v60-c06a', 'v60-c06b', 'v60-c06c', 'v60-c07'], 'status 4, 0, 1 and BlockedByEntity: still a rider'),
    ('RP-06', CLS_RP, 'RidingOnMe_0x60_TheHeroIsAMemberOfTheGroups81_82_83', 'prod', ['v60-c09a', 'v60-c09b', 'v60-c09c', 'v60-c09d', 'v60-c12', 'v60-c13a', 'v60-c13b'], '0x81 hero only; 0x82 with him; 0x83 without him'),
    ('RP-07', CLS_RP, 'RidingOnMe_0x60_TheCarrierIsTheLogicEntity_NotTheOwner', 'prod-owner-hero', ['v60-c10a', 'v60-c10b'], 'owner = hero, LogicEntity = the plate'),
    ('RP-08', CLS_RP, 'RidingOnMe_0x60_TheRawIdIsTheWholeWord_AndTheHeroIsNoRawIdCandidate', 'prod', ['v60-c14a', 'v60-c14b', 'v60-c15'], 'EntityRefId 0x105 vs 5; hero ref -1 vs raw id 0'),
    ('RP-09', CLS_RP, 'RidingOnMe_0x60_TheLogicEntityIsTheHero_ProgramB', 'prod', ['v60-c17', 'v60-c04b'], 'owner = logic = hero'),
    ('RP-10', CLS_RP, 'RidingOnMe_0x60_ABareLogicEntityWithoutIdentityIsRiddenByNobody', 'anon', ['v60-a01'], 'LogicContextEntity null on the logic entity; RidingEntity null on the member'),
    ('RP-11', CLS_RP, 'RidingOnMe_0x60_TheOtherSearchGroupsPassThrough', 'prod', ['v60-c18a', 'v60-c18b', 'v60-c19'], 'types 0x86, 0x84'),
    ('RP-12', CLS_RP, 'RidingOnMe_0x60_TheLastOfTenSlotsCounts', 'prod', ['v60-c21'], 'N = 9'),
    # ---- 0x61
    ('RP-21', CLS_RP, 'RidingOn_0x61_TheLogicEntityRidesAMemberOfTheGroup_GivesOne', 'prod', ['v61-c01', 'v61-c12a', 'v61-c12b', 'v61-c15', 'v61-c16', 'v61-c06a', 'v61-c07a'], 'raw id, second/first member, self group, hero group, logic = hero'),
    ('RP-22', CLS_RP, 'RidingOn_0x61_TheLogicEntityRidesNoMemberOfTheGroup_GivesZero', 'prod', ['v61-c02', 'v61-c03', 'v61-c04', 'v61-c11', 'v61-c06b', 'v61-c07b', 'v61-c07c', 'v61-c12c'], 'rides nothing (null guard) / outside the group / foreign / empty group'),
    ('RP-23', CLS_RP, 'RidingOn_0x61_TheReverseRelationIsNotEnough', 'prod', ['v61-c05a', 'v60-c20', 'v61-c06c'], 'the member rides the logic entity: 0x61 gives 0 where 0x60 gives 1 (same world)'),
    ('RP-24', CLS_RP, 'RidingOn_0x61_TheCarrierStatusIsNotTested_TheReferenceStatusIs', 'prod', ['v61-c09a', 'v61-c09b', 'v61-c09c', 'v61-c10a', 'v61-c10b', 'v61-c10c'], 'carrier 4, 0, 1 found; reference 0, 4 empty, 1 found'),
    ('RP-25', CLS_RP, 'RidingOn_0x61_TheGroups82And83', 'prod', ['v61-c13a', 'v61-c13b'], 'the logic entity rides the hero: 0x82 yes, 0x83 no'),
    ('RP-26', CLS_RP, 'RidingOn_0x61_TheRawIdIsTheWholeWord', 'prod', ['v61-c14'], 'EntityRefId 0x100 vs 0'),
    ('RP-27', CLS_RP, 'RidingOn_0x61_ACandidateWithoutIdentityIsNeverACarrier', 'anon', ['v61-a01'], 'candidate LogicContextEntity null, logic RidingEntity null'),
    # ---- common
    ('RP-30', CLS_RP, 'RidingPredicates_AreImplementedNotSkipped_SizeTwo_AndWriteOnlyResult', 'prod', ['v60-c01', 'v60-c02', 'v61-c01', 'v61-c02'], 'trace Kind Implemented; CodeIndex 2; every field of every proxy and Parameters[0, 2..9] unchanged'),
    ('RP-31', CLS_RP, 'RidingPredicates_OnABareHost_GiveZero_AndDoNotThrow', 'bare-host', [], 'runner without world context (IsPorted shape): Result 0, no exception'),
]
for op, nm in ((0x3D, '3D'), (0x3C, '3C')):
    p = 'v%s-' % nm.lower()
    TESTS += [
        ('ZP-%s-01' % nm, CLS_ZP, 'InZone_0x%s_AnEntityInsideTheBox_GivesOne' % nm, 'prod', [p + 'c01', p + 'c20'], 'inside; several inside'),
        ('ZP-%s-02' % nm, CLS_ZP, 'InZone_0x%s_TheTwoCornersAndTheSixFacesAreInside' % nm, 'prod', [p + 'c02', p + 'c03'] + [p + 'c05' + x for x in 'abcdef'], 'inclusive bounds'),
        ('ZP-%s-03' % nm, CLS_ZP, 'InZone_0x%s_OneStepOutsideOnAnyAxis_GivesZero' % nm, 'prod', [p + 'c04' + x for x in 'abcdef'], 'x, y, z one below / one above'),
        ('ZP-%s-04' % nm, CLS_ZP, 'InZone_0x%s_AnInvertedBoxHoldsNobody' % nm, 'prod', [p + 'c06'], 'min above max'),
        ('ZP-%s-05' % nm, CLS_ZP, 'InZone_0x%s_TheOperandsAreUnsignedBytes' % nm, 'prod', [p + 'c07' + x for x in 'abcd'], 'box [0x80..0xFF]'),
        ('ZP-%s-06' % nm, CLS_ZP, 'InZone_0x%s_ATileOutsideTheByteRangeIsOutside' % nm, 'prod', [p + 'c08' + x for x in 'abc'] + [p + 'c09' + x for x in 'abc'] + [p + 'c10' + x for x in 'abc'], 'negative, 256..65536, INT_MAX, INT_MIN; box [0,0,0,0,0,0]'),
        ('ZP-%s-07' % nm, CLS_ZP, 'InZone_0x%s_OnlyLoadedNormalAndDeactivatedCount' % nm, 'prod', [p + 'c11' + x for x in '01234'] + [p + 'c21'], 'status 0..4; a Destroyed hole'),
        ('ZP-%s-08' % nm, CLS_ZP, 'InZone_0x%s_TheHeroCounts_InTheListAndAsPlayerEntityOnly' % nm, 'prod+ctx', [p + 'c12'], 'both montages'),
        ('ZP-%s-09' % nm, CLS_ZP, 'InZone_0x%s_TheFirstAndTheLastEntityCount' % nm, 'prod+ctx', [p + 'c13', p + 'c14', p + 'c22'], 'N = 4 and N = 9'),
        ('ZP-%s-10' % nm, CLS_ZP, 'InZone_0x%s_NobodyInside_GivesZero' % nm, 'prod', [p + 'c15'], ''),
        ('ZP-%s-11' % nm, CLS_ZP, 'InZone_0x%s_TheLogicEntityIsNotTheOnlyOneTested' % nm, 'prod', [p + 'c16', p + 'c17'], 'logic outside / inside'),
        ('ZP-%s-12' % nm, CLS_ZP, 'InZone_0x%s_TheAxesAreNotPermuted' % nm, 'prod', [p + 'c18' + x for x in 'abc'], 'degenerate box (10,20,30)'),
        ('ZP-%s-13' % nm, CLS_ZP, 'InZone_0x%s_TheRealBoxOf226B3' % nm, 'prod', [p + 'c19' + x for x in 'abcdef'], 'bytes 2B 2B 30 33 0C 0C'),
    ]
TESTS += [
    ('ZP-3C-20', CLS_ZP, 'OnTheGround_0x3C_TheThreeConditions', 'prod', ['v3c-d01', 'v3c-d02a', 'v3c-d02b', 'v3c-d03', 'v3c-d04a', 'v3c-d04b'], 'Collidable set, NoEntityCollision clear, no PlatformEntity'),
    ('ZP-3C-21', CLS_ZP, 'OnTheGround_0x3C_TheDecompilationsReadingIsNotTheBinarys', 'prod', ['v3c-d05'], 'Collidable clear + NoEntityCollision set: 0'),
    ('ZP-3C-22', CLS_ZP, 'OnTheGround_0x3C_OnlyBit0x80OfEachWordMatters', 'prod', ['v3c-d06a', 'v3c-d06b', 'v3c-d06c'], 'flags 0xFFFFFFFF/0xFFFFFF7F; anim 0x7F/0xFFFFFF7F'),
    ('ZP-3C-23', CLS_ZP, 'OnTheGround_0x3C_TheHeroIsTestedByHisOwnFlags', 'prod+ctx', ['v3c-d07a', 'v3c-d07b'], '0x3118C vs 0x3118C without 0x80'),
    ('ZP-3C-24', CLS_ZP, 'OnTheGround_0x3C_EveryCandidateIsTested_NotOnlyTheFirstInTheBox', 'prod', ['v3c-d08', 'v3c-d09'], ''),
    ('ZP-3C-25', CLS_ZP, 'ZoneOpcodes_0x3C_And_0x3D_AreNotTheSame', 'prod', ['v3d-d10', 'v3c-d02a'], 'same world: 0x3D 1, 0x3C 0'),
    ('ZP-30', CLS_ZP, 'ZonePredicates_AreImplementedNotSkipped_SizeSeven_AndWriteOnlyResult', 'prod', ['v3d-c01', 'v3d-c15', 'v3c-c01', 'v3c-c15'], 'trace Kind Implemented; CodeIndex 7; nothing but Result changed'),
    ('ZP-31', CLS_ZP, 'ZonePredicates_OnABareHost_GiveZero_AndDoNotThrow', 'bare-host', [], 'no world context, no hero'),
]


def main():
    doc = json.load(open(sys.argv[1], encoding='utf-8'))
    outdir = sys.argv[2]
    byid = {v['id']: v for v in doc['vectors']}
    # sanity: every vector id exists
    rows = []
    kills = defaultdict(list)       # mutant -> [test ids]
    for tid, cls, method, montage, vids, note in TESTS:
        for x in vids:
            assert x in byid, (tid, x)
        vs = [byid[x] for x in vids]
        ops = sorted({v['op'] for v in vs})
        expected = [(v['id'], v['binary']['result']) for v in vs]
        hist = sorted({r for _, r in expected})
        ms = []
        for mut in L.MUTANTS_60_61 + L.MUTANTS_3C_3D:
            killed = False
            for v in vs:
                if mut in L.MUTANTS_60_61 and v['op'] not in (0x60, 0x61):
                    continue
                if mut in L.MUTANTS_3C_3D and v['op'] not in (0x3C, 0x3D):
                    continue
                for hero_in_list in ((True,) if montage in ('prod', 'anon', 'prod-owner-hero') else (True, False)):
                    mt = L.Montage(v, hero_in_list=hero_in_list, anon=v.get('anon', ()))
                    if L.dll_case(mt, v['op'], v['code'], mut) != L.dll_case(mt, v['op'], v['code'], None):
                        killed = True
            if killed:
                ms.append(mut)
                kills[mut].append(tid)
        rows.append((tid, cls, method, ','.join('0x%02X' % o for o in ops), montage, ' '.join(vids) if vids else '-', ','.join(str(h) for h in hist) if hist else '-', note, ' '.join(ms)))
    with open(os.path.join(outdir, 'tests.tsv'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('id\tclass\tmethod\topcodes\tmontage\tvectors\texpected_Result_values\tnote\tmutants_killed\n')
        for r in rows:
            f.write('\t'.join(r) + '\n')
    with open(os.path.join(outdir, 'mutants.tsv'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('mutant\topcodes\tkilled_by_named_tests\n')
        for mut in L.MUTANTS_60_61 + L.MUTANTS_3C_3D:
            ops = '0x60/0x61' if mut in L.MUTANTS_60_61 else '0x3C/0x3D'
            f.write('%s\t%s\t%s\n' % (mut, ops, ' '.join(kills[mut]) if kills[mut] else 'NONE (equivalent: see equivalent-mutants.txt)' if mut == 'unsigned_tile' else ' '.join(kills[mut]) or 'NONE'))
    print(len(rows), 'tests;', sum(1 for m in L.MUTANTS_60_61 + L.MUTANTS_3C_3D if kills[m]), 'of', len(L.MUTANTS_60_61 + L.MUTANTS_3C_3D), 'mutants killed')
    for mut in L.MUTANTS_60_61 + L.MUTANTS_3C_3D:
        print('%-30s %s' % (mut, ' '.join(kills[mut][:8]) or 'NONE'))
    # vectors used by no test
    used = {x for _, _, _, _, vids, _ in TESTS for x in vids}
    cur = [v['id'] for v in doc['vectors'] if 'random' not in v['tags']]
    print('curated vectors not used by a named test:', [x for x in cur if x not in used])


main()
