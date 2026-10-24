"""dll_model.py - walk-release-disc W2/W4: a TRANSCRIPTION (not an execution) of the DLL's horizontal force of the hero in the open,
Alundra/Physics/ScriptedMotion.cs  TickPlayer -> RunOneMotionTick -> RunOneKinematicTick -> PlanForce (:707-775) + IncrementForce (:1008-1033),
fed by Alundra/Hero/PlayerManager.cs MovePlayer (pad -> direction, Idle/Moving, :330-380, ApplyGroundOrAirTail), no obstacle, no ice, no water.
Two data sets for AnimSetsByAnim of the hero:
  REAL = alundra-project/Data/sprite-records.json, record 192c2eeb-... (the hero), anim 0 = Speed 0 Acceleration 1 ; anim 1 = Speed 208 Acceleration 1  (what production uses: WorldProxy.ApplyHeroSpriteHeader)
  RIG  = Alundra.Tests/Support/JumpHeroRig.cs:131-139 (the default of JumpHeroRig/CarryRig):                anim 0 = Speed 0 (Acceleration 0)           ; anim 1 = Speed 208 Acceleration 1
Prints, for the pad scripts of w1_trace.py, the ForceX/ForceY and PosX/PosY per tick of each data set next to the ORACLE (binary) values, and the verdict.
Run from the copy of critic-scenarios:  python -B -I dll_model.py
"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import gl
import w1_trace as W

OFFX = [0x0, -0x96, -0x126, -0x1a6, -0x21f, -0x27f, -0x2c6, -0x2f1, -0x300, -0x2f1, -0x2c6, -0x27f, -0x21f, -0x1a6, -0x126, -0x96, 0x0, 0x96, 0x126, 0x1a6, 0x21f, 0x27f, 0x2c6, 0x2f1, 0x300, 0x2f1, 0x2c6, 0x27f, 0x21f, 0x1a6, 0x126, 0x96]
OFFY = [0x200, 0x1f6, 0x1d9, 0x1aa, 0x16a, 0x11c, 0xc4, 0x64, 0x0, -0x64, -0xc4, -0x11c, -0x16a, -0x1aa, -0x1d9, -0x1f6, -0x200, -0x1f6, -0x1d9, -0x1aa, -0x16a, -0x11c, -0xc4, -0x64, 0x0, 0x64, 0xc4, 0x11c, 0x16a, 0x1aa, 0x1d9, 0x1f6]
# the DLL tables (AnimationTables.cs:71-90) as short[] ; checked equal to the ones above by assert below
DLL_X = [0x0, 0xff6a, 0xfeda, 0xfe5a, 0xfde1, 0xfd81, 0xfd3a, 0xfd0f, 0xfd00, 0xfd0f, 0xfd3a, 0xfd81, 0xfde1, 0xfe5a, 0xfeda, 0xff6a, 0x0, 0x96, 0x126, 0x1a6, 0x21f, 0x27f, 0x2c6, 0x2f1, 0x300, 0x2f1, 0x2c6, 0x27f, 0x21f, 0x1a6, 0x126, 0x96]
DLL_X = [v - 0x10000 if v >= 0x8000 else v for v in DLL_X]
DLL_Y = [0x200, 0x1f6, 0x1d9, 0x1aa, 0x16a, 0x11c, 0xc4, 0x64, 0x0, 0xff9c, 0xff3c, 0xfee4, 0xfe96, 0xfe56, 0xfe27, 0xfe0a, 0xfe00, 0xfe0a, 0xfe27, 0xfe56, 0xfe96, 0xfee4, 0xff3c, 0xff9c, 0x0, 0x64, 0xc4, 0x11c, 0x16a, 0x1aa, 0x1d9, 0x1f6]
DLL_Y = [v - 0x10000 if v >= 0x8000 else v for v in DLL_Y]
assert DLL_X == OFFX and DLL_Y == OFFY
DIR_BY_BUTTONS = [None, 0x10, 0x18, 0x14, 0x0, None, 0x1C, None, 0x8, 0xC, None, None, 0x4, None, None, None]

REAL = {0: (0, 1), 1: (208, 1)}
RIG = {0: (0, 0), 1: (208, 1)}


def check_real():
    d = json.load(open(r'D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json', encoding='utf-8-sig'))
    a = {x['Anim']: x for x in d['192c2eeb-bdda-5bb4-92d6-39a4ae298307']['AnimSets']}
    assert (a[0]['Speed'], a[0]['Acceleration']) == REAL[0] and (a[1]['Speed'], a[1]['Acceleration']) == REAL[1]


def increment(force, target, step):
    if target != force:
        if force < target:
            force += step
            if target < force:
                return target
        else:
            force -= step
            if force < target:
                return target
    return force


class Hero:
    def __init__(self, sets, x, y, direction):
        self.sets = sets
        self.x, self.y = x * 65536, y * 65536
        self.fx = self.fy = 0
        self.tfx = self.tfy = 0
        self.stx = self.sty = 0
        self.speed = 0
        self.acc = 0
        self.cur_dir = 0
        self.target_dir = direction
        self.anim = 0

    def tick(self, hold):
        nib = hold >> 12
        d = DIR_BY_BUTTONS[nib] if nib < 16 else None
        if d is None:
            d = self.target_dir
        self.target_dir = d
        self.anim = 1 if nib != 0 else 0          # ApplyGroundOrAirTail: buttonsHold != 0 -> Moving else Idle (PlayerManager.cs)
        speed, acc = self.sets.get(self.anim, (0, 0))
        acc &= 0xF
        if self.speed != speed or self.target_dir != self.cur_dir or self.acc != acc:
            self.cur_dir = self.target_dir
            self.speed, self.acc = speed, acc
            self.tfx = OFFX[d] * speed
            self.tfy = OFFY[d] * speed
            self.stx = abs(self.tfx - self.fx) >> acc
            self.sty = abs(self.tfy - self.fy) >> acc
        self.fx = increment(self.fx, self.tfx, self.stx)
        self.fy = increment(self.fy, self.tfy, self.sty)
        self.x += self.fx
        self.y += self.fy


def model_rows(sets, x, y, direction, pads, total):
    h = Hero(sets, x, y, direction)
    rows = []
    for t in range(1, total + 1):
        h.tick(pads.get(t, 0))
        rows.append((t, h.fx, h.fy, h.x, h.y))
    return rows


SCEN = ['release_open', 'release_open_left', 'release_open_down', 'release_open_up', 'release_diag', 'tap1', 'tap2', 'reversal', 'release_regrab']
START = {'release_open': (100, 100, 24), 'release_open_left': (300, 100, 8), 'release_open_down': (100, 100, 0), 'release_open_up': (100, 300, 16), 'release_diag': (100, 100, 0),
         'tap1': (100, 100, 24), 'tap2': (100, 100, 24), 'reversal': (100, 100, 24), 'release_regrab': (100, 100, 24)}


def main():
    check_real()
    allok_real = allok_rig = True
    for n in SCEN:
        name, m, seq, = W.scenario(n)
        pads, total = gl.pad_script(seq)
        rows = gl.run(m, total, pads, post=W.snap)
        sx, sy, sd = START[n]
        mr = model_rows(REAL, sx, sy, sd, pads, total)
        mg = model_rows(RIG, sx, sy, sd, pads, total)
        okr = all((r['w1']['fx'], r['w1']['fy'], r['w1']['px'], r['w1']['py']) == (a[1], a[2], a[3], a[4]) for r, a in zip(rows, mr))
        okg_t = [r['tick'] for r, a in zip(rows, mg) if (r['w1']['fx'], r['w1']['fy'], r['w1']['px'], r['w1']['py']) != (a[1], a[2], a[3], a[4])]
        allok_real &= okr
        print('%-18s oracle == model(REAL: Idle acc 1): %-5s | oracle vs model(RIG: Idle acc 0) differs at ticks %s' % (n, okr, okg_t if okg_t else 'none'))
        if n == 'release_open':
            print('   tick  oracle(fx, x)            REAL(fx, x)              RIG(fx, x)')
            for r, a, g in zip(rows, mr, mg):
                if r['tick'] >= 8:
                    print('   t%02d  %8.4f %10.4f   %8.4f %10.4f   %8.4f %10.4f' % (r['tick'], r['w1']['fx'] / 65536, r['w1']['px'] / 65536, a[1] / 65536, a[3] / 65536, g[1] / 65536, g[3] / 65536))
    print('ALL oracle == REAL model:', allok_real)


if __name__ == '__main__':
    main()
