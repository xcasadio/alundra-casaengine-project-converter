"""D-E19-99: slot 0's Square readers (the text box, UIManager update 0x80046EF0 -> 0x80045E60 / 0x80045988 / 0x80045FE0), real code of 0x80045E60 (the box that waits for a press:
Square pressed = lbu 0x80126F2A >> 7), in the binary frame order (RenderScene then Update(0) = the real pad sampler fed by a PHYSICAL schedule)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu, drive
import frameorder as fo

SQUARE = drive.SQUARE
FLAGS = 0x80149CBC           # lui 0x8015 ; lw -0x6344 : bit 2 = the box waits for Square


def run(phys, frames):
    m = drive.new_machine()
    m.ww(FLAGS, 2)
    st = {'f': 0}
    m.hooks[fo.PADREAD] = lambda mm: mm.__setitem__(2, 0) if False else _pr(mm, st, phys)
    for a in (0x80059FE0, 0x80057B84):
        m.hooks[a] = lambda mm: None
    sounds = []
    m.hooks[0x800490FC] = lambda mm: sounds.append((st['f'], mm.r[4]))
    for f in range(1, frames + 1):
        st['f'] = f
        try:
            m.call(0x80045E60)         # RenderScene -> slot 0 -> the Square reader
        except RuntimeError as e:
            print('stopped in frame', f, e)
            break
        m.call(fo.SAMPLER)             # Update(0)
    return sounds


def _pr(mm, st, phys):
    mm.r[2] = phys(st['f']) & 0xFFFF


if __name__ == '__main__':
    P = 7
    s = run(lambda f: SQUARE if f == P else 0, 12)
    print('slot 0 box waiting for Square (flag bit 2), physical Square at frame %d: close sound 7 played in frames %s -> P+%s' % (P, [f for f, n in s], [f - P for f, n in s]))
