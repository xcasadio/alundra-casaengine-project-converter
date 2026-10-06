"""E19.f4 reference model: the speaker NAME BOX and the speaker PORTRAIT of the dialogue (ALUN_CD.EXE, France).

`model.py` (next to this file) is an unmodified copy of the f2a/f3 text-box model; this file adds the two
satellites of the text box.  Every rule is read from the binary (addresses in comments) and validated against the
REAL binary code run in a MIPS interpreter by `../validate.py`.

Frame = one main-loop iteration: RenderScene (UI dispatcher slots 0..12 = text box first, name box last; then the
OT build 0x80044C5C which runs the portrait pass through 0x80058134) then Update(0) (scripts).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import model as M
from model import TextBox, Slide, _tdiv, calc_text_width

# ------------------------------------------------------------------------------------------------ constants
NAME_X, NAME_X_OUT, NAME_Y = 64, 320, 140          # cfg 0x800A58BC {64, 140, 14 x 4 cells}; slide start x 0x140 (0x8005A290)
NAME_W = 14 * 8                                    # 112
NAME_TEXT_Y = 148                                  # cfg.y + slot[+0xA] (8)                       0x8005A50C-0x8005A534
NAME_CLIP = (16, 8, 258, 34)                       # (dx, dy, w, h) of the name text clip                0x8005A5B8-0x8005A6E8
PORTRAIT_REST = (8, 116)                           # 0x80057CAC (8) / 0x80057CB8 (0x74)
PORTRAIT_SIZE = (48, 56)                           # 0x80057E4C (0x30) / 0x80057E54 (0x38)
PORTRAIT_STEPS = 15                                # 0x80057E5C
FLAG_HAS_PORTRAIT = 0x800000                       # entity +0x6C bit 23 (0x8003D590)


class NameBox:
    """Slot 12 {cfg 0x800A58BC, init 0x8005A268, update 0x8005A3E0, layer 5}; globals 0x80180240 (flags, id at +0x48)."""

    def __init__(self, etc):
        self.etc = etc               # id -> bytes of ETC[id] (the ETC table 0x800C400C is runtime-loaded)
        self.flags = 0               # 0x80180240: 1 sliding in, 2 closing, 4 open
        self.slot = False            # runtime slot 12 flag bit 0 (set by OpenSlot 0x80047F94, cleared by 0x80047CB0)
        self.x = NAME_X              # cfg.x (the cfg is live data: UpdateUiBoxesPosition rewrites it)
        self.id = 0
        self.w = 0
        self.slide = None

    def open(self, id):              # 0x80059F6C
        if self.flags & 4:                                     # 0x80059F7C: already open (also while closing: 6)
            return False
        if not (0x100 <= id < 0x200):                          # 0x80059F88
            return False
        s = self.etc.get(id)                                   # 0x80059F94-0x80059FBC: pointer != 0 and first byte != 0
        if not s:
            return False
        self.id = id                                           # 0x80059FC4
        self.slot = True                                       # OpenSlot(12) 0x80059FC8 -> init 0x8005A268 at once
        self.flags = 5                                         # 0x8005A380
        self.slide = Slide(NAME_X_OUT, NAME_X)                 # 0x8005A280-0x8005A350 (cfg.x NOT written by the init)
        self.w = calc_text_width(s + b'\0', 0)                 # CalcTextWidth(ETC[id]) is re-run each pass: constant
        return True

    def close(self):                 # 0x80059FE0 (called by 0x80045F00 at the text box close trigger)
        if self.flags & 4:
            self.flags = 6
        # the rest is unconditional in the binary: the slide block is rewritten (start = current cfg.x)
        self.slide = Slide(self.x, NAME_X_OUT)

    def pass_(self):                 # 0x8005A3E0, dispatcher slot 12 (after the text box pass of the same frame)
        if not self.slot:
            return None
        if self.flags & 3:
            done, self.x = self.slide.update(self.x)
            if done:
                if self.flags & 1:
                    self.flags &= ~1
                if self.flags & 2:                             # 0x8005A44C: closing complete
                    self.x = NAME_X                            # cfg restored from the origin (0x8005A470-0x8005A484)
                    self.flags = 0                             # 0x8005A244 (+ CloseSlot 0x80047CB0)
                    self.slot = False
                    return None                                # nothing drawn
        tx = self.x + _tdiv(NAME_W - self.w, 2)                # 0x8005A4C4-0x8005A508
        cx = self.x + NAME_CLIP[0]
        cw = NAME_CLIP[2]
        if cx >= 320:
            cx = 320                                           # 0x8005A630-0x8005A648
        # clip width: (slot[+0xC] * 8 + 2) = 258, reduced when it passes the screen edge (0x8005A654-0x8005A674)
        if cx + cw >= 320:
            cw = 320 - cx
        return dict(frame=(self.x, NAME_Y), text=(tx, NAME_TEXT_Y), clip=(cx, NAME_TEXT_Y, cw, NAME_CLIP[3]))


class Portrait:
    """Portrait block 0x80180070: +0 state (hw), +0x68.. flight; prims at +4 (two display buffers of 0x28 bytes)."""

    def __init__(self):
        self.state = 0               # 0 off, 5 flying in / settling (bit 0), 4 at rest, 2 flying back (bit 1)
        self.c = 0                   # 0x80180070 + 0x80: counter, 15 at the start of a flight
        self.dst = (0, 0)            # +0x70/+0x74
        self.d = (0, 0)              # +0x78/+0x7C
        self.start = (0, 0)          # +0x68/+0x6C

    @staticmethod
    def screen(e, cam):              # 0x80057E08-0x80057E84 (entity halfwords at +0x116, +0x11A, +0x11E; camera 0x800E4328/C)
        return (e['x'] - cam[0], e['y'] - cam[1] - e['z'] - 0x20)

    def open(self, e, cam):          # 0x80057C84 -> 0x80057CF0 ; ignored while state != 0 (0x80057D30)
        if self.state != 0:
            return False
        sx, sy = self.screen(e, cam)
        self.start = (sx, sy)
        self.dst = PORTRAIT_REST
        self.d = (sx - PORTRAIT_REST[0], sy - PORTRAIT_REST[1])
        self.c = PORTRAIT_STEPS
        self.state = 5
        return True

    def close(self, e, cam):         # 0x80057B84 (called at the text box close trigger)
        if self.state == 0:
            return False
        sx, sy = self.screen(e, cam)
        self.start = PORTRAIT_REST                              # +0x68/+0x6C <- rest
        self.dst = (sx, sy)                                     # +0x70/+0x74 <- the entity NOW
        self.d = (PORTRAIT_REST[0] - sx, PORTRAIT_REST[1] - sy)
        self.state = 2
        self.c = PORTRAIT_STEPS
        return True

    def pass_(self):                 # 0x80057EBC through 0x80058134 (OT build, after the dispatcher), state != 0 only
        if self.state == 0:
            return None
        c = self.c
        if c == 0:
            if self.state & 2:                                   # last closing pass: degenerate quad at the rest position
                self.state = 0
                return dict(x=PORTRAIT_REST[0], y=PORTRAIT_REST[1], w=0, h=0, rgb=0, phase='gone')
            self.state &= ~1                                     # 5 -> 4
            return dict(x=PORTRAIT_REST[0], y=PORTRAIT_REST[1], w=PORTRAIT_SIZE[0], h=PORTRAIT_SIZE[1], rgb=0x80, phase='rest')
        x = self.dst[0] + _tdiv(self.d[0] * c, PORTRAIT_STEPS)
        y = self.dst[1] + _tdiv(self.d[1] * c, PORTRAIT_STEPS)
        w = h = 0
        rgb = 0
        phase = ''
        if self.state & 1:                                       # flying in: grows, 255 -> 135
            w = _tdiv(PORTRAIT_SIZE[0] * (PORTRAIT_STEPS - c), PORTRAIT_STEPS)
            h = _tdiv(PORTRAIT_SIZE[1] * (PORTRAIT_STEPS - c), PORTRAIT_STEPS)
            rgb = 0x7F + _tdiv(c << 7, PORTRAIT_STEPS)
            phase = 'in'
        elif self.state & 2:                                     # flying back: shrinks, 127 -> 246
            w = _tdiv(PORTRAIT_SIZE[0] * c, PORTRAIT_STEPS)
            h = _tdiv(PORTRAIT_SIZE[1] * c, PORTRAIT_STEPS)
            rgb = 0x7F + _tdiv((PORTRAIT_STEPS - c) << 7, PORTRAIT_STEPS)
            phase = 'out'
        self.c -= 1                                              # 0x8005809C-0x800580A8
        return dict(x=x, y=y, w=w, h=h, rgb=rgb, phase=phase)


class F4Box(TextBox):
    """The text box (f2a model) plus its name box and portrait.  `entity` dicts: x, y, z (integer parts of the
    16.16 position: halfwords at +0x116, +0x11A, +0x11E), flags (entity +0x6C), id (entity +0x68)."""

    def __init__(self, etc):
        super().__init__()
        self.nb = NameBox(etc)
        self.pt = Portrait()
        self.cam = (0, 0)
        self.speaker = None          # the entity the portrait pointers refer to (G+0x54..0x64, captured at open)
        self.attempts = []           # (frame, kind, result)

    # ---- the three opening opcodes (script phase) ------------------------------------------------------------
    def op_dialog(self, kind, entity, matched, text, mode, name_id=None):
        """kind 0x0D (entity = the script's own entity, always 'matched'), 0x5C (entity = first match of the search),
        0xC4 (same, name = explicit operand v2 | v3 << 8).  `matched` False = the search found nobody (0x8003C954 = 0).
        Returns what the opcode returns: True = the box opened (return 3 / 4 / 6), False = 0 (retry next tick)."""
        if kind == 0x0D or matched:
            if entity['flags'] & FLAG_HAS_PORTRAIT:                       # 0x8003D590 / 0x8003F060 / 0x80041DE8
                if self.pt.open(entity, self.cam):
                    self.speaker = entity
            nid = name_id if kind == 0xC4 else entity['id']              # 0x8003D63C / 0x8003F100 / 0x80041E8C
            self.nb.open(nid)
        ok = self.try_open(text, mode)                                    # TryOpenDialog 0x800423F8 (False while active)
        self.attempts.append((self.frame, kind, ok))
        return ok

    # ---- close trigger: name and portrait leave with the box (0x80045EF8-0x80045F08) ---------------------------------
    def _process_close(self, pressed):
        before = len(self.notes)
        super()._process_close(pressed)
        if 'close-trigger' in self.notes[before:]:
            self.nb.close()
            if self.pt.state != 0 and self.speaker is not None:
                self.pt.close(self.speaker, self.cam)

    def frame_passes(self, held, pressed):
        """RenderScene part of one frame: box pass (slot 0) then name box (slot 12) then portrait (OT build)."""
        self.render(held, pressed)
        return self.nb.pass_(), self.pt.pass_()
