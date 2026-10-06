"""Tick-exact reference model of the dialogue TEXT box of Alundra (ALUN_CD.EXE, France).

Every rule below is read from the binary (addresses in comments); the C# decompilation was only used to name things.
Read-only reference: it writes nothing and needs no repo state except, for the validation cases, the extracted
map JSONs (data-extracted/data/map_N.json) for the texts.

FRAME = one iteration of the main loop 0x8002C3F4:
    0x8002AC2C (debug input)  ->  RenderScene 0x8002BD60  ->  Update(0) 0x8002BAEC
RenderScene runs the UI callback dispatcher 0x80048054 (call at 0x8002BE5C) whose slot 0 is the text box
(MsgBoxRender 0x80046EF0; slot 12 is the name box). Update(0) reads the pad FIRST (0x8002E38C at 0x8002BB5C) then runs
the world (0x8002E058: RunMapEvents 0x8003C67C, UpdateEntities 0x8003B388): all event scripts run there.
So in one frame the box updates BEFORE the scripts, and the box reads the pad sampled during the previous frame's
Update(0). In this model, pad(f) is "the pad state the box sees in frame f" (held = pad.hold 0x80126F28 & 0x80,
pressed = pad.justPressed 0x80126F2A & 0x80, Square).

Per frame the runner does: box.render(pad(f))  then  one tick of the script (until it yields).
An opening opcode executed in script tick N therefore gets its first box update in frame N+1.
"""
import json
import os

# ---------------------------------------------------------------- constants (binary)
WIDTHS = [16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 6, 1, 11, 5, 5, 14, 14, 14, 14, 14, 14, 14, 14,
          14, 1, 1, 4, 3, 5, 11, 8, 11, 10, 3, 5, 5, 6, 7, 3, 4, 2, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 4, 4, 5, 6, 5, 6, 10,
          7, 7, 7, 7, 7, 7, 7, 8, 4, 6, 7, 7, 11, 8, 7, 7, 7, 8, 7, 8, 8, 7, 11, 7, 7, 7, 4, 8, 4, 4, 8, 4, 7, 7, 5, 7, 5,
          5, 6, 7, 4, 3, 7, 4, 11, 8, 6, 7, 7, 6, 5, 4, 8, 6, 11, 6, 6, 7, 5, 3, 5, 8, 1, 1, 1, 5, 8, 7, 11, 7, 7, 6, 16,
          8, 5, 13, 1, 1, 1, 1, 4, 4, 7, 7, 5, 9, 15, 6, 14, 6, 5, 9, 1, 1, 10, 16, 3, 7, 7, 8, 9, 3, 7, 5, 11, 5, 8, 7, 5,
          11, 8, 6, 8, 6, 5, 4, 8, 8, 3, 4, 5, 6, 8, 11, 11, 12, 7, 7, 7, 7, 7, 7, 7, 13, 9, 7, 7, 7, 7, 4, 4, 4, 4, 7, 8,
          7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 7, 7, 8, 7, 7, 7, 7, 7, 7, 9, 5, 5, 5, 5, 5, 4, 4, 4, 4, 6, 7, 6, 6, 6, 6, 6, 8,
          6, 8, 8, 8, 8, 6, 7, 6]  # advance width of glyph g = int32 at 0x800993C4 + 20*g
SCROLL_LEN = 8          # 0x80149CAC, set 8 at 0x80044FBC
SCROLL_WAIT = 10        # 0x80149CB8, set 10 at 0x80044FC8
CLOSE_TIMER = 360       # 0x80149CC8, set 0x168 at 0x80044FF0
DELAY_RESET = 4         # 0x80149BD0 = 4 at 0x80045548 (every open)
BOX_X, BOX_Y, BOX_Y_OUT = 16, 168, 240     # msgBoxCfg 0x8009CFBC; slide start/end 0xF0
NAME_X, NAME_X_OUT, NAME_Y = 64, 320, 140  # nameBoxCfg 0x800A58BC; slide x 0x140
TEXT_X, TEXT_Y0, LINE_H = 32, 173, 16      # callback slot 0 +8 = 16, +0xA = 5; rows 16 px apart
CURSOR_XY = (288, 200)                      # cfg.x + 36*8 - 16, cfg.y + 7*8 - 24 (RenderText 0x800458F8-0x8004592C)
CURSOR_U = (0xB0, 0xC0, 0xD0, 0xE0)         # 0x800A58CC + 40*k (+0xC u, +0xD v = 0x38)
SFX_OPEN, SFX_CLOSE, SFX_VOICE0 = 6, 7, 0x4F
MSGBOX, MENUOPEN = 0x10, 0x08               # 0x800DC4B8 bits set at 0x800452D4-0x800452FC
VOICE_CODES = {'B': -1, 'C': 0, 'D': 1, 'E': 2, 'F': 3, 'G': 4}  # handlers 0x80046AB0..0x80046B1C


