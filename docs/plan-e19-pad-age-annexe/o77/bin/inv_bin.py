"""O-E19-77: the REAL binary code of the main inventory (trigger block of Update(0) 0x8002BC54-0x8002BD00, DisplayInventory 0x80055570,
OpenSlot 0x80047F94, the slot-6 setup 0x80054F1C, the per-frame 0x80056598, the close setup 0x800556DC) and of the sub-inventory
(OpenSubInventory 0x80052618, init 0x800537F0, per-frame 0x80053328, close setup 0x800526CC), driven in the binary's frame order:
frame f = RenderScene(f) [the REAL dispatcher 0x80048054: slots 0..12 then the post-process tail] then Update(0)(f) [the REAL sampler
0x8002E38C (BIOS PadRead hooked to the PHYSICAL word of frame f) then the REAL trigger block]. UpdateWorld (0x8002E058) and the soft-reset
block are skipped (no world). Stubs (logged no-ops): drawing, portrait, HUD, icon names, equip, text reveal. Nothing is shifted by hand."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emu

PAD = 0x80126F18
PADREAD, SAMPLER = 0x8008A688, 0x8002E38C
DISPATCHER = 0x80048054
TRIG_BLOCK, TRIG_END = 0x8002BC54, 0x8002BD04
FLAG6, SEL6 = 0x8017FEF4, 0x8017FF28
FLAG4, SEL4 = 0x8017F340, 0x8017F734
CTRL = 0x800DC4B8            # g_playerControlFlags, MenuOpen = 8
POST = 0x80153194            # g_postProcessState
CDREADY = 0x800A82B0         # g_cdIsReady (1 in play, 0x8004E85C)
SLOT = lambda s: 0x80153028 + 28 * s

CROSS, CIRCLE, SQUARE, TRIANGLE = 0x40, 0x20, 0x80, 0x10
UP, RIGHT, DOWN, LEFT = 0x1000, 0x2000, 0x4000, 0x8000
L2, R2, L1, R1, SELECT, START = 0x1, 0x2, 0x4, 0x8, 0x100, 0x800

STUBS = {0x8004BE0C: 'hud_before_hide', 0x80050908: 'draw', 0x80050A74: 'draw', 0x80055D78: 'draw', 0x80055FE8: 'text6',
         0x800562DC: 'draw', 0x80056A98: 'draw', 0x80056FB4: 'draw', 0x80057854: 'equip_item', 0x8005795C: 'equip_weapon',
         0x80057B84: 'portrait_return', 0x80052C64: 'draw', 0x80052DD8: 'draw', 0x80052FB4: 'draw', 0x80053144: 'draw',
         0x80053270: 'draw', 0x80053FDC: 'text4', 0x80054388: 'draw', 0x800859C8: 'lib', 0x80085A70: 'lib', 0x8004BD9C: 'hud_init',
         0x80057B40: 'anim_image', 0x80057C18: 'portrait_start', 0x80055C84: 'icon_names', 0x80052F24: 'subinv_name',
         0x8004BE00: 'hud', 0x8005ABE0: 'x', 0x800506AC: 'DEAD_down', 0x80051218: 'DEAD_left', 0x80051F1C: 'DEAD_right'}
QUIET = {'draw', 'lib', 'text6', 'text4', 'anim_image', 'subinv_name'}


def build():
    m = emu.make_machine()
    for a, name in STUBS.items():
        m.hooks[a] = (lambda nm: (lambda mm: (None if nm in QUIET else mm.log.append(('stub', nm)), mm.r.__setitem__(2, 0))))(name)
    m.ww(PAD, 20); m.ww(PAD + 4, 0)
    for o in (0x10, 0x12, 0x14, 0x16): m.wh(PAD + o, 0)
    m.ww(CDREADY, 1)
    for a in (FLAG6, SEL6, FLAG4, SEL4, CTRL, POST, 0x80127D50, 0x80127164, 0x800DC4B4, 0x800C4980, 0x801EB424): m.ww(a, 0)
    m.ww(0x801EB410, 0x40000000)   # dispatcher head: skip the 0x8004BD9C branch (stubbed anyway)
    for s in range(13): m.wh(SLOT(s), 0)
    return m


def frame(m, phys_word):
    m.log.clear()
    pre = dict(pressed=m.rh(PAD + 0x12), interval=m.rh(PAD + 0x16), hold=m.rh(PAD + 0x10))   # what RenderScene reads
    m.call(DISPATCHER)
    rs_log = list(m.log); m.log.clear()
    after_rs = dict(flag6=m.rw(FLAG6), sel6=m.rw(SEL6), flag4=m.rw(FLAG4), sel4=m.rw(SEL4), ctrl=m.rw(CTRL), post=m.rw(POST),
                    slot4=m.rh(SLOT(4)) & 1, slot6=m.rh(SLOT(6)) & 1, upd6=m.rw(SLOT(6) + 0x14))
    m.hooks[PADREAD] = lambda mm: mm.r.__setitem__(2, phys_word & 0xFFFF)
    m.call(SAMPLER)
    smp = dict(pressed=m.rh(PAD + 0x12), interval=m.rh(PAD + 0x16), hold=m.rh(PAD + 0x10))
    def stop(mm): mm.r[31] = emu.SENTINEL
    m.hooks[TRIG_END] = stop
    m.call(TRIG_BLOCK)
    del m.hooks[TRIG_END]
    up_log = list(m.log)
    after_up = dict(flag6=m.rw(FLAG6), ctrl=m.rw(CTRL), slot6=m.rh(SLOT(6)) & 1, upd6=m.rw(SLOT(6) + 0x14))
    return dict(rs_seen=pre, rs_log=rs_log, rs=after_rs, sample=smp, up_log=up_log, up=after_up)


def run(phys, frames):
    m = build()
    return [dict(f=f, **frame(m, phys(f))) for f in range(1, frames + 1)]


def fmt(r):
    ev = lambda lg: ','.join(('snd%d' % e[1]) if e[0] == 'sound' else e[1] for e in lg)
    return ('f%3d RS(seen p%04x i%04x)[%s] flag6=%d sel6=%2d flag4=%d sel4=%2d post=%d ctrl=%x s4=%d s6=%d upd6=%08x | '
            'UP(sample p%04x i%04x)[%s] flag6=%d ctrl=%x s6=%d') % (
        r['f'], r['rs_seen']['pressed'], r['rs_seen']['interval'], ev(r['rs_log']), r['rs']['flag6'], r['rs']['sel6'], r['rs']['flag4'],
        r['rs']['sel4'], r['rs']['post'], r['rs']['ctrl'], r['rs']['slot4'], r['rs']['slot6'], r['rs']['upd6'],
        r['sample']['pressed'], r['sample']['interval'], ev(r['up_log']), r['up']['flag6'], r['up']['ctrl'], r['up']['slot6'])


if __name__ == '__main__':
    P = 5
    phys = lambda f: START if f == P else 0
    for r in run(phys, 30): print(fmt(r))
