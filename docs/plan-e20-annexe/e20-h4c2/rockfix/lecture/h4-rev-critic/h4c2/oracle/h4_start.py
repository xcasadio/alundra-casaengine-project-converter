"""h4_start.py - model of TryUseItem (0x8002ED64) -> item start (0x8003499C) -> tome start (0x800348E0), written from the disassembly
(lst/tryuseitem.txt, lst/start_8003499C.txt, lst/tome_start_800348E0.txt), compared with the REAL code on random vectors (vec in main()).

Helper results (current item id, owned count, weapon id) come from the REAL helpers of the binary (0x8004E0F8, 0x8004E030) run in the same machine
before the call, so that the model only has to decide with them; the helpers are already ported in the DLL.

usage: python -B h4_start.py [N] [seed]   -> prints the report; exit code 0 iff no mismatch."""
import random
import sys
from collections import Counter
import spellworld as S
import world as W
from machine import s32, M32

HERO = 0x80127D30
INUSE, COUNTER, PHASE = 0x80127164, 0x8012715C, 0x80127160
CTRL = 0x800DC4B8
EDGE = 0x80126F2A
VOICE_TABLE = 0x80023365          # byte[item + 0x80023365]


def model_try_use_item(st):
    """st: dict(edge, locked, cur_item (value returned by the helper = old +0xC, -1 when not owned), inuse, carried, tanim, weapon, mp, mpmax, voice_bytes)
    returns (result, sounds, voices, mp_after, tanim_after, inuse_after, counter_after, phase_after)"""
    sounds, voices = [], []
    mp, tanim, inuse, counter, phase = st['mp'], st['tanim'], st['inuse'], st['counter'], st['phase']

    def done(res):
        return res, sounds, voices, mp, tanim, inuse, counter, phase

    if (st['edge'] & 0x20) == 0:                       # 0x8002ED70-0x8002ED74 then 0x8002EDA8
        return done(1)
    if st['locked'] != 0:                              # 0x8002ED88: [0x800DC4B8] != 0
        sounds.append(3)
        return done(1)
    # --- 0x8003499C
    item = st['cur_item'] & M32                        # sltiu v0, s0, 0x62 : unsigned (-1 fails)
    if item >= 0x62:
        sounds.append(3)
        return done(1)
    if inuse != 0 and inuse != item:                   # 0x800349C4-0x800349CC
        sounds.append(3)
        return done(1)
    idx = item - 0x1F
    if idx < 0 or idx >= 0x14:                         # sltiu v0, v1, 0x14 (unsigned)
        sounds.append(3)
        return done(1)
    if not (0x2B <= item <= 0x32):
        # other items are not modelled here (they are other slices); the model only covers the tomes and the failures above
        return None
    # --- tome start 0x800348E0
    if st['carried'] == 0 and (tanim & M32) < 2 and inuse == 0:
        if st['weapon'] != 7:                          # 0x8004E030 == 7 -> skip the MP test and the cost
            if mp == 0:
                sounds.append(3)
                return done(1)
            newmp = mp - 1                             # 0x8004DF3C DecreaseMp(1) -> 0x8004DEBC SetMp(mp - 1): clamp to [0, MpMax]
            mp = st['mpmax'] if st['mpmax'] < newmp else (newmp if newmp >= 0 else 0)
        voices.append(st['voice_bytes'][item - 0x2B])  # 0x8005A9E0(byte[0x80023365 + item]) = 0,1,6,6,2,3,4,5 for 0x2B..0x32
        tanim = 0x32
        res = 0
    else:
        sounds.append(3)
        return done(1)
    if res == 0:                                       # 0x80034A88-0x80034A9C
        inuse = item
        counter = 0
        phase = 0
    return done(res)


def mk_machine(seed):
    m, STATS, placed = S.make(item=None, indices=(1,))
    # voice start: journalled stub (real code streams from the CD)
    def voice(mm):
        mm.journal.append((mm.tick_no, 'voice', mm.r[4] & M32))
        mm.r[2] = 0
    m.hooks[0x8005A9E0] = voice
    return m, STATS


def real_call(m, STATS, st):
    """write the state, call the REAL TryUseItem, read the observable effects"""
    m.poke16(EDGE, st['edge'])
    m.poke(CTRL, st['locked'])
    m.poke(INUSE, st['inuse'])
    m.poke(COUNTER, st['counter'])
    m.poke(PHASE, st['phase'])
    m.poke(HERO + 0x2C, st['carried'])
    m.poke(HERO + 0x88, st['tanim'])
    m.poke16(STATS + 4, st['mp'])
    m.poke16(STATS + 6, st['mpmax'])
    m.journal.clear()
    res = s32(m.call(0x8002ED64, limit=2_000_000))
    sounds = [j[2] for j in m.journal if j[1] == 'sound']
    voices = [j[2] for j in m.journal if j[1] == 'voice']
    return (res, sounds, voices, m.peek(STATS + 4) & 0xFFFF, s32(m.peek(HERO + 0x88)), s32(m.peek(INUSE)), s32(m.peek(COUNTER)), s32(m.peek(PHASE)))


