"""Builds tests.tsv: each proposed test, the vectors it pins (expected values filled from the vectors file), the RED value of today, the mutants it kills."""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
V = {}
for line in open(os.path.join(HERE, '..', 'vectors', 'sm3-vectors.jsonl'), encoding='utf-8'):
    r = json.loads(line)
    V[r['id']] = r
F1 = 'Alundra.Tests/AlundraContactGroupAndForceZOpcodesTests.cs'
F3 = 'Alundra.Tests/AlundraCellCountOpcodeTests.cs'
T = []


def t(tid, f, name, montage, vids, kills='', note=''):
    for i in vids:
        assert i in V, i
    T.append((tid, f, name, montage, vids, kills, note))


M1 = 'M1 bare runner (Context + RunOneScriptCall), hero IsPlayer EntityRefId -1 Normal in SpawnedEntities and PlayerEntity, Result before 7'
# ---- A9
t('U-A9-01', F1, 'BlockedByGroup_0xA9_HeroGroup_ContactIsTheHero_GivesOne', M1, ['A9-01-hero-blocks-me', 'A9-22-stale-zero-then-true'], 'A9:swapped,A9:hero_ref,keep_stale_on_true')
t('U-A9-02', F1, 'BlockedByGroup_0xA9_NullContact_GivesZero_AndOverwritesAStaleOne', M1, ['A9-02-null-contact', 'A9-21-stale-one-then-false'], 'keep_stale_on_false')
t('U-A9-03', F1, 'BlockedByGroup_0xA9_ContactOutsideTheGroup_GivesZero', M1, ['A9-03-other-contact'])
t('U-A9-04', F1, 'BlockedByGroup_0xA9_RawId24_HeroIsTheReference_Real440B9Shape', M1, ['A9-04-rawid24-hero-is-reference', 'A9-05-rawid-no-entity'], 'A9:hero_ref')
t('U-A9-05', F1, 'BlockedByGroup_0xA9_RawId_ReferenceMustBeLoadedNormalOrDeactivated[Theory 1,3,4]', M1, ['A9-07-rawid-reference-loaded', 'A9-08-rawid-reference-deactivated', 'A9-06-rawid-reference-not-active'])
t('U-A9-06', F1, 'BlockedByGroup_0xA9_RawId_TheCandidateStatusIsNotTested', M1, ['A9-09-rawid-candidate-status-ignored'])
t('U-A9-07', F1, 'BlockedByGroup_0xA9_RawId_ContactIsTheSecondOfTwoEntities', M1, ['A9-10-rawid-second-of-two'], 'A9:first_only')
t('U-A9-08', F1, 'BlockedByGroup_0xA9_Type0_SelfAndNotSelf', M1, ['A9-11-self-group', 'A9-12-self-group-other'])
t('U-A9-09', F1, 'BlockedByGroup_0xA9_Type2_ContactStatus[Theory 4,1,3]', M1, ['A9-13-all-contact-destroyed', 'A9-14-all-contact-loaded', 'A9-15-all-contact-deactivated'])
t('U-A9-10', F1, 'BlockedByGroup_0xA9_Type3_ExcludesTheHero', M1, ['A9-16-all-but-hero-hero-contact', 'A9-17-all-but-hero-npc-contact'])
t('U-A9-11', F1, 'BlockedByGroup_0xA9_Type11_CarriedContact', M1, ['A9-18-type11-carried-contact', 'A9-19-type11-not-carried'])
t('U-A9-12', F1, 'BlockedByGroup_0xA9_OnlyTheHeroExists_Type3Empty', M1, ['A9-20-empty-group'])
t('U-A9-13', F1, 'BlockedByGroup_0xA9_HeroIsTheLogicEntity', M1, ['A9-23-hero-reference-self-contact', 'A9-24-hero-reference-group-hero-null'])
t('U-A9-14', F1, 'BlockedByGroup_0xA9_FreeSlotsBetweenAreAbsent', M1, ['A9-25-holes-in-the-slots'], '', 'the DLL list has no free slot; the vector keeps holes only to prove N does not matter')
# ---- AA
t('U-AA-01', F1, 'GroupBlockedByMe_0xAA_HeroContactIsMe_GivesOne', M1, ['AA-01-hero-blocked-by-me', 'AA-18-stale-zero-then-true'], 'AA:swapped,AA:hero_ref,keep_stale_on_true')
t('U-AA-02', F1, 'GroupBlockedByMe_0xAA_HeroNullContact_GivesZero_AndOverwritesAStaleOne', M1, ['AA-02-hero-null-contact', 'AA-17-stale-one-then-false'], 'keep_stale_on_false')
t('U-AA-03', F1, 'GroupBlockedByMe_0xAA_HeroContactIsAnotherEntity', M1, ['AA-03-hero-other-contact'])
t('U-AA-04', F1, 'GroupBlockedByMe_0xAA_Type2_AnyMemberCounts[Theory last,first,none]', M1, ['AA-04-all-last-only', 'AA-05-all-first-only', 'AA-06-all-none'], 'AA:first_only')
t('U-AA-05', F1, 'GroupBlockedByMe_0xAA_Type3_ExcludesTheHero', M1, ['AA-07-all-but-hero-excludes-hero'])
t('U-AA-06', F1, 'GroupBlockedByMe_0xAA_Type0_SelfAndNotSelf', M1, ['AA-08-self', 'AA-09-self-not'])
t('U-AA-07', F1, 'GroupBlockedByMe_0xAA_MemberStatus[Theory 4,3,1]', M1, ['AA-10-member-destroyed', 'AA-11-member-deactivated', 'AA-12-member-loaded'])
t('U-AA-08', F1, 'GroupBlockedByMe_0xAA_RawId_ReferenceAndCandidateStatus', M1, ['AA-13-rawid-member', 'AA-14-rawid-reference-not-active', 'AA-15-rawid-candidate-destroyed'])
t('U-AA-09', F1, 'GroupBlockedByMe_0xAA_Real440Shape_ThreeBlocks_TheHeroPushesOne', M1, ['AA-19-real-440-shape', 'AA-20-real-440-other-block'])
t('U-AA-10', F1, 'GroupBlockedByMe_0xAA_Type11_CarriedMember', M1, ['AA-21-type11-member'])
t('U-AA-11', F1, 'GroupBlockedByMe_0xAA_HeroIsTheLogicEntity', M1, ['AA-16-hero-reference-hero-group'])
# ---- B2
t('U-B2-01', F1, 'CompareGroups_0xB2_Real258_RawId3AndCarried_GivesOne_Stale0', M1, ['B2-01-real-258-carried', 'B2-16-stale-zero-then-true'], 'keep_stale_on_true')
t('U-B2-02', F1, 'CompareGroups_0xB2_Real258_NotCarried_GivesZero_Stale1', M1, ['B2-02-real-258-not-carried', 'B2-15-stale-one-then-false'], 'B2:g1_only,B2:second_uses_first_type,keep_stale_on_false')
t('U-B2-03', F1, 'CompareGroups_0xB2_CarriedButNotLoadedNormalOrDeactivated_GivesZero', M1, ['B2-03-carried-but-destroyed'], 'B2:g1_only')
t('U-B2-04', F1, 'CompareGroups_0xB2_AnotherEntityIsCarried_GivesZero', M1, ['B2-04-other-carried'], 'B2:self_intersect')
t('U-B2-05', F1, 'CompareGroups_0xB2_TwoEntitiesOfTheId_OneCarried_GivesOne', M1, ['B2-05-two-same-id-one-carried'], 'B2:all_not_any,B2:first_of_each')
t('U-B2-06', F1, 'CompareGroups_0xB2_FirstGroupEmpty_GivesZero_OverwritesAStaleOne', M1, ['B2-06-g1-empty', 'B2-17-g1-empty-stale-one'], 'keep_stale_on_false')
t('U-B2-07', F1, 'CompareGroups_0xB2_RawId_ReferenceNotActive_GivesZero', M1, ['B2-07-reference-not-active'])
t('U-B2-08', F1, 'CompareGroups_0xB2_TypeGroups_Overlap_Disjoint_HeroInAll', M1, ['B2-08-all-but-hero-and-all', 'B2-09-hero-vs-all-but-hero', 'B2-10-hero-vs-all'], 'B2:only_last_of_g2')
t('U-B2-09', F1, 'CompareGroups_0xB2_SecondGroupEmpty_GivesZero', M1, ['B2-11-g2-empty-nobody-carried'], 'B2:g1_only')
t('U-B2-10', F1, 'CompareGroups_0xB2_SelfGroups', M1, ['B2-12-self-self', 'B2-13-self-hero', 'B2-14-self-hero-same'])
t('U-B2-11', F1, 'CompareGroups_0xB2_SixtyFourSlots_TheLastSlotAndTheFirst', M1, ['B2-18-64-slots-last-only', 'B2-19-64-slots-g2-first-g1-all'], 'B2:only_last_of_g2,B2:first_of_each')
t('U-B2-12', F1, 'CompareGroups_0xB2_RidingGroups_5And6', M1, ['B2-20-types-5-6'])
t('U-B2-13', F1, 'CompareGroups_0xB2_PartialOverlapIsEnough', M1, ['B2-21-three-way-partial'], 'B2:all_not_any')
t('U-B2-14', F1, 'CompareGroups_0xB2_ComparesEntitiesNotRefIds', M1, ['B2-22-same-ref-id-different-entities', 'B2-23-hero-minus-one-vs-carried-minus-one'], 'B2:by_ref_id')
# ---- B4 B5 B6
for op, nm, ge in (('B4', 'CheckForceZPositive_0xB4', 'B4:ge,B4:unsigned,B4:short'), ('B5', 'CheckForceZNegative_0xB5', 'B5:le,B5:ge_label,B5:unsigned,B5:short'), ('B6', 'CheckForceZZero_0xB6', 'B6:ne_label,B6:short')):
    hero_ids = [i for i in V if i.startswith(op + '-hero-fz-')]
    self_ids = [i for i in V if i.startswith(op + '-self-fz-')]
    t('U-%s-01' % op, F1, nm + '_HeroForceZ_Table[Theory 12 values]', M1, hero_ids, ge, 'ForceZ in {0, 1, -1, 0x7FFFFFFF, int.MinValue, +-0x10000, +-0xFFFF, 0x8000, +-0x100}; the real sites are B5 81')
    t('U-%s-02' % op, F1, nm + '_LogicEntityForceZ_Table[Theory 12 values]', M1, self_ids, ge, 'the real sites are B5 80 and B6 80')
    t('U-%s-03' % op, F1, nm + '_OnlyTheFirstMatchDecides[Theory 5 orders]', M1, [i for i in V if i.startswith(op + '-first-match-') and 'skips' not in i], '%s:last,%s:any' % (op, op))
    t('U-%s-04' % op, F1, nm + '_FirstMatchSkipsAnInactiveEntity', M1, [op + '-first-match-skips-inactive'], '%s:last' % op)
    t('U-%s-05' % op, F1, nm + '_EmptyGroupGivesZero_RawIdNobodyHas', M1, [op + '-empty-group', op + '-rawid-no-entity'], 'B6:empty_true' if op == 'B6' else '', 'must not throw on an empty list')
    t('U-%s-06' % op, F1, nm + '_OverwritesAStaleResult', M1, [op + '-stale-one', op + '-stale-zero'], 'keep_stale_on_false,keep_stale_on_true')
