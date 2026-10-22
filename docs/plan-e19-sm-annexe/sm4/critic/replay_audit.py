"""Replays the 44 entries of the preparation's named-tests-audit.json on the critic's own MIPS interpreter and compares the
`expected` finals (slot fields, stats, pad) with what the REAL handler leaves in memory; also lists any mapped field that changed
without being in `expected`, and compares redToday."""
import json, sys
import cmips, replay_critic as R

SM4 = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4'
audit = json.load(open(SM4 + '/named-tests-audit.json'))
m = cmips.Machine()
bad = 0
for e in audit:
    st = e['start']
    ins = list(e['instruction'])
    if ins[-1] == 0xFF:
        ins = ins[:-1]
    v = dict(world=dict(slotCount=len(st['slots']), slots=st['slots'], stats=st['stats'], pad=st.get('pad')),
             instruction=ins, logicSlot=e['logicSlot'], ownerSlot=e['ownerSlot'])
    h, ret, pre, post, stores, sr = R.run(m, v)
    why = []
    if 'returned' in e and ret != e['returned']:
        why.append('returned %s != %s' % (ret, e['returned']))
    exp = e['expected']
    seen_fields = set()
    for key, val in exp.get('slot', {}).items():
        i, fld = key.split('.')
        i = int(i)
        off, sz, sg = R.F[fld]
        got = m.rw(R.slot_addr(i) + off)
        if sg:
            got = cmips.s32(got)
        seen_fields.add((i, fld))
        if (got & cmips.M) != (val & cmips.M):
            why.append('slot %s: got %s expected %s' % (key, got, val))
    for fld, val in exp.get('stats', {}).items():
        off, sg = R.STATF[fld]
        got = m.rh(R.STATS + off)
        if sg:
            got = cmips.s16(got)
        seen_fields.add(('stats', fld))
        if got != val:
            why.append('stats %s: got %s expected %s' % (fld, got, val))
    for fld, val in exp.get('pad', {}).items():
        got = m.rh(R.PAD + R.PADF[fld])
        seen_fields.add(('pad', fld))
        if got != val:
            why.append('pad %s: got %s expected %s' % (fld, got, val))
    # anything that changed but is not in expected?
    n = len(st['slots'])
    for i in range(n):
        for k, (off, sz, sg) in R.F.items():
            if R.chg(pre, post, m, R.slot_addr(i) + off, 4) and (i, k) not in seen_fields:
                why.append('UNEXPECTED change slot %d.%s' % (i, k))
    for k, (off, sg) in R.STATF.items():
        if R.chg(pre, post, m, R.STATS + off, 2) and ('stats', k) not in seen_fields:
            why.append('UNEXPECTED change stats.%s' % k)
    for k, off in R.PADF.items():
        if R.chg(pre, post, m, R.PAD + off, 2) and ('pad', k) not in seen_fields:
            why.append('UNEXPECTED change pad.%s' % k)
    # redToday: any observable difference between the "skipped" world (= pre) and the final expected values
    diffs = []
    for key, val in exp.get('slot', {}).items():
        i, fld = key.split('.')
        i = int(i)
        off, sz, sg = R.F[fld]
        before = pre[m.a(R.slot_addr(i) + off) - m.a(R.WIN):][:4]
        import struct
        b = struct.unpack('<I', before)[0]
        if sg:
            b = cmips.s32(b)
        if (b & cmips.M) != (val & cmips.M):
            diffs.append(key)
    for fld, val in exp.get('stats', {}).items():
        off, sg = R.STATF[fld]
        b = struct.unpack('<H', pre[m.a(R.STATS + off) - m.a(R.WIN):][:2])[0]
        if sg:
            b = cmips.s16(b)
        if b != val:
            diffs.append('stats.' + fld)
    for fld, val in exp.get('pad', {}).items():
        b = struct.unpack('<H', pre[m.a(R.PAD + R.PADF[fld]) - m.a(R.WIN):][:2])[0]
        if b != val:
            diffs.append('pad.' + fld)
    red = bool(diffs)
    if red != e['redToday']:
        why.append('redToday claimed %s, computed %s (diffs %s)' % (e['redToday'], red, diffs))
    tag = 'OK ' if not why else 'BAD'
    if why:
        bad += 1
    print(tag, e['id'], e['binaryAudit'], 'red' if red else 'green', why)
print('bad', bad, 'of', len(audit))
