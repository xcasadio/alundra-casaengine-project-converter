"""derive_ticks.py - E20.A362 T0 (d): the ticks of the scene derived from the bytecode alone, before the scene ever ran in the DLL.

Input: ../e20-a362-disc/discovery/out/map362_programs.txt (the decoded programs of the map 362, one instruction per line: pc, opcode, name, [bytes]).
Rules, taken from Alundra/Events/EventProgramRunner.cs (RunOneScriptCall, Wait) and the binary 0x8003E3DC, as the plan writes them:
  - an instruction that does not suspend runs in the same tick as the one before it;
  - 0x37 Wait n suspends at its first call (tick t) and passes at t + n + 1 (n + 1 ticks);
  - 0x00 Break ends the call: the next instruction runs one tick later;
  - 0xFF ends the program.
An instruction that waits for something the bytecode does not fix (a flag, an animation, a walk, a dialogue: 0x36, 0x35, 0x39, 0x0B, 0x1C, 0x24)
stops a segment: the segment's tick 0 is the tick where the segment's first instruction runs, and it is named by the caller.
Prints, for each segment, the tick offset of the FIRST execution of each pc (the quantity ArcChecks.FrameOf reads from the trace).
usage: derive_ticks.py   (no argument; the segments are written below)"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LISTING = os.path.join(HERE, "..", "e20-a362-disc", "discovery", "out", "map362_programs.txt")

EXTERNAL = {0x36, 0x35, 0x39, 0x0B, 0x1C, 0x24}


def load():
    ins = {}
    pat = re.compile(r"^\s+(\d+) ([0-9a-f]{2}) (.*?)\s+\[([0-9, ]*)\]\s*$")
    for line in open(LISTING, encoding="utf-8"):
        m = pat.match(line)
        if m:
            pc = int(m.group(1))
            ins[pc] = (int(m.group(2), 16), m.group(3).strip(), [int(x) for x in m.group(4).split(",") if x.strip()])
    return ins


def segment(ins, label, pc, stop_after_external=True, max_pc=None):
    """Walks from pc at tick 0; returns [(pc, opcode, name, tick)] up to the first external wait (included, tick of its first call)."""
    t = 0
    out = []
    while pc in ins:
        op, name, args = ins[pc]
        out.append((pc, op, name, t))
        if op == 0xFF:
            break
        if op in EXTERNAL and stop_after_external:
            break
        if op == 0x37:
            t += args[1] + 1
        elif op == 0x00:
            t += 1
        # size of the instruction: the next pc in the listing
        nxt = min((p for p in ins if p > pc), default=None)
        if nxt is None:
            break
        pc = nxt
        if max_pc is not None and pc > max_pc:
            break
    return out


def show(title, rows, base_name):
    print("== " + title)
    for pc, op, name, t in rows:
        print("  @%-4d 0x%02X %-48s %s+%d" % (pc, op, name, base_name, t))
    print()


def main():
    ins = load()
    print("source:", os.path.relpath(LISTING, HERE).replace("\\", "/"))
    print()

    # Olen, C[4]: from the Wait 10 at @558 (the first call of @558 is tick 0 of the segment, named V) to the external wait of @588.
    rows = segment(ins, "C4", 558)
    show("Olen C[4] from @558 (V = tick of the first call of @558; S = the tick of @569)", rows, "V")
    s = [t for pc, op, n, t in rows if pc == 569][0]
    t4 = [t for pc, op, n, t in rows if pc == 582][0]
    print("  gap(@558 -> @562) = %d ; gap(@562 -> @569 = S) = %d ; gap(@569 -> @582 = T4) = %d ; V = S - %d" % (
        [t for pc, op, n, t in rows if pc == 562][0], s - [t for pc, op, n, t in rows if pc == 562][0], t4 - s, s))
    print()

    # The bomb, C[5]: nothing is fixed by the bytecode between its waits; the two flag waits are the external events.
    rows = segment(ins, "C5", 628)
    show("bomb C[5] from @628 (its first tick)", rows, "B")
    rows = segment(ins, "C5b", 635)
    show("bomb C[5] from @635 (the tick where it sees T4)", rows, "X4")
    rows = segment(ins, "C5c", 641)
    show("bomb C[5] from @641 (the tick where it sees T5; A = this tick)", rows, "A")

    # B[4]: from the Break at @171 (H + 2) to the external wait of the dialogue @226.
    rows = segment(ins, "B4", 171)
    show("map program B[4] from @171 (the Break, the tick where B[4] sees T6; call it U; U = H + 2)", rows, "U")
    print("  in terms of H (H = the tick of the hit pass; D[1] runs at H+1; U = H+2):")
    for pc, op, name, t in rows:
        if op in (0x8E, 0x8F, 0x8A, 0x00) or pc in (222, 223, 226):
            print("    @%-4d 0x%02X %-34s H+%d" % (pc, op, name, 2 + t))
    print()

    # D[1] of the lid
    rows = segment(ins, "D1", 728)
    show("lid D[1] from @728 (the tick H+1)", rows, "D")


if __name__ == "__main__":
    main()
