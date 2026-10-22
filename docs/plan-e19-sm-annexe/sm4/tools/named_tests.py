"""E19.sm4: the VALUE AUDIT of the named unit tests. Each named test that is a pure handler run (start state + instruction bytes -> expected
fields) is executed on the REAL handler of the binary; the value the test will assert must be the value the binary produced, and the value the
DLL gives TODAY (the skip by size: nothing written) must differ for a red test. A disagreement stops the audit.

Usage: python -B named_tests.py OUT.json
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import temu  # noqa: E402
import sm4model as S  # noqa: E402
from gen_vectors import hero, npc, world, run_case  # noqa: E402


def fields_after(r, w):
    m = r['m']
    out = {'slot': {}, 'stats': {}, 'pad': {}}
    for i in range(len(w['slots'])):
        for off, (name, _, kind) in S.SLOT_FIELDS.items():
            word = struct.unpack_from('<I', m.ram, (S.ent_addr(i) + off) & 0x1FFFFFFF)[0]
            out['slot'][(i, name)] = (S.s32(word) if kind == 's32' else S.slot_of(word) if kind == 'slotref' else word != 0 if kind == 'ptrbool' else word)
    for off, (name, _) in S.STATS_FIELDS.items():
        out['stats'][name] = struct.unpack_from('<h', m.ram, (S.PSTATS + off) & 0x1FFFFFFF)[0]
    for off, (name, _) in S.PAD_FIELDS.items():
        out['pad'][name] = struct.unpack_from('<H', m.ram, (S.PAD + off) & 0x1FFFFFFF)[0]
    return out


def initial(w):
    out = {'slot': {}, 'stats': {}, 'pad': {}}
    for i, s in enumerate(w['slots']):
        for name in (v[0] for v in S.SLOT_FIELDS.values()):
            out['slot'][(i, name)] = s[name]
    for k, v in w['stats'].items():
        out['stats'][k] = S.s16(v)
    for k in ('hold', 'pressed', 'released', 'interval'):
        out['pad'][k] = (w['pad'] or {}).get(k)
    return out


T = []


def t(tid, cls, name, rule, instr, w, logic, owner, expect, what, policy=False):
    T.append(dict(id=tid, cls=cls, name=name, rule=rule, instr=list(instr), world=w, logic=logic, owner=owner, expect=expect, what=what, policy=policy))


ES, LM, PW = 'EntityStateOpcodesTests', 'LifeAndMagicOpcodesTests', 'PadWriteOpcodeTests'

# ---- 0x72 -----------------------------------------------------------------------------------------------------------------------------
t('U72-1', ES, 'SetLastTarget_0x72_CopiesTheTargetOntoLastTarget', 'R1', [0x72], world([hero(targetAnimId=5, targetDir=9, lastTargetAnimId=1, lastTargetDir=2), npc(1)]), 0, 0,
  {'slot': {(0, 'lastTargetAnimId'): 5, (0, 'lastTargetDir'): 9, (0, 'targetAnimId'): 5, (0, 'targetDir'): 9}}, 'logic = owner = the entity')
t('U72-2', ES, 'SetLastTarget_0x72_ActsOnTheLogicEntity_NotOnTheOwner', 'R1', [0x72],
  world([hero(targetAnimId=7, targetDir=7, lastTargetAnimId=0, lastTargetDir=0), npc(1, targetAnimId=3, targetDir=21, lastTargetAnimId=99, lastTargetDir=98)]), 1, 0,
  {'slot': {(1, 'lastTargetAnimId'): 3, (1, 'lastTargetDir'): 21, (0, 'lastTargetAnimId'): 0, (0, 'lastTargetDir'): 0}}, 'owner = hero, logic = the NPC')
t('U72-3', ES, 'SetLastTarget_0x72_CopiesMaxUnsignedValuesVerbatim', 'R1', [0x72],
  world([hero(), npc(1, targetAnimId=0xFFFFFFFF, targetDir=0xFFFFFFFF)]), 1, 1,
  {'slot': {(1, 'lastTargetAnimId'): 0xFFFFFFFF, (1, 'lastTargetDir'): 0xFFFFFFFF}}, 'uint values')
t('U72-4', ES, 'SetLastTarget_0x72_ZeroTargetOverwritesANonZeroLast', 'R1', [0x72],
  world([hero(), npc(1, targetAnimId=0, targetDir=0, lastTargetAnimId=0x41, lastTargetDir=0x1F)]), 1, 1,
  {'slot': {(1, 'lastTargetAnimId'): 0, (1, 'lastTargetDir'): 0}}, 'zero is a value')
t('U72-5', ES, 'SetLastTarget_0x72_HasNoStatusGuard', 'R1', [0x72],
  world([hero(), npc(1, status=4, targetAnimId=8, targetDir=2), npc(2, status=0, refId=-1, targetAnimId=8, targetDir=2)]), 1, 1,
  {'slot': {(1, 'lastTargetAnimId'): 8, (1, 'lastTargetDir'): 2}}, 'FlagToDestroy logic entity')

# ---- 0x88 -----------------------------------------------------------------------------------------------------------------------------
t('U88-1', ES, 'SetStatus_0x88_Self_Index0_BecomesNormal', 'R2', [0x88, 0x80, 0], world([hero(), npc(1, status=3)]), 1, 0, {'slot': {(1, 'status'): 2, (0, 'status'): 2}}, 'Deactivated -> Normal')
t('U88-2', ES, 'SetStatus_0x88_Self_Index1_BecomesDeactivated', 'R2', [0x88, 0x80, 1], world([hero(), npc(1, status=2)]), 1, 0, {'slot': {(1, 'status'): 3}}, 'Normal -> Deactivated')
t('U88-3', ES, 'SetStatus_0x88_Self_Index2_BecomesFlagToDestroy_WithoutDestroyEntity', 'R2', [0x88, 0x80, 2], world([hero(), npc(1, status=3)]), 1, 0, {'slot': {(1, 'status'): 4}}, 'Deactivated -> FlagToDestroy')
t('U88-4', ES, 'SetStatus_0x88_AllActive_OnlyStatuses1To3Change', 'R2', [0x88, 0x82, 0],
  world([hero(status=3), npc(1, status=1), npc(2, status=3), npc(3, status=4), npc(4, status=0, refId=-1), npc(5, status=5), npc(6, status=2)]), 1, 0,
  {'slot': {(0, 'status'): 2, (1, 'status'): 2, (2, 'status'): 2, (3, 'status'): 4, (4, 'status'): 0, (5, 'status'): 5, (6, 'status'): 2}}, 'group 0x82 includes the hero')
t('U88-5', ES, 'SetStatus_0x88_HeroGroup_Index2_SetsTheHeroToo', 'R2', [0x88, 0x81, 2], world([hero(), npc(1)]), 1, 1, {'slot': {(0, 'status'): 4, (1, 'status'): 2}}, 'no guard on the hero')
t('U88-6', ES, 'SetStatus_0x88_EmptyGroup_WritesNothing', 'R2', [0x88, 0x8B, 2], world([hero(), npc(1), npc(2)]), 1, 0, {'slot': {(0, 'status'): 2, (1, 'status'): 2, (2, 'status'): 2}}, 'green guard')
t('U88-7', ES, 'SetStatus_0x88_RawId_CandidateStatusIsNotTested', 'R2', [0x88, 0x07, 0],
  world([hero(), npc(1, refId=7, status=4), npc(2, refId=7, status=0), npc(3, refId=6), npc(4, refId=7, status=2)]), 3, 0,
  {'slot': {(1, 'status'): 2, (2, 'status'): 2, (3, 'status'): 2, (4, 'status'): 2}}, 'corpse revived: no candidate guard')
t('U88-8', ES, 'SetStatus_0x88_RawId_InactiveReferenceFindsNothing', 'R2', [0x88, 0x07, 0],
  world([hero(), npc(1, refId=7, status=2), npc(2, status=4, refId=2, hp=1)]), 2, 0, {'slot': {(1, 'status'): 2, (2, 'status'): 4}}, 'reference status 4 -> empty (green guard)')

t('U88-9', ES, 'SetStatus_0x88_IndexBeyondTheTable_WritesNothing_AndWarnsOnce', 'R2', [0x88, 0x80, 3], world([hero(), npc(1, status=3)]), 1, 0, {'slot': {(1, 'status'): 3, (0, 'status'): 2}},
  'POLICY (Q-sm-6 default, precedent 0x40/0x41 D-E19-22): the binary stores a word read past its 3-word table; the DLL writes nothing and warns once. NOT audited against the binary', policy=True)

# ---- 0x96 -----------------------------------------------------------------------------------------------------------------------------
STATS = dict(hp=33, hpMax=44)
t('U96-1', LM, 'RestoreLife_0x96_HeroAtTheSiteShape_FillsToHpMax', 'R3', [0x96, 0x81, 0xFF], world([hero(hp=10, hpMax=50), npc(1)], stats=STATS), 1, 0,
  {'slot': {(0, 'hp'): 50}, 'stats': {'hp': 33, 'hpMax': 44}}, '31 sites shape: byte 1 = 0x81 (hero, +129), byte 2 = 0xFF ignored')
t('U96-2', LM, 'RestoreLife_0x96_SaveRoomShape_ReferenceIsTheLogicEntity', 'R3', [0x96, 0x80, 0x32], world([hero(hp=1, hpMax=20), npc(1)], stats=STATS), 0, 0,
  {'slot': {(0, 'hp'): 20}}, 'byte 1 = 0x80 (self, +128)')
t('U96-3', LM, 'RestoreLife_0x96_TheSecondByteIsNeverRead', 'R3', [0x96, 0x80, 0x00], world([hero(hp=1, hpMax=20), npc(1)], stats=STATS), 0, 0, {'slot': {(0, 'hp'): 20}}, 'same result with byte 2 = 0')
t('U96-4', LM, 'RestoreLife_0x96_RawId_SumBelowHpMax_IsNotClamped', 'R3', [0x96, 0x14, 0x32],
  world([hero(), npc(1, refId=0x14, hp=3, hpMax=50), npc(2, refId=0x14, hp=45, hpMax=50), npc(3, refId=5, hp=1, hpMax=50)]), 3, 0,
  {'slot': {(1, 'hp'): 23, (2, 'hp'): 50, (3, 'hp'): 1}}, 'amount = the raw id 20')
t('U96-5', LM, 'RestoreLife_0x96_SumEqualToHpMax_IsWritten', 'R3', [0x96, 0x14, 0], world([hero(), npc(1, refId=0x14, hp=30, hpMax=50)]), 1, 0, {'slot': {(1, 'hp'): 50}}, 'exact')
t('U96-6', LM, 'RestoreLife_0x96_HpAboveHpMax_IsLoweredToHpMax', 'R3', [0x96, 0x81, 0], world([hero(hp=120, hpMax=10)]), 0, 0, {'slot': {(0, 'hp'): 10}}, 'no ceiling test')
t('U96-7', LM, 'RestoreLife_0x96_NegativeHp_HasNoFloor', 'R3', [0x96, 0x14, 0], world([hero(), npc(1, refId=0x14, hp=-200, hpMax=50)]), 1, 0, {'slot': {(1, 'hp'): -180}}, 'stays negative')
t('U96-8', LM, 'RestoreLife_0x96_ThirtyTwoBitWrap_IsWrittenAsIs', 'R3', [0x96, 0x81, 0], world([hero(hp=0x7FFFFFFF, hpMax=50)]), 0, 0, {'slot': {(0, 'hp'): -2147483520}}, 'unchecked int arithmetic')
t('U96-9', LM, 'RestoreLife_0x96_NegativeHpMax_IsTheResult', 'R3', [0x96, 0x81, 0], world([hero(hp=5, hpMax=-1)]), 0, 0, {'slot': {(0, 'hp'): -1}}, 'HpMax < sum')
t('U96-10', LM, 'RestoreLife_0x96_ACorpseFoundByRawId_IsHealed', 'R3', [0x96, 0x09, 0], world([hero(), npc(1, refId=9, status=4, hp=0, hpMax=30)]), 0, 0, {'slot': {(1, 'hp'): 9}}, 'no life test')
t('U96-11', LM, 'RestoreLife_0x96_AllActiveGroup_EachUsesItsOwnHpMax', 'R3', [0x96, 0x82, 0],
  world([hero(hp=1, hpMax=10), npc(1, hp=5, hpMax=200), npc(2, status=4, hp=1, hpMax=10), npc(3, status=3, hp=130, hpMax=140)]), 1, 0,
  {'slot': {(0, 'hp'): 10, (1, 'hp'): 135, (2, 'hp'): 1, (3, 'hp'): 140}}, 'amount 130')
t('U96-12', LM, 'RestoreLife_0x96_DoesNotTouchThePlayerStats', 'R3', [0x96, 0x81, 0], world([hero(hp=2, hpMax=30)], stats=dict(hp=7, hpMax=9)), 0, 0,
  {'slot': {(0, 'hp'): 30}, 'stats': {'hp': 7, 'hpMax': 9}}, 'the mirror is the epilogue of MovePlayer, not the opcode')
t('U96-13', LM, 'RestoreLife_0x96_EmptyGroup_WritesNothing', 'R3', [0x96, 0x8B, 0], world([hero(hp=1, hpMax=50), npc(1, hp=1, hpMax=50)]), 1, 0, {'slot': {(0, 'hp'): 1, (1, 'hp'): 1}}, 'green guard')


# ---- 0x97 -----------------------------------------------------------------------------------------------------------------------------
def mp(tid, name, b1, expect_mp, **stats):
    base = dict(hp=10, hpMax=10, money=1234)
    base.update(stats)
    t(tid, LM, name, 'R4', [0x97, b1], world([hero(), npc(1)], stats=base), 0, 0, {'stats': dict(mp=expect_mp, money=base['money'], hp=10, hpMax=10)}, 'stats Mp/MpMax/Money')


mp('U97-1', 'AddMagic_0x97_SaveRoomShape_FillsToMpMax_MoneyUntouched', 0x32, 4, mp=0, mpMax=4, money=777)
mp('U97-2', 'AddMagic_0x97_BelowMax_IsNotClamped', 1, 2, mp=1, mpMax=4)
mp('U97-3', 'AddMagic_0x97_FullGaugeStaysFull', 0, 4, mp=4, mpMax=4)
mp('U97-4', 'AddMagic_0x97_MpMaxZero_StaysZero', 0x0A, 0, mp=0, mpMax=0)
mp('U97-5', 'AddMagic_0x97_MpAboveMpMax_IsLoweredToMpMax', 0, 2, mp=4, mpMax=2)
mp('U97-6', 'AddMagic_0x97_NegativeMp_FloorZero', 0, 0, mp=-1, mpMax=4)
mp('U97-7', 'AddMagic_0x97_NegativeMpSumZero_IsZero', 1, 0, mp=-1, mpMax=4)
mp('U97-8', 'AddMagic_0x97_NegativeMpSumNegative_FloorZero', 1, 0, mp=-3, mpMax=4)
mp('U97-9', 'AddMagic_0x97_TheSumIsNotWrappedTo16Bits', 0xFF, 4, mp=0x7FFF, mpMax=4)
mp('U97-10', 'AddMagic_0x97_MinInt16Mp_FloorZero', 0, 0, mp=-32768, mpMax=4)
mp('U97-11', 'AddMagic_0x97_NegativeMpMax_IsTheResult', 0, -1, mp=0, mpMax=-1)
mp('U97-12', 'AddMagic_0x97_InnShape_Plus10', 0x0A, 4, mp=3, mpMax=4)

# ---- 0xB3 -----------------------------------------------------------------------------------------------------------------------------
ZERO = dict(hold=0, pressed=0, released=0, interval=0)
t('UB3-1', PW, 'SetPadButtons_0xB3_WritesTheFourWordsLittleEndian', 'R5', [0xB3, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE], world([hero(), npc(1)], pad=ZERO), 0, 0,
  {'pad': dict(hold=0x1234, pressed=0x5678, released=0x9ABC, interval=0xDEF0)}, 'four distinct words')
t('UB3-2', PW, 'SetPadButtons_0xB3_TheRealSiteShape_HoldsUp', 'R5', [0xB3, 0x00, 0x10, 0, 0, 0, 0, 0, 0], world([hero(), npc(1)], pad=ZERO), 0, 0,
  {'pad': dict(hold=0x1000, pressed=0, released=0, interval=0)}, '161 B[1] @54')
t('UB3-3', PW, 'SetPadButtons_0xB3_OverwritesNeverOrs', 'R5', [0xB3] + [0] * 8, world([hero(), npc(1)], pad=dict(hold=0x1234, pressed=0x00F0, released=0x8001, interval=0xFFFF)), 0, 0,
  {'pad': dict(hold=0, pressed=0, released=0, interval=0)}, 'previous words dropped')
t('UB3-4', PW, 'SetPadButtons_0xB3_AllFFFF', 'R5', [0xB3] + [0xFF] * 8, world([hero(), npc(1)], pad=ZERO), 0, 0, {'pad': dict(hold=0xFFFF, pressed=0xFFFF, released=0xFFFF, interval=0xFFFF)}, 'no mask')
t('UB3-5', PW, 'SetPadButtons_0xB3_OwnerAndLogicAreIrrelevant', 'R5', [0xB3, 0x00, 0x10, 0, 0, 0, 0, 0, 0], world([hero(), npc(1)], pad=ZERO), 1, 0,
  {'pad': dict(hold=0x1000, pressed=0, released=0, interval=0)}, 'logic = NPC')


def main():
    out_path = sys.argv[1]
    m = temu.make_machine()
    rows = []
    for tc in T:
        before = initial(tc['world'])
        if tc['policy']:
            rows.append(dict(id=tc['id'], cls=tc['cls'], name=tc['name'], rule=tc['rule'], instruction=tc['instr'] + [0xFF], logicSlot=tc['logic'], ownerSlot=tc['owner'], what=tc['what'],
                             binaryAudit='not audited (policy)', redToday=False, redValues=[], start=dict(slots=tc['world']['slots'], stats=tc['world']['stats'], pad=tc['world']['pad']),
                             expected={c: {('%s.%s' % (k[0], k[1]) if isinstance(k, tuple) else k): v for k, v in d.items()} for c, d in tc['expect'].items()}))
            continue
        r = run_case(m, tc['instr'][0], bytes(tc['instr']), tc['world'], tc['logic'], tc['owner'])
        after = fields_after(r, tc['world'])
        bad = []
        red = []
        for cat in ('slot', 'stats', 'pad'):
            for key, val in tc['expect'].get(cat, {}).items():
                got = after[cat][key]
                if cat == 'slot':
                    kind = S.SLOT_FIELDS[S.SLOT_OFF_BY_NAME[key[1]]][2]
                    want = S.s32(val) if kind == 's32' else val & S.M32 if kind == 'u32' else val
                else:
                    want = val
                if got != want:
                    bad.append((cat, key, 'binary gave', got, 'test says', want))
                if before[cat][key] != want:
                    red.append('%s.%s today %s, expected %s' % (cat, key, before[cat][key], want))
        if bad:
            raise SystemExit('VALUE AUDIT FAILED %s: %s' % (tc['id'], bad))
        rows.append(dict(id=tc['id'], cls=tc['cls'], name=tc['name'], rule=tc['rule'], instruction=tc['instr'] + [0xFF], logicSlot=tc['logic'], ownerSlot=tc['owner'],
                         start=dict(slots=tc['world']['slots'], stats=tc['world']['stats'], pad=tc['world']['pad']), what=tc['what'],
                         expected={c: {('%s.%s' % (k[0], k[1]) if isinstance(k, tuple) else k): v for k, v in d.items()} for c, d in tc['expect'].items()},
                         returned=r['v0'], binaryAudit='ok',
                         redToday=bool(red), redValues=red))
    json.dump(rows, open(out_path, 'w', encoding='utf-8', newline='\n'), indent=1, sort_keys=True)
    print('%d named unit tests audited against the binary: all values equal; %d red today, %d green guards' %
          (len(rows), sum(1 for x in rows if x['redToday']), sum(1 for x in rows if not x['redToday'])))
    for x in rows:
        if not x['redToday']:
            print('  green today:', x['id'], x['name'])


if __name__ == '__main__':
    main()
