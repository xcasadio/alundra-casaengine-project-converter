"""h3_mut.py - sensitivity of h3_vec.py: each mutation of a MODEL (h3_models.py) must be caught by the vectors (exit 0 = all caught).
usage: python -B h3_mut.py [filter...] [--shard k/n] [--n VECTORS] [--seed S]   (run from this folder; a scratch copy is made in mutdir_k/)"""
import subprocess, sys, os, shutil
src = open('h3_models.py', encoding='utf-8').read()
MUT = [
    # ---- starts
    ('beans start: carried test removed', "def start_beans(env):\n    m = env.m\n    h = HERO\n    if m.u32(h + 0x2C) != 0 or ", "def start_beans(env):\n    m = env.m\n    h = HERO\n    if False or "),
    ('beans start: slope 4 -> 3', "def start_beans(env):\n    m = env.m\n    h = HERO\n    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 4", "def start_beans(env):\n    m = env.m\n    h = HERO\n    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 3"),
    ('beans start: in-use test removed', "def start_beans(env):\n    m = env.m\n    h = HERO\n    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 4 or m.u32(INUSE) != 0:", "def start_beans(env):\n    m = env.m\n    h = HERO\n    if m.u32(h + 0x2C) != 0 or m.s32(h + 0x18C) == 4:"),
    ('beans start: sprite 1 -> 2', "e = env.spawn(h, 0, 1, m.u32(h + 0x114)", "e = env.spawn(h, 0, 2, m.u32(h + 0x114)"),
    ('beans start: no sound on a null spawn becomes sound 3', "    if e == 0:                                                                  # 0x80034290 : no sound\n        return 1", "    if e == 0:\n        env.sound(3)\n        return 1"),
    ('beans start: y and z swapped', "env.spawn(h, 0, 1, m.u32(h + 0x114), m.u32(h + 0x118), m.u32(h + 0x11C), 0)", "env.spawn(h, 0, 1, m.u32(h + 0x114), m.u32(h + 0x11C), m.u32(h + 0x118), 0)"),
    ('bomb start: limit 3 -> 4', "    if n >= 3:", "    if n >= 4:"),
    ('bomb start: limit 3 -> 2', "    if n >= 3:", "    if n >= 2:"),
    ('bomb start: counts only status 2', "        if m.s32(ent(i) + 0x68) == 2:\n            n += 1", "        if m.s32(ent(i) + 0x68) == 2 and m.s32(ent(i) + 0x10) == 2:\n            n += 1"),
    ('bomb start: the hero slot is not counted', "    for i in range(0x40):", "    for i in range(1, 0x40):"),
    ('bomb start: only 63 slots', "    for i in range(0x40):", "    for i in range(0x3F):"),
    ('bomb start: fuse 0xF0 -> 0xEF', "m.w32(e + 0x274, 0xF0)", "m.w32(e + 0x274, 0xEF)"),
    ('bomb start: fuse written on the hero', "m.w32(e + 0x274, 0xF0)", "m.w32(h + 0x274, 0xF0)"),
    ('start: anim 5 <-> 0xB swapped', "m.w32(h + 0x88, 5 if m.u32(h + 0x144) != 0 else 0xB)", "m.w32(h + 0x88, 0xB if m.u32(h + 0x144) != 0 else 5)"),
    ('start: offset x not stored', "    m.w32(h + 0x30, (m.u32(e + 0x114) - m.u32(h + 0x114)) & M32)", "    pass"),
    ('start: offset z uses y', "m.w32(h + 0x38, (m.u32(e + 0x11C) - m.u32(h + 0x11C)) & M32)", "m.w32(h + 0x38, (m.u32(e + 0x118) - m.u32(h + 0x11C)) & M32)"),
    ('start: carrier link not stored', "    m.w32(e + 0x28, h)\n    m.w32(h + 0x88", "    m.w32(h + 0x88"),
    ('start: bomb sprite 2 -> 1', "e = env.spawn(h, 0, 2, m.u32(h + 0x114)", "e = env.spawn(h, 0, 1, m.u32(h + 0x114)"),
    # ---- small drivers
    ('driver: type test removed', "    if m.s32(c + 0x68) != typ:\n        return 1\n    if m.s32(HERO + 0x18C) != 4:", "    if m.s32(HERO + 0x18C) != 4:"),
    ('driver: slope 4 -> 3', "    if m.s32(HERO + 0x18C) != 4:\n        return 0", "    if m.s32(HERO + 0x18C) != 3:\n        return 0"),
    ('driver: returns 0 after the cancel', "    M.cancel(env)                                                               # 0x800350C0\n    return 1", "    M.cancel(env)\n    return 0"),
    ('driver: nothing carried returns 0', "    if c == 0:\n        return 1\n    if m.s32(c + 0x68) != typ:", "    if c == 0:\n        return 0\n    if m.s32(c + 0x68) != typ:"),
    ('drv bomb: type 2 -> 1', "def drv_bomb(env):\n    return _small_driver(env, 2)", "def drv_bomb(env):\n    return _small_driver(env, 1)"),
    # ---- cancels
    ('cancel: release (+0x24) test removed', "    if m.s32(c + 0x24) != 0:\n        return\n    m.w32(HERO + 0x2C, 0)", "    m.w32(HERO + 0x2C, 0)"),
    ('cancel: hero link not cleared', "    m.w32(HERO + 0x2C, 0)                                                       # the carried", "    # the carried"),
    ('cancel: type test removed', "    if m.s32(c + 0x68) != typ:\n        return\n    if m.s32(c + 0x24) != 0:", "    if m.s32(c + 0x24) != 0:"),
    ('cancel beans: effect 8 -> 12', "    _cancel_obj(env, 1, 8)", "    _cancel_obj(env, 1, 12)"),
    ('cancel bomb: effect 12 -> 8', "    _cancel_obj(env, 2, 12)", "    _cancel_obj(env, 2, 8)"),
    ('cancel: effect z offset', "env.fx(0, fxid, 0, m.u32(c + 0x114), m.u32(c + 0x118), m.u32(c + 0x11C))", "env.fx(0, fxid, 0, m.u32(c + 0x114), m.u32(c + 0x118), (m.u32(c + 0x11C) + 0x80000) & M32)"),
    ('cancel: destroy omitted', "    destroy_nofx(m, c)                                                          # 0x8003A774", "    pass"),
    # ---- C62
    ('c62: fuse decrement removed', "    m.w32(s + 0x274, v - 1)                                                     # always", "    m.w32(s + 0x274, v)                                                     # always"),
    ('c62: spark period 7 -> 15', "if v < 0x3C and (v & 7) == 0:", "if v < 0x3C and (v & 15) == 0:"),
    ('c62: spark window 60 -> 64', "if v < 0x3C and (v & 7) == 0:", "if v < 0x40 and (v & 7) == 0:"),
    ('c62: spark effect 9 -> 8', "fxe = env.fx(0, 9, 0, m.u32(s + 0x114), m.u32(s + 0x118), (m.u32(s + 0x11C) + 0x80000) & M32)\n        if fxe != 0:", "fxe = env.fx(0, 8, 0, m.u32(s + 0x114), m.u32(s + 0x118), (m.u32(s + 0x11C) + 0x80000) & M32)\n        if fxe != 0:"),
    ('c62: spark x range 0x30001 -> 0x30000', "r1 = (st1 * 0x30001) >> 32", "r1 = (st1 * 0x30000) >> 32"),
    ('c62: spark y range 0x20001 -> 0x20000', "r2 = (st2 * 0x20001) >> 32", "r2 = (st2 * 0x20000) >> 32"),
    ('c62: spark x offset -0x18000 -> -0x10000', "m.w32(fxe + 0x54, (r1 + 0xFFFE8000) & M32)", "m.w32(fxe + 0x54, (r1 + 0xFFFF0000) & M32)"),
    ('c62: spark y offset -0x10000 -> -0x8000', "m.w32(fxe + 0x58, (r2 + 0xFFFF0000) & M32)", "m.w32(fxe + 0x58, (r2 + 0xFFFF8000) & M32)"),
    ('c62: only one draw step', "            st2 = (st1 * 0x7D2B89DD + 0xE06A02E7) & M32\n            m.w32(RNG, st2)", "            st2 = st1\n            m.w32(RNG, st2)"),
    ('c62: status 3 at fuse end -> 2', "        m.w32(s + 0x10, 3)                                                      # Status 3", "        m.w32(s + 0x10, 2)                                                      # Status 3"),
    ('c62: fuse end does not free the hero', "        if carrier != 0:\n            m.w32(carrier + 0x2C, 0)\n            m.w32(s + 0x28, 0)\n        return _free(env, s)", "        return _free(env, s)"),
    ('c62: carried anim 0 -> 1', "    if m.s32(s + 0x24) == 0:                                                    # carried, not released : idle animation\n        m.w32(s + 0x88, 0)", "    if m.s32(s + 0x24) == 0:\n        m.w32(s + 0x88, 1)"),
    ('c62: flight anim 2 -> 3', "    m.w32(s + 0x88, 2)                                                          # released", "    m.w32(s + 0x88, 3)                                                          # released"),
    ('c62: release does not clear +0x24', "    m.w32(s + 0x24, 0)\n    carrier = m.u32(s + 0x28)", "    carrier = m.u32(s + 0x28)"),
    ('c62: release keeps the hero link', "    if carrier != 0:\n        m.w32(carrier + 0x2C, 0)\n    m.w32(s + 0x28, 0)\n\n\ndef _free", "    m.w32(s + 0x28, 0)\n\n\ndef _free"),
    ('c62: wall bounce quarter turn', "m.w32(s + 0x8C, (m.s32(s + 0x8C) + 0x10) & 0x1F)", "m.w32(s + 0x8C, (m.s32(s + 0x8C) + 8) & 0x1F)"),
    ('c62: wall bounce without the ForceZ test', "            if m.s32(s + 0xB8) <= 0:\n                return\n            if m.s32(s + 0x278) != 0:", "            if m.s32(s + 0x278) != 0:"),
    ('c62: wall bounce without the once-only mark', "            if m.s32(s + 0x278) != 0:\n                return\n            m.w32(s + 0x278, 1)", "            m.w32(s + 0x278, 1)"),
    ('c62: landing sound 0x18 -> 0x19', "    env.sound(0x18)", "    env.sound(0x19)"),
    ('c62: landing leaves the mark', "    env.sound(0x18)                                                             # landed (or hit something)\n    m.w32(s + 0x278, 0)", "    env.sound(0x18)                                                             # landed (or hit something)"),
    ('c62: landing test needs ground only', "    if m.s32(s + 0x144) == 0 and m.s32(s + 0x220) == 0:                         # still flying", "    if m.s32(s + 0x144) == 0:                         # still flying"),
    ('c62: pit mask 0x1001 -> 0x1000', "(m.u16(p) & 0x1001) == 0x1001", "(m.u16(p) & 0x1000) == 0x1000"),
    ('c62: blocked on the ground over a pit does not explode', "            if hit:\n                m.w32(s + 0x10, 3)", "            if False:\n                m.w32(s + 0x10, 3)"),
    ('c62: landing over a pit still goes idle', "    if any(_corner_pit(m, s)):                                                  # a corner over a pit : stays in flight animation\n        return", "    if False:\n        return"),
    ('c62: hole bit +0x184 & 4 -> & 8', "    if m.u32(s + 0x184) & 4:                                                    # 0x8007A988", "    if m.u32(s + 0x184) & 8:                                                    # 0x8007A988"),
    ('c62: idle anim of a free bomb at anim 0 does not return', "    if m.s32(s + 0x88) == 0:\n        return\n    if m.s32(s + 0x13C) != 0:", "    if m.s32(s + 0x13C) != 0:"),
    # ---- D30
    ('d30: attribute 4 only', "    if n in (4, 6, 10):", "    if n in (4,):"),
    ('d30: attribute 6 -> 7', "    if n in (4, 6, 10):", "    if n in (4, 7, 10):"),
    ('d30: attribute 10 -> 11', "    if n in (4, 6, 10):", "    if n in (4, 6, 11):"),
    ('d30: full byte instead of low nibble', "    n = m.u8(atk) & 0xF", "    n = m.u8(atk)"),
    ('d30: status 3 -> 4', "    if n in (4, 6, 10):\n        m.w32(s + 0x10, 3)", "    if n in (4, 6, 10):\n        m.w32(s + 0x10, 4)"),
    # ---- E12
    ('e12: anim 1 -> 2', "    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\n# ---- C73", "    m.w32(s + 0x88, 2)\n    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\n# ---- C73"),
    ('e12: flags keep 0x30', "    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\n# ---- C73", "    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40))\n\n\n# ---- C73"),
    ('e12: no 0x40 flag', "    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\n# ---- C73", "    m.w32(s + 0x6C, (m.u32(s + 0x6C)) & 0xFFFFFFCF)\n\n\n# ---- C73"),
    ('e12: grass cut omitted', "    env.cate(s)                                                                 # 0x8003A7B0", "    pass"),
    ('e12: destroys on anim end only', "    if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:\n        env.destroy(s, -1)\n        return\n    if m.u32(s + 0x184) & 4:\n        env.destroy(s, -1)\n        return\n    env.cate(s)", "    if m.s32(s + 0xAC) == 1:\n        env.destroy(s, -1)\n        return\n    if m.u32(s + 0x184) & 4:\n        env.destroy(s, -1)\n        return\n    env.cate(s)"),
    ('e12: hole destroy omitted', "    if m.u32(s + 0x184) & 4:\n        env.destroy(s, -1)\n        return\n    env.cate(s)", "    env.cate(s)"),
    # ---- C73
    ('c73: carried anim 0 -> 1', "        if m.s32(s + 0x24) == 0:\n            m.w32(s + 0x88, 0)\n            return\n        m.w32(s + 0x88, 2)", "        if m.s32(s + 0x24) == 0:\n            m.w32(s + 0x88, 1)\n            return\n        m.w32(s + 0x88, 2)"),
    ('c73: flight anim 2 -> 3', "        m.w32(s + 0x88, 2)\n        carrier = m.u32(s + 0x28)\n        m.w32(s + 0x24, 0)\n        if carrier != 0:\n            m.w32(carrier + 0x2C, 0)\n        m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x10)", "        m.w32(s + 0x88, 3)\n        carrier = m.u32(s + 0x28)\n        m.w32(s + 0x24, 0)\n        if carrier != 0:\n            m.w32(carrier + 0x2C, 0)\n        m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x10)"),
    ('c73: release flag 0x10 -> 0x20', "m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x10)", "m.w32(s + 0x6C, m.u32(s + 0x6C) | 0x20)"),
    ('c73: landing sets status 4', "    if m.s32(s + 0x144) != 0:\n        m.w32(s + 0x10, 3)", "    if m.s32(s + 0x144) != 0:\n        m.w32(s + 0x10, 4)"),
    ('c73: landing without the ground test', "    if m.s32(s + 0x144) != 0:\n        m.w32(s + 0x10, 3)", "    if True:\n        m.w32(s + 0x10, 3)"),
    ('c73: release does not clear +0x24', "        m.w32(s + 0x88, 2)\n        carrier = m.u32(s + 0x28)\n        m.w32(s + 0x24, 0)", "        m.w32(s + 0x88, 2)\n        carrier = m.u32(s + 0x28)"),
    # ---- E21
    ('e21: pot type 0x199 -> 0x198', "m.s32(sup + 0x68) == 0x199", "m.s32(sup + 0x68) == 0x198"),
    ('e21: pot anim test removed', "and m.s32(sup + 0x88) == 0:", ":"),
    ('e21: pot anim 1 -> 2', "        m.w32(sup + 0x88, 1)\n        destroy_nofx(m, s)", "        m.w32(sup + 0x88, 2)\n        destroy_nofx(m, s)"),
    ('e21: the bean is not destroyed on the pot', "        m.w32(sup + 0x88, 1)\n        destroy_nofx(m, s)", "        m.w32(sup + 0x88, 1)"),
    ('e21: support read at +0x128', "sup = m.u32(s + 0x12C)", "sup = m.u32(s + 0x128)"),
    ('e21: destroys on anim end only', "    if m.s32(s + 0x88) == 1 and m.s32(s + 0xAC) == 1:\n        env.destroy(s, -1)\n        return\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\nHANDLERS", "    if m.s32(s + 0xAC) == 1:\n        env.destroy(s, -1)\n        return\n    m.w32(s + 0x88, 1)\n    m.w32(s + 0x6C, (m.u32(s + 0x6C) | 0x40) & 0xFFFFFFCF)\n\n\nHANDLERS"),
]
import argparse
ap = argparse.ArgumentParser()
ap.add_argument('filters', nargs='*')
ap.add_argument('--shard', default='0/1')
ap.add_argument('--n', type=int, default=2500)
ap.add_argument('--seed', type=int, default=7)
args = ap.parse_args()
k_sh, n_sh = (int(x) for x in args.shard.split('/'))
MD = 'mutdir_%d' % k_sh
os.makedirs(MD, exist_ok=True)
for f in os.listdir('.'):
    if f.endswith('.py') and not f.startswith('h3_mut') and f != 'h3_models.py':
        shutil.copy(f, MD + '/' + f)
ONLY = args.filters
caught = 0
missed, notfound = [], []
total = 0
for idx, (name, a, b) in enumerate(MUT):
    if idx % n_sh != k_sh:
        continue
    if ONLY and not any(o in name for o in ONLY):
        continue
    total += 1
    if a not in src:
        print('PATTERN NOT FOUND', name); notfound.append(name); continue
    open(MD + '/h3_models.py', 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-B', 'h3_vec.py', str(args.n), str(args.seed)], cwd=MD, capture_output=True, text=True)
    ok = r.returncode != 0
    caught += ok
    line = [l for l in r.stdout.splitlines() if l.startswith('vectors')]
    print(('CAUGHT   ' if ok else 'MISSED   ') + name, '|', line[0] if line else r.stderr[-300:], flush=True)
    if not ok:
        missed.append(name)
print('SHARD %d/%d: %d/%d mutations caught; not found %d; missed: %s' % (k_sh, n_sh, caught, total - len(notfound), len(notfound), missed))
shutil.rmtree(MD, ignore_errors=True)
sys.exit(0 if caught == total - len(notfound) and not notfound else 1)
