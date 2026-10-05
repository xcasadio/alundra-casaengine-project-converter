"""E19.f3 discovery: tick-exact model of the CHOICE box (two options, opener 0x80050BA8) of Alundra (ALUN_CD.EXE, France).

Extends the f2a text-box model (model.py in this directory is an unmodified copy of ../../e19f2-disc/model/model.py).
Every rule is read from the binary (addresses in comments) AND checked against the real binary code executed in a
MIPS interpreter (../emu.py, ../drive.py): `validate.py` compares this model with that execution frame by frame.

FRAME conventions (same as the f2a model): one iteration of the main loop = RenderScene (callback dispatcher
0x80048054: slot 0 text box, slot 3 CHOICE box, slot 12 name box) then Update(0) (pad sample, scripts).
pad(f) = the raw pad word the boxes see in frame f (the one sampled in the previous frame's Update(0)).
N = the script tick in which the opener runs (opcode 0x44's first entry); the first update of the slot is N+1.

Pad layout (PsyQ PadRead, same as AlundraPadState.cs): Cross 0x40, Circle 0x20, Square 0x80, Triangle 0x10,
Up 0x1000, Right 0x2000, Down 0x4000, Left 0x8000.
"""
import model
from model import Slide, WIDTHS, calc_text_width

# ---------------------------------------------------------------- constants (binary)
CHOICE_X, CHOICE_Y = 176, 144          # cfg 0x800A4FEC {X 0xB0, Y 0x90, 16 x 4 cells of 8 px}
CHOICE_W, CHOICE_H = 16 * 8, 4 * 8      # 128 x 32 at rest: x 176..303, y 144..175
SLIDE_FROM_X = 320                      # S.x = 0x140 at 0x80050484 (slide-in start) and S.ex = 0x140 (slide-out end)
LABEL_DX, LABEL_DY, LABEL_PITCH = 16, 8, 48   # table 0x800A731C entry 3 (+8 = 16, +0xA = 8), 0x30 per label (0x8004FDB0..)
LABEL_H = 16
CURSOR_W = 16                           # SPRT 16 x 16 (0x80050750)
CURSOR_DY = -8                          # cfg.y + 8 - 16 (0x8004FD74-0x8004FD88)
CURSOR_U = (0xB0, 0xC0, 0xD0, 0xE0)     # 0x800A58CC + 40 * k, v 0x38
SFX_MOVE, SFX_OPEN, SFX_CLICK, SFX_FIRST, SFX_SECOND = 1, 4, 5, 2, 3   # 0x80050138, 0x80050648, 0x800500D0, 0x800500EC/0x800500DC
PAD_CROSS, PAD_CIRCLE, PAD_SQUARE, PAD_TRIANGLE = 0x40, 0x20, 0x80, 0x10
PAD_UP, PAD_RIGHT, PAD_DOWN, PAD_LEFT = 0x1000, 0x2000, 0x4000, 0x8000
REPEAT_DELAY, REPEAT_INTERVAL = 20, 0   # 0x80126F18 / 0x80126F1C set at 0x8002E36C / 0x8002E378


# ---------------------------------------------------------------- pad (0x8002E250)
class PadWords:
    """The pad words of 0x80126F18: hold (+0x10), justPressed (+0x12), justReleased (+0x14), byInterval (+0x16)."""

    def __init__(self):
        self.hold = 0
        self.pressed = 0
        self.released = 0
        self.interval = 0
        self.mode = 0          # +8: 0 = waiting for the initial delay, 1 = repeating
        self.counter = 0       # +0xC

    def update(self, raw):
        raw &= 0xFFFF
        old = self.hold
        self.pressed = raw & (raw ^ old)
        self.released = old & (raw ^ old)
        if old != raw or old == 0:                         # 0x8002E280-0x8002E2A0: any change of the held set restarts
            self.mode = 0
            self.counter = 0
            self.interval = self.pressed
        elif self.mode == 0:                               # 0x8002E2AC-0x8002E2D0
            if self.counter < REPEAT_DELAY:
                self.counter += 1
                self.interval = 0
            else:
                self.mode = 1
                self.counter = 0
                self.interval = raw
        else:                                              # 0x8002E2D8-0x8002E2F4
            if self.counter < REPEAT_INTERVAL:
                self.counter += 1
                self.interval = 0
            else:
                self.counter = 0
                self.interval = raw
        self.hold = raw