def main(N=3000, seed=20261017, mut=None):
    rnd = random.Random(seed)
    m, STATS = mk_machine(seed)
    if mut:
        mut(m)
    voice_bytes = [m.rb(VOICE_TABLE + 0x2B + k) for k in range(8)]
    cov0 = set()
    mism = 0
    cnt = Counter()
    unmodelled = 0
    items_pool = list(range(0x2B, 0x33)) * 4 + list(range(0x1F, 0x2B)) + [0x00, 0x61, 0x62, 0x63, 0x7F, 0xFFFF, 0x50, 0x5A]
    first_mismatch = []
    m.cov.clear()
    for n in range(N):
        cur = rnd.choice(items_pool)
        owned = rnd.choice([0, 1, 1, 1, 3])
        # inventory: the REAL table [0x8017631C] + 4*id + 2 is the count (halfword); out of range ids are handled by the real helper
        wslot = rnd.choice([-1, 1, 2, 3, 4, 5, 6, 6, 5, 2])
        wowned = rnd.choice([0, 1, 1])
        # reset inventory counts of the items under test; the weapon: staff item 7 is in inventory slot (properties[7*5]) -> choose by the real helper
        tab = m.peek(0x8017631C)
        if 0 <= (cur if cur < 0x8000 else cur - 0x10000) < 0x63:
            cid = cur if cur < 0x8000 else cur - 0x10000
            m.poke16(tab + 4 * cid + 2, owned)
        # staff ownership toggled
        m.poke16(tab + 4 * 7 + 2, wowned)
        m.poke16(STATS + 0xC, cur)
        if wslot == -1:
            m.poke16(STATS + 0xA, 0xFFFF)
        else:
            m.poke16(STATS + 0xA, wslot)
        # the helper results as the binary computes them (real code, first call: side effects of 0x8004E0F8 on +0xC are part of the state)
        stats_c_before = m.peek(STATS + 0xC) & 0xFFFF
        weapon = s32(m.call(0x8004E030))
        # the old item id the start function will see: replay the real helper on a copy of the state? it has a side effect on +0xC, so ask it once and restore
        cur_item = s32(m.call(0x8004E0F8))
        m.poke16(STATS + 0xC, stats_c_before)
        ready = rnd.random() < 0.5
        if ready:                                   # bias: a cast that would start unless one input is wrong (each field off with probability 1/6)
            cur = rnd.choice(list(range(0x2B, 0x33)))
            m.poke16(tab + 4 * cur + 2, 1 if rnd.random() < 0.85 else 0)
            m.poke16(STATS + 0xC, cur)
            stats_c_before = m.peek(STATS + 0xC) & 0xFFFF
            weapon = s32(m.call(0x8004E030))
            cur_item = s32(m.call(0x8004E0F8))
            m.poke16(STATS + 0xC, stats_c_before)
        st = dict(edge=rnd.choice([0x20, 0x20, 0x20, 0x00, 0x10, 0x30, 0x40]) if not ready else (0x20 if rnd.random() < 0.9 else 0x10),
                  locked=rnd.choice([0, 0, 0, 0, 0x34, 0x20, 1]) if not ready else (0 if rnd.random() < 0.9 else 0x34),
                  cur_item=cur_item, inuse=rnd.choice([0, 0, 0, 0, 0, cur_item & M32 if cur_item >= 0 else 0, 0x1F, 0x2C, 0x2B]) if not ready else (0 if rnd.random() < 0.9 else rnd.choice([0x2B, 0x1F, 0x2C])),
                  carried=rnd.choice([0, 0, 0, 0, HERO + 0x294]) if not ready else (0 if rnd.random() < 0.9 else HERO + 0x294),
                  tanim=rnd.choice([0, 1, 1, 0, 2, 5, 0x2D, 0x32, 0x33, -1, 0x10]) if not ready else (rnd.choice([0, 1]) if rnd.random() < 0.9 else rnd.choice([2, 0x2D, -1, 0x32])),
                  weapon=weapon, mp=rnd.choice([0, 1, 2, 3, 4, 4]), mpmax=rnd.choice([0, 1, 2, 4, 4]), counter=rnd.choice([0, 7, 99]), phase=rnd.choice([0, 1, 2]),
                  voice_bytes=voice_bytes)
        if st['mp'] > st['mpmax']:
            st['mp'] = st['mpmax']
        want = model_try_use_item(st)
        got = real_call(m, STATS, st)
        if want is None:
            unmodelled += 1
            cnt['unmodelled (not a tome / fails modelled earlier)'] += 1
            continue
        key = 'result=%d' % want[0]
        if 0x2B <= st['cur_item'] <= 0x32 and want[0] == 0:
            key += ' cast started (weapon %s)' % ('staff' if st['weapon'] == 7 else 'other')
        cnt[key] += 1
        if got != want:
            mism += 1
            if len(first_mismatch) < 5:
                first_mismatch.append((st, want, got))
    covered = set(a for a in m.cov if 0x8002ED64 <= a < 0x8002EDBC or 0x8003499C <= a < 0x80034AC0 or 0x800348E0 <= a < 0x8003499C)
    return mism, cnt, unmodelled, first_mismatch, covered


if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20261017
    mism, cnt, unm, fm, cov = main(N, seed)
    print('vectors', N, 'seed', seed, 'mismatches', mism, 'unmodelled', unm)
    for k, v in sorted(cnt.items()):
        print('  ', v, k)
    for st, want, got in fm:
        print('MISMATCH', st, '\n  model', want, '\n  real ', got)
    # coverage of the three functions
    allw = set(range(0x8002ED64, 0x8002EDBC, 4)) | set(range(0x800348E0, 0x8003499C, 4)) | set(range(0x8003499C, 0x80034A7C, 4)) | set(range(0x80034A7C, 0x80034AC0, 4))
    miss = sorted(allw - cov)
    print('instructions of the three functions executed: %d/%d' % (len(allw) - len(miss), len(allw)))
    print('never executed:', ' '.join('%08x' % a for a in miss))
    sys.exit(1 if mism else 0)