def _tdiv(a, b):
    """MIPS div (truncates toward zero)."""
    q = abs(a) // abs(b)
    return q if (a >= 0) == (b > 0) else -q


class Slide:
    """UpdateUiBoxesPosition 0x80047DD0 on a {step, total=15, settle=2, start, target} block."""

    def __init__(self, start, target):
        self.step, self.total, self.settle, self.start, self.target = 0, 15, 2, start, target

    def update(self, pos):
        if self.settle == 0:
            return True, pos                       # 0x80047DD8: returns 1, no write
        if self.step != self.total:
            pos = self.start + _tdiv((self.target - self.start) * self.step, self.total)
            self.step += 1
        else:
            pos = self.target
            self.settle -= 1
        return False, pos


def kanji_valid(code):
    """0x8004F304 calls BIOS A0:51h Krom2RawAdd((b0<<8)|b1) and takes the 2-byte path when it is != -1.
    The exact BIOS table is not modelled: we refuse the two SJIS ranges instead of guessing."""
    return 0x8140 <= code <= 0x84BE or 0x889F <= code <= 0x9872


def calc_text_width(buf, i):
    """CalcTextWidth 0x8004771C (jump table 0x80024098): sums widths until 0, \\A or \\N."""
    total = 0
    n = len(buf)
    while i < n and buf[i] != 0:
        b = buf[i]
        if b == 0x7B:
            total += WIDTHS[(buf[i + 1] + 0x50) & 0xFF]; i += 2; continue
        if b == 0x7D:
            total += WIDTHS[(buf[i + 1] + 0x90) & 0xFF]; i += 2; continue
        if b == 0x5C:
            i += 1
            c = buf[i] if i < n else 0
            k = c - 0x30
            if not (0 <= k < 0x2A):
                continue                          # re-read c as a normal char (0x800478A4)
            ch = chr(c)
            if ch.isdigit():
                while i < n and 0x30 <= buf[i] <= 0x39:
                    i += 1
                continue
            if ch == 'W':
                i += 1
                b = buf[i]
                total += WIDTHS[(b - 0x20) & 0xFF if b < 0x41 else (b - 0x27) & 0xFF]; i += 1; continue
            if ch == 'X':
                i += 2; continue
            if ch in 'BCDEFGTY':
                i += 1; continue
            if ch in 'AN':
                return total
            continue                              # ':'..'@', H, I-M, O-S, U, V: c counted as a char
        total += WIDTHS[b]; i += 1
    return total


def text_bytes(s):
    """Map a decoded extractor string back to the original bytes (Latin-1, 'œ' etc. through CP1252)."""
    out = bytearray()
    for ch in s:
        o = ord(ch)
        out += bytes([o]) if o < 256 else ch.encode('cp1252')
    return bytes(out)


def glyph_char(g):
    if 0x20 <= g < 0x7F or 0xA0 <= g <= 0xFF:
        return chr(g)
    return '<%d>' % g


