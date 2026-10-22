"""Replays the zero-site vectors of 0x7A and 0x15 (the 0x66 ones have a different state format and are not replayed here) on the critic's interpreter."""
import json
import cmips, replay_critic as R

SM4 = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4'
m = cmips.Machine()
bad = 0
d = json.load(open(SM4 + '/vectors/zero-site/op7A.json'))
for v in d['vectors']:
    base = m.a(R.WIN)
    m.mem[base:base + 0x90000] = bytes(0x90000)
    ins = list(v['instruction']) + [0xFF]
    for i, b in enumerate(ins):
        m.wb(R.CODEA + i, b, log=False)
    m.ww(R.CURSOR, R.CODEA, log=False)
    m.ww(R.STATE + 0x2C, v['resultBefore'], log=False)
    m.ww(R.STATE + 0x34, 0xDEAD, log=False)
    m.hooks = {}
    m.stores.clear()
    ret = m.call(m.rw(R.TABLE + 4 * 0x7A), a0=R.slot_addr(0), a1=R.slot_addr(0), a2=R.CURSOR, a3=R.STATE)
    ok = (ret & cmips.M) == (v['returned'] & cmips.M) or cmips.s32(ret) == v['returned']
    sw = {w['field']: w['value'] for w in v.get('stateWrites', [])}
    got34 = m.rw(R.STATE + 0x34)
    if '_34' in sw:
        ok = ok and got34 == R.CODEA + sw['_34']
    else:
        ok = ok and got34 == 0xDEAD
    if not ok:
        bad += 1
        print('BAD', v['id'], ret, got34, v)
print('7A vectors', len(d['vectors']), 'bad', bad)

d = json.load(open(SM4 + '/vectors/zero-site/op15.json'))
bad = 0
for v in d['vectors']:
    base = m.a(R.WIN)
    m.mem[base:base + 0x90000] = bytes(0x90000)
    ent = R.slot_addr(v['logicSlot'])
    rec = 0x80150400
    m.wb(rec + 9, v['recordHeight'], log=False)
    m.ww(ent + 0x44, rec, log=False)
    m.ww(ent + 0x1EC, v['modZ'] & cmips.M, log=False)
    m.ww(ent + 0x11C, v['posZBefore'] & cmips.M, log=False)
    for i, b in enumerate([0x15, 0xFF]):
        m.wb(R.CODEA + i, b, log=False)
    m.ww(R.CURSOR, R.CODEA, log=False)
    m.hooks = {0x8003C840: lambda mm: None}
    ret = m.call(m.rw(R.TABLE + 4 * 0x15), a0=ent, a1=ent, a2=R.CURSOR, a3=R.STATE)
    exp = v['slotWrites'][0]['value'] & cmips.M
    got = m.rw(ent + 0x11C)
    ok = ret == v['returned'] and got == exp
    if not ok:
        bad += 1
        print('BAD', v['id'], ret, hex(got), hex(exp))
print('15 vectors', len(d['vectors']), 'bad', bad)
