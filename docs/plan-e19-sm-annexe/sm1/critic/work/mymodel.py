"""Critic: MY model of the DLL (EntitySearchService.cs as read at HEAD + the four proposed cases as the report states them R1..R5).
Written from the C# text and the report's rules, not from the preparation's Python. Identities: engine Entity = a unique int; proxy.ctx = its LogicContextEntity (None = bare proxy)."""

LOADED, NORMAL, DEACT = 1, 2, 3
COLLIDABLE = 0x80
NOENTCOL = 0x80


class P:
    """EntityScriptProxy"""
    def __init__(self, idx, status, ref, flags, anim, tile, riding=None, xcoll=None, parent=None, platform=None,
                 blocked=False, ctx='auto', is_player=False):
        self.idx = idx
        self.status = status
        self.ref = ref
        self.flags = flags & 0xFFFFFFFF
        self.anim = anim
        self.tile = tile
        self.riding = riding        # Entity identity (int/str) or None
        self.xcoll = xcoll          # proxy or None
        self.parent = parent
        self.platform = platform
        self.blocked = blocked
        self.ctx = ('E%d' % idx) if ctx == 'auto' else ctx
        self.is_player = is_player

    @property
    def live(self):
        return self.status in (LOADED, NORMAL, DEACT)


def search(ref, stype, spawned, player):
    out = []
    if stype & 0x80 == 0:
        if ref.live:
            out = [c for c in spawned if c.ref == stype]
        return out
    f = stype & 0x7F
    if f == 0:
        out = [ref]
    elif f == 1:
        out = [player] if player is not None else []
    elif f == 2:
        out = [c for c in spawned if c.live]
    elif f == 3:
        out = [c for c in spawned if c.live and not c.is_player]
    elif f == 4:
        out = [c for c in spawned if ref.live and (c.flags & COLLIDABLE) != 0 and (c.anim & NOENTCOL) == 0 and c.platform is None]
    elif f == 5:
        out = [c for c in spawned if not c.is_player and c.live and ref.riding == c.ctx]
    elif f == 6:
        out = [c for c in spawned if not c.is_player and c.live and c.riding == ref.ctx]
    elif f == 7:
        out = [c for c in spawned if not c.is_player and c.live and ref.xcoll is c]
    elif f == 8:
        out = [c for c in spawned if not c.is_player and c.live and c.xcoll is ref]
    elif f == 9:
        out = [c for c in spawned if not c.is_player and c.live and c.parent == ref.ctx]
    elif f == 10:
        out = [c for c in spawned if not c.is_player and c.live and ref.parent == c.ctx]
    elif f == 11:
        out = [c for c in spawned if not c.is_player and c.live and c.platform is not None]
    else:
        out = []
    return out


def eligible(c):
    return (c.flags & COLLIDABLE) != 0 and (c.anim & NOENTCOL) == 0 and c.platform is None


def case60(entity, v, spawned, player):
    carrier = entity.ctx
    for m in search(entity, v[1], spawned, player):
        if m.riding is not None and m.riding == carrier:
            return 1
    return 0


def case61(entity, v, spawned, player):
    group = search(entity, v[1], spawned, player)
    if entity.riding is not None:
        for m in group:
            if entity.riding == m.ctx:
                return 1
    return 0


def zone(v, spawned, player, ground_only):
    seq = ([player] if player is not None else []) + list(spawned)
    for c in seq:
        if not c.live:
            continue
        if ground_only and not eligible(c):
            continue
        x, y, z = c.tile
        if v[1] <= x <= v[2] and v[3] <= y <= v[4] and v[5] <= z <= v[6]:
            return 1
    return 0


def run(op, entity, v, spawned, player):
    if op == 0x60:
        return case60(entity, v, spawned, player)
    if op == 0x61:
        return case61(entity, v, spawned, player)
    if op == 0x3D:
        return zone(v, spawned, player, False)
    if op == 0x3C:
        return zone(v, spawned, player, True)
    raise ValueError(op)
