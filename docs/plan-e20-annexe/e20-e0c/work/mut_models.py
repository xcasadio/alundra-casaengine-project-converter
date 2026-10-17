import os, shutil, subprocess, sys, re, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = open(os.path.join(HERE, 'models.py')).read()
GSRC = open(os.path.join(HERE, 'give_model.py')).read()
MUT = [
 ('models.py', 'return 1 if item in (0x26, 0x51, 0x52) and s32(mpmax) == 0 else 0', 'return 1 if item in (0x26, 0x51) and s32(mpmax) == 0 else 0'),
 ('models.py', 'return 1 if item in (0x26, 0x51, 0x52) and s32(mpmax) == 0 else 0', 'return 1 if item in (0x26, 0x51, 0x52, 0x25) and s32(mpmax) == 0 else 0'),
 ('models.py', 'return 1 if item in (0x26, 0x51, 0x52) and s32(mpmax) == 0 else 0', 'return 1 if item in (0x26, 0x51, 0x52) and s32(mpmax) <= 1 else 0'),
 ('models.py', "(600 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0,", "(60 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0,"),
 ('models.py', "(600 if itab_b4(item) else M32), 0x27C: 0, 0x280: ent[0x40] & M32,", "(600 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0,"),
 ('models.py', "out['spawn'] = (0, 0, item + 0x1E, X & M32, Y & M32, Z & M32, 0)", "out['spawn'] = (0, 0, item + 0x1E, X & M32, Y & M32, (Z - 1) & M32, 0)"),
 ('models.py', "out['spawn'] = (0, 0, item + 0x1E, X & M32, Y & M32, Z & M32, 0)", "out['spawn'] = (0, 0, item + 0x1E, X & M32, Z & M32, Y & M32, 0)"),
 ('models.py', "out['sounds'] = [0x54]\n    out['ret'] = 1\n    return out\n\ndef spawn_contents", "out['sounds'] = [0x55]\n    out['ret'] = 1\n    return out\n\ndef spawn_contents"),
 ('models.py', "0xB8: 0xA0000, 0x274: 1, 0x278: (600 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0, 0x284: 0xA0000", "0xB8: 0xA0000, 0x274: 1, 0x278: (600 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0, 0x284: 0xA0001"),
 ('models.py', "if (v1 & 0x7FFF) >= 0x800: v1 = 0", "if (v1 & 0x7FFF) > 0x800: v1 = 0"),
 ('models.py', "if word & (1 << (v1 & 31)):", "if word & (1 << ((v1 & 31) ^ 1)):"),
 ('models.py', "bank = flag_bank_t if (v1 & 0x8000) else flag_bank_g", "bank = flag_bank_g if (v1 & 0x8000) else flag_bank_t"),
 ('models.py', "code = rec['c12'] & 0xFF\n            if code == 0: code = hdr_contents & 0xFF", "code = rec['c12'] & 0xFF"),
 ('models.py', "if c >= 0x100: return 0, s, draws", "if c >= 0x101: return 0, s, draws"),
 ('models.py', "return (c if c < 0x62 else 0), s, draws", "return (c if c < 0x61 else 0), s, draws"),
 ('models.py', "s = lcg(s); draws += 1\n        c = rtab(c & 0x7F, s >> 28)", "s = lcg(lcg(s)); draws += 1\n        c = rtab(c & 0x7F, s >> 28)"),
 ('models.py', "c = rtab(c & 0x7F, s >> 28)", "c = rtab(c & 0x7F, (s >> 27) & 15)"),
 ('give_model.py', "n = {0x45: 1, 0x46: 5, 0x47: 10, 0x48: 30}[item]", "n = {0x45: 1, 0x46: 5, 0x47: 10, 0x48: 25}[item]"),
 ('give_model.py', "st['money'] = 9999 if v >= 0x2710", "st['money'] = 9999 if v >= 0x270F"),
 ('give_model.py', "st['hp'] = clamp_hp(st['hp'] + 2, st['hpmax'])", "st['hp'] = clamp_hp(st['hp'] + 3, st['hpmax'])"),
 ('give_model.py', "amt = amt if amt > 0 else 1\n        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])\n        fx = effect(0, False)", "amt = amt if amt > 1 else 2\n        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])\n        fx = effect(0, False)"),
 ('give_model.py', "amt = amt if amt >= 5 else 5\n        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])\n        for k in range(4):\n            fx = effect(0, True)", "amt = amt if amt >= 4 else 4\n        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])\n        for k in range(4):\n            fx = effect(0, True)"),
 ('give_model.py', "for k in range(4):\n            fx = effect(2, True)", "for k in range(3):\n            fx = effect(2, True)"),
 ('give_model.py', "if not (before < after):", "if not (before <= after):"),
 ('give_model.py', "return 1 if M.itab_b4(item) == 0 else 0", "return 1 if M.itab_b4(item) != 0 else 0"),
]
def run_tests(tmp):
    res = {}
    for t in ('t_choose.py', 't_spawn.py', 't_init.py', 't_give.py'):
        try:
            out = subprocess.run([sys.executable, '-B', t], cwd=tmp, capture_output=True, text=True, timeout=900).stdout
        except subprocess.TimeoutExpired:
            out = 'timeout'
        mm = re.findall(r'mismatches (\d+)', out)
        res[t] = sum(int(x) for x in mm) if mm else 'ERR'
    return res
killed = 0; surv = []
for fn, old, new in MUT:
    src = SRC if fn == 'models.py' else GSRC
    assert old in src, old
    tmp = tempfile.mkdtemp(prefix='mut_', dir=os.path.join(HERE, 'cache'))
    for f in os.listdir(HERE):
        p = os.path.join(HERE, f)
        if os.path.isfile(p) and f.endswith('.py'): shutil.copy(p, tmp)
    shutil.copytree(os.path.join(HERE, 'emu'), os.path.join(tmp, 'emu'))
    open(os.path.join(tmp, fn), 'w').write(src.replace(old, new, 1))
    r = run_tests(tmp)
    dead = any((v == 'ERR' or v > 0) for v in r.values())
    print(('KILLED  ' if dead else 'SURVIVOR'), fn, old[:60].replace('\n', ' '), '->', new[:60].replace('\n', ' '), r, flush=True)
    killed += dead
    if not dead: surv.append((fn, old, new))
    shutil.rmtree(tmp, ignore_errors=True)
print('mutants', len(MUT), 'killed', killed)