# ---- B7
t('U-B7-01', F1, 'CheckTargetAnimation_0xB7_EqualAndDifferent', M1, ['B7-01-equal', 'B7-02-not-equal', 'B7-06-zero-default'])
t('U-B7-02', F1, 'CheckTargetAnimation_0xB7_ThirtyTwoBitWordAgainstAZeroExtendedByte', M1, ['B7-03-high-bits-differ', 'B7-04-all-ones', 'B7-05-byte-ff'], 'B7:low_byte,B7:sign_extended')
t('U-B7-03', F1, 'CheckTargetAnimation_0xB7_Operand1IsTheSearchTypeOperand2TheValue', M1, ['B7-07-operand-roles', 'B7-08-hero-group'], 'B7:swapped')
t('U-B7-04', F1, 'CheckTargetAnimation_0xB7_AnyMemberOfTheGroup[Theory last,first,none]', M1, ['B7-09-last-of-group', 'B7-10-first-of-group', 'B7-11-none-of-group'], 'B7:first_only')
t('U-B7-05', F1, 'CheckTargetAnimation_0xB7_EmptyGroupAndInactiveMember', M1, ['B7-12-empty-group', 'B7-13-inactive-member'])
t('U-B7-06', F1, 'CheckTargetAnimation_0xB7_OverwritesAStaleResult', M1, ['B7-14-stale-one', 'B7-15-stale-zero'], 'keep_stale_on_false,keep_stale_on_true')
# ---- shared / labels
t('U-ALL-01', F1, 'TheSevenOpcodes_ReturnTheirTableSize_AndLeaveEverythingButResultAlone', M1,
  ['A9-01-hero-blocks-me', 'AA-01-hero-blocked-by-me', 'B2-01-real-258-carried', 'B4-hero-fz-p1', 'B5-hero-fz-n1', 'B6-self-fz-z0', 'B7-01-equal'], 'size mutants (a case returning 1 for 2, ...)',
  'asserts CodeIndex == size, Parameters[1..9] == their start values, no spawn/destroy/flag/pad write')
