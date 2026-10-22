"""Replays the vector files on the REAL handlers using only what the JSON says (world, instruction, logic/owner slots) and checks
every expectation (v0, Result, every written field, nothing else changed). It proves the format is complete: no hidden input.
This is the python twin of the C# replay test the slice adds.

Usage: python -B replay_vectors.py VECTORS_DIR
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import temu  # noqa: E402
import sm4model as S  # noqa: E402


def world_from_json(wj):
    st = dict(resultWord=S.SENTINEL, cursor=None)
    return dict(slots=wj['slots'], stats=wj['stats'], pad=wj['pad'], state=st)


def main():
    d = sys.argv[1]
    m = temu.make_machine()
    total = bad = skipped = 0
    for op in (0x72, 0x88, 0x96, 0x97, 0xB3):
        doc = json.load(open(os.path.join(d, 'op%02X.json' % op), encoding='utf-8'))
        nb = 0
        for v in doc['vectors']:
            if v.get('outOfTable'):
                skipped += 1
                continue
            total += 1
            w = world_from_json(v['world'])
            S.write_world(m, w)
            S.write_code(m, v['instruction'])
            base = S.LO & 0x1FFFFFFF
            before = bytes(m.ram[base:base + (S.HI - S.LO)])
            handler = m.rw(S.TABLE + 4 * op)
            v0 = m.call(handler, S.ent_addr(v['logicSlot']), S.ent_addr(v['ownerSlot']), S.PCP, S.STATE)
            after = bytes(m.ram[base:base + (S.HI - S.LO)])
            e = v['expect']
            errs = []
            if S.s32(v0) != e['returned']:
                errs.append('v0 %d != %d' % (S.s32(v0), e['returned']))
            res = struct.unpack_from('<I', m.ram, (S.STATE + 0x2C) & 0x1FFFFFFF)[0]
            if e['resultWritten']:
                if S.s32(res) != e['result']:
                    errs.append('Result %d != %d' % (S.s32(res), e['result']))
            elif res != S.SENTINEL:
                errs.append('Result written')
            # build the expected image from the JSON writes and compare the whole window except internal areas
            exp = bytearray(before)
            for sw in e['slotWrites']:
                off = S.SLOT_OFF_BY_NAME[sw['field']]
                kind = S.SLOT_FIELDS[off][2]
                val = sw['value']
                if kind == 'slotref':
                    val = S.pointer_word(val)
                elif kind == 'ptrbool':
                    val = S.ent_addr(1) if val else 0
                struct.pack_into('<I', exp, (S.ent_addr(sw['slot']) + off) - S.LO, val & S.M32)
            for st in e['statsWrites']:
                off = [k for k, x in S.STATS_FIELDS.items() if x[0] == st['field']][0]
                struct.pack_into('<H', exp, (S.PSTATS + off) - S.LO, st['value'] & 0xFFFF)
            for pw in e['padWrites']:
                off = [k for k, x in S.PAD_FIELDS.items() if x[0] == pw['field']][0]
                struct.pack_into('<H', exp, (S.PAD + off) - S.LO, pw['value'] & 0xFFFF)
            # areas that are internal to the search (match buffer) and the Result word are excluded from the comparison
            def mask(buf):
                b = bytearray(buf)
                lo = (S.RES - 4) - S.LO
                b[lo:lo + 0x120] = bytes(0x120)
                r = (S.STATE + 0x2C) - S.LO
                b[r:r + 4] = bytes(4)
                return bytes(b)
            if mask(after) != mask(exp):
                diffs = [hex(S.LO + i) for i in range(len(after)) if mask(after)[i] != mask(exp)[i]][:8]
                errs.append('memory differs at %s' % diffs)
            total_err = bool(errs)
            if total_err:
                bad += 1
                nb += 1
                if nb <= 3:
                    print('MISMATCH', v['id'], errs)
        print('op %02X: replayed, %d mismatches' % (op, nb))
    print('TOTAL replayed %d vectors (%d out-of-table skipped), %d mismatches' % (total, skipped, bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