class TextBox:
    """Globals of the box; method names follow the binary functions."""

    def __init__(self):
        self.dialog_flags = 0        # 0x80152F08: 1 slide-in, 2 slide-out, 4 active
        self.ctrl = 0                # 0x800DC4B8 bits 0x10/0x08 only
        self.close_timer = 0         # 0x80149CC4 (NOT reset at open)
        self.scroll_count = 0        # 0x80149CA8 (not reset at open)
        self.scroll_wait = 0         # 0x80149CB4 (not reset at open)
        self.text_flags = 3          # 0x80149BC8
        self.close_mode = 3          # 0x80149CBC
        self.scroll_mode = 3         # 0x80149CB0
        self.auto_adv = 0            # 0x80149BCC (0x4D latch)
        self.auto_adv2 = 0           # 0x80149CA4 (0x4F latch)
        self.script_close_req = 0    # 0x80149CCC (0x51 latch)
        self.temp_flags = set()
        self.vars = {}               # \V / \X insertions: {('V', d): int, ('X', d): str}
        self.y = BOX_Y_OUT
        self.name = None             # name box: dict(x, flags, slide) when present
        self.frame = 0
        self.sfx, self.flags_set, self.notes = [], [], []
        self.glyph_log = []          # (frame, glyph, row band)
        self._reset_text_state(b'')

    def _reset_text_state(self, text):
        self.buf = bytearray(text) + b'\0'
        self.cur = 0                 # 0x80149BE0
        self.line_index = 0          # 0x80149BDC
        self.buf_x = 0               # 0x80149BD8
        self.render_step = 0         # 0x80149BE4
        self.line_start_x = 0        # 0x80149CE0
        self.line_width = [0, 0, 0]  # 0x80149BE8[3]
        self.bands = [[], [], []]    # VRAM bands (960, 288+16k): glyph ids drawn
        self.voice = -1              # 0x80149CDC
        self.typing_done = 0         # 0x80149CC0
        self.scroll_pending = 0      # 0x80149CA0
        self.cursor_shown = 0        # 0x80149CD0
        self.cursor_tick = 0         # 0x80149CD4
        self.cursor_img = None
        self.scroll_px = 0
        self.delay_reset = DELAY_RESET
        self.delay = 1               # 0x80149BD4 = 1 at 0x80045554 -> first gate on the first interpreter call

    # ------------------------------------------------------------ script side (Update(0) of the frame)
    def is_active(self):                       # IsDialogActive 0x80045004
        return bool(self.dialog_flags & 4)

    def try_open(self, text, mode, name=False):
        """TryOpenDialog 0x800423F8 -> InitializeDialogMessage 0x800450F0. False = opcode returns 0 (retry next tick)."""
        if self.is_active():
            return False
        if len(text) >= 0x960:                 # 0x8004513C: strlen >= 2400 -> 27-byte error text copied instead
            raise ValueError('text longer than the 0x960 buffer')
        self._reset_text_state(text)
        self.slide = Slide(BOX_Y_OUT, BOX_Y)   # 0x800451EC-0x800452A4
        self.dialog_flags = 5                  # 0x800452B0
        self.ctrl |= MSGBOX if mode == 1 else MENUOPEN
        self.script_close_req = 0              # 0x80045320
        self.auto_adv2 = 0                     # 0x80045330
        self.auto_adv = 0                      # 0x8004533C
        self.text_flags = 3                    # 0x80045540
        self.scroll_mode = 3                   # 0x8004555C
        self.close_mode = 3                    # 0x80045568
        self.sfx.append(SFX_OPEN)              # 0x80045574
        if name:                               # 0x80059F6C -> SetTransitionType(12), init 0x8005A268 (slide x 320->64)
            if self.name is None or not (self.name['flags'] & 4):
                self.name = dict(x=NAME_X_OUT, flags=5, slide=Slide(NAME_X_OUT, NAME_X))
        return True

    def op4C(self, v): self.text_flags = v                                   # 0x800450B0
    def op4D(self):                                                          # 0x80045088
        if self.text_flags & 4: self.auto_adv = 1
    def op4E(self, v): self.scroll_mode = v                                  # 0x800450E4
    def op4F(self):                                                          # 0x800450BC
        if self.scroll_mode & 4: self.auto_adv2 = 1
    def op50(self, v): self.close_mode = v                                   # 0x8004507C
    def op51(self):                                                          # 0x80045054
        if self.close_mode & 4: self.script_close_req = 1

    # ------------------------------------------------------------ RenderScene side
    def render(self, held, pressed):
        """MsgBoxRender 0x80046EF0 (callback slot 0), then the name box callback (slot 12, 0x8005A3E0)."""
        self.scroll_px = 0
        if self.dialog_flags & 4:
            self._msgbox_render(held, pressed)
        if self.name is not None and self.name['flags'] & 3:
            done, self.name['x'] = self.name['slide'].update(self.name['x'])
            if done:
                self.name['flags'] &= ~1
                if self.name['flags'] & 2:
                    self.name = None

    def _msgbox_render(self, held, pressed):
        if self.dialog_flags & 3:
            done, self.y = self.slide.update(self.y)
            if done:
                self.dialog_flags &= ~1
                if self.dialog_flags & 2:
                    self.y = BOX_Y                 # cfg restored 0x80047038-0x80047050
                    self._dialog_closed()
                    return
            self._render_text()
            return
        if self.typing_done:
            self._process_close(pressed)
            self._render_text()
            return
        if self.scroll_pending:
            self._scroll_text(pressed)             # draws itself; RenderText skipped (0x80047094)
            return
        self._interpreter(held, pressed)
        self._render_text()

    def _dialog_closed(self):                  # 0x8004501C
        self.dialog_flags = 0
        self.ctrl &= ~0x18
        self.cursor_img = None
        self.notes.append('DialogClosed')

    def _render_text(self):                    # 0x800455B4 (only the cursor animation matters for timing)
        if self.cursor_shown:
            self.cursor_tick += 1
            if self.cursor_tick >= 40:
                self.cursor_tick = 0
            self.cursor_img = self.cursor_tick // 10
        else:
            self.cursor_img = None

    def _process_close(self, pressed):         # 0x80045E60
        go = False
        if self.close_mode & 2:
            go = bool(pressed)
        if self.close_mode & 1:
            self.close_timer -= 1
            if self.close_timer == 0:
                go = True
        if self.close_mode & 4 and self.script_close_req == 1:
            go = True
            self.script_close_req = 0
        if go:
            self.dialog_flags |= 2
            self.sfx.append(SFX_CLOSE)
            self.notes.append('close-trigger')
            if self.name is not None and self.name['flags'] & 4:      # 0x80059FE0
                self.name['flags'] = 6
                self.name['slide'] = Slide(NAME_X, NAME_X_OUT)
            self.slide = Slide(BOX_Y, BOX_Y_OUT)                       # 0x80045F10-0x80045FCC

    def _scroll_text(self, pressed):           # 0x80045988
        if self.scroll_count == SCROLL_LEN:
            start = False
            if self.scroll_mode & 8:
                start = True
                self.scroll_mode &= ~8
            if self.scroll_mode & 2 and pressed:
                start = True
            if self.scroll_mode & 1:
                self.scroll_wait -= 1
                if self.scroll_wait == 0:
                    start = True
            if self.scroll_mode & 4 and self.auto_adv2 == 1:
                start = True
                self.auto_adv2 = 0
            if not start:
                self.cursor_shown = 0
                self.cursor_img = None
                self.scroll_px = 0
                return
        self.scroll_count -= 1
        self.scroll_px = (SCROLL_LEN - self.scroll_count) * 16 // SCROLL_LEN
        self.cursor_img = None
        if self.scroll_count == 0:
            self.scroll_pending = 0
            self.buf_x = (self.buf_x + 1) % 3
            band = (self.buf_x + self.line_index) % 3
            self.line_width[band] = 0
            self.bands[band] = []               # VRAM band cleared (0x80045E30)
            self.notes.append('scroll-done')

    def _interpreter(self, held, pressed):     # 0x80045FE0
        if self.text_flags & 8:                # \A wait
            if pressed:                         # pad.justPressed & 0x80
                self.cursor_shown = 0
                self.text_flags &= ~8
                self.scroll_mode |= 8
                self.notes.append('A-release')
                self._newline()
            return
        gate = False
        if self.text_flags & 2:
            self.delay -= 1
            if self.delay == 0:
                self.delay = self.delay_reset
                gate = True
        if self.text_flags & 1 and held:
            gate = True
        if self.text_flags & 4 and self.auto_adv == 1:
            gate = True
            self.auto_adv = 0
        if gate:
            self._step()

    def _band(self):
        return (self.buf_x + self.line_index) % 3

    def _draw(self, g, counted):
        self.bands[self._band()].append(g)
        self.line_start_x += WIDTHS[g]
        self.glyph_log.append((self.frame, g))
        if counted:                             # 0x80046DE8-0x80046E30
            if (self.render_step & 1) == 0 and self.voice != 4 and self.voice >= 0:
                self.sfx.append(SFX_VOICE0 + self.voice)
            self.render_step += 1

    def _newline(self):                        # 0x80046E34 with s7 = 1
        self.render_step = 0
        self.line_start_x = 0
        if self.line_index == 2:
            self.scroll_pending = 1
            self.scroll_count = SCROLL_LEN
            if self.scroll_mode & 1:
                self.scroll_wait = SCROLL_WAIT
            self.notes.append('scroll-armed')
        else:
            self.line_index += 1

    def _insert(self, s):
        self.buf[self.cur:self.cur] = s

    def _step(self):
        """One step: zero-cost codes then exactly one of: glyph, \\N, \\A, \\T, \\Y, end of text."""
        b = self.buf
        while True:
            c0 = b[self.cur]
            if c0 == 0:                                             # 0x8004611C
                self.typing_done = 1
                if self.close_mode & 1:
                    self.close_timer = CLOSE_TIMER
                self.notes.append('typing-done')
                return
            if c0 == 0x0A:                                          # 0x80046154
                self.cur += 1
                continue
            if c0 in (0x7B, 0x7D):                                  # 0x80046168 / 0x800461EC
                g = (b[self.cur + 1] + (0x50 if c0 == 0x7B else 0x90)) & 0xFF
                self.cur += 2
                self._draw(g, counted=False)
                return
            if c0 != 0x5C:
                self._normal()
                return
            self.cur += 1                                           # 0x80046278
            c = b[self.cur]
            k = c - 0x30
            if not (0 <= k < 0x2A):
                self._normal()                                      # 0x80046CCC: renders c
                return
            ch = chr(c)
            if ch.isdigit():                                        # 0x80046B9C: temp flag, no step
                j = self.cur
                while 0x30 <= b[j] <= 0x39:
                    j += 1
                n = int(b[self.cur:j].decode())
                self.cur = j
                self.temp_flags.add(n)
                self.flags_set.append(n)
                continue
            if ch == 'A':                                           # 0x80046A80
                self.cursor_shown = 1
                self.text_flags |= 8
                self.cur += 1
                self.notes.append('A-wait')
                return
            if ch in VOICE_CODES:                                   # 0x80046AB0-0x80046B38
                self.voice = VOICE_CODES[ch]
                self.cur += 1
                continue
            if ch == 'H':                                           # 0x800469F0
                self.cur += 1
                self.line_width[self._band()] = calc_text_width(b, self.cur)
                continue
            if ch == 'M':                                           # 0x80046B3C: "\MCE" -> textFlags = 4
                self.cur += 1
                if b[self.cur] == 0x43:
                    self.cur += 1
                    if b[self.cur] == 0x45:
                        self.cur += 1
                        self.text_flags = 4
                continue
            if ch == 'N':                                           # 0x80046A60
                self.cur += 1
                self._newline()
                return
            if ch == 'T':                                           # 0x800469C4
                self.cur += 1
                self.delay = self.delay_reset * 2
                return
            if ch == 'V':                                           # 0x800463D0: decimal of 0x80191908[d] inserted
                d = b[self.cur + 1] - 0x30
                self.cur += 2
                self._insert(str(self.vars.get(('V', d), 0)).encode())
                continue
            if ch == 'W':                                           # 0x800462B8: glyph, no parity, no blip
                self.cur += 1
                a = b[self.cur]
                g = (a - 0x20) & 0xFF if a < 0x41 else (a + 0xD9) & 0xFF
                self.cur += 1
                self._draw(g, counted=False)
                return
            if ch == 'X':                                           # 0x800464F4 (table 0x80024050)
                self.cur += 1
                d = b[self.cur] - 0x30
                if not (0 <= d < 6):
                    continue                                        # the digit is then typed as a char
                self.cur += 1
                self._insert(str(self.vars.get(('X', d), '')).encode())
                continue
            if ch == 'Y':                                           # 0x800463B8
                self.cur += 1
                return
            self._normal()                                          # ':'..'@', I-L, O-S, U
            return

    def _normal(self):                                              # 0x80046CCC
        b = self.buf
        code = (b[self.cur] << 8) | b[self.cur + 1]
        if kanji_valid(code):
            raise NotImplementedError('byte pair 0x%04X may take the Kanji Error path (2 bytes, glyph "?")' % code)
        g = b[self.cur]
        self.cur += 1
        self._draw(g, counted=True)

    # ------------------------------------------------------------ observation helpers
    def visible_rows(self):
        return [''.join(glyph_char(g) for g in self.bands[(self.buf_x + r) % 3]) for r in range(3)]

    def phase(self):
        f = self.dialog_flags
        if not f & 4:
            return 'closed'
        if f & 2:
            return 'slide-out'
        if f & 1:
            return 'slide-in'
        if self.typing_done:
            return 'typed'
        if self.scroll_pending:
            return 'scroll' if self.scroll_count != SCROLL_LEN else 'scroll-wait'
        if self.text_flags & 8:
            return 'wait-A'
        return 'typing'


