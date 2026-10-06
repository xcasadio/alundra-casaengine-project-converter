"""Verifier's own driver of the REAL binary code (own harness, shared helpers emu/drive/slotorder from the f3c proof).
Frame f = RenderScene (dispatcher: slots 0..12 ascending) then Update(0) (first call: REAL pad sampler 0x8002E38C, with the BIOS PadRead 0x8008A688 hooked
to return the PHYSICAL word of frame f). Nothing is shifted by hand."""
import sys
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, SP + r"\save-order-disc")
sys.path.insert(0, SP + r"\e19j-disc")
import emu, drive, slotorder as so

CROSS, RIGHT, SQUARE, DOWN, UP = drive.CROSS, drive.RIGHT, drive.SQUARE, drive.DOWN, drive.UP
PADREAD = 0x8008A688
SAMPLER = 0x8002E38C
FLAGS10, RESULT10 = so.FLAGS10, so.RESULT10


def run(phys, frames, trace=False):
    m = so.build()
    cur = {'f': 0}
    m.hooks[PADREAD] = lambda mm: mm.__setitem__('_dummy', 0) if False else _pr(mm, cur, phys)
    rows = []
    for f in range(1, frames + 1):
        cur['f'] = f
        log = []
        m.log.clear()
        seen = drive.pad_words(m)          # what RenderScene reads this frame
        so.dispatcher(m, log)
        snd = [e[1] for e in m.log if e[0] == 'sound']
        m.call(SAMPLER)                     # Update(0)'s first call
        rows.append(dict(f=f, seen=seen, flags10=m.rw(FLAGS10), result10=m.rw(RESULT10), snd=snd, log=log))
    return m, rows


def _pr(m, cur, phys):
    m.r[2] = phys(cur['f']) & 0xFFFF


def summarize(rows, P):
    opened = None
    taken = None
    for r in rows:
        if opened is None and (r['flags10'] & 8):
            opened = r['f']
        if opened is not None and taken is None and r['f'] > opened and (r['flags10'] & 8) == 0:
            taken = r['f']
    return opened, taken


if __name__ == '__main__':
    P = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    print("P =", P)
    for q in range(10, 26):
        phys = lambda f, q=q: CROSS if f in (P, P + q) else 0
        m, rows = run(phys, P + 90)
        o, t = summarize(rows, P)
        res = [r['result10'] for r in rows if r['f'] == (t or 0)]
        print("Cross at P and P+%2d: question opened in frame %s (P+%s), slot-10 flag-8 cleared in frame %s (P+%s)  results=%s" % (
            q, o, None if o is None else o - P, t, None if t is None else t - P, [hex(x) for x in res]))
