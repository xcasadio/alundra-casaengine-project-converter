"""Drives the REAL binary code of the choice box frame by frame (see emu.py).
Frame order = the game loop: RenderScene (callback dispatcher: slot update functions) then Update(0) (pad sample,
scripts). The pad word the box sees in frame f is `seen(f)` (raw 16-bit held bits, PsyQ layout: Cross 0x40, Circle 0x20,
Square 0x80, Triangle 0x10, Up 0x1000, Right 0x2000, Down 0x4000, Left 0x8000); it goes through the REAL pad update
0x8002E250 (just-pressed 0x80126F2A, by-interval 0x80126F2E).
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu

PAD = 0x80126F18
G = 0x8017E640
S = 0x8017E8BC
CFG = 0x800A4FEC
SLOT3 = 0x80153028 + 28 * 3          # runtime slot 3 (table entry copy)
SLOT_PTR = 0x8015319C
RESULT_WORD = 0x8013D8D0              # opcode 0x44's own result word
PROG = 0x801F0000                     # fake program struct (Result at +0x2C, wait marker at +8)
PCWORD = 0x801F0100
STR_YES, STR_NO = 0x801F0200, 0x801F0210

CROSS, CIRCLE, SQUARE, TRIANGLE = 0x40, 0x20, 0x80, 0x10
UP, RIGHT, DOWN, LEFT = 0x1000, 0x2000, 0x4000, 0x8000


def new_machine(yes=b'OUI', no=b'NON'):
    m = emu.make_machine()
    m.ww(PAD, 20)          # initial repeat delay (0x8002E34C: sw 0x14)
    m.ww(PAD + 4, 0)       # repeat interval
    for i, c in enumerate(yes + b'\0'): m.wb(STR_YES + i, c)
    for i, c in enumerate(no + b'\0'): m.wb(STR_NO + i, c)
    m.hooks[0x800816D4] = _getstring
    m.ww(PCWORD, 0x80123456)
    m.ww(PROG + 8, 0)
    m.ww(PROG + 0x2C, 0xDEAD)
    return m


def _getstring(m):
    m.r[2] = STR_YES if m.r[4] == 0x43 else STR_NO


def pad_update(m, held):
    m.call(0x8002E250, PAD, held)


def pad_words(m):
    return dict(hold=m.rh(PAD + 0x10), pressed=m.rh(PAD + 0x12), released=m.rh(PAD + 0x14), interval=m.rh(PAD + 0x16))


def dispatch(m):
    """0x80048054 slot loop restricted to slot 3 (the only active slot): update called with a0 = &0x8015319C."""
    for slot in range(13):
        a = 0x80153028 + 28 * slot
        if m.rh(a) & 1:
            m.ww(SLOT_PTR, a)
            upd = m.rw(a + 0x14)
            if upd:
                m.call(upd, SLOT_PTR)


def snapshot(m):
    cfgx, cfgy = m.rhs(CFG), m.rhs(CFG + 2)
    cells = m.rw(CFG + 8)
    lab = []
    for i in range(2):
        p = G + 0x34 + 0x3C * i
        lab.append(dict(x=m.rhs(p + 8), y=m.rhs(p + 10), u=m.rb(p + 12), v=m.rb(p + 13), w=m.rhs(p + 0x10), h=m.rhs(p + 0x12), clut=m.rh(p + 14)))
    cur = dict(x=m.rhs(G + 0x10), y=m.rhs(G + 0x12), u=m.rb(G + 0x14), v=m.rb(G + 0x15), w=m.rhs(G + 0x18), h=m.rhs(G + 0x1A), clut=m.rh(G + 0x16))
    return dict(
        cfg=(cfgx, cfgy),
        cell0=(m.rhs(cells + 8), m.rhs(cells + 10)),
        cell63=(m.rhs(cells + 63 * 20 + 8), m.rhs(cells + 63 * 20 + 10)),
        S=dict(tick=m.rws(S), steps=m.rws(S + 4), mode=m.rws(S + 8), sx=m.rhs(S + 0xC), sy=m.rhs(S + 0xE), ex=m.rhs(S + 0x10), ey=m.rhs(S + 0x12), ox=m.rhs(S + 0x18), oy=m.rhs(S + 0x1A)),
        G=dict(result=m.rw(G), anim=m.rws(G + 4), sel=m.rws(G + 0x30)),
        slot=dict(flags=m.rh(SLOT3), update=m.rw(SLOT3 + 0x14)),
        labels=lab, cursor=cur,
        resptr=m.rw(0x8017E3F0), result_word=m.rws(RESULT_WORD), prog_result=m.rws(PROG + 0x2C),
    )


def run(seen, n_open, frames, anim0=0, handler_every_tick=True, text_log=True):
    """seen(f) -> held bits seen by the box in frame f.  The 0x44 handler runs in frame n_open's script phase
    (first entry) and in every later frame's script phase (poll) until it returns 1.
    Returns list of per-frame dicts (frame numbers are absolute, the opener frame is n_open)."""
    m = new_machine()
    m.ww(G + 4, anim0)
    trace = []
    done = False
    done_frame = None
    for f in range(1, frames + 1):
        m.log.clear()
        # RenderScene: box pass
        pad_update(m, seen(f))
        pw = pad_words(m)
        upd_before = m.rw(SLOT3 + 0x14) if (m.rh(SLOT3) & 1) else 0
        dispatch(m)
        ev_box = list(m.log)
        # Update(0): script phase
        hret = None
        if f >= n_open and not done:
            hret = m.call(0x8003E88C, 0, 0, PCWORD, PROG)
            if hret == 1:
                done = True
                done_frame = f
        snap = snapshot(m)
        snap.update(frame=f, pad=pw, upd_called=upd_before, events=ev_box + [e for e in m.log[len(ev_box):]], handler=hret)
        trace.append(snap)
    return trace, done_frame


if __name__ == '__main__':
    tr, d = run(lambda f: 0, 3, 40)
    for t in tr[:25]:
        print(t['frame'], hex(t['upd_called']), t['cfg'], t['S']['tick'], t['S']['mode'], t['G'], t['cursor']['x'], t['events'], t['handler'])