# ---------------------------------------------------------------- script co-simulation
class Ctx:
    """What an event script sees. Generators yield to end their tick (opcode returned 0, or 0x00 Break)."""

    def __init__(self, box):
        self.box = box
        self.frame = 0
        self.events = []

    def ev(self, what):
        self.events.append((self.frame, what))

    def open(self, text, mode, name=False, label='open'):     # 0x0D / 0x5C / 0xC4: retry while a box is active
        while not self.box.try_open(text, mode, name):
            yield
        self.ev(label)

    def wait39(self):                                           # 0x8003E464
        while self.box.is_active():
            yield
        self.ev('0x39 released')

    def wait36(self, flag):                                     # 0x8003E35C (temp flag when bit 0x8000)
        while flag not in self.box.temp_flags:
            yield
        self.ev('0x36 T%d' % flag)

    def wait37(self, n):                                        # 0x8003E3DC: advances in tick T0+n+1
        count = 0
        yield
        while True:
            old = count
            count += 1
            if old >= n:
                return
            yield

    def brk(self):                                              # opcode 0x00 (0x800421F4): pc+1 and yield
        yield

    def hold(self, k):                                          # stands for a physics wait (0x24, 0x0B...): k extra ticks
        for _ in range(k):
            yield

    def op(self, name, *a):
        getattr(self.box, 'op' + name)(*a)
        self.ev('0x%s%s' % (name, ''.join(' %d' % x for x in a)))


