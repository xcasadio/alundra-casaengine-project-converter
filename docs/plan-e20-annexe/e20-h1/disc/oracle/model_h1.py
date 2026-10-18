"""model_h1.py - E20.h1 discovery: MY OWN model of the instant-item functions of ALUN_CD.EXE (France), written from my reading of the listings in lst/ (not from the emulation).
Every function takes a Ctx (sparse memory that can only read the bytes a vector lists, the stub returns, the journal of the stub calls) and does what the listing does, in order.
The comments give the address of each step.  A port to C# follows this model member by member.
"""
M = 0xFFFFFFFF
LENIENT = False        # set by the mutation runner only: an unlisted byte then reads as 0 instead of raising

HERO = 0x80127D30
STATS_PTR = 0x80176318
INV_PTR = 0x8017631C
INV_COUNT = 0x8017638C
IN_USE = 0x80127164
COUNTER = 0x8012715C
PHASE = 0x80127160
RNG = 0x80098708
LCG_A = 0x7D2B89DD
LCG_C = 0xE06A02E7
TAB_X = 0x80023654      # 32 halfwords, 768 * cos
TAB_Y = 0x80023694      # 32 halfwords, 512 * sin


def s32(x):
    x &= M
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


def trunc_div(x, d):
    """C division of a signed 32-bit value by a positive constant: the (x + d-1 if x < 0) >> log2 idiom of the listing"""
    q = abs(x) // d
    return q if x >= 0 else -q


class Unlisted(Exception):
    pass


class Ctx:
    def __init__(self, reads, stubs):
        self.b = {}
        for a, n, v in reads:
            for i in range(n):
                self.b[a + i] = (v >> (8 * i)) & 0xFF
        self.out = {}
        self.rets = {a: list(r) for a, r in stubs.items()}
        self.calls = []
        self.bad = False           # a stub was called that the vector does not list (or more often than it lists)

    def _rd(self, a, n):
        v = 0
        for i in range(n):
            k = (a + i) & M
            if k in self.out:
                x = self.out[k]
            elif k in self.b:
                x = self.b[k]
            elif LENIENT:
                x = 0
            else:
                raise Unlisted('%#x' % a)
            v |= x << (8 * i)
        return v

    def _wr(self, a, n, v):
        for i in range(n):
            self.out[(a + i) & M] = (v >> (8 * i)) & 0xFF

    def rw(self, a): return self._rd(a, 4)
    def rh(self, a): return self._rd(a, 2)
    def ww(self, a, v): self._wr(a, 4, v & M)
    def wh(self, a, v): self._wr(a, 2, v & 0xFFFF)

    def stub(self, addr, args):
        self.calls.append(["0x%08x" % addr, [x & M for x in args]])
        lst = self.rets.get(addr)
        if not lst:
            self.bad = True
            return 0
        return lst.pop(0)


# ---- the callees the model reaches through the stubs ---------------------------------------------------------------------------------------------------------------
def play_sound(c, sound_id):
    c.stub(0x800490FC, [sound_id])


def create_effect(c, anim, x, y, z):
    """0x8003BDD8(0, 14, anim, x, y, z): the global effect 14; returns the effect object, 0 when the pool is full"""
    return c.stub(0x8003BDD8, [0, 14, anim, x, y, z])


def use_item(c, item):
    """0x8004E5C4 UseItem(item): the inventory table at [0x8017631C], 4 bytes per item, the count is the signed halfword at +2"""
    if s32(item) < 0:                                   # 0x8004E5CC bltz
        c.stub(0x8004DB90, [0x80026674])
        return 0
    size = s32(c.rw(INV_COUNT))
    if not (s32(item) < size):                          # 0x8004E5E0 slt
        c.stub(0x8004DB90, [0x80026674])
        return 0
    entry = (c.rw(INV_PTR) + 4 * item) & M
    count = s16(c.rh(entry + 2))
    if count == 0:                                      # 0x8004E614
        return -1
    c.wh(entry + 2, count - 1)
    return s16(count - 1)


def hero_xyz(c):
    return c.rw(HERO + 0x114), c.rw(HERO + 0x118), c.rw(HERO + 0x11C)


def set_mp_to(c, value):
    """0x8004DEBC SetPlayerMp(value) with value = MpMax read an instant before: clamped to [0, MpMax] on the stats' halfwords"""
    stats = c.rw(STATS_PTR)
    mpmax = s16(c.rh(stats + 6))
    if mpmax < s32(value):                              # 0x8004DED0: never taken here, value is MpMax itself
        c.wh(stats + 4, mpmax)
    elif s32(value) < 0:                                # 0x8004DEE4 bgez
        c.wh(stats + 4, 0)
    else:
        c.wh(stats + 4, value)


def get_mp(c):
    return s16(c.rh(c.rw(STATS_PTR) + 4))               # 0x8004DEA4


