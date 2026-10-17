"""g1a_models.py - Python models of the carrying/throwing functions of ALUN_CD.EXE (France), read from the disassembly (lst/*.txt).
Every model works on h.Ent images (0x294 bytes) and returns the list of journal events; vec_g1a.py compares them to the REAL code in the MIPS interpreter.
No expected value of the DLL is used here."""
from h import Ent, ENT0, STRIDE, M32, s32

HERO_PTR = ENT0
CTR = 0x80098F28            # [0x80098F28] = the mode-1 counter
PADEDGE = 0x80126F2A        # u16, "pressed" word of the pad struct (0x80126F18 + 0x12)
PADHOLD = 0x80126F28        # u16, "hold" word (0x80126F18 + 0x10)
NSLOT = 0x80134360          # last used entity index

# entity offsets (all read from the disassembly)
STATUS, T20, T24, CARRIER, CARRIED = 0x10, 0x20, 0x24, 0x28, 0x2C
OFFX, OFFY, OFFZ = 0x30, 0x34, 0x38
FLAGS, ANIM, DIR, FACE = 0x6C, 0x88, 0x8C, 0x98
POSX, POSY, POSZ = 0x114, 0x118, 0x11C
SUPPORT, CONTACT, GROUND = 0x12C, 0x130, 0x144
SHEET, LINK, PROG = 0x1C4, 0x1B8, 0x228
ADJ, COLLZ, FLOORH, TERRH, MOVED = 0x13C, 0x140, 0x134, 0x138, 0x168
BOX_X, BOX_Y, BOX_Z, BOXOFF_X, BOXOFF_Y, BOXOFF_Z = 0x1D8, 0x1DC, 0x1E0, 0x1E4, 0x1E8, 0x1EC
ANIMEND, VRAM184 = 0xAC, 0x184
FORCE_C4, FORCE_C8, FORCE_B8 = 0xC4, 0xC8, 0xB8


def step_towards(cur, tgt, step):
    """0x8002F854: |tgt-cur| < step (unsigned) -> tgt ; else cur +- step (sign of tgt-cur, 0 counts as minus)"""
    d = s32(tgt - cur)
    ad = (-d) & M32 if d < 0 else d
    if ad < (step & M32):
        return tgt & M32
    if d > 0:
        return (cur + step) & M32
    return (cur - step) & M32


def kind_of(flags):
    return (flags & 0x600) >> 9


def try_lift(hero: Ent, obj: Ent, obj_ptr: int, has3b: bool, j: list) -> int:
    """TryLift 0x8002EDBC"""
    k = kind_of(obj.u32(FLAGS))
    if k == 0:
        return 0
    if k != 1:                               # kinds 2 and 3 (sltiu v1,4 never fails: the mask gives 0..3)
        j.append(('item', 0x3B))
        if not has3b:
            return 0
    hero.set32(CARRIED, obj_ptr)
    obj.set32(CARRIER, HERO_PTR)
    ground = hero.u32(GROUND) != 0
    if obj.u32(SUPPORT) == HERO_PTR:
        hero.set32(ANIM, 0x0C if ground else 0x30)
    else:
        hero.set32(ANIM, 5 if ground else 0x0B)
    hero.set32(OFFX, (obj.u32(POSX) - hero.u32(POSX)) & M32)
    hero.set32(OFFY, (obj.u32(POSY) - hero.u32(POSY)) & M32)
    hero.set32(OFFZ, (obj.u32(POSZ) - hero.u32(POSZ)) & M32)
    return 1


def player_try_action(ents: dict, nslot: int, edge: int, has3b: bool, j: list) -> int:
    """PlayerTryAction 0x8002EEAC. ents: slot -> Ent (slot 0 = hero). Mutates in place."""
    hero = ents[0]
    if hero.u32(CARRIED) != 0:
        hero.set32(ANIM, 0x0C if hero.u32(GROUND) else 0x30)
        return 1
    c = hero.u32(CONTACT)
    if c != 0 and (edge & 0x80):
        slot = (c - ENT0) // STRIDE
        return try_lift(hero, ents[slot], c, has3b, j)
    if nslot < 0:
        return 0
    for s in range(0, nslot + 1):
        e = ents.get(s)
        if e is None:
            continue
        if (e.u32(STATUS) - 2) & M32 < 2 and e.u32(T20) == 0 and e.u32(SUPPORT) == HERO_PTR:
            if try_lift(hero, e, ENT0 + s * STRIDE, has3b, j):
                return 1
    return 0