def run(script, pad, frames=3000, open_frame=0, stop_after_close=None):
    """script(ctx) -> generator. pad(f, box) -> (held, pressed) as seen by the box in frame f.
    Returns (rows, ctx) with one dict per frame."""
    box = TextBox()
    ctx = Ctx(box)
    gen = script(ctx)
    alive = True
    rows = []
    for f in range(open_frame, open_frame + frames):
        box.frame = f
        ctx.frame = f
        box.sfx, box.flags_set, box.notes = [], [], []
        held, pressed = pad(f, box)
        before = len(box.glyph_log)
        box.render(held, pressed)
        drawn = len(box.glyph_log) - before
        if alive:
            try:
                next(gen)
            except StopIteration:
                alive = False
        rows.append(dict(frame=f, phase=box.phase(), y=box.y, name_x=None if box.name is None else box.name['x'],
                         rows=box.visible_rows(), scroll_px=box.scroll_px, cursor=box.cursor_img,
                         sfx=list(box.sfx), flags=list(box.flags_set), notes=list(box.notes),
                         text_flags=box.text_flags, close_mode=box.close_mode, ctrl=box.ctrl,
                         glyphs=len(box.glyph_log), drawn=drawn, pad=(held, pressed)))
        if not alive and not box.is_active() and box.name is None:
            break
    return rows, ctx


