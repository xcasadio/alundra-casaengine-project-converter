"""trace_birth.py - bin agent of the E20.e0a5 discovery (scratch copy, never run in place).

Pass-by-pass trace of an entity from its CREATION (inside the real InitializeEntity 0x80039D04, whose first switch is the real UpdateAnimation 0x80038AB4) to the end of its
animation, on the f-projectiles machine (pworld.py): real MovePlayer, real SpawnWarpEntity, real animation pass 0x80038E18, real physics pass 0x80038364 (which refreshes the world box
by 0x80038064), real hit pass.  Nothing here knows an expected value: every number is read from RAM.

usage: python -B trace_birth.py wave [sword] [direction]       -> wave (global sprite 27/28/29) born in MovePlayer at S = R + 21
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from machine import M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
PASS_NAMES = {0x80038634: 'slots', 0x800386D0: 'events', 0x80038998: 'counters', 0x800384F4: 'lists', 0x80038E18: 'anim', 0x80038364: 'physics', 0x80038E84: 'effects', 0x80039300: 'hits'}

# fields of the entity read at every snapshot: (name, offset, signed)
XF = [('status', 0x10, 1), ('c1c', 0x1C, 1), ('ctl20', 0x20, 1), ('a88_targetAnim', 0x88, 1), ('a8C_targetDir', 0x8C, 1), ('a90_curAnim', 0x90, 1), ('a94_curDir', 0x94, 1),
      ('a98_adir', 0x98, 1), ('a9C_hdr', 0x9C, 0), ('aA0_loop', 0xA0, 0), ('aA4_next', 0xA4, 0), ('aA8_cnt', 0xA8, 1), ('aAC_end', 0xAC, 1), ('aB0_loops', 0xB0, 1),
      ('aB4_animflags', 0xB4, 0), ('aF8_izf', 0xF8, 1), ('a1C8_atk', 0x1C8, 0), ('a1D4_box', 0x1D4, 0), ('b208', 0x208, 1), ('b20C', 0x20C, 1), ('b210', 0x210, 1),
      ('b214', 0x214, 1), ('b218', 0x218, 1), ('b21C', 0x21C, 1), ('w1FC', 0x1FC, 1), ('w200', 0x200, 1), ('w204', 0x204, 1), ('p114', 0x114, 1), ('p118', 0x118, 1), ('p11C', 0x11C, 1),
      ('a194', 0x194, 0), ('a1A8', 0x1A8, 1), ('a1AC', 0x1AC, 1), ('trig228', 0x228, 1), ('serial4', 0x4, 1)]


class Tracer:
    def __init__(self, m, slot):
        self.m = m
        self.slot = slot
        self.e = ENT0 + STRIDE * slot
        self.rows = []
        self.writes = []
        self.ctx = ('init', 0, '-')          # (tick, pass)
        self.sounds = []
        self.created_at = None

    def snap(self, label):
        m, e = self.m, self.e
        d = {}
        for n, off, sg in XF:
            d[n] = m.peeks(e + off) if sg else m.peek(e + off)
        d['label'] = label
        a0, a4, hdr = d['aA0_loop'], d['aA4_next'], d['a9C_hdr']
        d['frame_next'] = ((a4 - a0) // 5) if a0 and a4 >= a0 else None
        d['frame_shown'] = (d['frame_next'] - 1) if d['frame_next'] else None
        return d


def anim_script(m, e):
    """the animation the entity is playing, decoded from RAM (header 14 bytes at +0x9C, frames of 5 bytes at +0xA0 ...)"""
    hdr = m.peek(e + 0x9C)
    a0 = m.peek(e + 0xA0)
    out = dict(hdr_ptr=hdr, hdr_bytes=[m.rb(hdr + i) for i in range(14)], frames=[])
    p = a0
    for k in range(64):
        b0 = m.rb(p)
        if b0 & 0x80:
            boxo = m.rb(p + 1) | (m.rb(p + 2) << 8)
            partso = m.rb(p + 3) | (m.rb(p + 4) << 8)
            fr = dict(k=k, kind='frame', dur=b0 & 0x7F, boxoff=boxo, partsoff=partso)
            rec = m.peek(e + 0x64)
            if boxo != 0xFFFF:
                bp = m.peek(rec + 8) + boxo
                fr['box'] = [m.rb(bp) - 256 if m.rb(bp) & 0x80 else m.rb(bp), (m.rb(bp + 1) ^ 0x80) - 0x80, (m.rb(bp + 2) ^ 0x80) - 0x80, m.rb(bp + 3), m.rb(bp + 4), m.rb(bp + 5)]
            out['frames'].append(fr)
            p += 5
        else:
            op = b0
            arg = m.rb(p + 1)
            out['frames'].append(dict(k=k, kind='cmd', op=op, arg=arg))
            break
    return out


def run_wave(sword='steel', direction='east', n=150, ticks_hold=96, hero_px=(100, 100), extra=None):
    SW = {'steel': 2, 'demon': 3, 'holy': 4}
    DIRS = {'east': 24, 'north': 16, 'west': 8, 'south': 0}
    item = SW[sword]
    m, meta = P.setup(item=item, hero_px=hero_px, direction=DIRS[direction])
    if os.environ.get('NOSWITCH'):
        m.poke(0x80039EA8, 0)              # mutant: the jal 0x80038AB4 of InitializeEntity becomes a nop (no switch at creation)
    pad = W.Pad()
    holds = {t: W.SQUARE for t in range(1, ticks_hold)}
    tr = Tracer(m, 1)
    e = tr.e
    # ---- instrumentation: PC hooks and entity write log --------------------------------------------------------------------
    pc_watch = {}
    orig_exec1 = m.exec1

    def exec1(pc, w):
        f = pc_watch.get(pc)
        if f:
            f(pc)
        return orig_exec1(pc, w)
    m.exec1 = exec1

    def on_spawn_return(pc):                 # 0x8002F820: v0 = entity pointer returned by SpawnWarpEntity (MovePlayer's shot site)
        tr.rows.append((tr.ctx, 'after SpawnWarpEntity returned (inside MovePlayer, before any later pass)', tr.snap('creation')))
        tr.created_at = tr.ctx
    pc_watch[0x8002F820] = on_spawn_return

    def on_init_return(pc):                  # 0x80039EAC delay slot of jal 0x80038ab4 in InitializeEntity: log boundary
        tr.writes.append((tr.ctx, pc, 'InitializeEntity: about to run the first switch (jal 0x80038AB4)', None, None))
    pc_watch[0x80039EA8] = on_init_return

    def wlog(kind, orig):
        def f(a, v):
            o = a & 0x1FFFFFFF
            lo = e & 0x1FFFFFFF
            if lo <= o < lo + STRIDE:
                tr.writes.append((tr.ctx, m.cur_pc, kind, a - e, v & M32))
            return orig(a, v)
        return f
    m.ww = wlog('w', m.ww)
    m.wh = wlog('h', m.wh)
    m.wb = wlog('b', m.wb)
    # ---- run ----------------------------------------------------------------------------------------------------------------
    for t in range(1, n + 1):
        m.tick_no = t
        hold = holds.get(t, 0)
        pad.update(hold)
        W.write_pad(m, pad)
        for a in W.PASSES:
            tr.ctx = (t, PASS_NAMES[a])
            m.call(a, limit=5_000_000)
            if m.peek(e + 0x10) != 0 or tr.created_at is not None:
                tr.rows.append(((t, PASS_NAMES[a]), 'after pass', tr.snap('pass')))
    return m, tr


def fmt_row(r):
    (t, ps), what, d = r
    fn = d['frame_shown']
    return ('t%03d %-9s st=%d c1c=%-2d | anim tgt/cur=%d/%d adir=%d | frame_shown=%s next=%s cnt(+A8)=%-11d end(+AC)=%d loops(+B0)=%d animflags(+B4)=%X izf(+F8)=%d | box1D4=%X '
            'b=(%d,%d,%d,%d,%d,%d) world=(%.3f,%.3f,%.3f) pos=(%.3f,%.3f,%d)') % (
        t, ps, d['status'], d['c1c'], d['a88_targetAnim'], d['a90_curAnim'], d['a98_adir'], fn, d['frame_next'], d['aA8_cnt'], d['aAC_end'], d['aB0_loops'], d['aB4_animflags'], d['aF8_izf'],
        d['a1D4_box'], d['b208'] >> 16, d['b20C'] >> 16, d['b210'] >> 16, (d['b214'] + 1) >> 16, (d['b218'] + 1) >> 16, (d['b21C'] + 1) >> 16,
        d['w1FC'] / 65536.0, d['w200'] / 65536.0, d['w204'] / 65536.0, d['p114'] / 65536.0, d['p118'] / 65536.0, d['p11C'])


if __name__ == '__main__':
    which = sys.argv[1]
    if which == 'wave':
        sword = sys.argv[2] if len(sys.argv) > 2 else 'steel'
        direction = sys.argv[3] if len(sys.argv) > 3 else 'east'
        m, tr = run_wave(sword, direction, n=150)
        out = os.path.join(HERE, '..', 'out')
        os.makedirs(out, exist_ok=True)
        S = tr.created_at[0]
        lines = ['# wave %s %s: created at tick %d (inside pass %s)' % (sword, direction, S, tr.created_at[1])]
        sc = anim_script(m, tr.e) if False else None
        # script of the animation as left at the end (header of the animation still running)
        lines.append('# first rows: state right after the creation (InitializeEntity + first switch), then after every pass of tick S..S+30')
        for r in tr.rows:
            t = r[0][0]
            if t < S - 1 or t > S + 30:
                continue
            lines.append(fmt_row(r))
        open(os.path.join(out, 'trace_wave_%s_%s%s.txt' % (sword, direction, '_NOSWITCH' if os.environ.get('NOSWITCH') else '')), 'w').write('\n'.join(lines) + '\n')
        # writes log on the birth tick
        wl = ['# writes to the entity (offset, value) with pc, from the creation to the end of tick S+2']
        for (ctx, pc, kind, off, val) in tr.writes:
            if ctx[0] < S or ctx[0] > S + 2:
                continue
            if off is None:
                wl.append('t%d %s pc=%08X %s' % (ctx[0], ctx[1], pc, kind))
            elif off in (0x10, 0x1C, 0x88, 0x8C, 0x90, 0x94, 0x98, 0x9C, 0xA0, 0xA4, 0xA8, 0xAC, 0xB0, 0xB4, 0xF8, 0x1C8, 0x1D4, 0x208, 0x20C, 0x210, 0x214, 0x218, 0x21C, 0x1FC, 0x200, 0x204, 0x194, 0x1A8, 0x1AC, 0x228):
                wl.append('t%d %-8s pc=%08X +%03X <- %08X' % (ctx[0], ctx[1], pc, off, val))
        open(os.path.join(out, 'trace_wave_%s_%s%s_writes.txt' % (sword, direction, '_NOSWITCH' if os.environ.get('NOSWITCH') else '')), 'w').write('\n'.join(wl) + '\n')
        print('S =', S, 'rows', len(tr.rows), 'writes', len(tr.writes))