# ---------------------------------------------------------------- the choice box
def decode_label(raw):
    """0x800505FC copies at most 6 bytes (strncpy 0x8008202C, n = 6); the first update (0x800501FC) rewrites '{c' as
    c + 0x50 and '}c' as c + 0x90 (0x80050254-0x800502B8). Returns the decoded byte string."""
    raw = raw[:6]
    if b'\0' in raw:
        raw = raw[:raw.index(b'\0')]
    out = bytearray()
    i = 0
    while i < len(raw):
        c = raw[i]
        if c == 0x7B:
            out.append((raw[i + 1] + 0x50) & 0xFF) if i + 1 < len(raw) else None
            i += 2
        elif c == 0x7D:
            out.append((raw[i + 1] + 0x90) & 0xFF) if i + 1 < len(raw) else None
            i += 2
        else:
            out.append(c)
            i += 1
    return bytes(out)


def label_width(decoded):
    """CalcTextWidth 0x8004771C on the decoded label (plain glyph advances)."""
    return sum(WIDTHS[b] for b in decoded)


class ChoiceFrame(dict):
    """What one pass of slot 3 produced (see ChoiceBox.render)."""


class ChoiceBox:
    """Slot 3 of the callback table (entry 3 of 0x800A731C: no init, update 0x800501FC, layer 5).
    Update function states: 'init' (0x800501FC, first pass, no drawing), 'in' (0x800501A4), 'active' (0x8004FFA8),
    'out' (0x8004FEFC)."""

    def __init__(self, anim0=0):
        self.anim = anim0            # 0x8017E644: cursor tick, NEVER reset at open (only wraps at 40)
        self.sel = 0                 # 0x8017E670
        self.result = 0              # 0x8017E640 (selection + 1 once Cross is seen)
        self.res_word = 0            # caller's result word (opcode 0x44: 0x8013D8D0), written by the callback 0x80050B98
        self.active = False          # slot flags bit 0
        self.update = None
        self.cfg_x, self.cfg_y = CHOICE_X, CHOICE_Y
        self.slide = None
        self.labels = (b'', b'')
        self.sfx = []

    # ---- opener 0x80050BA8 (and 0x80050C00 with a default selection) -> 0x800505FC
    def open(self, yes, no, default_sel=0):
        self.labels = (yes[:6], no[:6])               # strncpy n = 6 (0x8005062C / 0x80050638)
        self.sfx.append(SFX_OPEN)                      # 0x800490FC(4) at 0x80050648
        self.active = True                             # OpenSlot(3): copy of entry 3, flags |= 1 (0x80047F94); no init call
        self.update = 'init'
        self.sel = default_sel                         # 0x80050BE4 (zero) or *a2 - 1 (0x80050C38-0x80050C44)
        self.res_word = 0                              # variant 0x80050C00 zeroes the caller's word; 0x44 zeroes it itself (0x8003E8C0)
        # NOT reset: self.anim (0x8017E644), self.result (0x8017E640)

    # ---- one dispatcher pass of slot 3
    def render(self, pad):
        """pad = PadWords seen in this frame. Returns a ChoiceFrame or None when the slot is inactive."""
        self.sfx = []
        fr = ChoiceFrame(update=None, drawn=False, closed=False, sfx=[])
        if not self.active:
            return None
        fr['update'] = self.update
        if self.update == 'init':                      # 0x800501FC
            self.decoded = [decode_label(self.labels[0]), decode_label(self.labels[1])]
            self.widths = [label_width(d) for d in self.decoded]
            self.slide = Slide(SLIDE_FROM_X, CHOICE_X)  # S = {tick 0, steps 15, mode 2, start (320, y), end (cfg.x, y)}: 0x80050470-0x80050594
            _, self.cfg_x = self.slide.update(self.cfg_x)   # 0x800505A4: first UpdateUiBoxesPosition (tick 0 = start position 320)
            self.update = 'in'
        elif self.update == 'in':                      # 0x800501A4
            done, self.cfg_x = self.slide.update(self.cfg_x)
            if done:
                self.update = 'active'
            self._draw(fr)
        elif self.update == 'active':                  # 0x8004FFA8
            if pad.pressed & PAD_CROSS:                # 0x8004FFD8-0x80050000
                self.result = self.sel + 1
                self.slide = Slide(CHOICE_X, SLIDE_FROM_X)
                self.sfx.append(SFX_CLICK)              # 0x800500D0
                self.sfx.append(SFX_FIRST if self.result == 1 else SFX_SECOND)   # 0x800500DC-0x800500F0
                self.update = 'out'
            if pad.interval & PAD_LEFT:                # 0x80050108-0x80050140: sound only when the selection changes
                if self.sel == 1:
                    self.sfx.append(SFX_MOVE)
                self.sel = 0
            if pad.interval & PAD_RIGHT:               # 0x80050140-0x8005017C
                if self.sel == 0:
                    self.sfx.append(SFX_MOVE)
                self.sel = 1
            self._draw(fr)
        elif self.update == 'out':                     # 0x8004FEFC
            done, self.cfg_x = self.slide.update(self.cfg_x)
            if done:                                   # 0x8004FF48-0x8004FF8C: cfg restored, slot flags cleared, callback(result)
                self.cfg_x, self.cfg_y = CHOICE_X, CHOICE_Y
                self.active = False
                self.res_word = self.result
                fr['closed'] = True
            else:
                self._draw(fr)
        fr['sfx'] = list(self.sfx)
        return fr

    def _draw(self, fr):                                # 0x8004FCE8
        self.anim += 1                                  # 0x800507E4 (called from the draw, once per drawn pass)
        if self.anim == 40:
            self.anim = 0
        x = self.cfg_x
        lab = []
        for i in range(2):
            lab.append(dict(text=bytes(self.decoded[i]), x=x + LABEL_DX + LABEL_PITCH * i, y=self.cfg_y + LABEL_DY,
                            w=self.widths[i], h=LABEL_H))
        cx = x + LABEL_DX + LABEL_PITCH * self.sel + 4 * len(self.decoded[self.sel]) - 8
        fr.update(drawn=True, frame_xy=(x, self.cfg_y), frame_wh=(CHOICE_W, CHOICE_H), labels=lab,
                  cursor=dict(x=cx, y=self.cfg_y + CURSOR_DY, w=CURSOR_W, h=CURSOR_W, img=self.anim // 10,
                              u=CURSOR_U[self.anim // 10], v=0x38),
                  sel=self.sel, anim=self.anim)


# ---------------------------------------------------------------- script side: opcode 0x44 (handler 0x8003E88C)
class ChoiceProgramState:
    """What the handler keeps: program +8 (first-entry marker: the pc of the opcode) and +0x2C (Result)."""

    def __init__(self):
        self.marker = None
        self.result = None


def op44(ctx, prog, yes=b'OUI', no=b'NON', default_sel=0):
    """Generator = one event program executing 0x44. First entry: res_word = 0, open, return 0. Every later tick:
    res_word == 0 -> return 0; else Result = 1 if res_word == 1 else 0, return 1 (the program continues in the same tick)."""
    pc = 'pc0x44'
    if prog.marker != pc:                              # 0x8003E8B8-0x8003E8F8
        ctx.choice.res_word = 0
        ctx.choice.open(yes, no, default_sel)
        prog.marker = pc
        ctx.ev('0x44 first entry')
        ctx.ev('sfx 4 (open)')
        yield
    while True:                                        # 0x8003E900-0x8003E930
        r = ctx.choice.res_word
        if r != 0:
            prog.result = 1 if r == 1 else 0
            ctx.ev('0x44 resolved Result=%d' % prog.result)
            return
        yield


class CtxC(model.Ctx):
    def __init__(self, box, choice):
        super().__init__(box)
        self.choice = choice


def run_choice(script, square_pad, raw_pad, frames=3000, open_frame=0, anim0=0, stop_after=None):
    """script(ctx) -> generator (uses ctx.choice / op44). square_pad(f, box) -> (held, pressed) for the TEXT box (f2a
    convention, may be the unreal pad A); raw_pad(f) -> raw 16-bit pad word the CHOICE box sees in frame f.
    Frame order: text box (slot 0, incl. name box), choice box (slot 3), then one script tick."""
    box = model.TextBox()
    choice = ChoiceBox(anim0)
    ctx = CtxC(box, choice)
    pad = PadWords()
    gen = script(ctx)
    alive = True
    rows = []
    for f in range(open_frame, open_frame + frames):
        box.frame = f
        ctx.frame = f
        box.sfx, box.flags_set, box.notes = [], [], []
        held, pressed = square_pad(f, box)
        before = len(box.glyph_log)
        box.render(held, pressed)
        drawn = len(box.glyph_log) - before
        pad.update(raw_pad(f))
        cfr = choice.render(pad)
        if alive:
            try:
                next(gen)
            except StopIteration:
                alive = False
        rows.append(dict(frame=f, phase=box.phase(), y=box.y, rows=box.visible_rows(), cursor=box.cursor_img,
                         sfx=list(box.sfx), notes=list(box.notes), glyphs=len(box.glyph_log), drawn=drawn,
                         close_mode=box.close_mode, choice=cfr, pad=dict(hold=pad.hold, pressed=pad.pressed, interval=pad.interval),
                         ctrl=box.ctrl))
        if not alive and not box.is_active() and not choice.active:
            break
        if stop_after is not None and f >= open_frame + stop_after:
            break
    return rows, ctx


# ---------------------------------------------------------------- scripts
def script_choice_only(prog=None, yes=b'OUI', no=b'NON', default_sel=0):
    """The program of a bare 0x44 (no text box): open at tick 0, then poll."""
    prog = prog or ChoiceProgramState()

    def g(ctx):
        yield from op44(ctx, prog, yes, no, default_sel)
        ctx.ev('continues')
    g.prog = prog
    return g


def script_389_sailor12_choice(prog=None):
    """Map 389, C[12] from @1390 (first visit): 0x0D [0x81,1]; 0x50 4; 0x36 T999; 0x44; 0x51; 0x03; 0x0D [0x82/0x83,1]..0x39
    with the REAL choice box (the f2a model took choice_ticks as a parameter)."""
    s1, s2 = model.map_string(389, 1), model.map_string(389, 2)
    prog = prog or ChoiceProgramState()

    def g(ctx):
        yield from ctx.open(s1, 1, label='open S001')
        ctx.op('50', 4)
        yield from ctx.wait36(999)
        yield from op44(ctx, prog)
        ctx.op('51')
        yield from ctx.open(s2, 1, label='open S002')
        yield from ctx.wait39()
    g.prog = prog
    return g


def raw_script(events):
    """events: {frame: raw_word} for the frames where the word differs from 0 (held words are listed per frame)."""
    return lambda f: events.get(f, 0)


def press(frames_to_word):
    """frames_to_word: {frame: word} one-frame presses (held for exactly that frame)."""
    return lambda f: frames_to_word.get(f, 0)


def choice_summary(rows, ctx, n_open):
    """Frames (relative to the opener tick n_open) of the choice: init, first drawn, interactive start, press frame,
    slide-out frames, written/closed frame, sounds."""
    rel = lambda f: None if f is None else f - n_open
    ch = [(r['frame'], r['choice']) for r in rows if r['choice'] is not None]
    init = next((f for f, c in ch if c['update'] == 'init'), None)
    first_draw = next((f for f, c in ch if c['drawn']), None)
    active0 = next((f for f, c in ch if c['update'] == 'active'), None)
    out0 = next((f for f, c in ch if c['update'] == 'out'), None)
    closed = next((f for f, c in ch if c['closed']), None)
    sounds = [(rel(f), s) for f, c in ch for s in c['sfx']]
    return dict(init=rel(init), first_draw=rel(first_draw), interactive=rel(active0), first_out=rel(out0),
                closed=rel(closed), sounds=sounds)