def summary(rows, ctx, nth_open=1):
    """Frames of the nth box (1-based): open, first glyph, last glyph, typing done, close trigger, closed, 0x39."""
    opens = [f for f, e in ctx.events if e.startswith('open')]
    n0 = opens[nth_open - 1]
    n1 = opens[nth_open] if nth_open < len(opens) else None
    seg = [r for r in rows if r['frame'] > n0 and (n1 is None or r['frame'] <= n1)]
    drawn = [r['frame'] for r in seg for _ in range(r['drawn'])]
    first = drawn[0] if drawn else None
    last = drawn[-1] if drawn else None
    done = next((r['frame'] for r in seg if 'typing-done' in r['notes']), None)
    trig = next((r['frame'] for r in seg if 'close-trigger' in r['notes']), None)
    closed = next((r['frame'] for r in seg if 'DialogClosed' in r['notes']), None)
    rel = next((f for f, e in ctx.events if e == '0x39 released' and f > n0 and (n1 is None or f <= n1)), None)
    blips = sum(1 for r in seg for s in r['sfx'] if s >= SFX_VOICE0)
    return dict(open=n0, first_glyph=first, last_glyph=last, typing_done=done, close_trigger=trig,
                dialog_closed=closed, release_0x39=rel, glyphs=len(drawn), blips=blips)


# ---------------------------------------------------------------- pads
def pad_every_frame(f, box):
    """DLL arc pad (AlundraArcSupport.cs:184-187 + RunUntilPressingTheButtonOnEveryDialogueFrame): held and
    just-pressed on every frame. Not producible by the real pad (0x8002E250 makes justPressed an edge)."""
    return True, True


def pad_none(f, box):
    return False, False


def pad_held_from(f0):
    """Square held from frame f0 on, just-pressed only at f0 (what an edge-deriving TickPad gives for a held button)."""
    return lambda f, box: (f >= f0, f == f0)


class PadOncePerPage:
    """One single-frame press (held+pressed) per page, at the earliest frame it can act: the frame after a \\A
    cursor appears, and the frame after the end of typing (first ProcessClose call)."""

    def __init__(self):
        self.done_for = set()

    def __call__(self, f, box):
        if not box.is_active() or box.dialog_flags & 3:
            return False, False
        key = None
        if box.text_flags & 8 and not box.scroll_pending:
            key = ('A', id(box.buf), box.cur)
        elif box.typing_done:
            key = ('close', id(box.buf))
        if key is not None and key not in self.done_for:
            self.done_for.add(key)
            return True, True
        return False, False


# ---------------------------------------------------------------- real texts and scripts
REPO = r'D:\development\repo\alundra-casaengine-project-converter'


def map_string(mid, idx):
    p = os.path.join(REPO, 'data-extracted', 'data', 'map_%d.json' % mid)
    return text_bytes(json.load(open(p, encoding='utf-8'))['Strings'][idx])


