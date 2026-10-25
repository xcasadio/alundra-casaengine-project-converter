import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mips import *
from replay_f255 import run, CH, OBJ, HERO
ETC = {0x4c: '', 0x4d: ' gagne!', 0x4e: 'Tu transportes trop de choses pour\ngarder ', 0x4f: '.', 0x46: ' (XA)'}
def full(contents, stock=0, dlg_seq=None, cdbusy_ticks=3):
    log, c88, o, cpu = run(contents, 0, stock={contents: stock})
    m = cpu.m
    # state after F255: set type in obj +0x68 = contents + 0x1e, chest flag +0x40
    m.w32(OBJ + 0x68, contents + 0x1e); m.w32(CH + 0x40, 777)
    ev = []
    buf = {'s': ''}
    cpu.stubs[0x80045004] = lambda c: 1 if (m.r32(0x80152F08) & 4) else 0
    cpu.stubs[0x800816D4] = lambda c: 0x80300000 + c.r[4]
    def strcpy(c): buf['s'] = ETC.get(c.r[5] - 0x80300000, '?'); return 0
    def strcat(c):
        a1 = c.r[5]
        if a1 >= 0x80300000 and a1 < 0x80301000: buf['s'] += ETC.get(a1 - 0x80300000, '?')
        else: buf['s'] += '<name %x>' % a1
        return 0
    cpu.stubs[0x80081FB8] = strcpy
    cpu.stubs[0x80081EAC] = strcat
    def openbox(c): ev.append(('OpenBox', repr(buf['s']), c.r[5])); m.w32(0x80152F08, 4); return 0
    cpu.stubs[0x800450F0] = openbox
    cpu.stubs[0x800490FC] = lambda c: (ev.append(('Sfx', c.r[4])), 0)[1]
    cpu.stubs[0x8004507C] = lambda c: (ev.append(('CloseMask', c.r[4])), 0)[1]
    cdb = {'n': cdbusy_ticks}
    def cdbusy(c):
        if cdb['n'] > 0: cdb['n'] -= 1; return 1
        return 0
    cpu.stubs[0x8005A7D4] = cdbusy
    cpu.stubs[0x80049AF4] = lambda c: (ev.append(('StopAllSound',)), 0)[1]
    cpu.stubs[0x8003ADAC] = lambda c: (ev.append(('Unblock', hex(c.r[4]))), 0)[1]
    cpu.stubs[0x8003A774] = lambda c: (ev.append(('DestroyLight', hex(c.r[4]))), 0)[1]
    cpu.stubs[0x80032B28] = lambda c: (ev.append(('SetGameFlag', c.r[4])), 0)[1]
    cpu.stubs[0x80033DBC] = lambda c: (ev.append(('ApplyItem', hex(c.r[4]), c.r[5])), 0)[1]
    cpu.stubs[0x8005A9E0] = lambda c: (ev.append(('PlayCD', c.r[4])), 0)[1]
    # chest anim: 2 for the first 61 ticks then 1 (A255 reads it)
    t = 0; first_c = None
    for t in range(0, 400):
        if t == 61: m.w32(CH + 0x88, 1)
        # state machine: obj state at +0x10 (1 -> run A255; stays 1 if A sets it)
        st = m.r32(OBJ + 0x10)
        if st == 1 or t == 0:
            m.w32(OBJ + 0x10, 2)  # engine sets 2 then A may reset to 1
            before = len(ev)
            cpu.call(0x80061BD4, (OBJ,))
        elif st == 2:
            n0 = len(ev)
            cpu.call(0x8007C174, (OBJ,))
            if len(ev) != n0 and first_c is None: first_c = t
        if m.r32(OBJ + 0x10) == 0xDEAD: break
        # dialogue closes after 20 ticks of being open
        if m.r32(0x80152F08) & 4:
            dlg_seq = (dlg_seq or 0) + 1
            if dlg_seq == 20: m.w32(0x80152F08, 0); dlg_seq = None
        if any(e[0] == 'DestroyLight' for e in ev): break
    return t, ev, m.r32(CH + 0x88), log
for contents, stock in ((36, 3), (36, 9), (80, 0), (2, 0), (61, 0), (72, 0)):
    t, ev, c88, log = full(contents, stock)
    print(contents, stock, 'end tick', t, 'chest+88', c88)
    for e in ev: print('   ', e)
