"""pf_run.py - E20.h3b T0 (b): runs a scenario script of the oracle with the PER-FRAME STEP of the binary's generator
(s <- s * 0x7D2B89DD + 0xE06A02E7, the end of the frame function 0x8002BD0C-0x8002BD34, plan section 1.20, block, point 3)
applied to 0x80098708 AFTER each logic tick (the tick's own draws are read before it, the next tick starts from the stepped state).
usage (from a copy of critic/run or bin/run): python -B pf_run.py <script.py> [args...]"""
import sys, os, runpy
sys.path.insert(0, os.path.dirname(os.path.abspath(sys.argv[1])))
import world as W
RNG = 0x80098708
_tick = W.tick
def tick(m, pad, hold, *a, **k):
    r = _tick(m, pad, hold, *a, **k)
    m.poke(RNG, (m.peek(RNG) * 0x7D2B89DD + 0xE06A02E7) & 0xFFFFFFFF)
    return r
W.tick = tick
script = os.path.abspath(sys.argv[1])
sys.argv = sys.argv[1:]
runpy.run_path(script, run_name="__main__")
