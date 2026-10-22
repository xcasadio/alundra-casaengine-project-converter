"""Critic's model of the DLL AFTER the proposed rules R1..R5, in DLL terms, written from the C# I read
(EntitySearchService.cs at HEAD, EventProgramRunner.cs, PlayerManager.SetPlayerMp) and from the report's rule text.
It consumes the preparation's JSON vectors (world described by slot indices) and compares the resulting final state with the
vector's `expect` (which the critic's own MIPS interpreter reproduced from the binary).  A disagreement means the rule, as stated
in DLL terms, differs from the binary on that state."""
import json, os, sys

I32 = lambda x: ((x + 2**31) % 2**32) - 2**31
U32 = lambda x: x % 2**32
NORMAL, DEACT, FLAGD = 2, 3, 4
STATUS_TABLE = [2, 3, 4]
COLLIDABLE = 0x80  # EntityFlags.Collidable (checked separately against the DLL source)


class P:  # proxy
    pass


def world_from(v):
    w = v['world']
    prox = []
    for i, s in enumerate(w['slots']):
        p = P()
        p.i = i
        p.status = s['status']; p.hp = s['hp']; p.hpMax = s['hpMax']; p.refId = s['refId']
        p.flags = s['flags']; p.animFlags = s['animFlags']; p.platform = bool(s['hasPlatform'])
        p.parent = s['parentSlot']; p.riding = s['ridingSlot']; p.xcoll = s['xCollSlot']
        p.tanim = s['targetAnimId']; p.tdir = s['targetDir']; p.lanim = s['lastTargetAnimId']; p.ldir = s['lastTargetDir']
        p.isPlayer = (i == 0)
        prox.append(p)
    return prox


def loaded(p):
    return p.status in (1, 2, 3)


def search(ref, st, prox, player):
    if (st & 0x80) == 0:
        if loaded(ref):
            return [c for c in prox if c.refId == st]
        return []
    t = st & 0x7F
    out = []
    # ReferenceEquals through LogicContextEntity: every proxy has its own non-null LogicContextEntity, so
    # "ref.RidingEntity == candidate.LogicContextEntity" is "ref.riding slot == candidate slot" etc.
    if t == 0:
        out.append(ref)
    elif t == 1:
        if player is not None:
            out.append(player)
    elif t == 2:
        out = [c for c in prox if loaded(c)]
    elif t == 3:
        out = [c for c in prox if loaded(c) and not c.isPlayer]
    elif t == 4:
        out = [c for c in prox if loaded(ref) and (c.flags & COLLIDABLE) != 0 and (c.animFlags & 0x80) == 0 and not c.platform]
    elif t == 5:
        out = [c for c in prox if not c.isPlayer and loaded(c) and ref.riding == c.i]
    elif t == 6:
        out = [c for c in prox if not c.isPlayer and loaded(c) and c.riding == ref.i]
    elif t == 7:
        out = [c for c in prox if not c.isPlayer and loaded(c) and ref.xcoll == c.i]
    elif t == 8:
        out = [c for c in prox if not c.isPlayer and loaded(c) and c.xcoll == ref.i]
    elif t == 9:
        out = [c for c in prox if not c.isPlayer and loaded(c) and c.parent == ref.i]
    elif t == 10:
        out = [c for c in prox if not c.isPlayer and loaded(c) and ref.parent == c.i]
    elif t == 11:
        out = [c for c in prox if not c.isPlayer and loaded(c) and c.platform]
    return out


def apply(v):
    prox = world_from(v)
    st = dict(v['world']['stats'])
    pad = dict(v['world']['pad']) if v['world'].get('pad') else None
    ins = v['instruction']
    op = ins[0]
    logic = prox[v['logicSlot']]
    player = prox[0]
    size = {0x72: 1, 0x88: 3, 0x96: 3, 0x97: 2, 0xB3: 9}[op]
    if op == 0x72:
        logic.lanim, logic.ldir = logic.tanim, logic.tdir
    elif op == 0x88:
        idx = ins[2]
        if idx < 3:
            for m in search(logic, ins[1], prox, player):
                m.status = STATUS_TABLE[idx]
    elif op == 0x96:
        for m in search(logic, ins[1], prox, player):
            s = I32(m.hp + ins[1])
            m.hp = m.hpMax if m.hpMax < s else s
    elif op == 0x97:
        s = st['mp'] + ins[1]
        s = max(-32768, min(32767, s))  # ClampToShort
        if st['mpMax'] < s:
            st['mp'] = st['mpMax']
        elif s < 0:
            st['mp'] = 0
        else:
            st['mp'] = s
    elif op == 0xB3:
        pad = dict(hold=ins[1] | ins[2] << 8, pressed=ins[3] | ins[4] << 8, released=ins[5] | ins[6] << 8, interval=ins[7] | ins[8] << 8)
    return size, prox, st, pad


def main(d):
    bad = 0; tot = 0
    for op in ['72', '88', '96', '97', 'B3']:
        doc = json.load(open(os.path.join(d, 'op%s.json' % op)))
        for v in doc['vectors']:
            if v.get('outOfTable'):
                # DLL policy: nothing written. The vector's stored word is NOT replayed.
                continue
            tot += 1
            size, prox, st, pad = apply(v)
            exp = v['expect']
            ok = size == exp['returned']
            w0 = world_from(v)
            fmap = dict(status='status', hp='hp', hpMax='hpMax', lastTargetAnimId='lanim', lastTargetDir='ldir', targetAnimId='tanim',
                        targetDir='tdir', refId='refId')
            expw = {(x['slot'], x['field']): x['value'] for x in exp['slotWrites']}
            for i, p in enumerate(prox):
                for fld, attr in fmap.items():
                    got = getattr(p, attr)
                    before = getattr(w0[i], attr)
                    if (i, fld) in expw:
                        if U32(got) != U32(expw[(i, fld)]):
                            ok = False; print(v['id'], 'slot', i, fld, got, '!=', expw[(i, fld)])
                    else:
                        if got != before:
                            ok = False; print(v['id'], 'slot', i, fld, 'changed', before, '->', got)
            ew = {x['field']: x['value'] for x in exp['statsWrites']}
            for k, val in v['world']['stats'].items():
                if k in ew:
                    if st[k] != ew[k]:
                        ok = False; print(v['id'], 'stats', k, st[k], '!=', ew[k])
                elif st[k] != val:
                    ok = False; print(v['id'], 'stats', k, 'changed')
            ep = {x['field']: x['value'] for x in exp['padWrites']}
            if op == 'B3':
                for k in ep:
                    if pad[k] != ep[k]:
                        ok = False; print(v['id'], 'pad', k, pad[k], '!=', ep[k])
                if set(ep) != {'hold', 'pressed', 'released', 'interval'}:
                    ok = False; print(v['id'], 'pad writes not 4 fields')
            if not ok:
                bad += 1
    print('DLL-term model: vectors', tot, 'disagreements', bad)
    return bad


if __name__ == '__main__':
    sys.exit(1 if main(sys.argv[1]) else 0)
