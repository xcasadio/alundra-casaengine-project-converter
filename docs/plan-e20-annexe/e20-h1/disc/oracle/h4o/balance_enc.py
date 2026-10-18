"""balance_enc.py - D2: encodes alundra-project/Data/balance.json into the byte layout the binary reads, and checks it against the disc blob.

Layout of one balance record (FACT: ResolveBalanceTarget 0x8004464C, ComputeNewHp 0x8003A374, UpdateAnimation 0x80038B74-0x80038BAC and 0x80044550,
all in out/dis_hits.txt; the RecordSize identity 15 + 2 * AttackCount):
    +0     BalanceLevel (u8)       0x80044550 compares it with the threshold [0x80143DF0]
    +1     RecordSize   (u8)       0x80044550 steps to the next level record with  addu a0,a0,[a0+1]
    +2     MaxHp        (u8)       InitializeEntity reads it as the entity's HP
    +3..+13  DamageResponses[0..10]  response of attribute k (1..11) = byte at record + k + 2 = ResolveBalanceTarget  lbu 2(record + (attr & 0xF))
    +14    AttackCount  (u8)       ComputeNewHp / ResolveBalanceTarget  lbu 0xe(sheet)
    +15    Attacks, 2 bytes each:  byte 0 = attribute byte ("AttackAttribute": low nibble AttackType, bit 7 UsesEquipmentPower), byte 1 = Power
                                  (ResolveBalanceTarget lbu 0(attack) and lbu 1(attack); attack k of the sheet is at record + 0xF + 2 * k)
JSON -> bytes is the identity on these fields; the encoder REFUSES an entry whose AttackType != AttackAttribute & 0xF or whose UsesEquipmentPower !=
bool(AttackAttribute & 0x80), so that nothing is silently reinterpreted.

The blob is laid out like the disc file DATA/BALANCE.BIN: 512 u16 offsets (table at +0), the records at the offsets given by the JSON FileOffset.
build_blob() returns the bytes; check_against_disc() compares them byte by byte with the disc file (all records reachable from the table).
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
BALANCE_JSON = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/balance.json"
BALANCE_BIN = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/BALANCE.BIN"


def load_entries():
    j = json.load(open(BALANCE_JSON, encoding='utf-8-sig'))
    return j['EntriesBySpriteIndex']


def encode_record(e):
    """one JSON record (a dict with BalanceLevel, RecordSize, MaxHp, DamageResponses, AttackCount, Attacks) -> bytes"""
    n = e['AttackCount']
    attacks = e['Attacks'] or []
    if len(attacks) != n:
        raise ValueError('AttackCount %d but %d attacks' % (n, len(attacks)))
    if len(e['DamageResponses']) != 11:
        raise ValueError('expected 11 damage responses')
    b = bytearray([e['BalanceLevel'], e['RecordSize'], e['MaxHp']])
    b += bytes(e['DamageResponses'])
    b.append(n)
    for a in attacks:
        attr = a['AttackAttribute']
        if a['AttackType'] != (attr & 0xF) or a['UsesEquipmentPower'] != bool(attr & 0x80):
            raise ValueError('attack %r: AttackType / UsesEquipmentPower disagree with AttackAttribute' % a)
        b.append(attr)
        b.append(a['Power'])
    if len(b) != e['RecordSize']:
        raise ValueError('RecordSize %d but encoded %d' % (e['RecordSize'], len(b)))
    return bytes(b)


def chain(e):
    """the level records of one sprite index: the entry itself, then its NextLevel chain"""
    out = []
    while e is not None:
        out.append(e)
        e = e.get('NextLevel')
    return out


def build_blob(entries=None):
    """returns (blob bytes of the disc file's length, {FileOffset: record bytes})"""
    entries = entries or load_entries()
    recs = {}
    for e in entries:
        for r in chain(e):
            recs[r['FileOffset']] = encode_record(r)
    end = max(off + len(b) for off, b in recs.items())
    blob = bytearray(end)
    for i, e in enumerate(entries):
        blob[2 * i] = e['FileOffset'] & 0xFF
        blob[2 * i + 1] = e['FileOffset'] >> 8
    for off, b in recs.items():
        blob[off:off + len(b)] = b
    return bytes(blob), recs


def check_against_disc():
    """compares the blob built from the JSON with the disc file; returns (n_records, differing_records, length_json, length_disc)"""
    blob, recs = build_blob()
    raw = open(BALANCE_BIN, 'rb').read()
    bad = [off for off, b in recs.items() if raw[off:off + len(b)] != b]
    table_bad = [i for i in range(512) if blob[2 * i:2 * i + 2] != raw[2 * i:2 * i + 2]]
    return len(recs), bad, table_bad, len(blob), len(raw)


if __name__ == '__main__':
    n, bad, tbad, lj, ld = check_against_disc()
    print('records encoded from balance.json: %d; differing from the disc blob: %d; table entries differing: %d; length json-built %d disc %d' % (n, len(bad), len(tbad), lj, ld))
