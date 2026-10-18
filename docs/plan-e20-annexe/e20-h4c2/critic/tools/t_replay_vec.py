"""Replays the camera / sway / E13 vectors of h4b on MY interpreter (reads loaded into an empty memory; writes compared)."""
import json, sys
from myemu import Emu, M32, s32
import myemu
VEC = r"D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-h4b/bin/vec/"
def replay(fname, destroy_ok=False):
    d = json.load(open(VEC + fname))
    bad = 0; n = 0; incompl = 0
    for v in d['vectors']:
        e = Emu()
        # empty memory: reads are served from the vector only; mark by a custom rb
        mem = {}
        for a, sz, val in v['reads']:
            for k in range(sz):
                mem[a + k] = (val >> (8 * k)) & 0xFF
        text_lo, text_hi = 0x80010000, 0x80100000
        orig_rb = e.rb
        touched_unlisted = []
        def rb(a, orig_rb=orig_rb, mem=mem, e=e):
            a &= M32
            if a in e.mem: return e.mem[a]
            if a in mem: return mem[a]
            if 0x80010000 <= a < 0x800A0000 and (a & 3) is not None:
                # instruction / table bytes of the EXE image: allowed for fetch; data reads of unlisted bytes are flagged only for RAM above 0x800C0000
                if a < 0x800C5000: return orig_rb(a)
            touched_unlisted.append(a)
            return 0
        e.rb = rb
        stop = int(v['stop_at'], 16) if 'stop_at' in v else (int(d['stop_at'], 16) if 'stop_at' in d else None)
        dest = []
        if destroy_ok:
            def destroy(m): dest.append((m.r[4], s32(m.r[5]))); m.r[2] = 0
            e.hooks[0x8003A59C] = destroy
        fn = int(v['fn'], 16)
        stack = 0x801FFF00
        e.run(fn, tuple(v['args']), stop_at=stop, stack=stack)
        # compare writes: final value per (address,size), excluding stack frame
        final = {}
        for a, sz, val in e.log:
            if 0x801FFE00 <= a < 0x801FFF00: continue
            final[(a, sz)] = val
        exp = {(a, sz): val for a, sz, val in v['writes']}
        ok = True
        for k, val in exp.items():
            if final.get(k) != val: ok = False
        for k in final:
            if k not in exp: ok = False
        n += 1
        if not ok:
            bad += 1
            if bad <= 3: print('DIFF', fname, v['id'], {k: (final.get(k), exp.get(k)) for k in set(final) | set(exp) if final.get(k) != exp.get(k)})
        if touched_unlisted: incompl += 1
    print(fname, 'vectors', n, 'diffs', bad, 'with unlisted reads', incompl)
replay('vec_camera_8002CDA0.json')
replay('vec_sway_8002C894.json')
replay('vec_e13_8007F30C.json', destroy_ok=True)
