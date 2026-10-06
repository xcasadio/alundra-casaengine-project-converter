"""E19.f4b design extension of the annex model (docs/plan-e19-f4-annexe/model/f4_model.py, NOT edited): the HEIGHT of the portrait image.

The binary draws every portrait as a 48 x 56 quad at rest (8, 116) whatever the image (a 48 x 72 image is squashed).  D-E19-49 shows the
48 x 72 images whole, bottom aligned: rest top = 172 - h.  The flight is the binary machine with its two constants made parameters:
    rest   = (8, 172 - h)                       (h = 56 -> (8, 116): the binary; h = 72 -> (8, 100))
    size   = (48, h) at rest; in: trunc(48 (15 - c) / 15) x trunc(h (15 - c) / 15); out: trunc(48 c / 15) x trunc(h c / 15)
    anchor = top-left; position = rest + trunc(span * c / 15) (in), head + trunc((rest - head) * c / 15) (out)
    rgb    = 127 + trunc(128 c / 15) (in) / 127 + trunc(128 (15 - c) / 15) (out); 128 at rest; 0 on the degenerate pass
With h = 56 every value equals the annex model (checked by check_regression.py against the annex values.json).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import model as M
import f4_model as F
from model import _tdiv

REST_X = F.PORTRAIT_REST[0]            # 8
REST_BOTTOM = F.PORTRAIT_REST[1] + F.PORTRAIT_SIZE[1]    # 172 (116 + 56)
WIDTH = F.PORTRAIT_SIZE[0]             # 48
STEPS = F.PORTRAIT_STEPS               # 15


def rest_of(h):
    return (REST_X, REST_BOTTOM - h)


class PortraitH(F.Portrait):
    """f4_model.Portrait with the image height h taken at the (accepted) open."""

    def __init__(self):
        super().__init__()
        self.h = F.PORTRAIT_SIZE[1]
        self.rest = F.PORTRAIT_REST

    def open(self, e, cam, h=56):                      # ignored while state != 0 (0x80057D30), the h of the ignored open is dropped
        if self.state != 0:
            return False
        self.h = h
        self.rest = rest_of(h)
        sx, sy = self.screen(e, cam)
        self.start = (sx, sy)
        self.dst = self.rest
        self.d = (sx - self.rest[0], sy - self.rest[1])
        self.c = STEPS
        self.state = 5
        return True

    def close(self, e, cam):                           # 0x80057B84, on the speaker accepted at the open
        if self.state == 0:
            return False
        sx, sy = self.screen(e, cam)
        self.start = self.rest
        self.dst = (sx, sy)
        self.d = (self.rest[0] - sx, self.rest[1] - sy)
        self.state = 2
        self.c = STEPS
        return True

    def pass_(self):
        if self.state == 0:
            return None
        c = self.c
        rx, ry = self.rest
        if c == 0:
            if self.state & 2:                          # the degenerate pass: invisible, at the (corrected) rest
                self.state = 0
                return dict(x=rx, y=ry, w=0, h=0, rgb=0, phase='gone')
            self.state &= ~1
            return dict(x=rx, y=ry, w=WIDTH, h=self.h, rgb=0x80, phase='rest')
        x = self.dst[0] + _tdiv(self.d[0] * c, STEPS)
        y = self.dst[1] + _tdiv(self.d[1] * c, STEPS)
        w = h = rgb = 0
        phase = ''
        if self.state & 1:
            w = _tdiv(WIDTH * (STEPS - c), STEPS)
            h = _tdiv(self.h * (STEPS - c), STEPS)
            rgb = 0x7F + _tdiv(c << 7, STEPS)
            phase = 'in'
        elif self.state & 2:
            w = _tdiv(WIDTH * c, STEPS)
            h = _tdiv(self.h * c, STEPS)
            rgb = 0x7F + _tdiv((STEPS - c) << 7, STEPS)
            phase = 'out'
        self.c -= 1
        return dict(x=x, y=y, w=w, h=h, rgb=rgb, phase=phase)


class F4BoxH(F.F4Box):
    """F4Box whose portrait machine knows the image height; entity dicts may carry 'h' (default 56)."""

    def __init__(self, etc):
        super().__init__(etc)
        self.pt = PortraitH()

    def op_dialog(self, kind, entity, matched, text, mode, name_id=None):
        if kind == 0x0D or matched:
            if entity['flags'] & F.FLAG_HAS_PORTRAIT:
                if self.pt.open(entity, self.cam, entity.get('h', 56)):
                    self.speaker = entity
            nid = name_id if kind == 0xC4 else entity['id']
            self.nb.open(nid)
        ok = self.try_open(text, mode)
        self.attempts.append((self.frame, kind, ok))
        return ok