def script_389_sailor12(choice_ticks):
    """Map 389, C[12] from @1390 (first visit): 0x0D [0x81,1]; 0x50 4; 0x36 T999; 0x44; 0x51; 0x03; 0x0D [0x82/0x83,1]..0x39.
    choice_ticks = ticks between the 0x44 first entry and its resolution (choice box: E19.f3, not modelled here)."""
    s1, s2 = map_string(389, 1), map_string(389, 2)

    def g(ctx):
        yield from ctx.open(s1, 1, label='open S001')
        ctx.op('50', 4)
        yield from ctx.wait36(999)
        ctx.ev('0x44 first entry')
        yield from ctx.hold(choice_ticks)          # 0x44 returns 0 until resolved
        ctx.ev('0x44 resolved')
        ctx.op('51')
        yield from ctx.open(s2, 1, label='open S002')   # 0x03 -> 0x0D [130,1] (OUI branch), retried while active
        yield from ctx.wait39()
    return g


def script_391_ship_block():
    """Map 391 @295-@334 with the box subroutine @706-@749 (0x4C 2, 0x50 4, poll T999, 0x37 60, 0x51, 0x39, 0x37 30)."""
    texts = [map_string(391, 19), map_string(391, 20), map_string(391, 22)]

    def box_sub(ctx):                                 # @706
        ctx.op('4C', 2)
        ctx.op('50', 4)
        while 999 not in ctx.box.temp_flags:          # @710 0x30 T999 -> 737
            yield                                     # @715 0x00
            # @716 0x31 T1000 off -> 710 ; else 0x8E, 0x5A, 0x5B (no yield), goto 710
        yield from ctx.wait37(60)                     # @737
        ctx.op('51')                                  # @739
        yield from ctx.wait39()                       # @740
        ctx.box.temp_flags.discard(999)               # @741 0x06 T999
        ctx.box.temp_flags.discard(1000)              # @744 0x06 T1000
        yield from ctx.wait37(30)                     # @747, then 0x7D returns

    def g(ctx):
        yield from ctx.open(texts[0], 1, label='open S019')   # @295 0x5C [1,147,1], 0x78 -> 706
        yield from box_sub(ctx)
        yield from ctx.brk()                                   # @302
        yield from ctx.open(texts[1], 1, label='open S020')   # @303 0x59, @306 0x5C [4,148,1]
        yield from box_sub(ctx)
        yield from ctx.brk()                                   # @313
        yield from ctx.wait37(30)                              # @314
        yield from ctx.wait37(15)                              # @316 0x5A, @319
        yield from ctx.wait37(30)                              # @321 0x5E, @325
        yield from ctx.open(texts[2], 1, label='open S022')   # @327 0x5C [6,150,1]
        yield from box_sub(ctx)
    return g


def script_164_septimus(k24a=0, k24b=0, k0b=0):
    """Map 164 @325-@404: 0x0D [0x83,1]; 0x36 T200; 0x4C 4; ...; 0x4D paced by 0x37; 0x4C 3; 0x39.
    k24a/k24b (0x24 at @341/@346) and k0b (0x0B at @386) are physics waits: extra ticks, 0 = advance at once."""
    s3 = map_string(164, 3)

    def g(ctx):
        yield from ctx.open(s3, 1, label='open S003')        # @325
        yield from ctx.wait36(200)                          # @328
        ctx.op('4C', 4)                                     # @331 (0x5B, 0x09, 0x1A: no yield)
        yield from ctx.hold(k24a)                           # @341 0x24
        yield from ctx.wait37(5)                            # @342 0x09, @344
        yield from ctx.hold(k24b)                           # @346 0x24 (0x1A @347)
        yield from ctx.brk()                                # @349
        ctx.op('4D')                                        # @350
        ctx.op('4C', 3)                                     # @351
        yield from ctx.wait36(201)                          # @353
        ctx.op('4C', 4)                                     # @356
        yield from ctx.wait37(15)                           # @358
        yield from ctx.brk()                                # @360
        ctx.op('4D')                                        # @361
        yield from ctx.wait37(2)                            # @362
        yield from ctx.wait37(5)                            # @364 0x08, @366
        yield from ctx.wait37(5)                            # @368 0x08, @370
        yield from ctx.brk()                                # @372
        for _ in range(4):                                  # @373 @376 @379 @382: 0x4D, 0x37 2
            ctx.op('4D')
            yield from ctx.wait37(2)
        yield from ctx.brk()                                # @385
        yield from ctx.hold(k0b)                            # @386 0x0B (0x1A @390)
        ctx.op('4D')                                        # @392
        yield from ctx.wait37(2)
        ctx.op('4D')                                        # @395
        yield from ctx.wait37(2)
        ctx.op('4D')                                        # @398
        yield from ctx.wait37(30)                           # @399
        yield from ctx.brk()                                # @401
        ctx.op('4C', 3)                                     # @402
        yield from ctx.wait39()                             # @404
    return g


