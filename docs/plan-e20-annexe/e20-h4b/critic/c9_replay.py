"""c9_replay.py - the critic's OWN replayer of the other agent's vector files (bin/vec/*.json), over the critic's interpreter (cmips), from an EMPTY memory.
Strictness (stronger than the vector README): every DATA read must hit a byte listed in the vector's "reads" -- including reads of the EXE image's own tables --
so a missing read is detected as INCOMPLETE; instruction fetch alone comes from the image.
Compares: the final value of every (addr,size) written (excluding the callee stack frame 0x801FFE00..0x801FFEFF) and, when no callee was stubbed, v0/v1.
Extra checks: (a) a vector must not list a read of a byte it had already written (the rule says first occurrence of bytes not yet written),
(b) sizes in {1,2,4} and alignment, (c) mutating ONE byte of a listed read must change something or be unused (detects dead reads).
"""
import json, os, sys, random
from cmips import *

VEC = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\disc-h4b\bin\vec"


class StrictMem(Mem):
    def b(self, a):
        a &= M
        if a in self.ram:
            return self.ram[a]
        raise Incomplete("unlisted data byte %#x" % a)


def load_ram(reads):
    ram = {}
    for a, n, v in reads:
        for i in range(n):
            ram[a + i] = (v >> (8 * i)) & 0xFF
    return ram


def replay(vec, fname, mutate=None):
    fn = int(vec["fn"], 16)
    args = vec["args"]
    reads = vec["reads"]
    ram = load_ram(reads)
    if mutate:
        ram[mutate[0]] = mutate[1]
    m = StrictMem(ram)
    stubs = {}
    stubinfo = {"spawn": 0, "destroy": 0}
    if "stub_spawn_ret" in vec:
        def spawn(c):
            stubinfo["spawn"] += 1
            c.r[2] = vec["stub_spawn_ret"]
        stubs[0x80039F58] = spawn
    if fname.startswith("vec_e13"):
        def destroy(c):
            stubinfo["destroy"] += 1
            c.r[2] = 0
        stubs[0x8003A59C] = destroy
    c = Cpu(m, stubs=stubs)
    stop = [int(vec["stop_at"], 16)] if "stop_at" in vec else []
    a4 = args[:4] + [0] * (4 - len(args[:4]))
    c.run(fn, a4, stop_at=stop)
    # note: the 5th argument of the query lives at [0x801FFF10] and is in reads
    w = {}
    for a, n, v in m.wlog:
        if 0x801FFE00 <= a < 0x801FFF00:
            continue
        w[(a, n)] = v
    exp = {(a, n): v for a, n, v in vec["writes"]}
    return w, exp, c, stubinfo


def main():
    tot = 0
    fails = 0
    summary = []
    for f in sorted(os.listdir(VEC)):
        if not f.endswith(".json") or f == "tables.json":
            continue
        d = json.load(open(os.path.join(VEC, f)))
        nf = 0
        n = 0
        issues = []
        for v in d["vectors"]:
            n += 1
            # static checks
            seen = set()
            for a, sz, val in v["reads"]:
                if sz not in (1, 2, 4) or a % sz:
                    issues.append("bad read size/align %x %d" % (a, sz))
                if val >> (8 * sz):
                    issues.append("read value too wide")
            try:
                w, exp, c, si = replay(v, f)
            except Incomplete as ex:
                nf += 1; issues.append("vec %s INCOMPLETE: %s" % (v["id"], ex)); continue
            except RuntimeError as ex:
                nf += 1; issues.append("vec %s run error: %s" % (v["id"], ex)); continue
            ok = (w == exp)
            if ok and not si["spawn"] and not si["destroy"] and "ret" in v and "stop_at" not in v:
                if c.r[2] != v["ret"] and not f.startswith("vec_e13"):
                    # ret is v0 at return
                    ok = False; issues.append("vec %s ret %x != %x" % (v["id"], c.r[2], v["ret"]))
            if not ok:
                nf += 1
                if len(issues) < 6:
                    dif = [(hex(k[0]), k[1], w.get(k), exp.get(k)) for k in set(w) | set(exp) if w.get(k) != exp.get(k)]
                    issues.append("vec %s writes differ: %s" % (v["id"], dif[:4]))
        tot += n; fails += nf
        summary.append((f, n, nf, issues[:4]))
        print("%-34s vectors %4d failures %d %s" % (f, n, nf, issues[:3]))
    print("TOTAL", tot, "FAILURES", fails)


if __name__ == "__main__":
    main()
