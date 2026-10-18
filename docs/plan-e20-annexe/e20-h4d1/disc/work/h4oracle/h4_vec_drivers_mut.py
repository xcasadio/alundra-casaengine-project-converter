"""h4_vec_drivers_mut.py - sensitivity of h4_vec_drivers.py: each mutation of a MODEL must be caught by the vectors (exit 0 = all caught)."""
import subprocess, sys, os, shutil
src = open('h4_models.py', encoding='utf-8').read()
MUT = [
    # earth 1
    ('earth1: end counter 0x64 -> 0x65', "if m.s32(COUNTER) < 0x64:", "if m.s32(COUNTER) < 0x65:"),
    ('earth1: direction delta from the wrong field', "(m.s32(HERO + 0x8C) + m.s16(h + 4)) & 0x1F", "(m.s32(HERO + 0x8C) + m.s16(h + 2)) & 0x1F"),
    ('earth1: ForceZ shift 8 -> 7', "m.w32(e + 0xB8, (m.s16(h + 2) << 8))", "m.w32(e + 0xB8, (m.s16(h + 2) << 7))"),
    ('earth1: sprite 0x10 -> 0x11', "env.spawn(HERO, 0, 0x10,", "env.spawn(HERO, 0, 0x11,"),
    # earth 2
    ('earth2: spawn height 0x1000000 -> 0x800000', "z = (m.u32(HERO + 0x11C) + 0x1000000) & M32\n            e = env.spawn(HERO, 0, 0x11", "z = (m.u32(HERO + 0x11C) + 0x800000) & M32\n            e = env.spawn(HERO, 0, 0x11"),
    ('earth2: radius base 0xA00 -> 0x9FF', "k = m.s16(h + 2) + 0xA00", "k = m.s16(h + 2) + 0x9FF"),
    ('earth2: terrain test < -> <=', "if s32(z) < m.s32(e + 0x138):", "if s32(z) <= m.s32(e + 0x138):"),
    ('earth2: direction from the first draw', "z, r2)\n            if e:", "z, r1)\n            if e:"),
    ('earth2: X and Y tables swapped', "yoff = s32(m.s16(TAB_Y + 2 * r1) * k)\n            xoff = s32(m.s16(TAB_X + 2 * r1) * k)", "xoff = s32(m.s16(TAB_Y + 2 * r1) * k)\n            yoff = s32(m.s16(TAB_X + 2 * r1) * k)"),
    ('earth2: fall speed 0xFFF80000 -> 0xFFF00000', "m.w32(e + 0xB8, 0xFFF80000)\n        if m.u16(a) == 0xFFFF:\n            m.w32(PHASE, m.s32(PHASE) + 1)\n        return 0\n    if P == 2:\n        return 0 if m.s32(HERO + 0x88) == 0x34 else 1\n    return 0\n\n\ndef drv_water",
     "m.w32(e + 0xB8, 0xFFF00000)\n        if m.u16(a) == 0xFFFF:\n            m.w32(PHASE, m.s32(PHASE) + 1)\n        return 0\n    if P == 2:\n        return 0 if m.s32(HERO + 0x88) == 0x34 else 1\n    return 0\n\n\ndef drv_water"),
    ('earth2: end counter 0x118 -> 0x117', "if m.s32(COUNTER) < 0x118:", "if m.s32(COUNTER) < 0x117:"),
    # water
    ('water: tables swapped', "tbl = WATER_UP if item == 0x2D else WATER_LO", "tbl = WATER_LO if item == 0x2D else WATER_UP"),
    ('water: spawn height 0x80000 -> 0x100000', "(m.u32(HERO + 0x11C) + 0x80000) & M32, 0)\n            if e:\n                m.w32(e + 0x70", "(m.u32(HERO + 0x11C) + 0x100000) & M32, 0)\n            if e:\n                m.w32(e + 0x70"),
    ('water: life counter sign', "m.w32(e + 0x290, m.s16(h + 0xC) - c)", "m.w32(e + 0x290, m.s16(h + 0xC) + c)"),
    ('water: angle mod uses floor instead of truncation', "t = v1 if v1 >= 0 else v1 + 0xFF", "t = v1"),
    ('water: heal at 0x1E -> 0x1F', "if t == 0x1E:", "if t == 0x1F:"),
    ('water: heal sounds swapped', "heal_water(env)\n            env.sound(0x31)", "heal_water(env)\n            env.sound(0x30)"),
    ('water: end 0x3C -> 0x3B', "if m.s32(0x80127118) < 0x3C:", "if m.s32(0x80127118) < 0x3B:"),
    ('water: phase 2 scan excludes the last slot', "for i in range(1, count + 1):\n            e = ent(i)\n            if m.s32(e + 0x68) == 0x13", "for i in range(1, count):\n            e = ent(i)\n            if m.s32(e + 0x68) == 0x13"),
    ('water: phase 2 waits for >= 0', "m.s32(e + 0x290) > 0:\n                return 0", "m.s32(e + 0x290) >= 0:\n                return 0"),
    ('heal water: minimum 5 -> 4', "    if add < 5:\n        add = 5", "    if add < 4:\n        add = 4"),
    ('heal water: spark speed constant', "m.w32(fx + 0x5C, 0x20000)", "m.w32(fx + 0x5C, 0x30000)"),
    ('heal full: spark speed 448 -> 449', "* 448))\n            m.w32(fx + 0x58, (m.s16(TAB_Y + 2 * idx) * 448))", "* 449))\n            m.w32(fx + 0x58, (m.s16(TAB_Y + 2 * idx) * 448))"),
    # fire 1
    ('fire1: projectile cadence & 7 -> & 15', "if (m.s32(COUNTER) & 7) != 0:", "if (m.s32(COUNTER) & 15) != 0:"),
    ('fire1: projectile height 0x100000 -> 0x80000', "(m.u32(HERO + 0x11C) + 0x100000) & M32, r)", "(m.u32(HERO + 0x11C) + 0x80000) & M32, r)"),
    ('fire1: projectile delay byte 0x10 -> 0x11', "m.w32(e + 0x274, 0x10)", "m.w32(e + 0x274, 0x11)"),
    ('fire1: projectile direction not drawn (0)', "(m.u32(HERO + 0x11C) + 0x100000) & M32, r)", "(m.u32(HERO + 0x11C) + 0x100000) & M32, 0)"),
    ('fire1: entity identity compare removed', "if m.u32(ent_ptr + 4) == m.u32(0x80127120):", "if True:"),
    # fire 2
    ('fire2: shake shift 4 -> 5', "m.w32(SHAKE + 8, c >> 4)", "m.w32(SHAKE + 8, c >> 5)"),
    ('fire2: shake shift 6 -> 7', "m.w32(SHAKE + 4, c >> 6)", "m.w32(SHAKE + 4, c >> 7)"),
    ('fire2: shake not restored at the end', "    copy9(m, SHAKE_BAK, SHAKE)\n    m.w32(HERO + 0x88, 0x34)", "    m.w32(HERO + 0x88, 0x34)"),
    ('fire2: cancel does not restore', "    restore_shake(m)\n    for i in range(1, s32(m.u32(G_COUNT)) + 1):\n        e = ent(i)\n        if m.s32(e + 0x68) == 0x15", "    for i in range(1, s32(m.u32(G_COUNT)) + 1):\n        e = ent(i)\n        if m.s32(e + 0x68) == 0x15"),
    # wind 1
    ('wind1: height limit 0x500000 -> 0x400000', "(m.u32(HERO + 0x11C) + 0x500000) & M32", "(m.u32(HERO + 0x11C) + 0x400000) & M32"),
    ('wind1: distance limit 0x6401 -> 0x6400', "if d >= 0x6401:", "if d >= 0x6400:"),
    ('wind1: strike height 0x800000 -> 0x400000 (target)', "(m.u32(sel + 0x11C) + 0x800000) & M32, 0)", "(m.u32(sel + 0x11C) + 0x400000) & M32, 0)"),
    ('wind1: random X range 0xC00001 -> 0xC00000', "rx = (st1 * 0xC00001) >> 32", "rx = (st1 * 0xC00000) >> 32"),
    ('wind1: random Y offset -0x400000 -> -0x300000', "0xFFC00000) & M32", "0xFFD00000) & M32"),
    ('wind1: last target not skipped', "if e == m.u32(0x8012712C):\n                        continue", "if False:\n                        continue"),
    ('wind1: query scans the last slot', "    for i in range(1, count):                          # t3 = 1 .. count-1 (the last used slot is NOT scanned)", "    for i in range(1, count + 1):"),
    ('wind1: sort at 2 results', "if n >= 3:                                         # 0x8003B0B4 slti 3: sorted only when 3 or more", "if n >= 2:"),
    ('wind1: ground test removed', "if m.s32(sp + 0x144) != 0:\n                            destroy_nofx(m, sp)", "if False:\n                            destroy_nofx(m, sp)"),
    ('query: immune response test 0x80 -> 0xC0', "if (m.u8(sheet + atk_type + 2) & 0xC0) == 0x80:", "if (m.u8(sheet + atk_type + 2) & 0xC0) == 0xC0:"),
    ('query: status 3 excluded', "if ((m.s32(e + 0x10) - 2) & M32) >= 2:         # status 2 or 3", "if m.s32(e + 0x10) != 2:"),
    # wind 2
    ('wind2: spawn sprite 0x18 -> 0x17', "env.spawn(HERO, 0, 0x18,", "env.spawn(HERO, 0, 0x17,"),
    ('wind2: phase 1 alive test inverted', "if m.u32(m.u32(0x80127130) + 4) == m.u32(0x80127134):\n            return 0", "if m.u32(m.u32(0x80127130) + 4) != m.u32(0x80127134):\n            return 0"),
    ('cancel wind2: 16 -> 15 particles', "for _ in range(16):", "for _ in range(15):"),
    ('cancel wind2: particle spread shift 9 -> 8', "m.w32(fx + 0x54, (m.s16(TAB_X + 2 * idx) << 9))", "m.w32(fx + 0x54, (m.s16(TAB_X + 2 * idx) << 8))"),
    ('cancel earth2: anim 1 -> 2', "m.w32(e + 0x88, 1)", "m.w32(e + 0x88, 2)"),
    ('cancel water: effect 0x1A -> 0x19', "env.fx(0, 0x1A, 0,", "env.fx(0, 0x19, 0,"),
    ('cancel fire1: effect variant 2 -> 1', "env.fx(0, 0x13, 2,", "env.fx(0, 0x13, 1,"),
    ('slope helper: slope 4 test removed', "    if m.s32(HERO + 0x18C) == 4:\n        cancel(env)\n        return 1", "    if False:\n        cancel(env)\n        return 1"),
    ('slope helper: anim 0x33 not accepted', "if ((m.s32(HERO + 0x88) - 0x32) & M32) < 2:", "if ((m.s32(HERO + 0x88) - 0x32) & M32) < 1:"),
    ('wrapper: counter increments after the drive removed', "    if not (0x7FFFFFFE < c):\n        m.w32(COUNTER, c + 1)", "    pass"),
    ('wrapper: in-use cleared only on 1', "    if res != 0:\n        m.w32(INUSE, 0)", "    if res == 1:\n        m.w32(INUSE, 0)"),
]
caught = 0
os.makedirs('mutdir', exist_ok=True)
for f in os.listdir('.'):
    if f.endswith('.py') and not f.startswith('h4_vec_drivers_mut') and f != 'h4_models.py':
        shutil.copy(f, 'mutdir/' + f)
missed, notfound = [], []
ONLY = sys.argv[1:]
for name, a, b in MUT:
    if ONLY and not any(o in name for o in ONLY):
        continue
    if a not in src:
        print('PATTERN NOT FOUND', name); notfound.append(name); continue
    open('mutdir/h4_models.py', 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-B', 'h4_vec_drivers.py', '1800'], cwd='mutdir', capture_output=True, text=True)
    ok = r.returncode != 0
    caught += ok
    line = [l for l in r.stdout.splitlines() if l.startswith('vectors')]
    print(('CAUGHT   ' if ok else 'MISSED   ') + name, '|', line[0] if line else r.stderr[-200:], flush=True)
    if not ok:
        missed.append(name)
print('%d/%d mutations caught; not found %d; missed: %s' % (caught, len(MUT) - len(notfound), len(notfound), missed))
shutil.rmtree('mutdir', ignore_errors=True)
sys.exit(0 if caught == len(MUT) - len(notfound) and not notfound else 1)