def get_mp_max(c):
    return s16(c.rh(c.rw(STATS_PTR) + 6))               # 0x8004DDDC


def spark(c, anim, z_lift, force_z):
    """one spark of effect 14 with two draws of the generator (0x80033000-0x800330C8 and its copies): the effect is created first, the draws happen only if it was"""
    x, y, z = hero_xyz(c)
    e = create_effect(c, anim, x, y, (z + z_lift) & M)
    if e == 0:
        return
    s = c.rw(RNG)
    r1 = (s * LCG_A + LCG_C) & M                        # first draw
    r2 = (r1 * LCG_A + LCG_C) & M                       # second draw
    hi1 = (r1 * 0x20001) >> 32
    hi2 = (r2 * 0x30001) >> 32
    c.ww(e + 0x5C, force_z)
    c.ww(RNG, r1)
    c.ww(RNG, r2)
    c.ww(e + 0x54, (hi1 + 0xFFFF0000) & M)              # hi1 - 0x10000
    c.ww(e + 0x58, (hi2 + 0xFFFE8000) & M)              # hi2 - 0x18000


def ring(c, i, anim):
    """one ring effect of the 8 (0x800332C8-0x80033378 and its copies): moved out on the ellipse by entry 4i of the tables, flying with the entry (8 + 4i) & 31"""
    x, y, z = hero_xyz(c)
    e = create_effect(c, anim, x, y, (z + 0x80000) & M)
    if e == 0:
        return
    px = s16(c.rh(TAB_X + 2 * (4 * i)))
    py = s16(c.rh(TAB_Y + 2 * (4 * i)))
    c.ww(e + 0x3C, (c.rw(e + 0x3C) + (px << 11)) & M)
    c.ww(e + 0x40, (c.rw(e + 0x40) + (py << 11)) & M)
    k = (8 + 4 * i) & 31
    vx = s16(c.rh(TAB_X + 2 * k))
    vy = s16(c.rh(TAB_Y + 2 * k))
    c.ww(e + 0x5C, 0x30000)
    c.ww(e + 0x54, (vx * 448) & M)
    c.ww(e + 0x58, (vy * 448) & M)


# ---- the heal functions ----------------------------------------------------------------------------------------------------------------------------------------------
def add_capped(c, hero, amount_of):
    """Hp = min(HpMax, Hp + amount) on the 32-bit signed words of the entity a0 (0x80032E58-0x80032EA0)"""
    hpmax = s32(c.rw(hero + 0x18))
    hp = s32(c.rw(hero + 0x14))
    new = s32(hp + amount_of(hpmax))
    if hpmax < new:
        new = hpmax
    c.ww(hero + 0x14, new)


def heal_herb(c, hero, item):
    """0x80032E50: + max(5, HpMax / 4), then one spark-less effect 14 with a z force of 0x20000"""
    def amount(hpmax):
        q = trunc_div(hpmax, 4)
        return 5 if q < 5 else q
    add_capped(c, hero, amount)
    x, y, z = hero_xyz(c)
    e = create_effect(c, 0, x, y, (z + 0x100000) & M)
    if e:
        c.ww(e + 0x5C, 0x20000)


def heal_half(c, hero, minimum):
    def amount(hpmax):
        q = trunc_div(hpmax, 2)
        return minimum if q < minimum else q
    add_capped(c, hero, amount)
    for _ in range(4):
        spark(c, 0, 0x100000, 0x20000)


def heal_water1(c, hero, item):
    """0x80032F84: + max(5, HpMax / 2), four sparks"""
    heal_half(c, hero, 5)


def heal_spotion(c, hero, item):
    """0x800330FC: + max(10, HpMax / 2), four sparks"""
    heal_half(c, hero, 10)


def heal_lpotion(c, hero, item):
    """0x80033274: Hp = HpMax, eight rings of animation 0"""
    c.ww(hero + 0x14, c.rw(hero + 0x18))
    for i in range(8):
        ring(c, i, 0)


def heal_elixir(c, hero, item):
    """0x800336F0: Mp = MpMax (0x8004DDDC then 0x8004DEBC), eight rings of animation 2"""
    set_mp_to(c, get_mp_max(c))
    for i in range(8):
        ring(c, i, 2)


def heal_cureale(c, hero, item):
    """0x80033A2C: Hp = HpMax, Mp = MpMax, eight rings of animations 2, 0, 2, 0, ..., then four times (a spark of animation 0 with a z force of 0x40000, a spark of animation 2 with 0x20000)"""
    c.ww(hero + 0x14, c.rw(hero + 0x18))
    set_mp_to(c, get_mp_max(c))
    for i in range(8):
        ring(c, i, 2 if i % 2 == 0 else 0)
    for _ in range(4):
        spark(c, 0, 0x100000, 0x40000)
        spark(c, 2, 0x100000, 0x20000)


