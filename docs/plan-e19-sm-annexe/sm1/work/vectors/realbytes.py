"""Real-bytes scenarios of E19.sm1: the real program bytes of the export, truncated right after the instructions under test (so every pc is the real pc and a jump
past the cut ends the call at that pc), run on a mini simulator of the DLL runner (only the opcodes these slices use, with the DLL's semantics read from
EventProgramRunner.cs) over a world of the vector schema. 'red' = the same run with 0x60 0x61 0x3C 0x3D skipped by their size (what the DLL does today).
Usage: python -B realbytes.py OUTFILE.tsv      (reads the export; writes OUTFILE)"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'corpus'))
import sm1lib as L  # noqa: E402
from sm1lib import S, HERO  # noqa: E402

REPO = "D:/development/repo/alundra-casaengine-project-converter"
BS = chr(92)
SIZES = {}
for line in open(REPO + "/Alundra/Events/EventOpcodeSizeTable.cs", encoding="utf-8-sig"):
    m = re.search(r"\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*\"([^\"]*)\"", line)
    if m:
        SIZES[int(m.group(1), 16)] = int(m.group(2))


def events(mapno):
    idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))
    rel = idx[str(mapno)].replace(BS, '/')
    folder = os.path.dirname(REPO + '/alundra-project/' + rel)
    name = os.path.splitext(os.path.basename(rel))[0]
    return json.load(open(folder + '/events/' + name + '.events.json', encoding='utf-8')), name


def s16(lo, hi):
    v = lo | (hi << 8)
    return v - 0x10000 if v & 0x8000 else v


class Sim:
    def __init__(self, codes, cut, entry, start, slots, owner, owner_logic, result0, flags, red, n=None, mutant=None):
        self.mutant = mutant
        self.codes = codes[:cut]
        self.entry = entry
        self.pc = start
        self.slots = slots
        self.owner = owner
        self.owner_logic = owner_logic          # slot index or None (= the owner itself)
        self.result = result0
        self.flags = set(flags)
        self.red = red
        self.p1 = 0
        self.p2 = 0
        self.t30 = 0
        self.log = []

    def vec(self, op, code):
        return dict(op=op, code=code, n=len(self.slots) - 1, logic=self.logic(), slots=self.slots, anon=[])

    def logic(self):
        return self.owner if self.owner_logic is None else self.owner_logic

    def fetch(self):
        if self.pc >= len(self.codes):
            return [0xFF] + [0] * 9
        return [self.codes[self.pc + i] if self.pc + i < len(self.codes) else 0 for i in range(10)]

    def call(self, budget=300):
        trace = []
        for _ in range(budget):
            v = self.fetch()
            op = v[0]
            if op == 0xFF:
                trace.append((self.pc, op, 'End'))
                return trace
            if op == 0x00:
                self.p1 = 0
                trace.append((self.pc, op, 'Break'))
                self.pc += 1
                return trace
            r = self.dispatch(op, v)
            trace.append((self.pc, op, 'Skipped' if (self.red and op in (0x60, 0x61, 0x3C, 0x3D)) else 'Implemented'))
            if r == 0:
                return trace
            self.p1 = 0
            self.pc += r
        trace.append((self.pc, op, 'LoopBudgetExceeded'))
        return trace

    def dispatch(self, op, v):
        if op in (0x60, 0x61, 0x3C, 0x3D):
            if self.red:
                return L.SIZE[op]
            mt = L.Montage(self.vec(op, v[:L.SIZE[op]]), hero_in_list=True)
            size, res = L.dll_case(mt, op, v[:L.SIZE[op]], self.mutant)
            if res is None:
                return size
            self.result = res
            return size
        if op == 0x02:
            return s16(v[1], v[2])
        if op == 0x03:
            return s16(v[1], v[2]) if self.result != 0 else 3
        if op == 0x04:
            return s16(v[1], v[2]) if self.result == 0 else 3
        if op == 0x49:
            return self.entry - self.pc
        if op == 0x4A:
            return self.entry - self.pc if self.result != 0 else 1
        if op == 0x4B:
            return self.entry - self.pc if self.result == 0 else 1
        if op in (0x30, 0x31):
            flag = v[1] + v[2] * 256
            is_set = flag in self.flags
            return s16(v[3], v[4]) if is_set == (op == 0x30) else 5
        if op == 0x05:
            self.flags.add(v[1] + v[2] * 256)
            return 3
        if op == 0x06:
            self.flags.discard(v[1] + v[2] * 256)
            return 3
        if op == 0x73:
            self.t30 = v[1]
            return 2
        if op == 0x74:
            self.t30 -= 1
            return 3 if self.t30 <= 0 else s16(v[1], v[2])
        if op == 0x37:
            if self.p1 != self.pc:
                self.p1 = self.pc
                self.p2 = 0
                return 0
            c = self.p2
            self.p2 = c + 1
            return 2 if c >= v[1] else 0
        if op == 0x1A:
            return 2
        if op == 0x42:
            self.owner_logic = 0
            return 1
        if op == 0x43:
            mt = L.Montage(self.vec(0x60, [0x60, v[1]]), hero_in_list=True)
            group = L.dll_search(mt, v[1])
            if group:
                self.owner_logic = group[-1].k
                self.result = 1
            else:
                self.result = 0
            return 2
        raise ValueError('opcode 0x%02X is not simulated' % op)


DEFAULT_MUTANTS = ()


def scenario(sid, title, mapno, slot, prog, start, cut, slots, owner, result0, flags=(), calls=2, owner_logic=None, mutants=None):
    if mutants is None:
        mutants = DEFAULT_MUTANTS
    d, name = events(mapno)
    codes = d['Codes']
    entry = d['EventCodes%sTable' % slot][prog]
    out = dict(id=sid, title=title, map=mapno, world=name, program='%s[%d]' % (slot, prog), entry=entry, start=start, cut=cut,
               bytes_hex=' '.join('%02X' % b for b in codes[start:cut]), result0=result0, flags_at_start=sorted(flags), calls=calls)
    variants = [('expected', False, None), ('red', True, None)] + [('mut:' + m, False, m) for m in mutants]
    for tag, red, mut in variants:
        sim = Sim(codes, cut, entry, start, slots, owner, owner_logic, result0, flags, red, mutant=mut)
        per = []
        try:
            for _ in range(calls):
                tr = sim.call()
                per.append(dict(end_pc=sim.pc, result=sim.result, logic=sim.logic(), flags=sorted(sim.flags), trace=['%d:%02X:%s' % t for t in tr]))
        except ValueError:
            if mut is None:
                raise
            per.append(dict(end_pc=-1, result=-1, logic=-1, flags=[], trace=['diverged']))   # the mutant ran into an opcode the model does not simulate: it diverges
        out[tag] = per
    return out


def hexops(sid):
    return sid


def main():
    plate = lambda ref, **kw: S(status=2, ref=ref, flags=0x60C0, **kw)                    # noqa: E731
    obj = lambda ref, **kw: S(status=2, ref=ref, flags=0x332384, **kw)                    # noqa: E731
    H = lambda **kw: HERO(tile=(1000, 1000, 1000), **kw)                                  # noqa: E731
    R = []
    # RB-01 27 C[26] (plate 25 waits for its object 31)
    for label, riding in (('the object rides the plate', 1), ('the object rides nothing', None)):
        R.append(scenario('RB-01', '27 C[26] @984: ' + label, 27, 'C', 26, 984, 990, [H(), plate(25), obj(31, riding=riding)], 1, 0))
    # RB-02 27 C[2] phase 1 (plate 1; objects 5..8), T29 clear
    for label, who in (('object 5 rides the plate (the right one in phase 1)', 5), ('object 6 rides the plate (a wrong one in phase 1)', 6), ('nobody rides', None)):
        sl = [H(), plate(1)] + [obj(r, riding=(1 if r == who else None)) for r in (5, 6, 7, 8)]
        R.append(scenario('RB-02', '27 C[2] @604: ' + label, 27, 'C', 2, 604, 635, sl, 1, 0, calls=2))
    # RB-02 (400 C[1], plate record 0, objects 4..7, phase 1)
    for label, who in (('object 4 rides the plate (the right one in phase 1)', 4), ('object 5 rides the plate (a wrong one in phase 1)', 5), ('nobody rides', None)):
        sl = [H(), plate(0)] + [obj(r, riding=(1 if r == who else None)) for r in (4, 5, 6, 7)]
        R.append(scenario('RB-02', '400 C[1] @440: ' + label, 400, 'C', 1, 440, 471, sl, 1, 0, calls=2))
    # RB-03 27 C[36] stack of four on the plate 35 (records 45..48)
    for label, chain in (('the four objects are stacked', True), ('the top object is not stacked', False)):
        sl = [H(), S(status=2, ref=35, flags=0x60C0), obj(45, riding=1), obj(46, riding=2), obj(47, riding=3), obj(48, riding=(4 if chain else None))]
        R.append(scenario('RB-03', '27 C[36] @1106: ' + label, 27, 'C', 36, 1106, 1136, sl, 1, 0, calls=2))
    # RB-04 29 C[17]
    for label, riding in (('rides', 1), ('does not ride', None)):
        R.append(scenario('RB-04', '29 C[17] @813: object 20 ' + label, 29, 'C', 17, 812, 818, [H(), plate(5), obj(20, riding=riding)], 1, 0))
    # RB-05 30 C[19] (the same wait behind a flag test: 0x83FC clear)
    for label, riding in (('object 22 rides', 1), ('object 22 does not ride', None)):
        R.append(scenario('RB-05', '30 C[19] @1686: ' + label, 30, 'C', 19, 1680, 1691, [H(), plate(5), obj(22, riding=riding)], 1, 0))
    # RB-06 113 C[9]
    for label, riding in (('rides', 1), ('does not ride', None)):
        R.append(scenario('RB-06', '113 C[9] @993: object 10 ' + label, 113, 'C', 9, 992, 998, [H(), plate(5), obj(10, riding=riding)], 1, 0))
    # RB-07 113 C[10] toggle
    sl_on = [H(), plate(5), obj(11, riding=1)]
    R.append(scenario('RB-07', '113 C[10] @1069: object 11 present for 3 calls', 113, 'C', 10, 1068, 1095, sl_on, 1, 0, calls=3))
    R.append(scenario('RB-07', '113 C[10] @1083: the rider has left, flag 0x8064 was on', 113, 'C', 10, 1083, 1095, [H(), plate(5), obj(11, riding=None)], 1, 1, flags=(0x8064,), calls=1))
    # RB-08 136 C[5]
    for label, riding in (('the hero stands on the switch', 1), ('the hero is elsewhere', None)):
        R.append(scenario('RB-08', '136 C[5] @1579: ' + label, 136, 'C', 5, 1577, 1584, [H(riding=riding), S(status=2, ref=5, flags=0x32080)], 1, 0, calls=3))
    # RB-09 159 B[5]: logic = hero
    for label, riding, r0 in (('the hero rides record 13, Result 0 before', 1, 0), ('the hero rides record 13, Result 7 before', 1, 7), ('the hero rides nothing, Result 7 before', None, 7), ('the hero rides nothing, Result 0 before', None, 0)):
        sl = [H(riding=riding), S(status=2, ref=13, flags=0x20C0)]
        R.append(scenario('RB-09', '159 B[5] @729: ' + label, 159, 'B', 5, 728, 732, sl, 0, r0, calls=2))
    # RB-10 159 C[13]
    for label, who in (('record 3 rides the bubble', 3), ('record 10 rides the bubble', 10), ('nobody rides', None)):
        sl = [H(), S(status=2, ref=12, flags=0x20C0)] + [obj(r, riding=(1 if r == who else None)) for r in (3, 4, 10)]
        R.append(scenario('RB-10', '159 C[13] @1654: ' + label, 159, 'C', 13, 1654, 1663, sl, 1, 0, calls=1))
    # RB-11 226 B[3]
    for label, tile, r0 in (('an entity at (43,50,12), Result 0 before', (43, 50, 12), 0), ('an entity at (43,50,12), Result 1 before', (43, 50, 12), 1),
                            ('an entity at (43,52,12), Result 1 before', (43, 52, 12), 1), ('an entity at (43,50,13), Result 1 before', (43, 50, 13), 1), ('nobody, Result 1 before', None, 1), ('nobody, Result 0 before', None, 0)):
        sl = [H(), S(status=2, ref=11, flags=0x80, tile=(tile if tile else (1, 1, 1)))]
        R.append(scenario('RB-11', '226 B[3] @247: ' + label, 226, 'B', 3, 247, 258, sl, 0, r0, calls=2))
    # RB-12 398 B[N]: the item N rides Lurvy (record 0)
    for n, (b, start) in enumerate([(1, 86), (2, 266), (3, 446), (4, 626), (5, 821), (6, 1041), (7, 1249)], start=1):
        for label, riding in (('rides Lurvy', 2), ('does not ride', None)):
            sl = [H(), S(status=2, ref=0, flags=0x3A180), S(status=2, ref=b, flags=0x230180)]
            sl = [H(), S(status=2, ref=0, flags=0x3A180), S(status=2, ref=n, flags=0x230180, riding=(1 if riding else None))]
            R.append(scenario('RB-12', '398 B[%d] @%d: item %d %s' % (n, start + 2, n, label), 398, 'B', n, start, start + 8, sl, 0, 0, calls=1, owner_logic=0))
    # RB-13 77 C[14]
    for label, riding, r0 in (('the wood rides the hero, Result 0 before', 0, 0), ('the wood rides the hero, Result 1 before', 0, 1), ('the wood rides nothing, Result 1 before', None, 1)):
        R.append(scenario('RB-13', '77 C[14] @1871: ' + label, 77, 'C', 14, 1869, 1876, [H(), S(status=2, ref=20, flags=0x301E2, riding=riding)], 1, r0, calls=1))
    for label, riding, r0 in (('the wood rides the hero, Result 0 before', 0, 0), ('the wood rides the hero, Result 1 before', 0, 1), ('the wood rides nothing, Result 1 before', None, 1)):
        R.append(scenario('RB-14', '78 C[19] @1679: ' + label, 78, 'C', 19, 1677, 1684, [H(), S(status=2, ref=20, flags=0x301E2, riding=riding)], 1, r0, calls=1))
    out = sys.argv[1]
    json.dump(R, open(out, 'w', encoding='utf-8', newline='\n'), indent=1, sort_keys=True)
    # compact view
    for s in R:
        print(s['id'], '|', s['title'], '| bytes', s['bytes_hex'][:70], '| start', s['start'], 'cut', s['cut'], 'Result0', s['result0'])
        for tag in ('expected', 'red'):
            print('   ', tag, [(p['end_pc'], p['result'], p['logic']) for p in s[tag]], ' trace of last call:', ' '.join(s[tag][-1]['trace'][:12]))
    return R


if __name__ == '__main__':
    main()
