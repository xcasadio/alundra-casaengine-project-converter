"""h4_vec_handlers_mut.py - sensitivity of h4_vec_handlers.py: each mutation of a MODEL must be caught by the vectors (exit 0 = all caught)."""
import subprocess, sys, os, shutil
src = open('h4_models_handlers.py', encoding='utf-8').read()
MUT = [
    ('e3: anim 1 -> 2', "    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13", "    m.w32(s + 0x88, 2)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13"),
    ('e3: flag mask 0x30 kept', "flags = (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13", "flags = (m.u32(s + 0x6C) | 0x40)\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13"),
    ('e3: destroy needs only the animation end', "if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:\n        destroy(env, s, -1)\n        return\n    flags = (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13",
     "if m.s32(s + 0xAC) == 1:\n        destroy(env, s, -1)\n        return\n    flags = (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, flags)\n\n\ndef e13"),
    ('e13: camera kick 2 -> 3', "m.w32(0x800DD874, 2)", "m.w32(0x800DD874, 3)"),
    ('e13: behaves like e3 when the animation runs', "        if m.s32(s + 0xAC) == 1:\n            destroy(env, s, -1)\n        return\n    m.w32(0x800DD874, 2)", "        if m.s32(s + 0xAC) == 1:\n            destroy(env, s, -1)\n            return\n    m.w32(0x800DD874, 2)"),
    ('e28: ring radius base 0x320 -> 0x321', "off = r + 0x320", "off = r + 0x321"),
    ('e28: random range 0x1801 -> 0x1800', "r = (st * 0x1801) >> 32", "r = (st * 0x1800) >> 32"),
    ('e28: 8 bursts -> 7', "for k in range(8):\n        st = rng_step(m)", "for k in range(7):\n        st = rng_step(m)"),
    ('e28: effect variant 2 -> 3', "env.fx(0, 0x13, 2, (m.u32(s + 0x114) + xoff)", "env.fx(0, 0x13, 3, (m.u32(s + 0x114) + xoff)"),
    ('e28: tables walked every 4th entry -> every 2nd', "m.s16(TAB_Y + 8 * k) * off", "m.s16(TAB_Y + 4 * k) * off"),
    ('c63: delay decrement removed', "        m.w32(s + 0x274, b0 - 1)\n        return\n    tgt", "        return\n    tgt"),
    ('c63: fall speed 0xFFFD0000 -> 0xFFFE0000', "m.w32(s + 0xB8, 0xFFFD0000)", "m.w32(s + 0xB8, 0xFFFE0000)"),
    ('c63: retry delay draw below 9 -> 8', "r = rng_draw(m, 9)", "r = rng_draw(m, 8)"),
    ('c63: turn step 4 -> 3', "m.w32(s + 0x8C, cur - 4)", "m.w32(s + 0x8C, cur - 3)"),
    ('c63: turn by the shortest way (mod 32)', "def turn_toward(m, s, d):\n    cur = m.s32(s + 0x8C)\n    if d < cur:", "def turn_toward(m, s, d):\n    cur = m.s32(s + 0x8C)\n    if d < cur and cur - d <= 16:"),
    ('c63: re-arm turn delay 2 -> 1', "    turn_toward(m, s, d)\n    m.w32(s + 0x274, 2)", "    turn_toward(m, s, d)\n    m.w32(s + 0x274, 1)"),
    ('c63: target status test removed', "if not (m.s32(t + 0x10) == 2 and m.u32(t + 4) == m.u32(s + 0x27C)):", "if not (m.u32(t + 4) == m.u32(s + 0x27C)):"),
    ('c63: target identity test removed', "if not (m.s32(t + 0x10) == 2 and m.u32(t + 4) == m.u32(s + 0x27C)):", "if not (m.s32(t + 0x10) == 2):"),
    ('c65: window X 0x1400000 -> 0x1000000', "> 0x1400000:", "> 0x1000000:"),
    ('c65: window Y 0xF00000 -> 0xE00000', "> 0xF00000:", "> 0xE00000:"),
    ('c65: rotation +0x14 -> +0x10', "idx = (d + 0x14) & 0x1F", "idx = (d + 0x10) & 0x1F"),
    ('c65: X push factor 3 -> 2', "(m.s16(COS256 + 16 * idx) * 3) << 8", "(m.s16(COS256 + 16 * idx) * 2) << 8"),
    ('c65: pulls every target of the window', "        m.w32(t + 0xD0, (m.s16(SIN256 + 16 * idx)) << 9)\n        return", "        m.w32(t + 0xD0, (m.s16(SIN256 + 16 * idx)) << 9)"),
    ('c75: life freezes when Circle NOT held', "if (m.u16(PAD_HOLD) & 0x20) == 0:", "if (m.u16(PAD_HOLD) & 0x20) != 0:"),
    ('c75: initial growth bonus 64 -> 32', "v1 = (1 if r < p0 else 0) << 6", "v1 = (1 if r < p0 else 0) << 5"),
    ('c75: X radius factor 3 -> 2', "s32(3 * s32(sx))", "s32(2 * s32(sx))"),
    ('c75: Y radius factor 2 -> 3', "s32(2 * s32(cy))", "s32(3 * s32(cy))"),
    ('c75: hero force not added', "+ m.u32(HERO + 0xC4))", "+ 0)"),
    ('c75: height above the hero 0x80000 -> 0x100000', "+ 0x80000)\n        return\n    if life == 0:", "+ 0x100000)\n        return\n    if life == 0:"),
    ('c75: target range 0x3840 -> 0x3841', "s32(q[0][1]) < 0x3840", "s32(q[0][1]) < 0x3841"),
    ('c75: launch flags 0x2130 -> 0x2030', "| 0x2130)", "| 0x2030)"),
    ('c75: launch lift 0xA0000 -> 0x90000', "m.w32(s + 0xB8, 0xA0000)", "m.w32(s + 0xB8, 0x90000)"),
    ('c75: launch speed shift 8 -> 7', "m.w32(s + 0x274, m.s16(TAB_X + 2 * d) << 8)", "m.w32(s + 0x274, m.s16(TAB_X + 2 * d) << 7)"),
    ('c75: random direction below 32 -> 31', "d = rng_draw(m, 0x20)", "d = rng_draw(m, 0x1F)"),
    ('c75: angle mask 0xFF -> 0x7F', "ang = (m.s32(a0 + 0x18) + m.s32(a0 + 0xC)) & 0xFF", "ang = (m.s32(a0 + 0x18) + m.s32(a0 + 0xC)) & 0x7F"),
    ('c75: mode toggle removed', "            m.w32(a0 + 0x10, mode ^ 1)", "            pass"),
]
caught = 0
os.makedirs('mutdir2', exist_ok=True)
for f in os.listdir('.'):
    if f.endswith('.py') and not f.startswith('h4_vec_handlers_mut') and f != 'h4_models_handlers.py':
        shutil.copy(f, 'mutdir2/' + f)
missed, notfound = [], []
ONLY = sys.argv[1:]
for name, a, b in MUT:
    if ONLY and not any(o in name for o in ONLY):
        continue
    if a not in src:
        print('PATTERN NOT FOUND', name); notfound.append(name); continue
    open('mutdir2/h4_models_handlers.py', 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-B', 'h4_vec_handlers.py', '1800'], cwd='mutdir2', capture_output=True, text=True)
    ok = r.returncode != 0
    caught += ok
    line = [l for l in r.stdout.splitlines() if l.startswith('vectors')]
    print(('CAUGHT   ' if ok else 'MISSED   ') + name, '|', line[0] if line else r.stderr[-200:], flush=True)
    if not ok:
        missed.append(name)
print('%d/%d mutations caught; not found %d; missed: %s' % (caught, len(MUT) - len(notfound), len(notfound), missed))
shutil.rmtree('mutdir2', ignore_errors=True)
sys.exit(0 if caught == len(MUT) - len(notfound) and not notfound else 1)
