"""The whole chest sequence on the REAL code of ALUN_CD.EXE (France): F255 (0x8007FC88), then the spawned object's A255 (0x80061BD4) and C255 (0x8007C174), tick by tick,
with the copied MIPS interpreter. World stubs (python, journaled): SpawnWarpEntity, Block/Unblock, destroy-light, the message opener and its flag word, the CD, the sound, ApplyItem
(logged, not run), SetGameFlag. The texts are the REAL ETC strings of the export, placed in RAM, so strcpy/strcat/GetEtcText give the final message.
The animation of the chest (not run here: the entity animation engine is not in the machine) is a parameter: ANIM_TICKS ticks after F255 the chest's +0x88 becomes 1 (data: 61 ticks for the
direction 0, chain 2 -> 1, then the chest's own tick).  Hold times of the message box (the player's reading time) are parameters."""
import sys, json, io
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import f255_oracle as F
m = F.m
M32 = F.M32
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

etc = json.load(open('D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/ETC_RES.R.json', encoding='utf-8'))
ek = list(etc.keys())
def etc_text(i):
    k = i + 1
    return (etc[ek[k]] if k < len(ek) else None) or ''

ETC_BASE = 0x801C0000
NAME_BASE = 0x801D0000
BUF = 0x80191268

def put_cstr(addr, text):
    raw = text.replace('\\N', '\n').encode('cp1252', 'replace')
    for i, c in enumerate(raw + b'\0'):
        m.wb(addr + i, c)

for i in range(0, 0x100):
    put_cstr(ETC_BASE + 0x100 * i, etc_text(i))
for it in range(0, 0x62):
    put_cstr(NAME_BASE + 0x40 * it, etc_text(0x200 + it))
    m.ww(F.ITEMTAB + 8 * it, NAME_BASE + 0x40 * it)

LOG = []
STATE = {'tick': 0, 'dlg_open_tick': None, 'mask': 3, 'cd_busy_until': -1, 'close_at': None}
DLG_HOLD = 30         # ticks the player reads the box before closing it (once closable)
CD_TICKS = 150        # length of the CD fanfare in ticks (the track 11 is played from the CD; unknown here)

def log(*a):
    LOG.append((STATE['tick'],) + a)

def hk_text(mc):
    mc.r[2] = ETC_BASE + 0x100 * (mc.r[4] & 0xFF)
m.hooks[0x800816D4] = hk_text

def strcat(mc):
    d = mc.r[4]
    s = mc.cstr(mc.r[5])
    e = d + len(mc.cstr(d))
    for i, c in enumerate(s + b'\0'):
        mc.wb(e + i, c)
    mc.r[2] = d
m.hooks[0x80081EAC] = strcat

def h_open(mc):
    mode = mc.r[5] & M32
    text = mc.cstr(mc.r[4]).decode('cp1252', 'replace')
    log('OPEN message', repr(text), 'mode', mode)
    mc.ww(0x80152F08, mc.rw(0x80152F08) | 4)
    STATE['dlg_open_tick'] = STATE['tick']
    STATE['close_at'] = None
m.hooks[0x800450F0] = h_open

def h_mask(mc):
    STATE['mask'] = mc.r[4] & M32
    log('SET close mask', STATE['mask'])
m.hooks[0x8004507C] = h_mask
m.hooks[0x800490FC] = lambda mc: log('SOUND', mc.r[4] & M32)
m.hooks[0x80049AF4] = lambda mc: log('StopAllSound (BGM restarts)')
m.hooks[0x80049B7C] = lambda mc: log('LoadBgm', mc.r[4] & M32, '(0 = stop the music)')
def h_cdplay(mc):
    log('CD play track', mc.r[4] & M32)
    STATE['cd_busy_until'] = STATE['tick'] + CD_TICKS
m.hooks[0x8005A9E0] = h_cdplay
def h_cdbusy(mc):
    mc.r[2] = 1 if STATE['tick'] < STATE['cd_busy_until'] else 0
m.hooks[0x8005A7D4] = h_cdbusy
m.hooks[0x8003AD30] = lambda mc: log('Block (all active entities, hero included, except the chest)', hex(mc.r[4] & M32))
m.hooks[0x8003ADAC] = lambda mc: log('Unblock', hex(mc.r[4] & M32), '(the entities blocked by the chest are released)')
m.hooks[0x8003A774] = lambda mc: log('DestroyLight', hex(mc.r[4] & M32), '(the object)' if (mc.r[4] & M32) == F.ITEM else '')
m.hooks[0x80033DBC] = lambda mc: log('ApplyItem(hero, item=%d)' % (mc.r[5] & M32))
m.hooks[0x80032B28] = lambda mc: log('SetGameFlag(chest flag = %d)' % (mc.r[4] & M32))