def destroy1(e: Ent, carrier: Ent | None):
    """DestroyEntity one-argument 0x8003A774 (no loot, no effect). link: (linked entity or None)"""
    e.set32(STATUS, 4)
    e.set32(PROG, 0xFFFFFFFF)
    if e.u32(LINK) != 0:                  # the linked entity (its +0x68 is cleared) -- link_ent is passed through e.link_obj in the tests
        if getattr(e, 'link_obj', None) is not None:
            e.link_obj.set32(0x68, 0)
        e.set32(LINK, 0)
    if e.u32(CARRIER) != 0 and carrier is not None:
        carrier.set32(CARRIED, 0)


def upce(mode: int, hero: Ent, carried: Ent | None, sheet_attack: int, mem: dict, j: list):
    """UpdatePlayerCarriedEntity 0x8002F884. carried None when hero.+0x2C == 0. sheet_attack = byte 0xE of carried.+0x1C4. mem['ctr'] = [0x80098F28]."""
    if carried is None:
        mem['ctr'] = 0
        return
    if sheet_attack == 0 and mode == 1:
        mode = 0
    if mode == 0:
        dx = s32(carried.u32(POSX) - hero.u32(POSX))
        dy = s32(carried.u32(POSY) - hero.u32(POSY))
        dz = s32(carried.u32(POSZ) - hero.u32(POSZ))
        ax, ay = abs(dx), abs(dy)
        e = abs(dz - 0x200000)
        m = max(ax, ay)
        if m < e:                       # only Z advances
            nz = step_towards(dz & M32, 0x200000, 0x10000)
            hero.set32(OFFX, dx & M32); hero.set32(OFFY, dy & M32); hero.set32(OFFZ, nz)
        else:
            nx = step_towards(dx & M32, 0, 0x10000)
            ny = step_towards(dy & M32, 0, 0x10000)
            nz = step_towards(dz & M32, 0x200000, 0x10000)
            hero.set32(OFFX, nx); hero.set32(OFFY, ny); hero.set32(OFFZ, nz)
        mem['ctr'] = 0
    elif mode == 1:
        ctr = mem['ctr']
        if s32(ctr) < 5:                # slti v0,v1,5 (signed)
            mem['ctr'] = (ctr + 1) & M32
            return
        dx = s32(carried.u32(POSX) - hero.u32(POSX))
        dy = s32(carried.u32(POSY) - hero.u32(POSY))
        dz = s32(carried.u32(POSZ) - hero.u32(POSZ))
        if abs(dx) <= 0x1FFFF and abs(dy) <= 0x1FFFF and abs(dz) <= 0x1FFFF:
            j.append(('destroy1',))
            destroy1(carried, hero)
            return                      # counter kept
        nx = step_towards(dx & M32, 0, 0x20000)
        ny = step_towards(dy & M32, 0, 0x20000)
        nz = step_towards(dz & M32, 0, 0x20000)
        hero.set32(OFFX, nx); hero.set32(OFFY, ny); hero.set32(OFFZ, nz)
        # counter kept
    else:                               # mode 2 (any other value)
        carried.set32(T24, 0xFFFFFFFF)
        carried.set32(DIR, hero.u32(DIR))
        carried.set32(POSX, hero.u32(POSX))
        carried.set32(POSY, hero.u32(POSY))
        carried.set32(POSZ, (hero.u32(POSZ) + 0x200000) & M32)
        mem['ctr'] = 0


def c23(e: Ent, e_ptr: int, hero: Ent, table, j: list):
    """C23 0x8006B848 (crates and jars). table(t) = word at 0x80027400 + 4*t."""
    if e.u32(CARRIER) == 0:
        return
    t = e.s32(T24)
    if t == 0:
        e.set32(ANIM, 0)
        return
    if t == -1:
        anim = 3
    else:
        anim = table(t)
    e.set32(ANIM, anim)
    carrier_ptr = e.u32(CARRIER)
    e.set32(T24, 0)
    if carrier_ptr != 0:
        # carrier.+0x2C = 0 (the hero image in the model tests)
        hero.set32(CARRIED, 0)
    e.set32(CARRIER, 0)
    e.set32(FLAGS, ((e.u32(FLAGS) | 0x30) & ~0x80) & M32)