def simple_script(text, mode=0, ops_same_tick=(), timed=None):
    """Generic: open, ops in the opening tick, then {relative tick: [ops]} and 0x39."""
    timed = timed or {}

    def g(ctx):
        yield from ctx.open(text, mode)
        n = ctx.frame
        for o in ops_same_tick:
            ctx.op(*o)
        released = False
        while True:
            rel = ctx.frame - n
            for o in timed.get(rel, ()):
                ctx.op(*o)
            if not released and not ctx.box.is_active():
                ctx.ev('0x39 released')
                released = True
            if released and rel >= max(timed, default=0):
                return
            yield
    return g


def selftest():
    """Hand-derived numbers (see notes.md) the model must reproduce."""
    # plain text "AB", default flags, no pad: glyphs at N+19, N+23; end at N+27; timer close at N+27+360; release +18
    rows, ctx = run(simple_script(b'AB'), pad_none, 1000)
    s = summary(rows, ctx)
    assert (s['first_glyph'], s['last_glyph'], s['typing_done']) == (19, 23, 27), s
    assert s['close_trigger'] == 27 + 360 and s['dialog_closed'] == 27 + 378 and s['release_0x39'] == 27 + 378, s
    ys = [r['y'] for r in rows[1:19]]
    assert ys == [240, 236, 231, 226, 221, 216, 212, 207, 202, 197, 192, 188, 183, 178, 173, 168, 168, 168], ys
    # every-frame pad: glyphs 19, 20; done 21; close 22; release 40
    rows, ctx = run(simple_script(b'AB'), pad_every_frame, 200)
    s = summary(rows, ctx)
    assert (s['first_glyph'], s['last_glyph'], s['typing_done'], s['close_trigger'], s['release_0x39']) == (19, 20, 21, 22, 40), s
    # \T: "A\TB" -> A 19, \T 23, B 31
    rows, ctx = run(simple_script(b'A\\TB'), pad_none, 100)
    gl = [r['frame'] for i, r in enumerate(rows) if i and r['glyphs'] != rows[i - 1]['glyphs']]
    assert gl == [19, 31], gl
    # 4th line: "a\Nb\Nc\Nd": a19 \N23 b27 \N31 c35 \N39 (3rd line: scroll armed) -> wait 40..49, scroll 49..56, d at 60
    rows, ctx = run(simple_script(b'a\\Nb\\Nc\\Nd'), pad_none, 200)
    gl = [r['frame'] for i, r in enumerate(rows) if i and r['glyphs'] != rows[i - 1]['glyphs']]
    assert gl == [19, 27, 35, 60], gl
    sp = [(r['frame'], r['scroll_px']) for r in rows if r['scroll_px']]
    assert sp == [(49 + k, 2 * (k + 1)) for k in range(8)], sp
    # stale scrollMode bit 8: "a\Ab\Nc\Nd", one press at 24 releases \A on line 0; the later 3rd-line scroll then
    # starts at once (41..48) instead of after 10 frames; d at 52 instead of 61
    press24 = lambda f, box: (f == 24, f == 24)
    rows, ctx = run(simple_script(b'a\\Ab\\Nc\\Nd'), press24, 200)
    gl = [r['frame'] for i, r in enumerate(rows) if i and r['glyphs'] != rows[i - 1]['glyphs']]
    assert gl == [19, 28, 36, 52], gl
    assert [(r['frame'], r['cursor']) for r in rows if r['cursor'] is not None] == [(23, 0)]
    # \N\A leaves an empty line: "a\N\Ab" -> rows a / '' / b
    rows, ctx = run(simple_script(b'a\\N\\Ab'), lambda f, box: (f == 28, f == 28), 100)
    assert rows[40]['rows'] == ['a', '', 'b'], rows[40]['rows']
    # 0x4C 4 in the opening tick: nothing typed until a 0x4D; a 0x4D during the slide is latched (one step at N+19)
    rows, ctx = run(simple_script(b'ABC', ops_same_tick=[('4C', 4), ('4D',)], timed={5: [('4D',)], 30: [('4D',)]}),
                    pad_every_frame, 100)
    gl = [r['frame'] for i, r in enumerate(rows) if i and r['glyphs'] != rows[i - 1]['glyphs']]
    assert gl == [19, 31], gl
    # cursor animation: 9 frames of image 0 on the first cycle, then 10 per image
    rows, ctx = run(simple_script(b'a\\Ab'), pad_none, 120)
    imgs = [r['cursor'] for r in rows if r['cursor'] is not None][:50]
    assert imgs == [0] * 9 + [1] * 10 + [2] * 10 + [3] * 10 + [0] * 10 + [1], imgs
    return True


if __name__ == '__main__':
    print('selftest', selftest())