def h_spawn(mc):
    sp = mc.r[29]
    log('SpawnWarpEntity parent=%s page=%d type=%d (item %d) X=%#x Y=%#x Z=%#x (chest Z + 16 px)' % (hex(mc.r[4] & M32), mc.r[5], mc.r[6], mc.r[6] - 0x1E, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    mc.r[2] = F.ITEM
m.hooks[0x80039F58] = h_spawn

def run_case(title, item, count, chest_flag=1234, anim_ticks=62, hold=30, cd_ticks=150, max_ticks=600):
    global DLG_HOLD, CD_TICKS
    DLG_HOLD, CD_TICKS = hold, cd_ticks
    LOG.clear()
    STATE.update({'tick': 0, 'dlg_open_tick': None, 'mask': 3, 'cd_busy_until': -1, 'close_at': None})
    F.setup(contents=item, chest88=0, flag=chest_flag, count={item: count} if count is not None else {}, itemflags=0x8180)
    m.ww(0x80152F08, 0)
    m.ww(F.ITEM + 0x68, item + 0x1E)       # SpriteTableIndex of the spawned object
    m.wb(BUF, 0)
    m.ww(F.ITEM + 0x144, 0)
    # tick 0: the hero presses the button, the pick runs F255
    log('--- F255 runs (the hero is against the chest and the Square edge is seen)')
    m.call(0x8007FC88, F.CHEST)
    log('chest +0x88 =', m.rw(F.CHEST + 0x88), '| object: Block-by', m.rw(F.ITEM + 0x20), 'kind', m.rw(F.ITEM + 0x274), 'delay', m.rw(F.ITEM + 0x278), 'rise force', hex(m.rw(F.ITEM + 0xB8)),
        'canTake(+0x284)', m.rw(F.ITEM + 0x284), 'fanfare(+0x288)', m.rw(F.ITEM + 0x288), 'flags', hex(m.rw(F.ITEM + 0x6C)))
    status = 1   # Loaded
    anim_done = False
    item_dead = False
    opened_tick = None
    for t in range(1, max_ticks):
        STATE['tick'] = t
        # the chest animation ends: chain 2 -> 1
        if not anim_done and t == anim_ticks:
            m.ww(F.CHEST + 0x88, 1)
            anim_done = True
            log('chest animation 2 ends, chain to 1: chest +0x88 = 1')
        # the message box is closed by the player when it is closable
        if m.rw(0x80152F08) & 4:
            if STATE['mask'] != 4:
                if STATE['close_at'] is None:
                    STATE['close_at'] = t + DLG_HOLD
                if t >= STATE['close_at']:
                    m.ww(0x80152F08, m.rw(0x80152F08) & ~4)
                    log('box closed by the player')
        # the object: A255 while Loaded, then C255 each tick
        if item_dead:
            continue
        if status == 1:
            m.ww(F.ITEM + 0x10, 2)   # the event pass moves it to Normal before the program, A255 may write 1 back
            was_cd = len([x for x in LOG if 'CD play' in str(x)])
            m.call(0x80061BD4, F.ITEM)
            status = m.rw(F.ITEM + 0x10)
            if status == 2:
                log('A255 lets the object go to Normal (chest is open)')
        elif status == 2:
            before = list(LOG)
            m.call(0x8007C174, F.ITEM)
            if any('DestroyLight' in str(x) and '(the object)' in str(x) for x in LOG[len(before):]):
                item_dead = True
                opened_tick = t
                break
    print('=' * 100)
    print(title)
    last = None
    for ev in LOG:
        print('  t=%4s' % ev[0] if isinstance(ev[0], int) else '  ', *ev[1:])
    print('  object dead at tick', opened_tick, '| chest +0x88 at the end =', m.rw(F.CHEST + 0x88))

if __name__ == '__main__':
    run_case('CASE 1: herb (0x24), 3 in stock: ordinary object (sound 52), room in the bag', 0x24, 3)
    run_case('CASE 2: herb (0x24), 9 in stock (the maximum): the bag is full', 0x24, 9)
    run_case('CASE 3: magic seed (0x50): key object (table byte 5 = 0): fanfare, music stopped, mask 4', 0x50, 0)
    run_case('CASE 4: 30 florins (0x48): sound 53', 0x48, 0)
    run_case('CASE 5: key (0x3D), 0 in stock: byte 5 = 52, NOT a key object despite the name', 0x3D, 0)
    run_case('CASE 6: Steel Edge (0x02): key object, fanfare', 0x02, 0)