def e2(e: Ent, slot: int, j: list):
    """E2 0x8007ED30"""
    if (e.u32(ANIM) == 2 and e.u32(ANIMEND) == 1) or (e.u32(VRAM184) & 4):
        j.append(('destroy', slot, -1))
        return
    e.set32(ANIM, 2)
    e.set32(FLAGS, ((e.u32(FLAGS) | 0x40) & ~0x30) & M32)


def move_entity_carried(e: Ent, carrier: Ent, j: list, carrier_moved: bool):
    """MoveEntity 0x80037E34, the branch e.+0x28 != 0 (the recursion into the carrier is journalled)."""
    e.set32(MOVED, 1)
    if not carrier_moved:
        j.append(('recurse',))
    e.set32(0x140, 1)
    e.set32(0x13C, 1)
    e.set32(0x130, 0)
    e.set32(0x134, carrier.u32(0x134))
    e.set32(0x138, carrier.u32(0x138))
    nx = (carrier.u32(POSX) + carrier.u32(OFFX)) & M32
    ny = (carrier.u32(POSY) + carrier.u32(OFFY)) & M32
    nz = (carrier.u32(POSZ) + carrier.u32(OFFZ)) & M32
    e.set32(POSX, nx); e.set32(POSY, ny); e.set32(POSZ, nz)
    e.set32(BOX_X, (nx + e.u32(BOXOFF_X)) & M32)
    e.set32(BOX_Y, (ny + e.u32(BOXOFF_Y)) & M32)
    e.set32(BOX_Z, (nz + e.u32(BOXOFF_Z)) & M32)


# ---- the carry group of MovePlayer (0x8003232C, 0x800322B4, 0x800322F0), decoded from lst/moveplayer_carry.txt ------------------------------------
DIRTBL = [0, 0x10, 8, 0x18]       # words at 0x80023644


def carry_group(state: int, hero: Ent, carried: Ent | None, hold: int, pressed: int, j: list, dirsel: int):
    """State transitions at the end of the three handlers. dirsel = word read from [0x80022C6C + 4*(hold>>12)] or -1 (then the pad direction is hero.+0x8C).
    Returns nothing, mutates hero (+0x88, +0x8C) and carried (throw)."""
    s3 = hold >> 12
    # MovePlayer head: s1 = table[s3] ; -1 -> hero.+0x8C
    s1 = dirsel if dirsel != -1 else hero.u32(DIR)
    ground = hero.u32(GROUND) != 0
    if state in (0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30):
        hero.set32(DIR, s1 & M32)                       # sw s1, 0x8c(s0) in the delay slot of the jal 0x8002ED64
        if carried is None:
            hero.set32(ANIM, 0 if ground else 0x2D)
            return
        if pressed & 0x80:                              # Square edge
            if hero.u32(0x180) & 0x2000:
                return
            carried.set32(DIR, DIRTBL[hero.u32(FACE) & 3] if hero.u32(FACE) < 4 else None)
            carried.set32(POSX, hero.u32(POSX)); carried.set32(POSY, hero.u32(POSY))
            carried.set32(POSZ, (hero.u32(POSZ) + 0x200000) & M32)
            if ground:
                hero.set32(ANIM, 9)
                carried.set32(T24, 2 if s3 else 1)
            else:
                hero.set32(ANIM, 0x0A)
                carried.set32(T24, 3 if s3 else 2)
            return
        if kind_of(carried.u32(FLAGS)) == 3:            # heavy: 0x600 mask == 0x600
            hero.set32(ANIM, 0x0C if ground else 0x30)
            return
        if ground:
            if pressed & 0x40:                          # Cross edge
                if hero.u32(0x180) & 0x4000:
                    return
                hero.set32(ANIM, 6 if s3 else 0x2E)
            else:
                hero.set32(ANIM, 7 if s3 else 0x0C)
        else:
            hero.set32(ANIM, 0x2F if s3 else 0x30)
    elif state == 0x09:                                 # ground throw gesture (attack group 0x80031E18): airborne -> fall state
        if not ground:
            hero.set32(ANIM, 0x2D)
    elif state == 0x0A:                                 # air throw gesture (attack group 0x80031DF8): landed -> idle
        if ground:
            hero.set32(ANIM, 0)
    elif state == 0x05:
        if carried is None:
            hero.set32(ANIM, 0x2D)
        elif not ground:
            hero.set32(ANIM, 0x30)
    elif state == 0x0B:
        if carried is None:
            hero.set32(ANIM, 0)
        elif ground:
            hero.set32(ANIM, 0x0C)
