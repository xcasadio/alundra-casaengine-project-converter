"""mut.py - sensitivity check of vec.py: each mutation of a model must be caught by the vectors (exit 0 = every mutation was detected)."""
import subprocess, sys, re, shutil, os
src = open('models.py', encoding='utf-8').read()
MUT = [
    ('c18 descend threshold 0x180000 -> 0x180001', 'dz > 0x180000 else 1) if dz > 0xFFFFF', 'dz > 0x180001 else 1) if dz > 0xFFFFF'),
    ('d12 parry threshold 0x46 -> 0x45', 'if r >= 0x46:', 'if r >= 0x45:'),
    ('d12 mace also parries at anim 7: removed', 'if a < 2 or a == 7:', 'if a < 2:'),
    ('c16 dust cadence mask 7 -> 15', '(e.u32(W28C) & 7) == 0', '(e.u32(W28C) & 15) == 0'),
    ('c17 iron-orc side test -1 -> -2', "0x18 if dx < -1 else 0)\n                return\n            if dy >= -4:", "0x18 if dx < -2 else 0)\n                return\n            if dy >= -4:"),
    ('c19 close window 3 -> 2', 'rel[0] < 3 and rel[1] < 3 and rel[2] <= 0x100000', 'rel[0] < 2 and rel[1] < 3 and rel[2] <= 0x100000'),
    ('c15 draw count (below(5) -> below(4))', 'if rng.below(5) == 0:\n                    j.append((\'sound\', 0x94))\n                e.set8(B0, 3)', 'if rng.below(4) == 0:\n                    j.append((\'sound\', 0x94))\n                e.set8(B0, 3)'),
    ('c18 attack branch after wall (decompilation flaw) : falls into the attack', "e.set32(TDIR, (e.u32(TDIR) + 4) & 0x1F)", "e.set32(TDIR, (e.u32(TDIR) + 4) & 0x1F); c18_attack(e, hero, gdt)"),
    ('c18 uses FloorHeight instead of PosZ (decompilation flaw)', 'rel = relpos(e, hero, from_posz=True)', 'rel = relpos(e, hero, from_posz=False)'),
    ('start_flying second draw removed', "e.set32(TDIR, rng.below(0x20))\n    e.set32(TANIM, anim & 0xFF)", "e.set32(TDIR, 0)\n    e.set32(TANIM, anim & 0xFF)"),
]
caught = 0
for name, a, b in MUT:
    if a not in src:
        print('PATTERN NOT FOUND', name); continue
    os.makedirs('mutdir', exist_ok=True)
    for f in ('h.py', 'vec.py', 'dis2.py', 'exelib.py'):
        shutil.copy(f, 'mutdir/' + f)
    shutil.copytree('emu', 'mutdir/emu', dirs_exist_ok=True)
    open('mutdir/models.py', 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-B', 'vec.py', '1500'], cwd='mutdir', capture_output=True, text=True)
    ok = r.returncode != 0
    caught += ok
    tail = [l for l in r.stdout.splitlines() if 'mismatches' in l and not l.endswith(' 0 mismatches')]
    print(('CAUGHT   ' if ok else 'MISSED   ') + name, '|', '; '.join(t.strip() for t in tail)[:160])
print('%d/%d mutations caught' % (caught, len(MUT)))
sys.exit(0 if caught == len(MUT) else 1)