# ---- the start functions (the item is a0; every one of them answers 1) -------------------------------------------------------------------------------------------------
def life_below_max(c):
    return s32(c.rw(HERO + 0x14)) < s32(c.rw(HERO + 0x18))


def magic_below_max(c):
    mp = get_mp(c)
    return mp < get_mp_max(c)


def start_herb(c, item):
    """0x80034680"""
    if life_below_max(c):
        heal_herb(c, HERO, item)
        play_sound(c, 0x31)
        use_item(c, item)
    else:
        play_sound(c, 3)
    return 1


def start_lpotion(c, item):
    """0x800346F0"""
    if life_below_max(c):
        heal_lpotion(c, HERO, item)
        play_sound(c, 0x32)
        use_item(c, item)
    else:
        play_sound(c, 3)
    return 1


def start_elixir(c, item):
    """0x80034760"""
    if magic_below_max(c):
        heal_elixir(c, HERO, item)
        play_sound(c, 0x30)
        use_item(c, item)
    else:
        play_sound(c, 3)
    return 1


def start_cureale(c, item):
    """0x800347D4: the life first, then the magic"""
    if life_below_max(c) or magic_below_max(c):
        heal_cureale(c, HERO, item)
        play_sound(c, 0x30)
        use_item(c, item)
    else:
        play_sound(c, 3)
    return 1


def start_spotion(c, item):
    """0x80034870"""
    if life_below_max(c):
        heal_spotion(c, HERO, item)
        play_sound(c, 0x31)
        use_item(c, item)
    else:
        play_sound(c, 3)
    return 1


# ---- the dispatcher 0x8003499C ------------------------------------------------------------------------------------------------------------------------------------
INSTANT = {0x24: 0x80034680, 0x25: 0x800346F0, 0x26: 0x80034760, 0x27: 0x800347D4, 0x29: 0x80034870}
OTHER = {0x1F: 0x80034224, 0x20: 0x80034320, 0x23: 0x8003453C}
OTHER.update({i: 0x800348E0 for i in range(0x2B, 0x33)})


def dispatch(c):
    s0 = c.stub(0x8004E0F8, []) & M
    fail = False
    if s0 >= 0x62:                                      # 0x800349B0 sltiu, unsigned
        fail = True
    else:
        cur = c.rw(IN_USE)
        if cur != 0 and cur != s0:                      # 0x800349C0-0x800349CC
            fail = True
        elif s0 - 0x1F >= 0x14 or s0 < 0x1F:           # 0x800349D4 sltiu on s0 - 0x1F
            fail = True
        elif s0 in (0x21, 0x22, 0x28, 0x2A):            # the holes of the table 0x8002339C
            fail = True
    if fail:
        play_sound(c, 3)
        return 1
    if s0 in INSTANT:
        c.stub(INSTANT[s0], [s0])                       # the answer is ignored: the instant items answer the constant 1 (0x80034A38...)
        return 1
    v = c.stub(OTHER.get(s0, 0), [s0]) & M
    if v == 0:                                          # 0x80034A88-0x80034A9C
        c.ww(IN_USE, s0)
        c.ww(COUNTER, 0)
        c.ww(PHASE, 0)
    return v


CTRL = 0x800DC4B8
PRESSED = 0x80126F2A


def try_use_item(c):
    """0x8002ED64 TryUseItem: no front of the Circle (bit 0x20 of the pressed word) answers 1; a front with ANY bit of g_playerControlFlags plays the sound 3 and answers 1; otherwise the dispatcher answers"""
    pressed = c.rh(PRESSED)
    if (pressed & 0x20) == 0:
        return 1
    if c.rw(CTRL) != 0:
        play_sound(c, 3)
        return 1
    return c.stub(0x8003499C, []) & M


FUNCTIONS = {
    0x80032E50: lambda c, a: heal_herb(c, a[0], a[1]),
    0x80032F84: lambda c, a: heal_water1(c, a[0], a[1]),
    0x800330FC: lambda c, a: heal_spotion(c, a[0], a[1]),
    0x80033274: lambda c, a: heal_lpotion(c, a[0], a[1]),
    0x800336F0: lambda c, a: heal_elixir(c, a[0], a[1]),
    0x80033A2C: lambda c, a: heal_cureale(c, a[0], a[1]),
    0x80034680: lambda c, a: start_herb(c, a[0]),
    0x800346F0: lambda c, a: start_lpotion(c, a[0]),
    0x80034760: lambda c, a: start_elixir(c, a[0]),
    0x800347D4: lambda c, a: start_cureale(c, a[0]),
    0x80034870: lambda c, a: start_spotion(c, a[0]),
    0x8003499C: lambda c, a: dispatch(c),
    0x8002ED64: lambda c, a: try_use_item(c),
}
RETURNS_VALUE = {0x8002ED64, 0x80034680, 0x800346F0, 0x80034760, 0x800347D4, 0x80034870, 0x8003499C}      # the heal functions leave v0 unspecified (the harness does not compare it)
