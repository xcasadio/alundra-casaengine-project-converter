"""sim398.py - control-flow trace of the Lurvy shop map programs B[1]..B[4] of map 398 under the semantics of Alundra/Events/EventProgramRunner.cs AT HEAD:
  * 0x61 (Is riding entity 2) has NO case: UnknownOpcode returns the table size (2) and leaves state.Result untouched (EventProgramRunner.cs:3109-3134);
  * 0x70 writes Result = logic entity IsOnGround (the hero, on the ground: 1); 0x03 / 0x04 jump on Result (EventProgramRunner.cs:572-580).
A MODEL of the control flow of the exported bytes (it does not run the DLL): it shows which branch the first pass takes when the hero stands on the ground.
READ-ONLY. python -B -I -X utf8 sim398.py [implemented]   ('implemented' = 0x61 gives Result 0, as the binary does for an item that is not on Lurvy)"""
import sys
sys.path.insert(0, '.')
import evlib as L
impl = len(sys.argv) > 1 and sys.argv[1] == 'implemented'
doc = L.load_events(398)
codes = doc['Codes']


def s16(lo, hi):
    v = lo | hi << 8
    return v - 0x10000 if v & 0x8000 else v


def trace(prog, hero_on_ground=1, item_on_ground=0, item_tile=None, steps=60):
    pc = doc['EventCodesBTable'][prog]
    result = 0
    out = []
    waits = 0
    for _ in range(steps):
        op = codes[pc]
        size = L.SIZES[op][0]
        v = codes[pc:pc + size]
        nm = L.SIZES[op][1]
        if op == 0x37:
            waits += 1
            out.append('@%d Wait' % pc)
            pc += size
            if waits > 3:
                break
            continue
        if op == 0x70:
            result = hero_on_ground
            pc += size
        elif op in (0x04, 0x03):
            jump = (result == 0) if op == 0x04 else (result != 0)
            out.append('@%d %s Result=%d -> %s' % (pc, nm, result, 'JUMP to %d' % (pc + s16(v[1], v[2])) if jump else 'falls through'))
            pc = pc + s16(v[1], v[2]) if jump else pc + size
        elif op in (0x43, 0x42, 0x8D, 0x07):
            if op == 0x8D:
                result = 1
            if op == 0x07:
                x, y, z = item_tile
                result = 1 if (v[2] <= x <= v[3] and v[4] <= y <= v[5] and v[6] <= z <= v[7]) else 0
            pc += size
        elif op == 0x61:
            out.append('@%d 0x61 %s' % (pc, 'IMPLEMENTED -> Result 0' if impl else 'NOT IMPLEMENTED: skipped, Result stays %d' % result))
            if impl:
                result = 0
            pc += size
        elif op in (0x05, 0x10, 0x1A, 0x59, 0x27):
            out.append('@%d %s' % (pc, nm))
            pc += size
        elif op == 0x5C:
            out.append('@%d DIALOG %s (id %d)  <-- first dialog of the pass' % (pc, nm, v[2]))
            break
        else:
            out.append('@%d ?? %s' % (pc, nm))
            break
    return out


HOME = {1: (10, 21, 2), 2: (8, 21, 2), 3: (6, 21, 2), 4: (5, 23, 2)}   # tile of the record of the item at load (px/24, px/16, z/16): the cell the program ignores
for prog in (1, 2, 3, 4):
    print('== B[%d]  (hero on the ground, item resting on its shelf cell %s, %s)' % (prog, HOME[prog], 'with 0x61 implemented' if impl else '0x61 not implemented, as at HEAD'))
    for x in trace(prog, item_tile=HOME[prog]):
        print('   ' + x)
