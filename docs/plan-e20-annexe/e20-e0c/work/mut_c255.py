import pickle, re, types, sys, importlib
sys.path.insert(0, '.')
import models as M
SRC = open('c255model.py').read()
REC = pickle.load(open('cache/c255_vectors.pkl', 'rb'))
# reuse Ext from t_c255 without running it: copy the class
class Ext:
    def __init__(self, rv, chest_img):
        self.rv = rv; self.j = []; self.chest = chest_img; self.nb = False; self.bi = 0
    def busy(self):
        v = self.rv['busy'][self.bi % len(self.rv['busy'])]; self.bi += 1; self.j.append(('busy',)); return v
    def cdbusy(self): self.j.append(('cdbusy',)); return self.rv['cd']
    def give(self, item): self.j.append(('give', item)); return self.rv['give']
    def strcpy(self, src): self.j.append(('strcpy', src)); self.nb = True
    def strcat(self, src): self.j.append(('strcat', src))
    def bufclear(self): self.nb = False
    def bufnonempty(self): return self.nb
    def open(self, mode): self.j.append(('open', mode))
    def anim(self, n): self.j.append(('anim', n))
    def sound(self, n): self.j.append(('sound', n))
    def setflag(self, f): self.j.append(('setflag', f & 0xFFFFFFFF))
    def apply(self, item): self.j.append(('apply', item))
    def block(self): self.j.append(('block',))
    def unblock(self, who): self.j.append(('unblock', who))
    def bgm0(self): self.j.append(('bgm0',))
    def cd(self, n): self.j.append(('cd', n))
    def stopsound(self): self.j.append(('stopsound',))
    def destroy_light(self): self.j.append(('destroy_light',))
    def chest40(self, ptr): return self.chest.g(0x40)
    def chest88zero(self, ptr): self.chest.s(0x88, 0)
E_ADDR = 0x80127D30 + 9 * 0x294
def run(mod, vecs):
    bad = 0
    for v in vecs:
        em = mod.Img(bytearray(v['e'])); hm = mod.Img(bytearray(v['h'])); chm = mod.Img(bytearray(v['ch']))
        X = Ext(v['rv'], chm); jm = []
        try:
            for t in range(v['K']):
                X.j = []
                mod.c255(em, hm, X, v['thr'], v['w'], v['c29'])
                jm.append(list(X.j))
                if v['tick_in'][t] is not None: em.s(0x144, v['tick_in'][t])
                if ('destroy_light',) in X.j: break
        except Exception:
            bad += 1; continue
        if jm != v['jr'] or bytes(em.raw) != v['fe'] or bytes(chm.raw) != v['chr']:
            bad += 1
    return bad
def load(src):
    mod = types.ModuleType('c255mut'); exec(compile(src, 'c255mut', 'exec'), mod.__dict__); return mod
base = run(load(SRC), REC)
print('baseline mismatches', base)
MUT = [
 ('* 3 * 4', '* 3 * 5'), ('* 3 * 4) & M32) >> 4', '* 3 * 4) & M32) >> 3'),
 ('not (s32((thr_u16 << 8)) < v)', 'not (s32((thr_u16 << 8)) <= v)'),
 ('e.s(0xB8, e.g(0x284))', 'e.s(0xB8, 0)'),
 ('if e.g(0x144) != 0 and e.g(0x284) != 0:', 'if e.g(0x144) != 0 or e.g(0x284) != 0:'),
 ('if e.g(0x144) != 0 and', 'if False and'),
 ('((d + 1) & M32) < 2', '((d + 1) & M32) < 1'), ('((d + 1) & M32) < 2', '((d + 1) & M32) < 3'),
 ('d == 0x78', 'd == 0x77'), ('e.s(0x1CC, 0x78)', 'e.s(0x1CC, 0x77)'), ('if d == 0:', 'if d == 1:'),
 ('if e.gs(0xB8) > 0: return', 'if e.gs(0xB8) >= 0: return'), ('e.s(0x27C, 1)\n        return', 'e.s(0x27C, 2)\n        return'),
 ('a0 < s32(hero.g(sz) + 1)', 'a0 <= s32(hero.g(sz) + 1)'), ('a0 < s32(hero.g(sz) + 1)', 'a0 < s32(hero.g(sz))'),
 ('s32(v1 - a1) < s32(e.g(sz) + 1)', 's32(v1 - a1) <= s32(e.g(sz) + 1)'), ('s32(v1 - a1) < s32(e.g(sz) + 1)', 's32(v1 - a1) < s32(hero.g(sz) + 1)'),
 ('if a0 >= 0:', 'if a0 > 0:'),
 ('for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):', 'for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4)):'),
 ('(0x1E0, 0x1F8)', '(0x1E0, 0x1F4)'),
 ('r == 0 and e.g(0x274) != 0', 'r == 0 or e.g(0x274) != 0'), ('r == 0 and e.g(0x274) != 0', 'r == 0'),
 ('X.setflag(e.g(0x280))\n    s0', 'pass\n    s0'),
 ("X.strcat(('str', 0x45))", "X.strcat(('str', 0x44))"),
 ('X.sound(s0)\n        if X.bufnonempty(): X.open(0)', 'X.sound(s0)\n        if X.bufnonempty(): X.open(1)'),
 ('e.s(0x27C, e.g(0x27C) + 2); return\n    if const', 'e.s(0x27C, e.g(0x27C) + 1); return\n    if const'),
 ('X.block(); X.bgm0(); X.cd(0xB)', 'X.bgm0(); X.cd(0xB)'), ('X.cd(0xB)', 'X.cd(0xC)'), ('X.anim(4); return\n    X.strcat', 'X.anim(3); return\n    X.strcat'),
 ('if sub == 3:\n        if X.busy(): return\n        X.unblock', 'if sub == 3:\n        X.unblock'),
 ("X.unblock(('self',)); X.destroy_light(); return", "X.destroy_light(); return"),
 ('if sub == 2:\n        return _wait_cd(e, X)', 'if sub == 2:\n        return'),
 ('X.anim(3); X.stopsound()', 'X.anim(3)'), ('X.anim(3); X.stopsound()', 'X.anim(2); X.stopsound()'),
 ('if st274 == 2:', 'if st274 == 3:'),
 ('X.apply(item); X.setflag(X.chest40(chest))', 'X.apply(item)'),
 ('X.chest88zero(chest)', 'pass'),
 ("X.strcpy(('str', 0x4C))", "X.strcpy(('str', 0x4D))"), ("X.strcat(('str', 0x4D))", "X.strcat(('str', 0x4C))"),
 ("X.strcpy(('str', 0x4E))", "X.strcpy(('str', 0x4F))"),
 ('if e.g(0x288) != 0:', 'if e.g(0x288) == 0:'),
 ("if s0 == 0: X.strcat(('str', 0x46))", "if s0 != 0: X.strcat(('str', 0x46))"),
 ('e.s(0xB8, 0)\n                if X.busy()', 'if X.busy()'),
 ('item = (e.g(0x68) - 0x1E) & M32', 'item = (e.g(0x68) - 0x1D) & M32'),
 ('if const_29bcc != 0 or word_99858 != 0:', 'if const_29bcc != 0 and word_99858 != 0:'),
 ('e.s(0x278, d - 1); return', 'e.s(0x278, d - 2); return'),
]
killed = 0; survivors = []
for old, new in MUT:
    assert old in SRC, old
    mod = load(SRC.replace(old, new, 1))
    b = run(mod, REC)
    if b > 0: killed += 1
    else: survivors.append((old, new))
print('mutants', len(MUT), 'killed', killed)
for s in survivors: print('SURVIVOR', s)