t('U-ALL-02', F1, 'TheSearchRunsFromTheLogicEntity_NotFromTheHero', M1, ['A9-01-hero-blocks-me', 'AA-20-real-440-other-block'], 'A9:hero_ref,AA:hero_ref',
  'the logic entity of RunOneScriptCall is the entity passed; a 0x42-style retarget is not needed')
t('U-LBL-01', F1, 'TheLabelsOfTheSevenOpcodes_AreTheBinarys', 'none (EventOpcodeSizeTable.Entries)', [], 'label mutants',
  "A9 'Is blocked by entity of group', AA 'Is any entity of group blocked by me', B4 'Check ForceZ > 0', B5 'Check ForceZ < 0', B6 'Check ForceZ == 0', B2 'Compare entity groups' (unchanged), B7 'Check TargetAnimation' (unchanged)")
# ---- 9E (gated Q-sm-6)
M4 = 'M4 bare runner + a private fake ICellMutator reading a 52x60 walkability grid (gated Q-sm-6)'
t('U-9E-01', F3, 'CountCells_0x9E_OneCell_YesAndMinimum', M4, ['9E-01-one-cell-yes', '9E-02-one-cell-min-2'])
t('U-9E-02', F3, 'CountCells_0x9E_TheMaskIsBit1OfTheWalkabilityByte', M4, ['9E-03-bit-2-not-4', '9E-04-bit-1-and-0', '9E-05-ground-property-only', '9E-06-high-byte-bit1', '9E-07-both-bytes'], '9E:mask4,9E:high_byte')
t('U-9E-03', F3, 'CountCells_0x9E_CountEqualsMinimumGivesOne', M4, ['9E-08-rect-count-equals-min', '9E-09-rect-count-below-min'], '9E:gt')
t('U-9E-04', F3, 'CountCells_0x9E_AdvancesAlongXAndAcrossRows', M4, ['9E-10-advance-along-x', '9E-11-advance-along-x-first-zero', '9E-12-row-stride'], '9E:no_x_advance')
t('U-9E-05', F3, 'CountCells_0x9E_ZeroWidthOrHeight', M4, ['9E-13-w-zero', '9E-14-w-zero-min-1', '9E-15-h-zero', '9E-16-h-zero-min-1', '9E-17-min-zero-always-one'])
t('U-9E-06', F3, 'CountCells_0x9E_ColumnPast51RunsIntoTheNextRow', M4, ['9E-18-wrap-to-next-row', '9E-19-wrap-not-same-row', '9E-20-last-cell', '9E-21-full-width-row', '9E-22-full-width-row-minus-one'], '9E:no_wrap')
t('U-9E-07', F3, 'CountCells_0x9E_OperandOrder', M4, ['9E-24-x-y-order', '9E-25-w-h-order', '9E-23-min-255'], '9E:swap_xy,9E:swap_wh')
t('U-9E-08', F3, 'CountCells_0x9E_OverwritesAStaleResult', M4, ['9E-26-stale-one-false', '9E-27-stale-zero-true'], 'keep_stale_on_false,keep_stale_on_true')
t('U-9E-09', F3, 'CountCells_0x9E_NoCellReader_DegradesWithZeroAndALog', 'M4 with CellMutator null', [], '',
  'policy of Q-sm-6 (default: Result 0, once-logged degraded warning, size 6); a cell outside the 52x60 grid counts as 0 and logs once')

with open(os.path.join(HERE, '..', 'tests.tsv'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('id\tfile\ttest\tmontage\tvectors(expected result)\tred_today\tkills\tnote\n')
    for tid, fl, name, montage, vids, kills, note in T:
        exp = '; '.join('%s=%s' % (i, V[i]['expect']['result']) for i in vids)
        if vids:
            red = 'Result stays %s (UnknownSkipped, CodeIndex advances by the table size)' % ('/'.join(sorted({str(V[i]['red_today']['result']) for i in vids})))
        elif tid == 'U-LBL-01':
            red = "labels today: A9 and AA empty, B4 '< 0', B5 '>= 0', B6 '!= 0'"
        else:
            red = 'Result stays 7 (UnknownSkipped)'
        f.write('\t'.join([tid, fl, name, montage, exp, red, kills, note]) + '\n')
print(len(T), 'tests,', sum(len(x[4]) for x in T), 'vector references,', len({i for x in T for i in x[4]}), 'distinct hand-written vectors referenced')
